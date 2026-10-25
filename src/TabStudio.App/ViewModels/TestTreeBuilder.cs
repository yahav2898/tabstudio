namespace TabStudio.App.ViewModels;

/// <summary>How the Test Explorer groups tests, from its "Group By" menu.</summary>
public enum TestGroupBy
{
    /// <summary>Project, then namespace, then class: Visual Studio's default.</summary>
    Hierarchy,

    /// <summary>Class only.</summary>
    Class,

    /// <summary>Outcome (failed, skipped, not run, passed), then class.</summary>
    Outcome,

    /// <summary>Duration (slow, medium, fast, not run), then class.</summary>
    Duration,

    /// <summary>Trait, such as <c>Category [Integration]</c>, then class. A test with several traits is in several groups.</summary>
    Traits,
}

/// <summary>
/// Builds the Test Explorer tree from a list of tests: the groups of the chosen <see cref="TestGroupBy"/>, then the
/// test methods, with the data rows of parameterized tests under their method.
/// </summary>
internal static class TestTreeBuilder
{
    private static readonly TimeSpan SlowTest = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MediumTest = TimeSpan.FromMilliseconds(100);

    /// <summary>One group a test belongs to at one level of the tree.</summary>
    /// <param name="Kind">The kind of row.</param>
    /// <param name="Key">Identifies the group among its siblings.</param>
    /// <param name="Name">The text shown.</param>
    /// <param name="Order">Groups sort by this first, then by name.</param>
    /// <param name="Description">The row's tooltip.</param>
    private sealed record Group(TestNodeKind Kind, string Key, string Name, int Order, string? Description = null);

    /// <summary>Builds the top-level rows.</summary>
    /// <param name="entries">The tests to show, in discovery order.</param>
    /// <param name="groupBy">How to group them.</param>
    /// <param name="actions">What the rows' commands do.</param>
    public static List<TestNodeViewModel> Build(IReadOnlyList<TestEntry> entries, TestGroupBy groupBy, ITestNodeActions actions)
    {
        Func<TestEntry, IEnumerable<Group>>[] levels = groupBy switch
        {
            TestGroupBy.Class => [ClassOf],
            TestGroupBy.Outcome => [OutcomeOf, ClassOf],
            TestGroupBy.Duration => [DurationOf, ClassOf],
            TestGroupBy.Traits => [TraitsOf, ClassOf],
            _ => [ProjectOf, NamespaceOf, ClassOf],
        };

        var roots = new List<TestNodeViewModel>();
        AddGroups(roots.Add, entries, levels, 0, string.Empty, selectsByName: true, actions);
        return roots;
    }

    private static void AddGroups(
        Action<TestNodeViewModel> add,
        IReadOnlyList<TestEntry> entries,
        Func<TestEntry, IEnumerable<Group>>[] levels,
        int depth,
        string parentKey,
        bool selectsByName,
        ITestNodeActions actions)
    {
        if (depth == levels.Length)
        {
            AddMethods(add, entries, depth, parentKey, selectsByName, actions);
            return;
        }

        var groups = new Dictionary<string, (Group Group, List<TestEntry> Entries)>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            foreach (var group in levels[depth](entry))
            {
                if (!groups.TryGetValue(group.Key, out var found))
                {
                    found = (group, []);
                    groups[group.Key] = found;
                }

                found.Entries.Add(entry);
            }
        }

        var sorted = groups.Values
            .OrderBy(g => g.Group.Order)
            .ThenBy(g => g.Group.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var (group, members) in sorted)
        {
            var node = new TestNodeViewModel(group.Kind, parentKey + "/" + group.Key, group.Name, depth, members, actions)
            {
                Description = group.Description,
                SelectsByName = selectsByName,
            };
            add(node);
            AddGroups(node.AddChild, members, levels, depth + 1, node.Key, selectsByName && group.Kind != TestNodeKind.Group, actions);
        }
    }

    /// <summary>Adds a row per test method, in declaration order; a parameterized method gets its rows as children.</summary>
    private static void AddMethods(Action<TestNodeViewModel> add, IReadOnlyList<TestEntry> entries, int depth, string parentKey, bool selectsByName, ITestNodeActions actions)
    {
        foreach (var method in entries.GroupBy(e => e.MethodFullName, StringComparer.Ordinal))
        {
            var tests = method.ToList();
            if (tests is [{ DataRow: null, IsPlaceholder: false } single])
            {
                add(Test(single, depth, parentKey, selectsByName, actions));
                continue;
            }

            var node = new TestNodeViewModel(TestNodeKind.Method, parentKey + "/M:" + method.Key, tests[0].MethodName, depth, tests, actions)
            {
                Description = method.Key,
                SelectsByName = selectsByName,
            };
            foreach (var test in tests)
            {
                node.AddChild(Test(test, depth + 1, node.Key, selectsByName, actions));
            }

            add(node);
        }
    }

    private static TestNodeViewModel Test(TestEntry entry, int depth, string parentKey, bool selectsByName, ITestNodeActions actions) =>
        new(TestNodeKind.Test, parentKey + "/T:" + entry.Id, entry.DisplayName, depth, [entry], actions)
        {
            Description = entry.MethodFullName + entry.DataRow,
            SelectsByName = selectsByName,
        };

    private static IEnumerable<Group> ProjectOf(TestEntry entry) =>
        [new(TestNodeKind.Project, "P:" + entry.ProjectFilePath, entry.ProjectName, 0, entry.ProjectFilePath)];

    private static IEnumerable<Group> NamespaceOf(TestEntry entry) =>
        [new(TestNodeKind.Namespace, "N:" + entry.NamespaceName, entry.NamespaceName.Length == 0 ? "(global namespace)" : entry.NamespaceName, 0)];

    private static IEnumerable<Group> ClassOf(TestEntry entry) =>
        [new(TestNodeKind.Class, "C:" + entry.ProjectFilePath + "|" + entry.TypeFullName, entry.ClassName, 0, entry.TypeFullName)];

    private static IEnumerable<Group> OutcomeOf(TestEntry entry) => entry.State switch
    {
        TestState.Failed => [new(TestNodeKind.Group, "O:Failed", "Failed Tests", 0)],
        TestState.Running or TestState.Queued => [new(TestNodeKind.Group, "O:Running", "Running Tests", 1)],
        TestState.Skipped => [new(TestNodeKind.Group, "O:Skipped", "Skipped Tests", 2)],
        TestState.Passed => [new(TestNodeKind.Group, "O:Passed", "Passed Tests", 4)],
        _ => [new(TestNodeKind.Group, "O:NotRun", "Not Run Tests", 3)],
    };

    private static IEnumerable<Group> DurationOf(TestEntry entry)
    {
        if (entry.Result is not { } result || entry.State is not (TestState.Passed or TestState.Failed or TestState.Skipped))
        {
            return [new(TestNodeKind.Group, "D:NotRun", "Not Run", 3)];
        }

        if (result.Duration > SlowTest)
        {
            return [new(TestNodeKind.Group, "D:Slow", "Slow (> 1 sec)", 0)];
        }

        return result.Duration > MediumTest
            ? [new(TestNodeKind.Group, "D:Medium", "Medium (> 100 ms)", 1)]
            : [new(TestNodeKind.Group, "D:Fast", "Fast (< 100 ms)", 2)];
    }

    private static IEnumerable<Group> TraitsOf(TestEntry entry) =>
        entry.Traits.Count == 0
            ? [new(TestNodeKind.Group, "T:", "No Traits", 1)]
            : entry.Traits
                .Select(t => new Group(TestNodeKind.Group, "T:" + t.Name + "\n" + t.Value, $"{t.Name} [{t.Value}]", 0))
                .DistinctBy(g => g.Key, StringComparer.Ordinal);
}
