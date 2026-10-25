using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>One choice in a <see cref="QuickPickViewModel"/>.</summary>
/// <param name="Label">What the choice is, such as a theme's name.</param>
/// <param name="Description">A short note after the label, such as "current".</param>
/// <param name="Detail">A second line, such as the extension that contributes the choice.</param>
/// <param name="Value">What the choice stands for, handed back when it is picked.</param>
/// <param name="Group">The heading of the group the choice belongs to, such as "Dark themes", or <see langword="null"/>.</param>
public sealed record QuickPickItem(string Label, string? Description, string? Detail, object? Value, string? Group = null)
{
    /// <summary>Whether the item shows a second line.</summary>
    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    /// <summary>Whether the item shows a note after its label.</summary>
    public bool HasDescription => !string.IsNullOrEmpty(Description);

    /// <summary>Whether the item starts a group, so its heading is shown above it; set by the list.</summary>
    public bool StartsGroup { get; init; }
}

/// <summary>
/// A short list to pick from at the top of the window, as VS Code's quick pick: the color theme and file icon theme
/// pickers and Insert Snippet. Typing filters the list, the arrow keys move through it (a theme previews as you move),
/// Enter or a tap picks, and Escape or a tap outside cancels.
/// </summary>
public sealed partial class QuickPickViewModel : ObservableObject
{
    private IReadOnlyList<QuickPickItem> _all = [];
    private TaskCompletionSource<QuickPickItem?>? _pending;
    private Action<QuickPickItem?>? _highlighted;

    /// <summary>The items that match the filter.</summary>
    public ObservableCollection<QuickPickItem> Items { get; } = [];

    /// <summary>Whether the picker is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>What is being picked, such as "Color Theme".</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    /// <summary>The hint in the filter box.</summary>
    [ObservableProperty]
    public partial string Placeholder { get; private set; } = string.Empty;

    /// <summary>What was typed to filter the list.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    /// <summary>The highlighted item, which Enter picks.</summary>
    [ObservableProperty]
    public partial QuickPickItem? SelectedItem { get; set; }

    /// <summary>Whether nothing matches the filter.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>
    /// Shows the picker and waits for the user. A picker that was already open is cancelled first.
    /// </summary>
    /// <param name="title">What is being picked.</param>
    /// <param name="placeholder">The hint in the filter box.</param>
    /// <param name="items">The choices.</param>
    /// <param name="selected">The choice highlighted at first, such as the current theme, or <see langword="null"/> for the first.</param>
    /// <param name="highlighted">Called whenever another item is highlighted, and with <see langword="null"/> when the picker closes.</param>
    /// <returns>The picked item, or <see langword="null"/> when the user cancelled.</returns>
    public Task<QuickPickItem?> ShowAsync(string title, string placeholder, IReadOnlyList<QuickPickItem> items, QuickPickItem? selected = null, Action<QuickPickItem?>? highlighted = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        Close(null);
        _all = WithGroups(items);
        _highlighted = null;
        Title = title;
        Placeholder = placeholder;
        FilterText = string.Empty;
        Refilter();
        SelectedItem = (selected is null ? null : Items.FirstOrDefault(i => Equals(i.Value, selected.Value))) ?? Items.FirstOrDefault();
        _highlighted = highlighted;
        _pending = new TaskCompletionSource<QuickPickItem?>();
        IsOpen = true;
        return _pending.Task;
    }

    /// <summary>Moves the highlight, as the arrow keys do.</summary>
    /// <param name="delta">How many rows to move; negative moves up.</param>
    public void MoveSelection(int delta)
    {
        if (Items.Count == 0)
        {
            return;
        }

        var index = SelectedItem is null ? -1 : Items.IndexOf(SelectedItem);
        SelectedItem = Items[Math.Clamp(index < 0 ? 0 : index + delta, 0, Items.Count - 1)];
    }

    /// <summary>Picks an item, or the highlighted one.</summary>
    /// <param name="item">The item, or <see langword="null"/> for the highlighted one.</param>
    [RelayCommand]
    public void Accept(QuickPickItem? item = null)
    {
        if (IsOpen && (item ?? SelectedItem) is { } picked)
        {
            Close(picked);
        }
    }

    /// <summary>Closes the picker without picking anything.</summary>
    [RelayCommand]
    public void Cancel() => Close(null);

    partial void OnFilterTextChanged(string value)
    {
        if (IsOpen)
        {
            Refilter();
            SelectedItem = Items.FirstOrDefault();
        }
    }

    partial void OnSelectedItemChanged(QuickPickItem? value)
    {
        if (IsOpen && value is not null)
        {
            _highlighted?.Invoke(value);
        }
    }

    private void Refilter()
    {
        var filter = FilterText.Trim();
        Items.Clear();
        foreach (var item in _all)
        {
            if (filter.Length == 0
                || item.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || item.Detail?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true
                || item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true)
            {
                Items.Add(filter.Length == 0 ? item : item with { StartsGroup = false });
            }
        }

        IsEmpty = Items.Count == 0;
    }

    private void Close(QuickPickItem? picked)
    {
        if (_pending is not { } pending)
        {
            return;
        }

        _pending = null;
        var highlighted = _highlighted;
        _highlighted = null;
        IsOpen = false;
        highlighted?.Invoke(null);
        pending.TrySetResult(picked);
    }

    private static List<QuickPickItem> WithGroups(IReadOnlyList<QuickPickItem> items)
    {
        var grouped = new List<QuickPickItem>(items.Count);
        string? group = null;
        foreach (var item in items)
        {
            var starts = item.Group is not null && !string.Equals(item.Group, group, StringComparison.Ordinal);
            group = item.Group;
            grouped.Add(item with { StartsGroup = starts });
        }

        return grouped;
    }
}
