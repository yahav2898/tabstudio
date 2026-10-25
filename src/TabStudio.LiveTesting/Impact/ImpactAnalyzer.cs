using TabStudio.LiveTesting.Building;
using TabStudio.LiveTesting.Coverage;
using TabStudio.TestRunner;

namespace TabStudio.LiveTesting.Impact;

/// <summary>
/// Picks the tests of a test project that an edit can affect, from the coverage of their last run, so Live Unit
/// Testing runs only those.
/// </summary>
/// <remarks>
/// <para>A test runs again when:</para>
/// <list type="bullet">
/// <item>its coverage is unknown: it never ran live, or it is new;</item>
/// <item>it ran a member of the code under test whose body changed or that was removed;</item>
/// <item>a member was added to a type it ran code of (a new override can change what it calls);</item>
/// <item>its own test method changed, or a member of its test class that is not a test, such as its constructor or a helper.</item>
/// </list>
/// <para>
/// Every test of the project runs again when coverage cannot tell: code outside function bodies changed (a field, a
/// constant, a base type, a using directive), a file was added or removed, a helper outside the test classes
/// changed, a project without coverage changed, or a project's instrumentation was turned on or off.
/// </para>
/// </remarks>
public static class ImpactAnalyzer
{
    /// <summary>Picks the tests to run after a test project's code changed.</summary>
    /// <param name="tests">The tests of the test project, from its newest build.</param>
    /// <param name="build">The newest coverage build of the test project.</param>
    /// <param name="changes">What changed in the build's files since the coverage of the tests was collected.</param>
    /// <param name="coverageOf">The coverage of a test's last live run, or <see langword="null"/> when it never ran live.</param>
    /// <returns>The tests to run, in the order of <paramref name="tests"/>. Explicit tests are left out.</returns>
    public static IReadOnlyList<TestCase> Select(
        IReadOnlyList<TestCase> tests,
        CoverageBuild build,
        IReadOnlyList<FileChange> changes,
        Func<TestCase, TestCoverage?> coverageOf)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(coverageOf);

        var candidates = tests.Where(t => !t.IsExplicit).ToList();
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var test in candidates)
        {
            if (coverageOf(test) is null)
            {
                selected.Add(test.Id);
            }
        }

        var testProject = build.Projects.Count > 0 ? build.Projects[^1] : null;
        foreach (var change in changes)
        {
            // A file that left the build, or that has no coverage, can affect anything.
            var owner = build.ProjectOf(change.FilePath);
            if (owner is null)
            {
                return candidates;
            }

            if (ReferenceEquals(owner, testProject))
            {
                if (!AddTestFileImpact(change, candidates, selected))
                {
                    return candidates;
                }
            }
            else if (!owner.IsInstrumented || change.IsStructural)
            {
                return candidates;
            }
            else
            {
                AddProductImpact(change, candidates, coverageOf, selected);
            }
        }

        return candidates.Where(t => selected.Contains(t.Id)).ToList();
    }

    /// <summary>Adds the tests that ran the changed members of a file under test.</summary>
    private static void AddProductImpact(FileChange change, List<TestCase> tests, Func<TestCase, TestCoverage?> coverageOf, HashSet<string> selected)
    {
        foreach (var member in change.Members)
        {
            var reference = new MemberRef(change.FilePath, member.Key);
            foreach (var test in tests)
            {
                if (coverageOf(test) is not { } coverage)
                {
                    continue;
                }

                var affected = member.Kind == MemberChangeKind.Added
                    ? coverage.TouchesType(member.TypeName)
                    : coverage.Touches(reference);
                if (affected)
                {
                    selected.Add(test.Id);
                }
            }
        }
    }

    /// <summary>Adds the tests a change to a file of the test project itself affects.</summary>
    /// <returns><see langword="false"/> when the change can affect every test of the project.</returns>
    private static bool AddTestFileImpact(FileChange change, List<TestCase> tests, HashSet<string> selected)
    {
        if (change.IsStructural)
        {
            return false;
        }

        foreach (var member in change.Members)
        {
            var methodTests = tests.Where(t => t.TypeFullName == member.TypeName && t.MethodName == member.Name).ToList();
            if (methodTests.Count > 0)
            {
                // A test method: it runs again, unless it is gone.
                if (member.Kind != MemberChangeKind.Removed)
                {
                    selected.UnionWith(methodTests.Select(t => t.Id));
                }

                continue;
            }

            // Anything else of a test class (its constructor, setup or a helper) affects the tests of that class.
            var classTests = tests.Where(t => t.TypeFullName == member.TypeName).ToList();
            if (classTests.Count == 0)
            {
                return false;
            }

            selected.UnionWith(classTests.Select(t => t.Id));
        }

        return true;
    }
}
