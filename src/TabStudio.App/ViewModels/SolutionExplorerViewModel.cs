using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The Solution Explorer: the solution, its projects, their dependencies and files. Tapping a folder expands
/// it; tapping a file opens it.
/// </summary>
public sealed partial class SolutionExplorerViewModel : ObservableObject
{
    private const int MaximumSearchResults = 500;

    private Workspace? _workspace;
    private IReadOnlyDictionary<string, CompatibilityReport> _reports = new Dictionary<string, CompatibilityReport>();
    private List<ExplorerItemViewModel> _treeRows = [];

    /// <summary>The visible rows, in display order.</summary>
    public RangeObservableCollection<ExplorerItemViewModel> Rows { get; } = [];

    /// <summary>The selected row.</summary>
    [ObservableProperty]
    public partial ExplorerItemViewModel? SelectedItem { get; set; }

    /// <summary>Filters the list to files whose name contains this text.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Raised when the user opens a file row.</summary>
    public event EventHandler<string>? OpenFileRequested;

    /// <summary>Raised when the selection changes, for the Properties panel.</summary>
    public event EventHandler<WorkspaceNode?>? SelectionChanged;

    /// <summary>Shows a workspace, with the solution and its projects expanded.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="reports">The Compatibility Doctor's verdicts, for the project pills.</param>
    public void Load(Workspace workspace, IEnumerable<CompatibilityReport> reports)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(reports);

        _workspace = workspace;
        _reports = reports.GroupBy(r => r.ProjectName, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var root = CreateRow(workspace.Tree, 0);
        _treeRows = [root];
        Expand(root, _treeRows);
        foreach (var project in _treeRows.Where(r => r.IsProject).ToList())
        {
            Expand(project, _treeRows);
        }

        SearchText = string.Empty;
        ShowRows(_treeRows);
        SelectedItem = root;
    }

    /// <summary>Removes everything, when the workspace closes.</summary>
    public void Clear()
    {
        _workspace = null;
        _treeRows = [];
        Rows.Clear();
        SelectedItem = null;
    }

    /// <summary>Expands the folders above a file and selects it, as Visual Studio's "Sync with Active Document" does.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <returns><see langword="true"/> when the file is in the tree.</returns>
    public bool Reveal(string filePath)
    {
        if (_workspace is null || !string.IsNullOrEmpty(SearchText))
        {
            return false;
        }

        var chain = FindChain(_workspace.Tree, filePath);
        if (chain is null)
        {
            return false;
        }

        var row = _treeRows[0];
        foreach (var node in chain.Skip(1))
        {
            var parent = row;
            Expand(parent, _treeRows);
            var index = _treeRows.IndexOf(parent);
            var child = _treeRows.Skip(index + 1).TakeWhile(r => r.Depth > parent.Depth).FirstOrDefault(r => ReferenceEquals(r.Node, node));
            if (child is null)
            {
                return false;
            }

            row = child;
        }

        ShowRows(_treeRows);
        SelectedItem = row;
        return true;
    }

    /// <summary>Opens a file row, or expands and collapses any other row.</summary>
    /// <param name="item">The row.</param>
    [RelayCommand]
    public void Activate(ExplorerItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedItem = item;
        if (item.IsFile)
        {
            OpenFileRequested?.Invoke(this, item.Node.FullPath!);
        }
        else
        {
            Toggle(item);
        }
    }

    /// <summary>Expands or collapses a row.</summary>
    /// <param name="item">The row.</param>
    [RelayCommand]
    public void Toggle(ExplorerItemViewModel? item)
    {
        if (item is null || !item.IsExpandable || !string.IsNullOrEmpty(SearchText))
        {
            return;
        }

        if (item.IsExpanded)
        {
            Collapse(item, _treeRows);
        }
        else
        {
            Expand(item, _treeRows);
        }

        ShowRows(_treeRows);
    }

    /// <summary>Collapses everything below the solution.</summary>
    [RelayCommand]
    private void CollapseAll()
    {
        if (_treeRows.Count == 0)
        {
            return;
        }

        var root = _treeRows[0];
        Collapse(root, _treeRows);
        Expand(root, _treeRows);
        SearchText = string.Empty;
        ShowRows(_treeRows);
    }

    partial void OnSelectedItemChanged(ExplorerItemViewModel? value) => SelectionChanged?.Invoke(this, value?.Node);

    partial void OnSearchTextChanged(string value)
    {
        if (_workspace is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            ShowRows(_treeRows);
            return;
        }

        var text = value.Trim();
        var root = _workspace.RootDirectory;
        var matches = _workspace.Tree.DescendantsAndSelf()
            .Where(n => n.IsOpenableFile && n.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(MaximumSearchResults)
            .Select(n => new ExplorerItemViewModel(n, 0)
            {
                Location = Path.GetRelativePath(root, Path.GetDirectoryName(n.FullPath!)!) is var folder && folder != "." ? folder : null,
            })
            .ToList();
        ShowRows(matches);
    }

    private ExplorerItemViewModel CreateRow(WorkspaceNode node, int depth)
    {
        var report = node.Project is { } project && _reports.TryGetValue(project.Name, out var found) ? found : null;
        return new ExplorerItemViewModel(node, depth, report) { Detail = DetailFor(node) };
    }

    private string? DetailFor(WorkspaceNode node)
    {
        if (node.Kind != WorkspaceNodeKind.Solution || _workspace is null)
        {
            return null;
        }

        var count = _workspace.Projects.Count;
        return count == 1 ? "(1 project)" : $"({count.ToString(CultureInfo.InvariantCulture)} projects)";
    }

    private void Expand(ExplorerItemViewModel item, List<ExplorerItemViewModel> rows)
    {
        if (item.IsExpanded || !item.IsExpandable)
        {
            return;
        }

        item.IsExpanded = true;
        var index = rows.IndexOf(item);
        var children = item.Node.Children.Select(child => CreateRow(child, item.Depth + 1)).ToList();
        rows.InsertRange(index + 1, children);
    }

    private static void Collapse(ExplorerItemViewModel item, List<ExplorerItemViewModel> rows)
    {
        if (!item.IsExpanded)
        {
            return;
        }

        item.IsExpanded = false;
        var index = rows.IndexOf(item);
        var count = rows.Skip(index + 1).TakeWhile(r => r.Depth > item.Depth).Count();
        rows.RemoveRange(index + 1, count);
    }

    private void ShowRows(IReadOnlyList<ExplorerItemViewModel> rows)
    {
        // A reset clears the list's selection; put it back when the selected row is still shown.
        var selected = SelectedItem;
        Rows.ReplaceAll(rows);
        if (selected is not null && rows.Contains(selected))
        {
            SelectedItem = selected;
        }
    }

    private static List<WorkspaceNode>? FindChain(WorkspaceNode node, string filePath)
    {
        if (node.IsOpenableFile && string.Equals(node.FullPath, filePath, StringComparison.Ordinal))
        {
            return [node];
        }

        foreach (var child in node.Children)
        {
            if (FindChain(child, filePath) is { } chain)
            {
                chain.Insert(0, node);
                return chain;
            }
        }

        return null;
    }
}
