using Microsoft.CodeAnalysis.Text;
using TabStudio.LiveTesting.Building;
using TabStudio.LiveTesting.Coverage;
using TabStudio.LiveTesting.Impact;
using TabStudio.TestRunner;

namespace TabStudio.LiveTesting.Results;

/// <summary>A test's latest live state, for counts and lists.</summary>
/// <param name="Key">The test.</param>
/// <param name="Test">The test as the newest build of its project found it.</param>
/// <param name="Result">Its latest live result, or <see langword="null"/> before it finished a live run.</param>
/// <param name="IsPending">Whether it waits to run again.</param>
public sealed record LiveTestState(TestKey Key, TestCase Test, TestResult? Result, bool IsPending);

/// <summary>
/// Everything Live Unit Testing knows: every test's latest result and coverage, and the coverage maps of the newest
/// builds, from which it answers which tests run each line of a file and how they fare.
/// </summary>
/// <remarks>
/// Results arrive test by test, from runs that only include the tests an edit affects, so the index merges them:
/// a test keeps its coverage until it runs again. Coverage is kept per member, and the newest build's map turns it
/// into lines, so markers stay on the right lines while the code around them changes. The index is safe to use
/// from any thread; <see cref="Changed"/> is raised on the thread that changed it.
/// </remarks>
public sealed class CoverageIndex
{
    private readonly Lock _gate = new();
    private readonly Dictionary<TestKey, Entry> _tests = [];
    private readonly Dictionary<MemberRef, HashSet<TestKey>> _byMember = [];
    private readonly Dictionary<string, FileMap> _files = new(StringComparer.Ordinal);
    private readonly HashSet<MemberRef> _changedMembers = [];

    /// <summary>Raised after results, coverage or maps changed.</summary>
    public event EventHandler? Changed;

    /// <summary>The source files with coverage markers: the files of the instrumented projects of the newest builds.</summary>
    public IReadOnlyList<string> Files
    {
        get
        {
            lock (_gate)
            {
                return [.. _files.Keys];
            }
        }
    }

    /// <summary>Every test known, with its latest result.</summary>
    public IReadOnlyList<LiveTestState> Tests
    {
        get
        {
            lock (_gate)
            {
                return [.. _tests.Select(p => new LiveTestState(p.Key, p.Value.Test, p.Value.Result, p.Value.IsPending))];
            }
        }
    }

    /// <summary>
    /// Takes in a new build of a test project: the maps of its instrumented projects replace the old ones, and the
    /// members that changed are stale until their tests run again.
    /// </summary>
    /// <param name="build">The successful build.</param>
    /// <param name="changes">What changed in its files since the previous build of the same test project.</param>
    public void ApplyBuild(CoverageBuild build, IReadOnlyList<FileChange> changes)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(changes);
        lock (_gate)
        {
            foreach (var project in build.Projects)
            {
                var path = project.Project.FilePath;
                foreach (var file in _files.Where(f => f.Value.Module.ProjectFilePath == path).Select(f => f.Key).ToList())
                {
                    _files.Remove(file);
                }
            }

            foreach (var module in build.Modules)
            {
                var sources = build.Projects.First(p => p.Project.FilePath == module.ProjectFilePath).Sources;
                foreach (var file in module.Files)
                {
                    if (sources.TryGetValue(file, out var text))
                    {
                        _files[file] = new FileMap(module, text);
                    }
                }
            }

            foreach (var change in changes)
            {
                if (!_files.TryGetValue(change.FilePath, out var map))
                {
                    continue;
                }

                var members = change.IsStructural
                    ? map.Module.MembersIn(change.FilePath).Select(m => m.Ref)
                    : change.Members.Where(m => m.Kind != MemberChangeKind.Removed).Select(m => new MemberRef(change.FilePath, m.Key));
                _changedMembers.UnionWith(members);
            }
        }

        OnChanged();
    }

    /// <summary>Sets the tests of a test project, from its newest build. Tests that are gone are forgotten.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="tests">Its tests.</param>
    public void SetTests(string projectFilePath, IReadOnlyList<TestCase> tests)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        ArgumentNullException.ThrowIfNull(tests);
        lock (_gate)
        {
            var current = tests.Select(t => new TestKey(projectFilePath, t.Id)).ToHashSet();
            foreach (var gone in _tests.Keys.Where(k => k.ProjectFilePath == projectFilePath && !current.Contains(k)).ToList())
            {
                SetCoverage(gone, null);
                _tests.Remove(gone);
            }

            foreach (var test in tests)
            {
                var key = new TestKey(projectFilePath, test.Id);
                if (_tests.TryGetValue(key, out var entry))
                {
                    entry.Test = test;
                }
                else
                {
                    _tests[key] = new Entry(test);
                }
            }
        }

        OnChanged();
    }

    /// <summary>Marks tests as waiting to run again, so the lines they run are drawn stale.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="testIds">The tests.</param>
    public void MarkPending(string projectFilePath, IEnumerable<string> testIds)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        ArgumentNullException.ThrowIfNull(testIds);
        var changed = false;
        lock (_gate)
        {
            foreach (var id in testIds)
            {
                if (_tests.TryGetValue(new TestKey(projectFilePath, id), out var entry) && !entry.IsPending)
                {
                    entry.IsPending = true;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            OnChanged();
        }
    }

    /// <summary>Forgets the results and coverage of tests, which then count for no line until they run again.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="testIds">The tests.</param>
    public void Forget(string projectFilePath, IEnumerable<string> testIds)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        ArgumentNullException.ThrowIfNull(testIds);
        lock (_gate)
        {
            foreach (var id in testIds)
            {
                var key = new TestKey(projectFilePath, id);
                if (_tests.TryGetValue(key, out var entry))
                {
                    SetCoverage(key, null);
                    entry.Result = null;
                    entry.IsPending = false;
                }
            }
        }

        OnChanged();
    }

    /// <summary>The tests of a test project that wait to run again, for example after a cancelled run.</summary>
    /// <param name="projectFilePath">The test project.</param>
    public IReadOnlyList<string> PendingTests(string projectFilePath)
    {
        lock (_gate)
        {
            return [.. _tests.Where(p => p.Key.ProjectFilePath == projectFilePath && p.Value.IsPending).Select(p => p.Key.TestId)];
        }
    }

    /// <summary>Records a test's result and the coverage of the run that produced it.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="result">The result.</param>
    /// <param name="coverage">What the test ran, or <see langword="null"/> when it did not start.</param>
    public void Record(string projectFilePath, TestResult result, TestCoverage? coverage)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        ArgumentNullException.ThrowIfNull(result);
        lock (_gate)
        {
            var key = new TestKey(projectFilePath, result.Test.Id);
            if (!_tests.TryGetValue(key, out var entry))
            {
                entry = new Entry(result.Test);
                _tests[key] = entry;
            }

            if (result.Outcome == TestOutcome.NotRun)
            {
                // The run stopped before the test finished: it still has to run.
                entry.IsPending = true;
            }
            else
            {
                entry.Result = result;
                entry.IsPending = false;
                SetCoverage(key, coverage ?? TestCoverage.Empty);
            }
        }

        OnChanged();
    }

    /// <summary>Ends a run that was not stopped: once no test waits any more, changed code is no longer stale.</summary>
    public void CompleteRun()
    {
        lock (_gate)
        {
            if (_tests.Values.Any(e => e.IsPending))
            {
                return;
            }

            _changedMembers.Clear();
        }

        OnChanged();
    }

    /// <summary>The coverage of a test's latest live run.</summary>
    /// <param name="key">The test.</param>
    /// <returns>The coverage, or <see langword="null"/> when the test never finished a live run.</returns>
    public TestCoverage? CoverageOf(TestKey key)
    {
        lock (_gate)
        {
            return _tests.TryGetValue(key, out var entry) ? entry.Coverage : null;
        }
    }

    /// <summary>The coverage of a file, line by line, or <see langword="null"/> when no instrumented project of a build has it.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public FileCoverage? GetFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        lock (_gate)
        {
            if (!_files.TryGetValue(filePath, out var map))
            {
                return null;
            }

            // Until every test has run once, a line no test runs yet may still be run by one.
            var coverageIncomplete = _tests.Values.Any(e => e.Coverage is null && e.IsPending);
            var lines = new SortedDictionary<int, LineBuilder>();
            foreach (var member in map.Module.MembersIn(filePath))
            {
                var reference = member.Ref;
                var memberStale = _changedMembers.Contains(reference);
                var covering = _byMember.TryGetValue(reference, out var keys) ? keys : null;
                foreach (var statement in map.Module.StatementsOf(member.Index))
                {
                    if (!lines.TryGetValue(statement.Line, out var line))
                    {
                        line = new LineBuilder();
                        lines[statement.Line] = line;
                    }

                    line.IsStale |= memberStale;
                    if (covering is null)
                    {
                        continue;
                    }

                    foreach (var key in covering)
                    {
                        if (_tests.TryGetValue(key, out var entry) && entry.Coverage?.Covers(reference, statement.Ordinal) == true)
                        {
                            line.Tests.Add(key);
                        }
                    }
                }
            }

            var result = new List<LineCoverage>(lines.Count);
            foreach (var (number, line) in lines)
            {
                var tests = line.Tests
                    .Select(key => (Key: key, Entry: _tests[key]))
                    .Select(t => new CoveringTest(t.Key, t.Entry.Test, t.Entry.Result?.Outcome, t.Entry.Result?.Duration ?? TimeSpan.Zero, t.Entry.IsPending))
                    .OrderBy(t => t.IsFailing ? 0 : 1)
                    .ThenBy(t => t.Test.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var state = tests.Count == 0
                    ? LineCoverageState.NotCovered
                    : tests.Exists(t => t.IsFailing) ? LineCoverageState.Failing : LineCoverageState.Covered;
                var stale = line.IsStale || tests.Exists(t => t.IsPending) || (tests.Count == 0 && coverageIncomplete);
                result.Add(new LineCoverage(number, state, stale, tests));
            }

            return new FileCoverage(filePath, map.Text, result);
        }
    }

    /// <summary>Forgets everything, for example when Live Unit Testing stops.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _tests.Clear();
            _byMember.Clear();
            _files.Clear();
            _changedMembers.Clear();
        }

        OnChanged();
    }

    private void SetCoverage(TestKey key, TestCoverage? coverage)
    {
        var entry = _tests[key];
        if (entry.Coverage is { } old)
        {
            foreach (var member in old.Members)
            {
                if (_byMember.TryGetValue(member, out var keys))
                {
                    keys.Remove(key);
                    if (keys.Count == 0)
                    {
                        _byMember.Remove(member);
                    }
                }
            }
        }

        entry.Coverage = coverage;
        if (coverage is null)
        {
            return;
        }

        foreach (var member in coverage.Members)
        {
            if (!_byMember.TryGetValue(member, out var keys))
            {
                keys = [];
                _byMember[member] = keys;
            }

            keys.Add(key);
        }
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>What the index keeps about one test.</summary>
    private sealed class Entry(TestCase test)
    {
        public TestCase Test { get; set; } = test;

        public TestResult? Result { get; set; }

        public TestCoverage? Coverage { get; set; }

        public bool IsPending { get; set; }
    }

    /// <summary>The newest map of a file: the module of its project and the text it was built from.</summary>
    private sealed record FileMap(CoverageModule Module, SourceText Text);

    /// <summary>A line being put together from the statements on it.</summary>
    private sealed class LineBuilder
    {
        public bool IsStale { get; set; }

        public HashSet<TestKey> Tests { get; } = [];
    }
}
