using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Windows;
using Microsoft.Build.Utilities;
using TabStudio.Core.Diagnostics;
using Task = System.Threading.Tasks.Task;

namespace TabStudio.WindowsCompatibility.Building.Wpf;

/// <summary>What the WPF markup compiler compiles: one project's XAML, with everything its tasks are given.</summary>
public sealed record WpfMarkupInput
{
    /// <summary>The project file.</summary>
    public required string ProjectFile { get; init; }

    /// <summary>The project's name, for diagnostics.</summary>
    public required string ProjectName { get; init; }

    /// <summary>The assembly's name.</summary>
    public required string AssemblyName { get; init; }

    /// <summary>The root namespace.</summary>
    public required string RootNamespace { get; init; }

    /// <summary>The output type, as MSBuild names it: <c>WinExe</c>, <c>Exe</c> or <c>Library</c>.</summary>
    public required string OutputType { get; init; }

    /// <summary>The intermediate folder, relative to the project, such as <c>obj/Debug/net10.0-windows/</c>.</summary>
    public required string IntermediateDirectory { get; init; }

    /// <summary>The application definition (<c>App.xaml</c>), relative to the project, if the project has one.</summary>
    public string? ApplicationMarkup { get; init; }

    /// <summary>The pages: every other XAML file, relative to the project.</summary>
    public IReadOnlyList<string> PageMarkup { get; init; } = [];

    /// <summary>The <c>Resource</c> items, such as images, relative to the project.</summary>
    public IReadOnlyList<string> Resources { get; init; } = [];

    /// <summary>The C# files, relative to the project.</summary>
    public IReadOnlyList<string> SourceFiles { get; init; } = [];

    /// <summary>The assemblies to resolve the XAML's types in: .NET's and Windows' reference assemblies, packages and project references.</summary>
    public IReadOnlyList<string> References { get; init; } = [];

    /// <summary>The preprocessor symbols, separated by semicolons.</summary>
    public string DefineConstants { get; init; } = string.Empty;

    /// <summary>Whether the BAML keeps the XAML's line numbers, as Debug builds do.</summary>
    public bool XamlDebuggingInformation { get; init; }
}

/// <summary>What the first pass of the WPF markup compiler produced.</summary>
/// <param name="Success">Whether it compiled without errors.</param>
/// <param name="CodeFiles">The generated partial classes (<c>.g.cs</c>), as full paths.</param>
/// <param name="BamlFiles">The compiled pages (<c>.baml</c>), as full paths.</param>
/// <param name="RequiresPass2">Whether some XAML uses types of the project itself, which a second pass compiles with the assembly.</param>
/// <param name="Diagnostics">Errors and warnings in the XAML.</param>
public sealed record WpfMarkupResult(bool Success, IReadOnlyList<string> CodeFiles, IReadOnlyList<string> BamlFiles, bool RequiresPass2, IReadOnlyList<BuildDiagnostic> Diagnostics);

/// <summary>The resources the WPF build packs into an assembly.</summary>
/// <param name="Name">The resource's name, <c>&lt;assembly&gt;.g.resources</c>, or <see langword="null"/> when there was nothing to pack.</param>
/// <param name="Content">Its bytes.</param>
/// <param name="Diagnostics">Problems with the resources.</param>
public sealed record WpfResources(string? Name, byte[]? Content, IReadOnlyList<BuildDiagnostic> Diagnostics);

/// <summary>
/// WPF's markup compiler, as the .NET SDK runs it: <c>PresentationBuildTasks</c>' MarkupCompilePass1 turns XAML
/// pages into BAML and the partial classes with <c>InitializeComponent</c>, MarkupCompilePass2 compiles the pages
/// that use the project's own types once its assembly exists, and FileClassifier and ResourcesGenerator pack the
/// BAML and resources into the assembly's <c>&lt;name&gt;.g.resources</c>.
/// </summary>
/// <remarks>
/// The tasks run in TabStudio's process, outside MSBuild, with the same parameters Microsoft.WinFX.targets gives
/// them. Like every MSBuild task they resolve the project's relative paths against the current directory, which
/// MSBuild sets to the project's folder, so a compile does the same, one compile at a time.
/// </remarks>
public static class WpfMarkupCompiler
{
    private static readonly SemaphoreSlim CurrentDirectoryGate = new(1, 1);

    /// <summary>Runs the first pass.</summary>
    /// <param name="input">The project.</param>
    /// <param name="cancellationToken">Gives up before the compile starts.</param>
    public static Task<WpfMarkupResult> CompilePass1Async(WpfMarkupInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return InProjectDirectoryAsync(input, engine =>
        {
            var task = new MarkupCompilePass1
            {
                BuildEngine = engine,
                Language = "C#",
                LanguageSourceExtension = ".cs",
                AssemblyName = input.AssemblyName,
                RootNamespace = input.RootNamespace,
                OutputType = input.OutputType,
                OutputPath = Slashed(input.IntermediateDirectory),
                ApplicationMarkup = input.ApplicationMarkup is { } app ? [new TaskItem(app)] : [],
                PageMarkup = [.. input.PageMarkup.Select(p => new TaskItem(p))],
                SourceCodeFiles = [.. input.SourceFiles.Select(p => new TaskItem(p))],
                References = [.. input.References.Select(p => new TaskItem(p))],
                KnownReferencePaths = [.. input.References.Select(Path.GetDirectoryName).OfType<string>().Distinct(StringComparer.Ordinal)],
                AssembliesGeneratedDuringBuild = [],
                DefineConstants = input.DefineConstants,
                XamlDebuggingInformation = input.XamlDebuggingInformation,
                SupportCustomOutputPaths = true,
            };
            var success = task.Execute();
            var directory = Path.GetDirectoryName(input.ProjectFile)!;
            return new WpfMarkupResult(
                success && engine.Diagnostics.All(d => d.Severity != DiagnosticSeverityLevel.Error),
                FullPaths(task.GeneratedCodeFiles, directory),
                FullPaths(task.GeneratedBamlFiles, directory),
                task.RequirePass2ForMainAssembly,
                engine.Diagnostics);
        }, cancellationToken);
    }

    /// <summary>
    /// Runs the second pass, for the pages that use the project's own types, with the assembly built from the
    /// first pass's code.
    /// </summary>
    /// <param name="input">The project.</param>
    /// <param name="localTypesAssembly">The project's assembly, built with the generated code, as a file.</param>
    /// <param name="cancellationToken">Gives up before the compile starts.</param>
    /// <returns>The BAML files it compiled, and the problems.</returns>
    public static Task<WpfMarkupResult> CompilePass2Async(WpfMarkupInput input, string localTypesAssembly, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(localTypesAssembly);
        return InProjectDirectoryAsync(input, engine =>
        {
            var references = input.References.Append(localTypesAssembly).ToList();
            var task = new MarkupCompilePass2
            {
                BuildEngine = engine,
                Language = "C#",
                AssemblyName = input.AssemblyName,
                RootNamespace = input.RootNamespace,
                OutputType = input.OutputType,
                OutputPath = Slashed(input.IntermediateDirectory),
                References = [.. references.Select(p => new TaskItem(p))],
                KnownReferencePaths = [.. input.References.Select(Path.GetDirectoryName).OfType<string>().Distinct(StringComparer.Ordinal)],
                AssembliesGeneratedDuringBuild = [],
                XamlDebuggingInformation = input.XamlDebuggingInformation,
                SupportCustomOutputPaths = true,
            };
            var success = task.Execute();
            return new WpfMarkupResult(
                success && engine.Diagnostics.All(d => d.Severity != DiagnosticSeverityLevel.Error),
                [],
                FullPaths(task.GeneratedBaml, Path.GetDirectoryName(input.ProjectFile)!),
                RequiresPass2: false,
                engine.Diagnostics);
        }, cancellationToken);
    }

    /// <summary>
    /// Packs the compiled pages and the <c>Resource</c> items into the assembly's <c>&lt;name&gt;.g.resources</c>,
    /// where WPF's pack URIs find them.
    /// </summary>
    /// <param name="input">The project.</param>
    /// <param name="bamlFiles">The BAML files of both passes.</param>
    /// <param name="cancellationToken">Gives up before packing starts.</param>
    public static Task<WpfResources> PackResourcesAsync(WpfMarkupInput input, IReadOnlyList<string> bamlFiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(bamlFiles);
        return InProjectDirectoryAsync(input, engine =>
        {
            var directory = Path.GetDirectoryName(input.ProjectFile)!;
            var sources = bamlFiles.Select(f => Path.GetRelativePath(directory, f)).Concat(input.Resources).Select(p => (ITaskItem)new TaskItem(p)).ToArray();
            if (sources.Length == 0)
            {
                return new WpfResources(null, null, engine.Diagnostics);
            }

            var classifier = new FileClassifier { BuildEngine = engine, SourceFiles = sources, OutputType = input.OutputType };
            if (!classifier.Execute() || classifier.MainEmbeddedFiles is not { Length: > 0 } embedded)
            {
                return new WpfResources(null, null, engine.Diagnostics);
            }

            var name = input.AssemblyName + ".g.resources";
            var output = Slashed(input.IntermediateDirectory) + name;
            var generator = new ResourcesGenerator
            {
                BuildEngine = engine,
                ResourceFiles = embedded,
                OutputPath = Slashed(input.IntermediateDirectory),
                OutputResourcesFile = [new TaskItem(output)],
            };
            return generator.Execute()
                ? new WpfResources(name, File.ReadAllBytes(Path.Combine(directory, output)), engine.Diagnostics)
                : new WpfResources(null, null, engine.Diagnostics);
        }, cancellationToken);
    }

    /// <summary>Runs a task's work with the project's folder as the current directory, one at a time.</summary>
    private static async Task<T> InProjectDirectoryAsync<T>(WpfMarkupInput input, Func<TaskBuildEngine, T> work, CancellationToken cancellationToken)
    {
        await CurrentDirectoryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () =>
                {
                    var previous = Directory.GetCurrentDirectory();
                    Directory.SetCurrentDirectory(Path.GetDirectoryName(input.ProjectFile)!);
                    try
                    {
                        return work(new TaskBuildEngine(input.ProjectFile, input.ProjectName));
                    }
                    finally
                    {
                        Directory.SetCurrentDirectory(previous);
                    }
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            CurrentDirectoryGate.Release();
        }
    }

    private static string Slashed(string folder) => folder.EndsWith('/') ? folder : folder + "/";

    private static List<string> FullPaths(ITaskItem[]? items, string directory) =>
        items is null ? [] : [.. items.Select(item => Path.GetFullPath(item.ItemSpec, directory))];
}
