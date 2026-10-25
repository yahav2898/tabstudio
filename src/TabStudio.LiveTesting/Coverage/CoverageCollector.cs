using System.Collections.Concurrent;
using TabStudio.TestRunner;

namespace TabStudio.LiveTesting.Coverage;

/// <summary>
/// Collects coverage during one test run: gives every test, and every class's one-time setup and cleanup, a hit
/// table of its own, and turns the tables into <see cref="TestCoverage"/> once the tests finish.
/// </summary>
/// <remarks>
/// Pass it to the session as <see cref="TestRunOptions.ThreadHook"/>, through <see cref="CoverageHook"/>. Code that a
/// class's setup runs, such as an xUnit class fixture's constructor, counts for every test of the class in the run.
/// </remarks>
public sealed class CoverageCollector : ITestThreadHook
{
    private readonly CoverageModule[] _modules;
    private readonly int _slots;
    private readonly ConcurrentDictionary<string, bool[][]> _tests = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<(HashSet<string> Tests, bool[][] Table)> _stages = new();

    /// <summary>Creates a collector for the instrumented projects of a build.</summary>
    /// <param name="modules">The coverage maps of the build's instrumented projects.</param>
    public CoverageCollector(IReadOnlyList<CoverageModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _modules = [.. modules];
        _slots = _modules.Length == 0 ? 0 : _modules.Max(m => m.Index) + 1;
        CoverageRecorder.EnsureRegistered();
    }

    /// <inheritdoc />
    public IDisposable? EnterTest(TestCase test)
    {
        ArgumentNullException.ThrowIfNull(test);
        var table = CreateTable();
        _tests[test.Id] = table;
        return CoverageRecorder.Enter(table);
    }

    /// <inheritdoc />
    public IDisposable? EnterClassStage(IReadOnlyList<TestCase> tests)
    {
        ArgumentNullException.ThrowIfNull(tests);
        var table = CreateTable();
        _stages.Enqueue((tests.Select(t => t.Id).ToHashSet(StringComparer.Ordinal), table));
        return CoverageRecorder.Enter(table);
    }

    /// <summary>The coverage of a test that ran in this run.</summary>
    /// <param name="testId">The test's <see cref="TestCase.Id"/>.</param>
    /// <returns>The coverage, or <see langword="null"/> when the test did not start, for example because it is skipped.</returns>
    public TestCoverage? CoverageOf(string testId)
    {
        ArgumentNullException.ThrowIfNull(testId);
        if (!_tests.TryGetValue(testId, out var table))
        {
            return null;
        }

        var tables = new List<bool[][]> { table };
        tables.AddRange(_stages.Where(s => s.Tests.Contains(testId)).Select(s => s.Table));
        return Read(tables);
    }

    private bool[][] CreateTable()
    {
        var table = new bool[_slots][];
        foreach (var module in _modules)
        {
            table[module.Index] = new bool[module.PointCount];
        }

        return table;
    }

    /// <summary>Turns hit tables into the members and statements they reached.</summary>
    private TestCoverage Read(List<bool[][]> tables)
    {
        var members = new Dictionary<MemberRef, SortedSet<int>>();
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in _modules)
        {
            foreach (var table in tables)
            {
                if (table[module.Index] is not { } hits)
                {
                    continue;
                }

                for (var point = 0; point < hits.Length; point++)
                {
                    if (!Volatile.Read(ref hits[point]))
                    {
                        continue;
                    }

                    var member = module.Members[module.MemberOf(point)];
                    if (!members.TryGetValue(member.Ref, out var ordinals))
                    {
                        ordinals = [];
                        members[member.Ref] = ordinals;
                        types.Add(member.TypeName);
                    }

                    if (module.OrdinalOf(point) is >= 0 and var ordinal)
                    {
                        ordinals.Add(ordinal);
                    }
                }
            }
        }

        return new TestCoverage(members.ToDictionary(p => p.Key, p => p.Value.ToArray()), types);
    }
}
