using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.Build.Framework;
using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Wpf.Compiler;

/// <summary>
/// OpenSilver's own XAML compiler, run inside TabStudio from the restored OpenSilver package, the way OpenSilver's
/// build targets run it: its <c>OpenSilver.Compiler.XamlPreprocessor</c> MSBuild task, which turns each XAML file into
/// C#, once before the C# compiler (pass 1) and once after it (pass 2), against the assembly of the first compile.
/// </summary>
/// <remarks>
/// <para>
/// The compiler is <c>tools/OpenSilver.Compiler.&lt;build&gt;.dll</c> in the package, with the Mono.Cecil it reads
/// assemblies with. It is loaded once per package folder into a load context of its own; TabStudio is its MSBuild,
/// handing it the project's items and collecting what it logs.
/// </para>
/// <para>The task keeps state in static fields, so passes run one at a time.</para>
/// </remarks>
public sealed class OpenSilverXamlCompiler
{
    /// <summary>The task OpenSilver's targets run for XAML files.</summary>
    public const string TaskTypeName = "OpenSilver.Compiler.XamlPreprocessor";

    private static readonly ConcurrentDictionary<string, OpenSilverXamlCompiler> Compilers = new(StringComparer.Ordinal);
    private static readonly Lock Gate = new();

    private readonly Type _taskType;

    private OpenSilverXamlCompiler(string packageDirectory, string assemblyPath, Type taskType)
    {
        PackageDirectory = packageDirectory;
        AssemblyPath = assemblyPath;
        _taskType = taskType;
    }

    /// <summary>The OpenSilver package folder, such as <c>packages/opensilver/3.3.3</c>.</summary>
    public string PackageDirectory { get; }

    /// <summary>The compiler assembly.</summary>
    public string AssemblyPath { get; }

    /// <summary>The package's version: the name of its folder.</summary>
    public string Version => Path.GetFileName(PackageDirectory.TrimEnd(Path.DirectorySeparatorChar));

    /// <summary>The compiler assembly of an extracted OpenSilver package, or <see langword="null"/> when it has none.</summary>
    /// <param name="packageDirectory">The package folder.</param>
    public static string? FindCompilerAssembly(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageDirectory);
        var tools = Path.Combine(packageDirectory, "tools");
        return Directory.Exists(tools)
            ? Directory.EnumerateFiles(tools, "OpenSilver.Compiler*.dll")
                .Where(path => !Path.GetFileName(path).Contains("SharedBuildTasks", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .LastOrDefault()
            : null;
    }

    /// <summary>Loads the compiler of an extracted OpenSilver package, once per package folder.</summary>
    /// <param name="packageDirectory">The package folder, such as <c>packages/opensilver/3.3.3</c>.</param>
    /// <exception cref="FileNotFoundException">The package has no XAML compiler in its <c>tools</c> folder.</exception>
    /// <exception cref="InvalidDataException">The compiler has no <see cref="TaskTypeName"/> task.</exception>
    public static OpenSilverXamlCompiler Load(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageDirectory);
        return Compilers.GetOrAdd(Path.GetFullPath(packageDirectory), static directory =>
        {
            var assemblyPath = FindCompilerAssembly(directory)
                ?? throw new FileNotFoundException($"The OpenSilver package in '{directory}' has no XAML compiler in its tools folder.");
            var context = new CompilerLoadContext(Path.GetDirectoryName(assemblyPath)!);
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            var taskType = assembly.GetType(TaskTypeName, throwOnError: false)
                ?? throw new InvalidDataException($"{Path.GetFileName(assemblyPath)} has no {TaskTypeName} task.");
            return new OpenSilverXamlCompiler(directory, assemblyPath, taskType);
        });
    }

    /// <summary>Runs one pass over a project's XAML.</summary>
    /// <param name="request">What to compile.</param>
    /// <param name="projectName">The project, for the diagnostics.</param>
    /// <returns>The generated C# files and the compiler's problems. An exception in the compiler is an error, never thrown.</returns>
    public XamlCompilerResult Compile(XamlCompilerRequest request, string projectName)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IsSecondPass && request.FirstPassAssemblyPath is null)
        {
            throw new ArgumentException("Pass 2 reads the assembly of pass 1.", nameof(request));
        }

        Directory.CreateDirectory(request.IntermediateDirectory);
        var outputDirectory = request.IsSecondPass ? Path.GetDirectoryName(request.FirstPassAssemblyPath!)! : request.IntermediateDirectory;
        var pages = Items(request.Files.Where(f => f.Item.Kind == XamlItemKind.Page));
        var applications = Items(request.Files.Where(f => f.Item.Kind == XamlItemKind.ApplicationDefinition));
        var references = request.References.Distinct(StringComparer.Ordinal).Select(path => (ITaskItem)new XamlTaskItem(path, path)).ToArray();
        var engine = new XamlBuildEngine(request.ProjectFilePath);

        bool succeeded;
        ITaskItem[] generated;
        lock (Gate)
        {
            // The task sets the thread's culture to the invariant one; the calling thread keeps its own.
            var culture = CultureInfo.CurrentCulture;
            var uiCulture = CultureInfo.CurrentUICulture;
            try
            {
                var task = (ITask)Activator.CreateInstance(_taskType)!;
                task.BuildEngine = engine;
                Set(task, "Language", "C#");
                Set(task, "PageFiles", pages);
                Set(task, "ApplicationDefinitionFiles", applications);
                Set(task, "ContentFiles", Array.Empty<ITaskItem>());
                Set(task, "ResolvedReferences", references);
                Set(task, "AssemblyName", request.AssemblyName);
                Set(task, "RootNamespace", request.RootNamespace ?? request.AssemblyName);
                Set(task, "IntermediateOutputPath", WithSeparator(request.IntermediateDirectory));
                Set(task, "OutputPath", WithSeparator(outputDirectory));
                Set(task, "Options", "Auto");
                Set(task, "IsSecondPass", request.IsSecondPass);
                Set(task, "VerifyHash", false);
                Set(task, "IsDesignTime", false);
                succeeded = task.Execute();
                generated = (ITaskItem[]?)_taskType.GetProperty("GeneratedFiles")?.GetValue(task) ?? [];
            }
#pragma warning disable CA1031 // A compiler that crashes is reported like any of its errors, whatever it throws; the IDE stays up.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                var cause = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                return new XamlCompilerResult(
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    [new BuildDiagnostic(WpfDiagnosticIds.XamlCompilerFailed, DiagnosticSeverityLevel.Error, $"OpenSilver's XAML compiler {Version} failed: {cause.Message}", null, projectName)],
                    false);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = uiCulture;
            }
        }

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var byIdentity = request.Files.ToDictionary(f => Normalize(f.Item.RelativePath), f => f.Item.FilePath, StringComparer.OrdinalIgnoreCase);
        foreach (var output in generated)
        {
            var path = output.ItemSpec;
            var identity = Normalize(Path.GetRelativePath(request.IntermediateDirectory, path));
            var suffix = request.IsSecondPass ? ".g.cs" : ".g.i.cs";
            if (identity.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && byIdentity.TryGetValue(identity[..^suffix.Length], out var source) && File.Exists(path))
            {
                files[source] = path;
            }
        }

        var diagnostics = XamlCompilerDiagnostics.Convert(engine.Problems, request, projectName);
        return new XamlCompilerResult(files, diagnostics, succeeded && diagnostics.All(d => d.Severity != DiagnosticSeverityLevel.Error));
    }

    private static ITaskItem[] Items(IEnumerable<XamlCompilerInput> files) =>
        files.Select(f => (ITaskItem)new XamlTaskItem(f.Item.RelativePath, f.ContentPath ?? f.Item.FilePath)).ToArray();

    private void Set(ITask task, string property, object value) =>
        (_taskType.GetProperty(property) ?? throw new MissingMethodException(_taskType.FullName, property)).SetValue(task, value);

    private static string WithSeparator(string directory) =>
        directory.EndsWith(Path.DirectorySeparatorChar) ? directory : directory + Path.DirectorySeparatorChar;

    private static string Normalize(string path) => path.Replace('\\', '/');
}
