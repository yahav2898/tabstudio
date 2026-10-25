using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>What a Test Explorer row's Live Unit Testing menu does. Implemented by the Test Explorer.</summary>
public interface ILiveTestingNodeActions
{
    /// <summary>Includes or excludes the tests under a row in Live Unit Testing.</summary>
    /// <param name="node">The row.</param>
    /// <param name="include">Whether to include them.</param>
    void SetLiveTesting(TestNodeViewModel node, bool include);
}

/// <summary>Live Unit Testing in the Test Explorer: its toggle and status, live results, and including or excluding tests.</summary>
public sealed partial class TestExplorerViewModel : ILiveTestingNodeActions
{
    /// <summary>Live Unit Testing, whose toggle and status the Test Explorer shows. Set by the workspace.</summary>
    public LiveTestingViewModel? LiveTesting { get; internal set; }

    /// <inheritdoc />
    void ILiveTestingNodeActions.SetLiveTesting(TestNodeViewModel node, bool include)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (LiveTesting is not { } live)
        {
            return;
        }

        foreach (var (project, name) in LiveTestingScopes(node))
        {
            live.SetInclusion(project, name, include);
        }
    }

    /// <summary>
    /// After a live run ended, puts back the tests it queued but did not run: a test with an earlier result shows it,
    /// marked out of date, and a test without one shows as not run.
    /// </summary>
    public void ReportLiveRunEnded()
    {
        if (IsRunning)
        {
            return;
        }

        var unfinished = _entries.Where(e => e.State is TestState.Queued or TestState.Running).ToList();
        foreach (var entry in unfinished)
        {
            if (entry.Result is { } result)
            {
                entry.State = result.Outcome switch
                {
                    TestOutcome.Passed => TestState.Passed,
                    TestOutcome.Failed or TestOutcome.Errored => TestState.Failed,
                    TestOutcome.Skipped => TestState.Skipped,
                    _ => TestState.NotRun,
                };
                entry.IsStale = true;
            }
            else
            {
                entry.State = TestState.NotRun;
            }
        }

        if (unfinished.Count > 0)
        {
            RefreshNodes(unfinished);
        }
    }

    /// <summary>
    /// Live Unit Testing ran every test the changes affected: the latest results of the others still hold, so they
    /// are no longer marked out of date.
    /// </summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="testIds">Its tests that have a live result.</param>
    public void ReportLiveResultsCurrent(string projectFilePath, IEnumerable<string> testIds)
    {
        ArgumentNullException.ThrowIfNull(testIds);
        var ids = testIds.ToHashSet(StringComparer.Ordinal);
        var current = _entries
            .Where(e => e.ProjectFilePath == projectFilePath && e.IsStale && e.Result is not null && ids.Contains(e.Id) && e.State is not (TestState.Queued or TestState.Running))
            .ToList();
        foreach (var entry in current)
        {
            entry.IsStale = false;
        }

        if (current.Count > 0)
        {
            RefreshNodes(current);
        }
    }

    /// <summary>Selects a test's row, expanding its method when the test is a data row, and brings the Test Explorer's details to it.</summary>
    /// <param name="entry">The test.</param>
    public void Reveal(TestEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (Rows.FirstOrDefault(r => r.Entry == entry) is null)
        {
            foreach (var node in _roots.SelectMany(Descendants).Where(n => !n.IsExpanded && n.IsExpandable && n.Entries.Contains(entry)).ToList())
            {
                Toggle(node);
            }
        }

        if (Rows.FirstOrDefault(r => r.Entry == entry) is { } row)
        {
            SelectedNode = row;
        }
    }

    /// <summary>What a row stands for in the include and exclude list: its project, namespace, class, method or test.</summary>
    private static IEnumerable<(string Project, string Name)> LiveTestingScopes(TestNodeViewModel node)
    {
        var first = node.Entries[0];
        if (!node.SelectsByName || node.Kind == TestNodeKind.Group || (node.Kind == TestNodeKind.Namespace && first.NamespaceName.Length == 0))
        {
            return node.Entries.Select(e => (e.ProjectFilePath, e.Id));
        }

        return node.Kind switch
        {
            TestNodeKind.Project => [(first.ProjectFilePath, string.Empty)],
            TestNodeKind.Namespace => [(first.ProjectFilePath, first.NamespaceName)],
            TestNodeKind.Class => [(first.ProjectFilePath, first.TypeFullName)],
            TestNodeKind.Method => [(first.ProjectFilePath, first.MethodFullName)],
            _ => [(first.ProjectFilePath, first.Id)],
        };
    }

    private static IEnumerable<TestNodeViewModel> Descendants(TestNodeViewModel node) =>
        new[] { node }.Concat(node.Children.SelectMany(Descendants));
}
