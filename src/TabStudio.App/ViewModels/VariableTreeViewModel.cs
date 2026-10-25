using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Debugging;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Values of the paused program as a tree, shown as a flat list of rows like the Solution Explorer: the Locals and
/// Watch panels and DataTips. A row's children are read the first time it is expanded, and rows expanded at one
/// pause are expanded again at the next, as in Visual Studio.
/// </summary>
/// <remarks>
/// Reading a value may run the program's code (a <c>ToString</c> override, a property getter, each with a time
/// limit), so values are read off the UI thread. Results that arrive after a newer pause was shown are dropped.
/// </remarks>
public sealed partial class VariableTreeViewModel : ObservableObject
{
    private List<VariableViewModel> _roots = [];
    private Dictionary<string, string> _previousValues = new(StringComparer.Ordinal);
    private Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private int _version;

    /// <summary>The visible rows, in display order.</summary>
    public RangeObservableCollection<VariableViewModel> Rows { get; } = [];

    /// <summary>Whether the program runs again, so the values shown may be out of date.</summary>
    [ObservableProperty]
    public partial bool IsStale { get; set; }

    /// <summary>Raised when the remove button of a row is tapped.</summary>
    public event EventHandler<VariableViewModel>? RemoveRequested;

    /// <summary>Raised when "Add Watch" is chosen on a row.</summary>
    public event EventHandler<VariableViewModel>? AddWatchRequested;

    /// <summary>
    /// Shows new values: the roots in order, re-expanding the rows that were expanded, and marking values that
    /// changed since the last call.
    /// </summary>
    /// <param name="sources">The roots.</param>
    public async Task ShowAsync(IReadOnlyList<VariableSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var version = ++_version;
        var expanded = Expanded(_roots).Select(r => r.Path).ToHashSet(StringComparer.Ordinal);
        var previous = _values;

        var roots = await Task.Run(() => sources.Select(s => Read(s, previous)).ToList());
        if (version != _version)
        {
            return;
        }

        _previousValues = previous;
        _values = new Dictionary<string, string>(StringComparer.Ordinal);
        _roots = roots;
        Remember(roots);
        await ExpandAgainAsync(roots, expanded, version);
        if (version != _version)
        {
            return;
        }

        IsStale = false;
        ShowRows();
    }

    /// <summary>Removes every row, when the debug session ends.</summary>
    public void Clear()
    {
        _version++;
        _roots = [];
        _previousValues = new Dictionary<string, string>(StringComparer.Ordinal);
        _values = new Dictionary<string, string>(StringComparer.Ordinal);
        IsStale = false;
        Rows.Clear();
    }

    /// <summary>Expands or collapses a row, reading its children the first time.</summary>
    /// <param name="row">The row.</param>
    [RelayCommand]
    public async Task ToggleAsync(VariableViewModel? row)
    {
        if (row is null || !row.IsExpandable)
        {
            return;
        }

        if (row.IsExpanded)
        {
            row.IsExpanded = false;
        }
        else
        {
            var version = _version;
            await ExpandAsync(row);
            if (version != _version)
            {
                return;
            }
        }

        ShowRows();
    }

    [RelayCommand]
    private void Remove(VariableViewModel? row)
    {
        if (row is { IsRemovable: true })
        {
            RemoveRequested?.Invoke(this, row);
        }
    }

    [RelayCommand]
    private void AddWatch(VariableViewModel? row)
    {
        if (row is { IsError: false })
        {
            AddWatchRequested?.Invoke(this, row);
        }
    }

    private static VariableViewModel Read(VariableSource source, Dictionary<string, string> previous)
    {
        if (source.Value is not { } value)
        {
            return source.Variable is { } variable
                ? VariableViewModel.Read(variable, source.Name, source.Path, 0, previous.GetValueOrDefault(source.Path), source.IsRemovable)
                : VariableViewModel.Failed(source.Name, source.Error ?? string.Empty, 0, source.IsRemovable);
        }

        try
        {
            return VariableViewModel.Read(value, source.Name, source.Path, 0, previous.GetValueOrDefault(source.Path), source.IsRemovable);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return VariableViewModel.Failed(source.Name, $"Could not read the value: {ex.Message}", 0, source.IsRemovable);
        }
    }

    private static List<VariableViewModel> ReadChildren(VariableViewModel parent, Dictionary<string, string> previous)
    {
        var depth = parent.Depth + 1;
        try
        {
            if (parent.Variable is { } variable)
            {
                return variable.ReadChildren()
                    .Select(child =>
                    {
                        var path = child.Value.EvaluateName ?? VariableViewModel.ChildPath(parent.Path, child.Name);
                        return VariableViewModel.Read(child.Value, child.Name, path, depth, previous.GetValueOrDefault(path));
                    })
                    .ToList();
            }

            return parent.Source!.Expand()
                .Select(child =>
                {
                    var path = VariableViewModel.ChildPath(parent.Path, child.Name);
                    return VariableViewModel.Read(child, child.Name, path, depth, previous.GetValueOrDefault(path));
                })
                .ToList();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return [VariableViewModel.Failed(parent.Name, $"Could not read the members: {ex.Message}", depth)];
        }
    }

    private static IEnumerable<VariableViewModel> Expanded(IEnumerable<VariableViewModel> rows)
    {
        foreach (var row in rows.Where(r => r.IsExpanded))
        {
            yield return row;
            foreach (var child in Expanded(row.Children ?? []))
            {
                yield return child;
            }
        }
    }

    private async Task ExpandAsync(VariableViewModel row)
    {
        if (row.Children is null)
        {
            var previous = _previousValues;
            var children = await Task.Run(() => ReadChildren(row, previous));
            row.Children = children;
            Remember(children);
        }

        row.IsExpanded = true;
    }

    private async Task ExpandAgainAsync(IReadOnlyList<VariableViewModel> rows, HashSet<string> paths, int version)
    {
        foreach (var row in rows.Where(r => r.IsExpandable && paths.Contains(r.Path)))
        {
            await ExpandAsync(row);
            if (version != _version)
            {
                return;
            }

            await ExpandAgainAsync(row.Children ?? [], paths, version);
        }
    }

    private void Remember(IEnumerable<VariableViewModel> rows)
    {
        foreach (var row in rows.Where(r => !r.IsError))
        {
            _values[row.Path] = row.Value;
        }
    }

    private void ShowRows()
    {
        var rows = new List<VariableViewModel>();
        AddVisible(_roots, rows);
        foreach (var row in rows)
        {
            row.Owner = this;
        }

        Rows.ReplaceAll(rows);
    }

    private static void AddVisible(IEnumerable<VariableViewModel> source, List<VariableViewModel> rows)
    {
        foreach (var row in source)
        {
            rows.Add(row);
            if (row.IsExpanded && row.Children is { } children)
            {
                AddVisible(children, rows);
            }
        }
    }
}
