using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels;

/// <summary>The tree: grouping, searching and filtering the tests, expanding rows, and the selected row's details.</summary>
public sealed partial class TestExplorerViewModel
{
    /// <summary>Expands or collapses a row.</summary>
    /// <param name="node">The row.</param>
    [RelayCommand]
    public void Toggle(TestNodeViewModel? node)
    {
        if (node is not { IsExpandable: true })
        {
            return;
        }

        node.IsExpanded = !node.IsExpanded;
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            _expanded[node.Key] = node.IsExpanded;
        }

        ShowRows();
    }

    /// <summary>Rebuilds the tree from the tests that pass the search and the outcome filter.</summary>
    private void Rebuild()
    {
        _rebuildScheduled = false;
        var selectedKey = SelectedNode?.Key;
        var selectedTest = SelectedNode?.Entry?.Id;

        var searching = !string.IsNullOrWhiteSpace(SearchText);
        _roots = TestTreeBuilder.Build(_entries.Where(Matches).ToList(), GroupBy, this);
        _owners.Clear();
        var all = new List<TestNodeViewModel>();
        foreach (var root in _roots)
        {
            Index(root, searching, all);
        }

        ShowRows();
        var visible = Rows.ToHashSet();
        SelectedNode = all.Find(n => n.Key == selectedKey && visible.Contains(n))
            ?? (selectedTest is null ? null : all.Find(n => n.Entry?.Id == selectedTest && visible.Contains(n)));
        UpdateCounts();
        UpdateEmptyText();
    }

    /// <summary>Records which rows show each test, and expands rows as the user left them.</summary>
    private void Index(TestNodeViewModel node, bool searching, List<TestNodeViewModel> all)
    {
        all.Add(node);
        node.IsExpanded = searching || (_expanded.TryGetValue(node.Key, out var expanded) ? expanded : node.Kind != TestNodeKind.Method);
        foreach (var entry in node.Entries)
        {
            if (!_owners.TryGetValue(entry, out var owners))
            {
                owners = [];
                _owners[entry] = owners;
            }

            owners.Add(node);
        }

        foreach (var child in node.Children)
        {
            Index(child, searching, all);
        }
    }

    private bool Matches(TestEntry entry)
    {
        if (OutcomeFilter is { } filter && FilterStateOf(entry.State) != filter)
        {
            return false;
        }

        var text = SearchText.Trim();
        return text.Length == 0
            || entry.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)
            || entry.MethodFullName.Contains(text, StringComparison.OrdinalIgnoreCase)
            || entry.Traits.Any(t => t.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || t.Value.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static TestState FilterStateOf(TestState state) =>
        state is TestState.Queued or TestState.Running ? TestState.NotRun : state;

    /// <summary>Shows the expanded part of the tree as the list's rows.</summary>
    private void ShowRows()
    {
        var rows = new List<TestNodeViewModel>();
        foreach (var root in _roots)
        {
            Flatten(root, rows);
        }

        // A reset clears the list's selection; put it back when the selected row is still shown.
        var selected = SelectedNode;
        Rows.ReplaceAll(rows);
        if (selected is not null && rows.Contains(selected))
        {
            SelectedNode = selected;
        }
    }

    private static void Flatten(TestNodeViewModel node, List<TestNodeViewModel> rows)
    {
        rows.Add(node);
        if (node.IsExpanded)
        {
            foreach (var child in node.Children)
            {
                Flatten(child, rows);
            }
        }
    }

    /// <summary>Updates the rows that show tests whose state changed, and the counts and details.</summary>
    private void RefreshNodes(IEnumerable<TestEntry> entries)
    {
        var nodes = new HashSet<TestNodeViewModel>();
        foreach (var entry in entries)
        {
            if (_owners.TryGetValue(entry, out var owners))
            {
                nodes.UnionWith(owners);
            }
        }

        foreach (var node in nodes)
        {
            node.Refresh();
        }

        UpdateCounts();
        if (SelectedNode is { } selected && nodes.Contains(selected))
        {
            UpdateDetails();
        }

        // Groups and filters that depend on outcomes move rows around: rebuild once the current batch of results is in.
        if ((GroupBy is TestGroupBy.Outcome or TestGroupBy.Duration || OutcomeFilter is not null) && !_rebuildScheduled)
        {
            _rebuildScheduled = true;
            _ui.Post(() =>
            {
                if (_rebuildScheduled)
                {
                    Rebuild();
                }
            });
        }
    }

    private void UpdateDetails() =>
        Details = SelectedNode is { } node ? TestDetailsViewModel.For(node, _host.NavigateToAsync) : null;

    private void UpdateEmptyText() => EmptyText = Rows.Count > 0
        ? null
        : !HasTestProjects
            ? "No test projects in this solution. Add a project that references xUnit, NUnit or MSTest, and its tests appear here."
            : _entries.Count == 0
                ? "No tests found yet. Tests are methods marked [Fact], [Theory], [Test], [TestCase] or [TestMethod]."
                : "No tests match the search or the filter.";
}
