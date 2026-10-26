using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Core.Diagnostics;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.Razor;

/// <summary>
/// What Razor changes in the diagnostics and builds of a project: the Razor compiler's own errors join the C#
/// compiler's, and errors in generated code point to the Razor file they came from, as in Visual Studio. A build
/// with a Razor error fails, as with the SDK.
/// </summary>
internal static class RazorCompilations
{
    /// <summary>
    /// Converts the compiler's diagnostics of a project for the Error List, adding the Razor compiler's for a Razor
    /// project and placing both in the Razor files.
    /// </summary>
    /// <param name="project">The Roslyn project.</param>
    /// <param name="info">The project.</param>
    /// <param name="compilerDiagnostics">The C# compiler's diagnostics of its compilation.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public static async Task<List<BuildDiagnostic>> MapAsync(Project project, TsProject info, IEnumerable<Diagnostic> compilerDiagnostics, CancellationToken cancellationToken)
    {
        if (Uno.UnoBuild.UsesUno(info) && await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false) is { } unoCompilation)
        {
            return await Uno.UnoCompilations.MapAsync(project, info, unoCompilation, compilerDiagnostics, cancellationToken).ConfigureAwait(false);
        }

        if (!RazorProjects.UsesRazor(info))
        {
            // What source generators report, such as a generator that threw (CS8785), joins the compiler's errors.
            var generated = await GeneratorDiagnostics.GetAsync(project, cancellationToken).ConfigureAwait(false);
            return [.. DiagnosticMapper.Map(compilerDiagnostics.Concat(generated), info.Name)];
        }

        var generator = await GeneratorDiagnostics.GetAsync(project, cancellationToken).ConfigureAwait(false);
        var mapper = await CreateMapperAsync(project, cancellationToken).ConfigureAwait(false);
        return mapper.Map(compilerDiagnostics.Concat(generator), info.Name);
    }

    /// <summary>Compiles a project into memory, failing on the Razor compiler's errors and placing errors in Razor files.</summary>
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
        if (Uno.UnoBuild.UsesUno(info))
        {
            return await Uno.UnoCompilations.EmitAsync(project, info, compilation, checkInProcessSafety, stopwatch, cancellationToken).ConfigureAwait(false);
        }

        if (!RazorProjects.UsesRazor(info))
        {
            // A generator's errors fail the build, as with the SDK.
            var generated = await GeneratorDiagnostics.GetAsync(project, cancellationToken).ConfigureAwait(false);
            return CSharpCompiler.Emit(
                compilation,
                checkInProcessSafety,
                stopwatch,
                info.Name,
                ManifestResources.For(info),
                generated.IsEmpty ? null : diagnostics => DiagnosticMapper.Map(diagnostics.Concat(generated), info.Name),
                cancellationToken);
        }

        var generator = await GeneratorDiagnostics.GetAsync(project, cancellationToken).ConfigureAwait(false);
        var mapper = await CreateMapperAsync(project, cancellationToken).ConfigureAwait(false);
        return CSharpCompiler.Emit(
            compilation,
            checkInProcessSafety,
            stopwatch,
            info.Name,
            ManifestResources.For(info),
            diagnostics => mapper.Map(diagnostics.Concat(generator), info.Name),
            cancellationToken);
    }

    /// <summary>A mapper that knows the current text of every Razor file of the project.</summary>
    public static async Task<RazorDiagnostics> CreateMapperAsync(Project project, CancellationToken cancellationToken)
    {
        var texts = new Dictionary<string, SourceText>(StringComparer.Ordinal);
        foreach (var document in project.AdditionalDocuments)
        {
            if (document.FilePath is { } path && RazorProjects.IsRazorFile(path))
            {
                texts[path] = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return new RazorDiagnostics(path => texts.GetValueOrDefault(path));
    }
}
