using System.Diagnostics;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.LiveTesting.Coverage;
using TabStudio.LiveTesting.Instrumentation;
using ProjectInfo = TabStudio.Core.Workspaces.ProjectInfo;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.LiveTesting.Building;

/// <summary>
/// Builds a test project for Live Unit Testing: the test project as it is, and the projects it references
/// instrumented, so every test records the lines it runs. C# and Visual Basic projects are built, and may reference
/// each other.
/// </summary>
/// <remarks>
/// <para>
/// Builds go through the <see cref="RoslynWorkspaceHost"/>, so they compile the editor's current text, saved or
/// not. A project is compiled again only when its compilation changed: Roslyn keeps the compilation of a project
/// whose code and dependencies did not change, and the builder keeps the assembly it emitted from it, so after an
/// edit only the edited project and the projects that depend on it are rebuilt.
/// </para>
/// <para>
/// Instrumentation starts only once the original code compiles without errors. Instances are safe to share; builds
/// of different test projects may run at the same time.
/// </para>
/// </remarks>
public sealed class CoverageBuilder
{
    /// <summary>Reported when instrumented code fails to compile, which is a bug in Live Unit Testing.</summary>
    public const string InstrumentationFailedId = "TS3101";

    private static readonly EmitOptions EmitOptions = new(debugInformationFormat: DebugInformationFormat.PortablePdb);

    private readonly RoslynWorkspaceHost _host;
    private readonly Func<string, int> _slots;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, BuiltProject> _cache = new(StringComparer.Ordinal);

    /// <summary>Creates a builder over a loaded workspace host, for tests that run in this process.</summary>
    /// <param name="host">The host that holds the projects.</param>
    public CoverageBuilder(RoslynWorkspaceHost host)
        : this(host, SlotOf)
    {
    }

    /// <summary>
    /// Creates a builder over a loaded workspace host, for tests that run in another process, such as a tablet that
    /// a TabStudio Link host builds for: the instrumented code reports to the slots that process gives its projects.
    /// </summary>
    /// <param name="host">The host that holds the projects.</param>
    /// <param name="slots">The slot of a project, by its full path on this device, as <see cref="SlotOf"/> gives it in the process that runs the tests.</param>
    public CoverageBuilder(RoslynWorkspaceHost host, Func<string, int> slots)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(slots);
        _host = host;
        _slots = slots;
    }

    /// <summary>
    /// The slot of a project in the hit table of this process: the same for every build of the project for as long
    /// as the process runs, so every assembly built for it reports to its own slot.
    /// </summary>
    /// <param name="projectFilePath">The project.</param>
    public static int SlotOf(string projectFilePath)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        return CoverageModuleBuilder.IndexOf(projectFilePath);
    }

    /// <summary>Builds a test project and the projects it references, instrumenting the projects it tests.</summary>
    /// <param name="testProject">The test project.</param>
    /// <param name="uninstrumented">Projects to leave without coverage, such as those excluded from Live Unit Testing.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    public async Task<CoverageBuild> BuildAsync(ProjectInfo testProject, IReadOnlySet<string>? uninstrumented = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testProject);

        var stopwatch = Stopwatch.StartNew();
        var diagnostics = new List<BuildDiagnostic>();
        var projects = new List<CoverageBuildProject>();
        var modules = new List<CoverageModule>();
        var dependencies = new List<CompiledAssembly>();
        if (!testProject.IsRoslynLanguage || _host.FindProject(testProject.FilePath) is null)
        {
            diagnostics.Add(new BuildDiagnostic(
                ProjectBuilder.UnsupportedLanguageId,
                DiagnosticSeverityLevel.Error,
                $"'{testProject.Name}' cannot be built on this device: only C# and Visual Basic projects are compiled by TabStudio.",
                null,
                testProject.Name));
            return Finish(testProject, diagnostics, null, dependencies, projects, modules, stopwatch);
        }

        CompiledAssembly? entry = null;
        foreach (var current in _host.GetBuildOrder(testProject.FilePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            diagnostics.AddRange(current.PackageReferences
                .Where(package => !_host.IsPackageRestored(current.FilePath, package.Name))
                .Select(package => new BuildDiagnostic(
                    ProjectBuilder.UnrestoredPackageId,
                    DiagnosticSeverityLevel.Warning,
                    $"NuGet package '{package.Name}' is not restored on this device, so code that uses it will not compile.",
                    null,
                    current.Name)));

            var isEntry = string.Equals(current.FilePath, testProject.FilePath, StringComparison.Ordinal);
            var instrument = !isEntry && !current.IsTestProject && uninstrumented?.Contains(current.FilePath) != true;
            if (await _host.GetCompilationAsync(current.FilePath, cancellationToken).ConfigureAwait(false) is not { } compilation
                || Compile(current, compilation, instrument, diagnostics, cancellationToken) is not { } built)
            {
                return Finish(testProject, diagnostics, null, dependencies, projects, modules, stopwatch);
            }

            projects.Add(new CoverageBuildProject(current, instrument, built.Sources));
            if (built.Module is { } module)
            {
                modules.Add(module);
            }

            if (isEntry)
            {
                entry = built.Assembly;
            }
            else
            {
                dependencies.Add(built.Assembly);
            }
        }

        return Finish(testProject, diagnostics, entry, dependencies, projects, modules, stopwatch);
    }

    /// <summary>Forgets the assemblies kept from earlier builds, under memory pressure. The next build compiles everything again.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _cache.Clear();
        }
    }

    /// <summary>Compiles one project, or returns what was compiled from the same compilation before.</summary>
    private BuiltProject? Compile(ProjectInfo project, RoslynCompilation compilation, bool instrument, List<BuildDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var slot = instrument ? _slots(project.FilePath) : -1;
        lock (_gate)
        {
            if (_cache.TryGetValue(project.FilePath, out var cached) && ReferenceEquals(cached.Compilation, compilation) && cached.IsInstrumented == instrument
                && (cached.Module?.Index ?? -1) == slot)
            {
                return cached;
            }
        }

        var errors = DiagnosticMapper.Map(compilation.GetDiagnostics(cancellationToken), project.Name)
            .Where(d => d.Severity == DiagnosticSeverityLevel.Error)
            .ToList();
        if (errors.Count > 0)
        {
            diagnostics.AddRange(errors);
            return null;
        }

        CompiledAssembly? assembly;
        CoverageModule? module = null;
        var resources = ManifestResources.For(project);
        if (instrument)
        {
            var (instrumented, map) = CoverageInstrumenter.Instrument(compilation, project.FilePath, slot, cancellationToken);
            assembly = Emit(instrumented, project.Name, resources, out var emitErrors, cancellationToken);
            if (assembly is null)
            {
                var failure = emitErrors.Count == 0
                    ? "the compiler reported no error"
                    : $"{emitErrors[0].Id}: {emitErrors[0].GetMessage(CultureInfo.InvariantCulture)} at {emitErrors[0].Location.GetLineSpan()}";
                diagnostics.Add(new BuildDiagnostic(
                    InstrumentationFailedId,
                    DiagnosticSeverityLevel.Error,
                    $"Live Unit Testing could not prepare '{project.Name}' ({failure}). Run its tests from the Test Explorer, and please report this as a TabStudio bug.",
                    null,
                    project.Name));
                return null;
            }

            module = map;
        }
        else
        {
            assembly = Emit(compilation, project.Name, resources, out var emitErrors, cancellationToken);
            if (assembly is null)
            {
                diagnostics.AddRange(DiagnosticMapper.Map(emitErrors, project.Name));
                return null;
            }
        }

        var built = new BuiltProject(compilation, instrument, assembly, module, SourcesOf(compilation, cancellationToken));
        lock (_gate)
        {
            _cache[project.FilePath] = built;
        }

        return built;
    }

    /// <summary>Emits a compilation into memory, with its portable PDB and the project's resources.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <param name="projectName">The assembly name to use when the compilation has none.</param>
    /// <param name="resources">The project's embedded and Avalonia resources.</param>
    /// <param name="errors">The errors, when emitting failed.</param>
    /// <param name="cancellationToken">Cancels emitting.</param>
    private static CompiledAssembly? Emit(
        RoslynCompilation compilation,
        string projectName,
        IReadOnlyList<ResourceDescription> resources,
        out List<Diagnostic> errors,
        CancellationToken cancellationToken)
    {
        using var image = new MemoryStream();
        using var symbols = new MemoryStream();
        var emit = compilation.Emit(image, symbols, manifestResources: resources, options: EmitOptions, cancellationToken: cancellationToken);
        errors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        return emit.Success ? new CompiledAssembly(compilation.AssemblyName ?? projectName, image.ToArray(), symbols.ToArray()) : null;
    }

    /// <summary>The text of every source file the user wrote, by full path.</summary>
    private static Dictionary<string, SourceText> SourcesOf(RoslynCompilation compilation, CancellationToken cancellationToken)
    {
        var sources = new Dictionary<string, SourceText>(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (!GeneratedSource.IsGenerated(tree, cancellationToken))
            {
                sources[tree.FilePath] = tree.GetText(cancellationToken);
            }
        }

        return sources;
    }

    private static CoverageBuild Finish(
        ProjectInfo project,
        List<BuildDiagnostic> diagnostics,
        CompiledAssembly? entry,
        List<CompiledAssembly> dependencies,
        List<CoverageBuildProject> projects,
        List<CoverageModule> modules,
        Stopwatch stopwatch)
    {
        var sorted = diagnostics.OrderByDescending(d => d.Severity).ToList();
        var result = new BuildResult(project.Name, sorted, entry, dependencies, stopwatch.Elapsed);
        return entry is null
            ? new CoverageBuild(result, projects, [])
            : new CoverageBuild(result, projects, modules);
    }

    /// <summary>A project compiled for coverage, kept until its compilation changes.</summary>
    /// <param name="Compilation">The compilation it was built from.</param>
    /// <param name="IsInstrumented">Whether it was instrumented.</param>
    /// <param name="Assembly">The emitted assembly.</param>
    /// <param name="Module">Its coverage map, when instrumented.</param>
    /// <param name="Sources">Its source files.</param>
    private sealed record BuiltProject(
        RoslynCompilation Compilation,
        bool IsInstrumented,
        CompiledAssembly Assembly,
        CoverageModule? Module,
        IReadOnlyDictionary<string, SourceText> Sources);
}
