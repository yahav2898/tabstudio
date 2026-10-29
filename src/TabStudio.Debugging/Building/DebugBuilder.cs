using System.Diagnostics;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.VisualBasic;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Instrumentation;
using TabStudio.Debugging.Instrumentation.VisualBasic;
using TabStudio.Debugging.Symbols;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Debugging.Building;

/// <summary>
/// Builds a project for debugging: like <see cref="ProjectBuilder"/>, but every project in the build is
/// instrumented before it is emitted, so breakpoints work in referenced projects too. C# and Visual Basic projects
/// are both instrumented, and may reference each other.
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
        var language = project.IsVisualBasic ? ProjectLanguage.VisualBasic : ProjectLanguage.CSharp;
        if (ProjectClassifier.PlatformWorkloadReason(project) is { } workload)
        {
            diagnostics.Add(new BuildDiagnostic(
                ProjectBuilder.NeedsPlatformWorkloadId,
                DiagnosticSeverityLevel.Error,
                $"'{project.Name}' cannot be debugged on this device: it {workload}.",
                null,
                project.Name));
            return Finish(project.Name, diagnostics, null, [], stopwatch, info, language);
        }

        if (!project.IsRoslynLanguage || _host.FindProject(project.FilePath) is null)
        {
            diagnostics.Add(new BuildDiagnostic(
                ProjectBuilder.UnsupportedLanguageId,
                DiagnosticSeverityLevel.Error,
                $"'{project.Name}' cannot be debugged on this device: only C# and Visual Basic projects are compiled by TabStudio.",
                null,
                project.Name));
            return Finish(project.Name, diagnostics, null, [], stopwatch, info, language);
        }

        var dependencies = new List<CompiledAssembly>();
        CompiledAssembly? entry = null;
        foreach (var current in _host.GetBuildOrder(project.FilePath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // As in ProjectBuilder: once the IDE restored a project's packages, the compilation references them, and
            // only the packages its restore could not restore are missing.
            diagnostics.AddRange(current.PackageReferences
                .Where(package => !_host.IsPackageRestored(current.FilePath, package.Name))
                .Select(package => new BuildDiagnostic(
                    ProjectBuilder.UnrestoredPackageId,
                    DiagnosticSeverityLevel.Warning,
                    $"NuGet package '{package.Name}' is not restored on this device, so code that uses it will not compile.",
                    null,
                    current.Name)));

            var isEntry = string.Equals(current.FilePath, project.FilePath, StringComparison.Ordinal);
            var compilation = await _host.GetCompilationAsync(current.FilePath, cancellationToken).ConfigureAwait(false);
            var assembly = compilation is not (CSharpCompilation or VisualBasicCompilation)
                ? null
                : Compile(compilation, current.Name, isEntry && current.IsExecutable, ManifestResources.For(current), info, diagnostics, cancellationToken);
            if (assembly is null)
            {
                return Finish(project.Name, diagnostics, null, dependencies, stopwatch, info, language);
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

        return Finish(project.Name, diagnostics, entry, dependencies, stopwatch, info, language);
    }

    /// <summary>
    /// Instruments and emits one compilation, such as a program compiled with <see cref="TabStudio.Compilation.CSharpCompiler"/>
    /// or <see cref="TabStudio.Compilation.VisualBasic.VisualBasicCompiler"/>.
    /// </summary>
    /// <param name="compilation">The C# or Visual Basic compilation to debug.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <exception cref="ArgumentException">The compilation is neither C# nor Visual Basic.</exception>
    public static DebugBuild Build(RoslynCompilation compilation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        if (compilation is not (CSharpCompilation or VisualBasicCompilation))
        {
            throw new ArgumentException("Only C# and Visual Basic compilations can be debugged.", nameof(compilation));
        }

        var stopwatch = Stopwatch.StartNew();
        var info = new DebugInfoBuilder();
        var diagnostics = new List<BuildDiagnostic>();
        var name = compilation.AssemblyName ?? "Program";
        var isProgram = compilation.Options.OutputKind == OutputKind.ConsoleApplication;
        var language = compilation is VisualBasicCompilation ? ProjectLanguage.VisualBasic : ProjectLanguage.CSharp;
        var assembly = Compile(compilation, name, isProgram, resources: null, info, diagnostics, cancellationToken);
        return Finish(name, diagnostics, assembly, [], stopwatch, info, language);
    }

    private static CompiledAssembly? Compile(
        RoslynCompilation compilation,
        string projectName,
        bool checkInProcessSafety,
        IEnumerable<ResourceDescription>? resources,
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

        RoslynCompilation instrumented = compilation is VisualBasicCompilation basic
            ? VisualBasicDebugInstrumenter.Instrument(basic, info, cancellationToken)
            : DebugInstrumenter.Instrument((CSharpCompilation)compilation, info, cancellationToken);
        using var image = new MemoryStream();
        using var symbols = new MemoryStream();
        var emit = instrumented.Emit(image, symbols, manifestResources: resources, options: EmitOptions, cancellationToken: cancellationToken);
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

        return new CompiledAssembly(compilation.AssemblyName ?? projectName, image.ToArray(), symbols.ToArray())
        {
            HotReloadCompilation = instrumented,
        };
    }

    private static DebugBuild Finish(
        string projectName,
        List<BuildDiagnostic> diagnostics,
        CompiledAssembly? entry,
        IReadOnlyList<CompiledAssembly> dependencies,
        Stopwatch stopwatch,
        DebugInfoBuilder info,
        ProjectLanguage language)
    {
        var sorted = diagnostics.OrderByDescending(d => d.Severity).ToList();
        var result = new BuildResult(projectName, sorted, entry, dependencies, stopwatch.Elapsed);
        return new DebugBuild(result, entry is null ? DebugInfoMap.Empty : info.Build()) { Language = language };
    }
}
