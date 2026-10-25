using System.Reflection;
using TabStudio.Compilation;
using TabStudio.TestRunner.Execution;
using TabStudio.TestRunner.Frameworks;
using TabStudio.TestRunner.Loading;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner;

/// <summary>
/// The engine behind Test Explorer: loads a built test project, lists its tests and runs them inside
/// TabStudio's process.
/// </summary>
/// <remarks>
/// <para>
/// Create a session after every successful build of a test project with <see cref="Load"/> or
/// <see cref="LoadAsync"/>, passing the project's assemblies (the test project and the projects it
/// references, from <c>BuildResult</c>) and the restored package assemblies, such as <c>xunit.core.dll</c>.
/// Then read <see cref="Tests"/> and call <see cref="RunAllAsync"/>, <see cref="RunSelectedAsync"/> or
/// <see cref="RunFailedAsync"/>.
/// </para>
/// <para>
/// Tests are found by the names of the frameworks' attributes, so xUnit v2, NUnit 3 and 4 and MSTest work in
/// any version without their runners. By default one test runs at a time, each on its own thread with a
/// timeout, so a hung test cannot freeze the IDE.
/// </para>
/// <para>
/// <see cref="TestStarted"/>, <see cref="TestFinished"/> and <see cref="RunFinished"/> are raised on runner
/// threads, never on the UI thread; with <see cref="TestRunOptions.MaxParallelClasses"/> above 1 they may be
/// raised concurrently. Only one run happens at a time per session, and only one session redirects the console
/// at a time; a second run waits for the first.
/// </para>
/// </remarks>
public sealed class TestSession : IDisposable
{
    private readonly List<RunnableTest> _tests;
    private readonly List<MethodLocator> _locators;
    private readonly TestRunOptions _options;
    private readonly StackTraceCleaner _stackTraces;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, TestResult> _latestResults = new(StringComparer.Ordinal);
    private bool _running;
    private bool _disposed;

    private TestSession(List<RunnableTest> tests, List<string> problems, List<MethodLocator> locators, HashSet<string> userTypes, TestRunOptions options)
    {
        _tests = tests;
        _locators = locators;
        _options = options;
        _stackTraces = new StackTraceCleaner(userTypes);
        Tests = tests.Select(t => t.Case).ToList();
        DiscoveryMessages = problems;
    }

    /// <summary>Raised when a test starts. Skipped tests get one too, just before they finish.</summary>
    public event EventHandler<TestStartedEventArgs>? TestStarted;

    /// <summary>Raised when a test finishes, with its result.</summary>
    public event EventHandler<TestFinishedEventArgs>? TestFinished;

    /// <summary>Raised when a run finishes, with its totals.</summary>
    public event EventHandler<TestRunFinishedEventArgs>? RunFinished;

    /// <summary>Every test found, in the order the runner runs them: by assembly, class and declaration.</summary>
    public IReadOnlyList<TestCase> Tests { get; }

    /// <summary>Problems found while discovering, such as types that could not be loaded.</summary>
    public IReadOnlyList<string> DiscoveryMessages { get; }

    /// <summary>Whether a run is in progress.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    /// <summary>The latest result of every test that has run in this session, by <see cref="TestCase.Id"/>.</summary>
    public IReadOnlyDictionary<string, TestResult> LatestResults
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, TestResult>(_latestResults, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>Loads a test project and discovers its tests.</summary>
    /// <param name="assemblies">
    /// The assemblies compiled in memory: the test project and the projects it references. Tests are discovered in all of them.
    /// </param>
    /// <param name="runtimeAssemblyPaths">
    /// The files of the test framework and other packages, such as <c>xunit.core.dll</c>, <c>xunit.assert.dll</c>
    /// and <c>xunit.abstractions.dll</c>. Assemblies next to them are found too.
    /// </param>
    /// <param name="options">How tests run, or <see langword="null"/> for <see cref="TestRunOptions.Default"/>.</param>
    /// <remarks>
    /// Discovery runs the user's data sources, such as <c>[MemberData]</c> properties, to list each data row as its
    /// own test. Call this off the UI thread, or use <see cref="LoadAsync"/>.
    /// </remarks>
    public static TestSession Load(IReadOnlyList<CompiledAssembly> assemblies, IReadOnlyList<string> runtimeAssemblyPaths, TestRunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(runtimeAssemblyPaths);
        options ??= TestRunOptions.Default;
        ArgumentOutOfRangeException.ThrowIfLessThan(options.StackSize, 256 * 1024, nameof(options));

        var name = assemblies.Count > 0 ? assemblies[0].Name : "tests";
        var packages = options.ShareRuntimeAssemblies ? PackageLoadContext.For(runtimeAssemblyPaths) : null;
        var context = new TestLoadContext(name, assemblies, runtimeAssemblyPaths, packages);
        var problems = new List<string>();
        var loaded = new List<Assembly>();
        var locators = new Dictionary<Assembly, MethodLocator>();
        foreach (var compiled in assemblies)
        {
            try
            {
                var assembly = context.LoadCompiled(compiled);
                loaded.Add(assembly);
                if (MethodLocator.TryCreate(compiled.Symbols) is { } locator)
                {
                    locators[assembly] = locator;
                }
            }
            catch (BadImageFormatException ex)
            {
                problems.Add($"'{compiled.Name}' could not be loaded: {ex.Message}");
            }
        }

        var discovery = new DiscoveryContext(method =>
            method.Module.Assembly is { } owner && locators.TryGetValue(owner, out var locator) ? locator.Find(method) : null);
        var tests = ReflectionDiscoverer.Discover(loaded, discovery, problems);
        var userTypes = StackTraceCleaner.TypeNamesOf(loaded.SelectMany(a => AssemblyTypes.Of(a)));
        return new TestSession(tests, problems, [.. locators.Values], userTypes, options);
    }

    /// <summary>Loads a test project and discovers its tests on a background thread.</summary>
    /// <param name="assemblies">The assemblies compiled in memory, as for <see cref="Load"/>.</param>
    /// <param name="runtimeAssemblyPaths">The framework and package assemblies, as for <see cref="Load"/>.</param>
    /// <param name="options">How tests run, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">Cancels before loading starts.</param>
    public static Task<TestSession> LoadAsync(
        IReadOnlyList<CompiledAssembly> assemblies,
        IReadOnlyList<string> runtimeAssemblyPaths,
        TestRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(runtimeAssemblyPaths);
        return Task.Factory.StartNew(
            () => Load(assemblies, runtimeAssemblyPaths, options),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    /// <summary>Finds the tests that a set of names selects.</summary>
    /// <param name="names">
    /// Each name selects the tests whose <see cref="TestCase.Id"/>, <see cref="TestCase.FullyQualifiedName"/>
    /// (every row of a method), <see cref="TestCase.TypeFullName"/> (every test of a class) or namespace matches.
    /// </param>
    /// <returns>The selected tests, in run order. Explicit tests are included only when their id or method name is given.</returns>
    public IReadOnlyList<TestCase> FindTests(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var wanted = names.ToHashSet(StringComparer.Ordinal);
        return _tests
            .Where(t => wanted.Contains(t.Case.Id) || wanted.Contains(t.Case.FullyQualifiedName) ||
                (!t.Case.IsExplicit && (wanted.Contains(t.Case.TypeFullName) || IsInNamespace(t.Case, wanted))))
            .Select(t => t.Case)
            .ToList();
    }

    /// <summary>Runs every test except explicit ones.</summary>
    /// <param name="cancellationToken">Stops the run.</param>
    public Task<TestRunSummary> RunAllAsync(CancellationToken cancellationToken = default) =>
        RunCoreAsync(_tests.Where(t => !t.Case.IsExplicit).ToList(), cancellationToken);

    /// <summary>Runs the tests that a set of names selects, as described in <see cref="FindTests"/>.</summary>
    /// <param name="names">Test ids, method names, class names or namespaces.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    public Task<TestRunSummary> RunSelectedAsync(IEnumerable<string> names, CancellationToken cancellationToken = default) =>
        RunAsync(FindTests(names), cancellationToken);

    /// <summary>Runs the given tests of this session.</summary>
    /// <param name="tests">Tests from <see cref="Tests"/>. Tests this session does not know are ignored.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    public Task<TestRunSummary> RunAsync(IEnumerable<TestCase> tests, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tests);
        var ids = tests.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        return RunCoreAsync(_tests.Where(t => ids.Contains(t.Case.Id)).ToList(), cancellationToken);
    }

    /// <summary>Runs the tests whose latest result in this session is failed or errored.</summary>
    /// <param name="cancellationToken">Stops the run.</param>
    public Task<TestRunSummary> RunFailedAsync(CancellationToken cancellationToken = default)
    {
        HashSet<string> failed;
        lock (_gate)
        {
            failed = _latestResults.Values
                .Where(r => r.Outcome is TestOutcome.Failed or TestOutcome.Errored)
                .Select(r => r.Test.Id)
                .ToHashSet(StringComparer.Ordinal);
        }

        return RunCoreAsync(_tests.Where(t => failed.Contains(t.Case.Id)).ToList(), cancellationToken);
    }

    /// <summary>Releases the symbol readers. The loaded assemblies stay in memory until the process ends.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        foreach (var locator in _locators)
        {
            locator.Dispose();
        }
    }

    private async Task<TestRunSummary> RunCoreAsync(List<RunnableTest> tests, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_running)
            {
                throw new InvalidOperationException("A test run is already in progress in this session.");
            }

            _running = true;
        }

        try
        {
            var executor = new TestExecutor(_options, _stackTraces, OnTestStarted, OnTestFinished);
            var summary = await executor.RunAsync(tests, cancellationToken).ConfigureAwait(false);
            RunFinished?.Invoke(this, new TestRunFinishedEventArgs(summary));
            return summary;
        }
        finally
        {
            lock (_gate)
            {
                _running = false;
            }
        }
    }

    private void OnTestStarted(TestCase test) => TestStarted?.Invoke(this, new TestStartedEventArgs(test));

    private void OnTestFinished(TestResult result)
    {
        lock (_gate)
        {
            _latestResults[result.Test.Id] = result;
        }

        TestFinished?.Invoke(this, new TestFinishedEventArgs(result));
    }

    private static bool IsInNamespace(TestCase test, HashSet<string> names)
    {
        var namespaceName = test.NamespaceName;
        while (namespaceName.Length > 0)
        {
            if (names.Contains(namespaceName))
            {
                return true;
            }

            var dot = namespaceName.LastIndexOf('.');
            namespaceName = dot < 0 ? string.Empty : namespaceName[..dot];
        }

        return false;
    }
}
