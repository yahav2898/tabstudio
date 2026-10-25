using TabStudio.App.Swarm;
using TabStudio.App.Swarm.Recording;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Recorded UI tests in the Test Explorer: each one is a row per screen it replays on (every device of the swarm, the
/// app preview and the virtual devices), under the test's method, as the rows of a parameterized test are; running one
/// replays it on its screen rather than in-process.
/// </summary>
public sealed partial class TestExplorerViewModel
{
    private readonly Dictionary<string, List<TestEntry>> _screenRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ScreenRow> _screenTargets = new(StringComparer.Ordinal);

    /// <summary>Shows a project's recorded UI tests, one row for each screen; without screens, one row each.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="projectName">Its name.</param>
    /// <param name="tests">Its recorded tests.</param>
    /// <param name="screens">The screens they replay on.</param>
    public void SetScreenTests(string projectFilePath, string projectName, IReadOnlyList<UiTestDefinition> tests, IReadOnlyList<IAppScreen> screens)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(screens);
        foreach (var key in _screenTargets.Keys.Where(k => k.StartsWith(projectFilePath + "\n", StringComparison.Ordinal)).ToList())
        {
            _screenTargets.Remove(key);
        }

        List<TestEntry> rows = [];
        foreach (var test in tests)
        {
            if (screens.Count == 0)
            {
                rows.Add(Row(projectFilePath, projectName, test, null));
                continue;
            }

            foreach (var screen in screens)
            {
                rows.Add(Row(projectFilePath, projectName, test, screen));
            }
        }

        if (rows.Count == 0)
        {
            _screenRows.Remove(projectFilePath);
        }
        else
        {
            _screenRows[projectFilePath] = rows;
        }

        MergeEntries();
    }

    /// <summary>
    /// Takes the recorded UI tests a run selects out of it: the rows to replay on their screens, and the run of the rest,
    /// which the test runner runs.
    /// </summary>
    /// <param name="request">The run.</param>
    public (IReadOnlyList<TestEntry> Rows, TestRunRequest Rest) SplitScreenTests(TestRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<TestEntry> rows = [];
        List<TestProjectSelection> rest = [];
        foreach (var selection in request.Projects)
        {
            if (!_screenRows.TryGetValue(selection.ProjectFilePath, out var projectRows))
            {
                rest.Add(selection);
                continue;
            }

            var selected = projectRows.Where(r => Selects(selection.Names, r)).ToList();
            if (selected.Count == 0)
            {
                rest.Add(selection);
                continue;
            }

            rows.AddRange(selected);

            // The other tests of the project, named one by one, so the runner does not run the recorded ones in-process.
            var others = _entries
                .Where(e => e.ProjectFilePath == selection.ProjectFilePath && !projectRows.Contains(e) && Selects(selection.Names, e))
                .Select(e => e.Id)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (others.Count > 0)
            {
                rest.Add(new TestProjectSelection(selection.ProjectFilePath, others));
            }
        }

        return (rows, request with { Projects = rest });
    }

    /// <summary>A run of every recorded UI test on every screen, project by project.</summary>
    public IReadOnlyList<TestProjectSelection> ScreenTestSelections() =>
        [.. _screenRows.Select(p => new TestProjectSelection(p.Key, [.. p.Value.Select(r => r.Id)]))];

    /// <summary>The recorded test and screen of a row.</summary>
    /// <param name="row">A row from <see cref="SplitScreenTests"/>.</param>
    /// <returns>The test, and its screen, or <see langword="null"/> for a row of no screen.</returns>
    public (UiTestDefinition Test, IAppScreen? Screen)? ScreenTestOf(TestEntry row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return _screenTargets.TryGetValue(EntryKey(row.ProjectFilePath, row.Id), out var target) ? (target.Test, target.Screen) : null;
    }

    /// <summary>The test case of a row, as the runner's results name it.</summary>
    /// <param name="row">The row.</param>
    public static TestCase CaseOf(TestEntry row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new TestCase
        {
            Id = row.Id,
            FullyQualifiedName = row.MethodFullName,
            DisplayName = row.DisplayName,
            Framework = TestFramework.XUnit,
            AssemblyName = row.ProjectName,
            TypeFullName = row.TypeFullName,
            MethodName = row.MethodName,
            DataRow = row.DataRow,
            Traits = row.Traits,
            Location = row.Location,
        };
    }

    /// <summary>Records where the picture of a row's failure is, for the details pane.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="id">The row's id.</param>
    /// <param name="screenshotPath">The PNG, or <see langword="null"/> when the row passed or has none.</param>
    public void SetScreenshot(string projectFilePath, string id, string? screenshotPath)
    {
        if (Find(projectFilePath, id) is { } entry)
        {
            entry.ScreenshotPath = screenshotPath;
            if (ReferenceEquals(SelectedNode?.Entry, entry))
            {
                UpdateDetails();
            }
        }
    }

    /// <summary>Puts the rows of recorded tests in place of what the source and the build say of them. Called by <see cref="MergeEntries"/>.</summary>
    private List<TestEntry> WithScreenRows(List<TestEntry> merged)
    {
        if (_screenRows.Count == 0)
        {
            return merged;
        }

        var recorded = _screenRows.Values
            .SelectMany(rows => rows)
            .Select(r => (r.ProjectFilePath, r.MethodFullName))
            .ToHashSet();
        List<TestEntry> result = [];
        HashSet<string> placed = new(StringComparer.Ordinal);
        foreach (var entry in merged)
        {
            if (!recorded.Contains((entry.ProjectFilePath, entry.MethodFullName)))
            {
                result.Add(entry);
            }
            else if (placed.Add(entry.ProjectFilePath + "\n" + entry.MethodFullName))
            {
                result.AddRange(_screenRows[entry.ProjectFilePath].Where(r => r.MethodFullName == entry.MethodFullName));
            }
        }

        // Tests the source scanner did not find, such as before the test project's packages are restored.
        foreach (var (project, rows) in _screenRows)
        {
            if (!_projects.Exists(p => p.FilePath == project))
            {
                continue;
            }

            foreach (var method in rows.GroupBy(r => r.MethodFullName, StringComparer.Ordinal))
            {
                if (placed.Add(project + "\n" + method.Key))
                {
                    result.AddRange(method);
                }
            }
        }

        return result;
    }

    private TestEntry Row(string projectFilePath, string projectName, UiTestDefinition test, IAppScreen? screen)
    {
        var location = new TestSourceLocation(test.FilePath, test.Line);
        var entry = new TestEntry
        {
            Id = screen is null ? test.MethodFullName : $"{test.MethodFullName}(on {screen.Id})",
            ProjectFilePath = projectFilePath,
            ProjectName = projectName,
            TypeFullName = test.TypeFullName,
            MethodFullName = test.MethodFullName,
            MethodName = test.MethodName,
            DisplayName = screen is null ? test.DisplayName : $"on {screen.Profile.Name} ({SwarmScreens.DescribeSize(screen.Profile.Width, screen.Profile.Height)})",
            DataRow = screen is null ? null : $"(on {screen.Profile.Name})",
            Traits = [new TestTrait("Category", UiTestWriter.Category)],
            Location = location,
            ClassLocation = location,
        };
        _screenTargets[EntryKey(projectFilePath, entry.Id)] = new ScreenRow(test, screen);
        return entry;
    }

    /// <summary>Whether a run's names select a test: every test, or by its id, method, class or namespace.</summary>
    private static bool Selects(IReadOnlyList<string>? names, TestEntry entry) =>
        names is null
        || names.Contains(entry.Id)
        || names.Contains(entry.MethodFullName)
        || names.Contains(entry.TypeFullName)
        || names.Any(n => entry.NamespaceName == n || entry.NamespaceName.StartsWith(n + ".", StringComparison.Ordinal));

    /// <summary>A row's recorded test and screen.</summary>
    private sealed record ScreenRow(UiTestDefinition Test, IAppScreen? Screen);
}
