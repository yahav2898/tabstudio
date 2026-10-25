using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation.Wpf;
using TabStudio.Compilation.Wpf.Xaml;
using TabStudio.Core.Diagnostics;
using TabStudio.Wpf.Compiler;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.Wpf.Building;

/// <summary>
/// Completes a WPF project's build with the second pass of OpenSilver's own XAML compiler, from the OpenSilver
/// package the project compiles against: the first pass's assembly is written where the compiler reads the project's
/// types from, the XAML goes through <see cref="XamlSanitizer"/>, and the generated C# takes the place of the first
/// pass's in the project's compilation.
/// </summary>
/// <param name="frameworkReferenceDirectory">
/// .NET's reference assemblies as files (<c>ReferenceAssemblies.ExtractTo</c>), for references the compilation holds in
/// memory; <see langword="null"/> when every reference is a file.
/// </param>
public sealed class OpenSilverWpfCompiler(string? frameworkReferenceDirectory) : IWpfXamlCompiler
{
    /// <inheritdoc />
    public Task<WpfSecondPass> CompileAsync(WpfSecondPassRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => Compile(request, cancellationToken), cancellationToken);
    }

    /// <summary>The OpenSilver package a compilation references, from the folder of its <c>OpenSilver.dll</c>.</summary>
    /// <param name="referencePaths">The compilation's reference files.</param>
    public static string? FindOpenSilverPackage(IEnumerable<string> referencePaths)
    {
        ArgumentNullException.ThrowIfNull(referencePaths);
        foreach (var path in referencePaths)
        {
            if (string.Equals(Path.GetFileName(path), "OpenSilver.dll", StringComparison.OrdinalIgnoreCase)
                && Directory.GetParent(path)?.Parent?.Parent is { } package
                && OpenSilverXamlCompiler.FindCompilerAssembly(package.FullName) is not null)
            {
                return package.FullName;
            }
        }

        return null;
    }

    private WpfSecondPass Compile(WpfSecondPassRequest request, CancellationToken cancellationToken)
    {
        var project = request.Project;
        var compilation = request.Compilation;
        var output = WpfProject.OutputDirectory(project);
        var references = ReferencePaths(compilation, Path.Combine(output, "references"), cancellationToken);
        if (FindOpenSilverPackage(references) is not { } package)
        {
            return Failed(WpfDiagnosticIds.OpenSilverMissing, $"{project.Name} uses WPF, which TabStudio builds with OpenSilver, and OpenSilver is not restored yet. Restore the project's packages and build again.", project.Name);
        }

        OpenSilverXamlCompiler compiler;
        try
        {
            compiler = OpenSilverXamlCompiler.Load(package);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or BadImageFormatException or FileLoadException)
        {
            return Failed(WpfDiagnosticIds.XamlCompilerFailed, $"OpenSilver's XAML compiler could not be loaded from {package}: {ex.Message}", project.Name);
        }

        var assemblyName = compilation.AssemblyName ?? project.Name;
        var firstPass = Path.Combine(output, "pass1", assemblyName + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(firstPass)!);
        File.WriteAllBytes(firstPass, request.FirstPassImage.ToArray());
        cancellationToken.ThrowIfCancellationRequested();

        var (inputs, sanitizing) = XamlSanitizer.Prepare(WpfProject.XamlItems(project), XamlTypeSystem.For(compilation), Path.Combine(output, "sanitized"), project.Name);
        var result = compiler.Compile(new XamlCompilerRequest
        {
            ProjectFilePath = project.FilePath,
            AssemblyName = assemblyName,
            RootNamespace = project.GetProperty("RootNamespace") ?? assemblyName,
            Files = inputs,
            References = references,
            IntermediateDirectory = WpfProject.SecondPassDirectory(project),
            IsSecondPass = true,
            FirstPassAssemblyPath = firstPass,
        }, project.Name);

        IReadOnlyList<BuildDiagnostic> diagnostics = [.. sanitizing, .. result.Diagnostics];
        if (!result.Succeeded)
        {
            return new WpfSecondPass(null, diagnostics);
        }

        var options = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions ?? CSharpParseOptions.Default;
        var generated = result.GeneratedFiles.Values
            .Order(StringComparer.Ordinal)
            .Select(path => CSharpSyntaxTree.ParseText(SourceText.From(File.ReadAllText(path), Encoding.UTF8), options, path, cancellationToken))
            .ToList();
        var completed = compilation
            .RemoveSyntaxTrees(compilation.SyntaxTrees.Where(WpfXamlGenerator.IsFirstPassTree))
            .AddSyntaxTrees(generated);
        return new WpfSecondPass(completed, diagnostics);
    }

    /// <summary>
    /// Every assembly the compilation references, as a file the XAML compiler can read: the files it was made from,
    /// .NET's reference assemblies for those held in memory, and the projects it references, written out as reference
    /// assemblies.
    /// </summary>
    private List<string> ReferencePaths(RoslynCompilation compilation, string projectReferenceDirectory, CancellationToken cancellationToken)
    {
        var paths = new List<string>();
        foreach (var reference in compilation.References)
        {
            switch (reference)
            {
                case PortableExecutableReference { FilePath: { } path } when File.Exists(path):
                    paths.Add(path);
                    break;
                case PortableExecutableReference { FilePath: { } name } when frameworkReferenceDirectory is not null
                    && Path.Combine(frameworkReferenceDirectory, Path.GetFileName(name)) is var framework && File.Exists(framework):
                    paths.Add(framework);
                    break;
                case CompilationReference { Compilation: var referenced }:
                    var file = Path.Combine(projectReferenceDirectory, (referenced.AssemblyName ?? "Reference") + ".dll");
                    Directory.CreateDirectory(projectReferenceDirectory);
                    using (var stream = File.Create(file))
                    {
                        referenced.Emit(stream, options: new EmitOptions(metadataOnly: true), cancellationToken: cancellationToken);
                    }

                    paths.Add(file);
                    break;
            }
        }

        return paths;
    }

    private static WpfSecondPass Failed(string id, string message, string projectName) =>
        new(null, [new BuildDiagnostic(id, DiagnosticSeverityLevel.Error, message, null, projectName)]);
}
