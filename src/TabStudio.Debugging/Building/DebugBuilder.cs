using System.Diagnostics;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Instrumentation;
using TabStudio.Debugging.Symbols;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Debugging.Building;

/// <summary>
/// Builds a project for debugging: like <see cref="ProjectBuilder"/>, but every project in the build is
/// instrumented before it is emitted, so breakpoints work in referenced projects too.
/// </summary>
/// <remarks>
/// The Error List shows the diagnostics of the original code, with the user's own line and column numbers.
/// Instrumentation only starts once the original code compiles without errors and passes the in-process safety
/// check. Instances are stateless and safe to share.
/// </remarks>
public sealed class DebugBuilder
{
    /// <summary>Reported when the instrumented code fails to compile, which is a bug in the debugger.</summary>
    public const string InstrumentationFailedId = "TS3001";

    private static readonly EmitOptions EmitOptions = new(debugInformationFormat: DebugInformationFormat.PortablePdb);

    private readonly RoslynWorkspaceHost _host;

    /// <summary>Creates a builder over a loaded workspace host.</summary>
    /// <param name="host">The host that holds the projects.</param>
    public DebugBuilder(RoslynWorkspaceHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <summary>Builds a project and the projects it references, instrumented for debugging.</summary>
    /// <param name="project">The project to debug, usually the startup project.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    public async Task<DebugBuild> BuildAsync(TsProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var stopwatch = Stopwatch.StartNew();
        var info = new DebugInfoBuilder();
        var diagnostics = new List<BuildDiagnostic>();
        if (project.Language != ProjectLanguage.CSharp || _host.FindProject(project.FilePath) is null)
        {
            diagnostics.Add(new BuildDiagnostic(
                ProjectBuilder.UnsupportedLanguageId,
                DiagnosticSeverityLevel.Error,
                $"'{project.Name}' cannot be debugged on this device: only C# projects are compiled by TabStudio.",
                null,
                project.Name));
            return Finish(project.Name, diagnostics, null, [], stopwatch, info);
        }

        var dependencies = new List<CompiledAssembly>();
        CompiledAssembly? entry = null;
        foreach (var current in _host.GetBuildOrder(project.FilePath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            diagnostics.AddRange(current.PackageReferences.Select(package => new BuildDiagnostic(
                ProjectBuilder.UnrestoredPackageId,
                DiagnosticSeverityLevel.Warning,
                $"NuGet package '{package.Name}' is not restored on this device, so code that uses it will not compile.",
                null,
                current.Name)));

            var isEntry = string.Equals(current.FilePath, project.FilePath, StringComparison.Ordinal);
            var compilation = await _host.GetCompilationAsync(current.FilePath, cancellationToken).ConfigureAwait(false) as CSharpCompilation;
            var assembly = compilation is null
                ? null
                : Compile(compilation, current.Name, isEntry && current.IsExecutable, info, diagnostics, cancellationToken);
            if (assembly is null)
            {
                return Finish(project.Name, diagnostics, null, dependencies, stopwatch, info);
            }

            if (isEntry)
            {
                entry = assembly;
            }
            else
            {
                dependencies.Add(assembly);
            }
        }

        return Finish(project.Name, diagnostics, entry, dependencies, stopwatch, info);
    }

    /// <summary>Instruments and emits one compilation, such as a program compiled with <see cref="TabStudio.Compilation.CSharpCompiler"/>.</summary>
    /// <param name="compilation">The compilation to debug.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    public static DebugBuild Build(CSharpCompilation compilation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var stopwatch = Stopwatch.StartNew();
        var info = new DebugInfoBuilder();
        var diagnostics = new List<BuildDiagnostic>();
        var name = compilation.AssemblyName ?? "Program";
        var isProgram = compilation.Options.OutputKind == OutputKind.ConsoleApplication;
        var assembly = Compile(compilation, name, isProgram, info, diagnostics, cancellationToken);
        return Finish(name, diagnostics, assembly, [], stopwatch, info);
    }

    private static CompiledAssembly? Compile(
        CSharpCompilation compilation,
        string projectName,
        bool checkInProcessSafety,
        DebugInfoBuilder info,
        List<BuildDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var original = DiagnosticMapper.Map(compilation.GetDiagnostics(cancellationToken), projectName).ToList();
        if (checkInProcessSafety)
        {
            original.AddRange(InProcessSafetyAnalyzer.Analyze(compilation, projectName, cancellationToken));
        }

        diagnostics.AddRange(original);
        if (original.Any(d => d.Severity == DiagnosticSeverityLevel.Error))
        {
            return null;
        }

        var instrumented = DebugInstrumenter.Instrument(compilation, info, cancellationToken);
        using var image = new MemoryStream();
        using var symbols = new MemoryStream();
        var emit = instrumented.Emit(image, symbols, options: EmitOptions, cancellationToken: cancellationToken);
        if (!emit.Success)
        {
            var error = emit.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
            diagnostics.Add(new BuildDiagnostic(
                InstrumentationFailedId,
                DiagnosticSeverityLevel.Error,
                $"The debugger could not prepare '{projectName}' ({error?.Id}: {error?.GetMessage(CultureInfo.InvariantCulture)} at {error?.Location.GetLineSpan()}). " +
                "Run it without debugging, and please report this as a TabStudio bug.",
                null,
                projectName));
            return null;
        }

        return new CompiledAssembly(compilation.AssemblyName ?? projectName, image.ToArray(), symbols.ToArray());
    }

    private static DebugBuild Finish(
        string projectName,
        List<BuildDiagnostic> diagnostics,
        CompiledAssembly? entry,
        IReadOnlyList<CompiledAssembly> dependencies,
        Stopwatch stopwatch,
        DebugInfoBuilder info)
    {
        var sorted = diagnostics.OrderByDescending(d => d.Severity).ToList();
        var result = new BuildResult(projectName, sorted, entry, dependencies, stopwatch.Elapsed);
        return new DebugBuild(result, entry is null ? DebugInfoMap.Empty : info.Build());
    }
}
