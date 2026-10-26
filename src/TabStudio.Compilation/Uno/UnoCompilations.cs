using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation.Razor;
using TabStudio.Core.Diagnostics;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.Uno;

/// <summary>
/// What Uno Platform changes in the diagnostics and builds of a project: the XAML generator's own errors join the C#
/// compiler's, errors in the code it generated point to the XAML element they came from, the compiler's "not found"
/// errors about Windows-only APIs say so, and WinUI APIs Uno does not implement are reported (see
/// <see cref="UnoCompatibility"/>). A build with a XAML error fails, as with Uno.Sdk.
/// </summary>
internal static class UnoCompilations
{
    /// <summary>The errors and warnings of an Uno project, for the Error List.</summary>
    /// <param name="project">The Roslyn project.</param>
    /// <param name="info">The project.</param>
    /// <param name="compilation">Its compilation.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public static Task<List<BuildDiagnostic>> MapAsync(Project project, TsProject info, RoslynCompilation compilation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        return MapAsync(project, info, compilation, compilation.GetDiagnostics(cancellationToken), cancellationToken);
    }

    /// <summary>Converts the compiler's diagnostics of an Uno project for the Error List, adding Uno's.</summary>
    /// <param name="project">The Roslyn project.</param>
    /// <param name="info">The project.</param>
    /// <param name="compilation">Its compilation.</param>
    /// <param name="compilerDiagnostics">The C# compiler's diagnostics of the compilation.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public static async Task<List<BuildDiagnostic>> MapAsync(
        Project project,
        TsProject info,
        RoslynCompilation compilation,
        IEnumerable<Diagnostic> compilerDiagnostics,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(info);
        var generator = await GeneratorDiagnostics.GetAsync(project, cancellationToken).ConfigureAwait(false);
        var map = await CreateMapAsync(project, info, cancellationToken).ConfigureAwait(false);
        var compatibility = UnoCompatibility.FindAll(compilation, info, cancellationToken).ToList();
        return Convert(map, compilerDiagnostics.Concat(generator).Concat(compatibility));
    }

    /// <summary>Compiles an Uno project into memory, failing on XAML errors and placing errors in their XAML files.</summary>
    /// <param name="project">The Roslyn project.</param>
    /// <param name="info">The project.</param>
    /// <param name="compilation">Its compilation.</param>
    /// <param name="checkInProcessSafety">Whether to reject calls that would close TabStudio.</param>
    /// <param name="stopwatch">Measures the build.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    public static async Task<CompilationResult> EmitAsync(
        Project project,
        TsProject info,
        RoslynCompilation compilation,
        bool checkInProcessSafety,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(info);
        var generator = await GeneratorDiagnostics.GetAsync(project, cancellationToken).ConfigureAwait(false);
        var map = await CreateMapAsync(project, info, cancellationToken).ConfigureAwait(false);
        var compatibility = UnoCompatibility.FindAll(compilation, info, cancellationToken).ToList();
        return CSharpCompiler.Emit(
            compilation,
            checkInProcessSafety,
            stopwatch,
            info.Name,
            ManifestResources.For(info),
            diagnostics => Convert(map, diagnostics.Concat(generator).Concat(compatibility)),
            cancellationToken);
    }

    private static List<BuildDiagnostic> Convert(UnoXamlSourceMap map, IEnumerable<Diagnostic> diagnostics) =>
        [.. diagnostics
            .Where(d => d.Severity != DiagnosticSeverity.Hidden && !IsUnoAnalyzerDuplicate(d))
            .Select(UnoCompatibility.Explain)
            .Select(map.Map)
            .Distinct()
            .OrderByDescending(d => d.Severity)
            .ThenBy(d => d.Location?.FilePath, StringComparer.Ordinal)
            .ThenBy(d => d.Location?.Start ?? 0)];

    /// <summary>Uno's own analyzer reports what <see cref="UnoCompatibility"/> reports, without the advice; it is not run, but its id is kept out.</summary>
    private static bool IsUnoAnalyzerDuplicate(Diagnostic diagnostic) => diagnostic.Id == "Uno0001";

    /// <summary>A map that knows the current text of every XAML file of the project.</summary>
    private static async Task<UnoXamlSourceMap> CreateMapAsync(Project project, TsProject info, CancellationToken cancellationToken)
    {
        var texts = new Dictionary<string, SourceText>(StringComparer.Ordinal);
        foreach (var document in project.AdditionalDocuments)
        {
            if (document.FilePath is { } path && path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                texts[path] = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return new UnoXamlSourceMap(info, texts);
    }
}
