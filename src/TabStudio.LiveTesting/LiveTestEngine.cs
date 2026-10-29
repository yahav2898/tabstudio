using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation;
using TabStudio.Compilation.VisualBasic;
using TabStudio.Core.Workspaces;
using TabStudio.LiveTesting.Building;
using TabStudio.LiveTesting.Coverage;
using TabStudio.LiveTesting.Impact;
using TabStudio.LiveTesting.Results;
using TabStudio.TestRunner;

namespace TabStudio.LiveTesting;

/// <summary>Builds a test project for coverage, as <see cref="CoverageBuilder.BuildAsync"/> does.</summary>
/// <param name="testProject">The test project.</param>
/// <param name="uninstrumented">Projects to leave without coverage.</param>
/// <param name="cancellationToken">Cancels the build.</param>
public delegate Task<CoverageBuild> CoverageBuildFunction(ProjectInfo testProject, IReadOnlySet<string> uninstrumented, CancellationToken cancellationToken);

/// <summary>How the engine runs tests.</summary>
public sealed record LiveTestEngineOptions
{
    /// <summary>The default options.</summary>
    public static LiveTestEngineOptions Default { get; } = new();

    /// <summary>
    /// How long a test may run when it sets no timeout of its own. Live runs keep it short, so a test that hangs
    /// after an edit does not hold back the results of the others.
    /// </summary>
    public TimeSpan TestTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>What a live run did.</summary>
/// <param name="TestsRun">How many tests ran.</param>
/// <param name="FailedBuilds">The builds that failed, whose tests did not run.</param>
/// <param name="Problems">Messages that belong to no test, such as a test project that could not be loaded.</param>
public sealed record LiveRunSummary(int TestsRun, IReadOnlyList<CoverageBuild> FailedBuilds, IReadOnlyList<string> Problems);

/// <summary>
/// The engine of Live Unit Testing: builds test projects with the code they test instrumented, works out which
/// tests the latest edits affect, runs them, and keeps every test's result and coverage in <see cref="Index"/>.
/// </summary>
/// <remarks>
/// <para>
/// A run goes test project by test project: a coverage build, which only recompiles what changed; a comparison
/// of every file with the previous build, member by member; the tests that the changes affect (see
/// <see cref="ImpactAnalyzer"/>), the tests a stopped run left behind, and the tests asked for; then those tests,
/// each recording its coverage. A test project whose assemblies did not change keeps its loaded session.
/// </para>
/// <para>
/// Runs never overlap: a second one waits for the first. Cancelling a run stops it after the test in progress,
/// and the tests it did not reach run in the next one. The engine does not decide when to run; the IDE does, after
/// a pause in typing or a save.
/// </para>
/// </remarks>
public sealed class LiveTestEngine : IDisposable
{
    private readonly CoverageBuildFunction _build;
    private readonly Func<string, IReadOnlyList<string>> _runtimeAssemblies;
    private readonly LiveTestEngineOptions _options;
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, ProjectState> _projects = new(StringComparer.Ordinal);
    private readonly CoverageHook _hook = new();
    private bool _disposed;

    /// <summary>Creates an engine.</summary>
    /// <param name="build">Builds a test project for coverage.</param>
    /// <param name="runtimeAssemblies">The package assemblies a test project needs to run, such as xUnit's.</param>
    /// <param name="inclusion">Which projects and tests are included, or <see langword="null"/> for everything.</param>
    /// <param name="options">How tests run, or <see langword="null"/> for the defaults.</param>
    public LiveTestEngine(CoverageBuildFunction build, Func<string, IReadOnlyList<string>> runtimeAssemblies, LiveTestInclusion? inclusion = null, LiveTestEngineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(runtimeAssemblies);
        _build = build;
        _runtimeAssemblies = runtimeAssemblies;
        Inclusion = inclusion ?? new LiveTestInclusion();
        _options = options ?? LiveTestEngineOptions.Default;
    }

    /// <summary>Every test's latest result and coverage, and the maps that turn coverage into lines.</summary>
    public CoverageIndex Index { get; } = new();

    /// <summary>Which projects and tests run live.</summary>
    public LiveTestInclusion Inclusion { get; }

    /// <summary>Runs the tests of test projects that the changes since their last live run affect.</summary>
    /// <param name="testProjects">The test projects to consider. Excluded ones are skipped.</param>
    /// <param name="listener">Follows the run.</param>
    /// <param name="requested">Tests to run whatever changed, such as a test the user asked to run from a marker.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <exception cref="OperationCanceledException">The run was stopped.</exception>
    public async Task<LiveRunSummary> RunAsync(
        IReadOnlyList<ProjectInfo> testProjects,
        ILiveTestListener listener,
        IReadOnlyCollection<TestKey>? requested = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testProjects);
        ArgumentNullException.ThrowIfNull(listener);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _runGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var testsRun = 0;
            var failedBuilds = new List<CoverageBuild>();
            var problems = new List<string>();
            foreach (var project in testProjects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Inclusion.IsProjectIncluded(project.FilePath))
                {
                    testsRun += await RunProjectAsync(project, listener, requested ?? [], failedBuilds, problems, cancellationToken).ConfigureAwait(false);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            Index.CompleteRun();
            return new LiveRunSummary(testsRun, failedBuilds, problems);
        }
        finally
        {
            _hook.Collector = null;
            _runGate.Release();
        }
    }

    /// <summary>Makes tests run again in the next run, for example after they were included again.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="testIds">The tests.</param>
    public void Invalidate(string projectFilePath, IEnumerable<string> testIds) => Index.MarkPending(projectFilePath, testIds);

    /// <summary>Forgets the results and coverage of tests, for example after they were excluded, so no line counts them.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="testIds">The tests.</param>
    public void Forget(string projectFilePath, IEnumerable<string> testIds) => Index.Forget(projectFilePath, testIds);

    /// <summary>Forgets what was built, so the next run of each test project builds and runs everything.</summary>
    public void Reset()
    {
        List<ProjectState> states;
        lock (_gate)
        {
            states = [.. _projects.Values];
            _projects.Clear();
        }

        foreach (var state in states)
        {
            state.Session?.Dispose();
        }

        Index.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Reset();
        _runGate.Dispose();
    }

    private async Task<int> RunProjectAsync(
        ProjectInfo project,
        ILiveTestListener listener,
        IReadOnlyCollection<TestKey> requested,
        List<CoverageBuild> failedBuilds,
        List<string> problems,
        CancellationToken cancellationToken)
    {
        listener.BuildStarted(project);
        var build = await _build(project, Inclusion.ExcludedProjects, cancellationToken).ConfigureAwait(false);
        listener.BuildFinished(project, build);
        if (!build.Success)
        {
            failedBuilds.Add(build);
            return 0;
        }

        var state = StateOf(project.FilePath);
        var sources = build.Sources;
        var changes = await Task.Run(() => Compare(state.Sources, build, cancellationToken), cancellationToken).ConfigureAwait(false);
        var instrumented = build.Projects.Where(p => p.IsInstrumented).Select(p => p.Project.FilePath).ToHashSet(StringComparer.Ordinal);
        var instrumentationChanged = state.Instrumented is { } previous && !previous.SetEquals(instrumented);
        Index.ApplyBuild(build, changes);

        TestSession session;
        try
        {
            session = await LoadAsync(state, project, build, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            problems.Add($"The tests of {project.Name} could not be loaded: {ex.Message}");
            return 0;
        }

        Index.SetTests(project.FilePath, session.Tests);
        listener.TestsDiscovered(project, session.Tests);

        var path = project.FilePath;
        var affected = instrumentationChanged
            ? session.Tests.Where(t => !t.IsExplicit).ToList()
            : ImpactAnalyzer.Select(session.Tests, build, changes, t => Index.CoverageOf(new TestKey(path, t.Id)));
        var wanted = affected.Select(t => t.Id)
            .Concat(Index.PendingTests(path))
            .ToHashSet(StringComparer.Ordinal);
        var asked = requested.Where(k => k.ProjectFilePath == path).Select(k => k.TestId).ToHashSet(StringComparer.Ordinal);
        var tests = session.Tests
            .Where(t => asked.Contains(t.Id) || (wanted.Contains(t.Id) && !t.IsExplicit && Inclusion.IsIncluded(path, t)))
            .ToList();

        state.Sources = sources;
        state.Instrumented = instrumented;
        Index.MarkPending(path, tests.Select(t => t.Id));
        listener.TestsQueued(project, tests);
        if (tests.Count == 0)
        {
            return 0;
        }

        var collector = new CoverageCollector(build.Modules);
        void OnStarted(object? sender, TestStartedEventArgs e) => listener.TestStarted(project, e.Test);
        void OnFinished(object? sender, TestFinishedEventArgs e)
        {
            Index.Record(path, e.Result, collector.CoverageOf(e.Result.Test.Id));
            listener.TestFinished(project, e.Result);
        }

        _hook.Collector = collector;
        session.TestStarted += OnStarted;
        session.TestFinished += OnFinished;
        try
        {
            var summary = await session.RunAsync(tests, cancellationToken).ConfigureAwait(false);
            problems.AddRange(summary.Messages);
            return summary.Results.Count;
        }
        finally
        {
            session.TestStarted -= OnStarted;
            session.TestFinished -= OnFinished;
            _hook.Collector = null;
        }
    }

    /// <summary>Loads a build into a test session, or keeps the session of the previous build when its assemblies are the same.</summary>
    private async Task<TestSession> LoadAsync(ProjectState state, ProjectInfo project, CoverageBuild build, CancellationToken cancellationToken)
    {
        List<CompiledAssembly> assemblies = [build.Result.EntryAssembly!, .. build.Result.Dependencies];
        var packages = _runtimeAssemblies(project.FilePath);
        if (state.Session is { } loaded && state.IsLoadedFrom(assemblies, packages))
        {
            return loaded;
        }

        // Discovery can run the code under test, through data sources: the coverage runtime must find its link first.
        CoverageRecorder.EnsureRegistered();
        // A session is loaded after every edit that changes code, and none can be unloaded: they share the packages.
        var options = new TestRunOptions { ThreadHook = _hook, DefaultTimeout = _options.TestTimeout, ShareRuntimeAssemblies = true };
        var session = await TestSession.LoadAsync(assemblies, packages, options, cancellationToken).ConfigureAwait(false);
        state.Session?.Dispose();
        state.Session = session;
        state.Assemblies = assemblies;
        state.Packages = packages;
        return session;
    }

    /// <summary>
    /// Compares every file of two builds of a test project. Members of Visual Basic files are named with their
    /// project's root namespace, as the instrumented build names them.
    /// </summary>
    private static List<FileChange> Compare(IReadOnlyDictionary<string, SourceText>? before, CoverageBuild build, CancellationToken cancellationToken)
    {
        var changes = new List<FileChange>();
        if (before is null)
        {
            return changes;
        }

        var after = build.Sources;
        foreach (var path in before.Keys.Union(after.Keys, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rootNamespace = build.ProjectOf(path)?.Project is { IsVisualBasic: true } project ? VisualBasicCompiler.RootNamespaceOf(project) : string.Empty;
            if (FileChange.Compare(path, before.GetValueOrDefault(path), after.GetValueOrDefault(path), rootNamespace, cancellationToken) is { } change)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    private ProjectState StateOf(string projectFilePath)
    {
        lock (_gate)
        {
            if (!_projects.TryGetValue(projectFilePath, out var state))
            {
                state = new ProjectState();
                _projects[projectFilePath] = state;
            }

            return state;
        }
    }

    /// <summary>What the engine keeps about one test project between runs.</summary>
    private sealed class ProjectState
    {
        /// <summary>The loaded tests of its newest build.</summary>
        public TestSession? Session { get; set; }

        /// <summary>The assemblies the session was loaded from.</summary>
        public List<CompiledAssembly>? Assemblies { get; set; }

        /// <summary>The package assemblies the session was loaded with.</summary>
        public IReadOnlyList<string>? Packages { get; set; }

        /// <summary>The source files of the build whose changes the coverage reflects.</summary>
        public IReadOnlyDictionary<string, SourceText>? Sources { get; set; }

        /// <summary>The projects that build instrumented.</summary>
        public HashSet<string>? Instrumented { get; set; }

        /// <summary>Whether a build produced exactly the assemblies the session was loaded from.</summary>
        public bool IsLoadedFrom(List<CompiledAssembly> assemblies, IReadOnlyList<string> packages) =>
            Assemblies is { } loaded
            && Packages is { } loadedPackages
            && loadedPackages.SequenceEqual(packages, StringComparer.Ordinal)
            && loaded.Count == assemblies.Count
            && loaded.Zip(assemblies).All(pair => ReferenceEquals(pair.First, pair.Second)
                || (pair.First.Name == pair.Second.Name && pair.First.Image.AsSpan().SequenceEqual(pair.Second.Image)));
    }
}
