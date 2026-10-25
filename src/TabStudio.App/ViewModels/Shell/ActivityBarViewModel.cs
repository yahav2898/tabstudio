using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels.Shell;

/// <summary>
/// The activity bar: one icon per tool window along the side bar's outer edge, replacing the tool window tabs that
/// wrapped into rows. Tapping an icon shows its tool window; tapping the shown one folds the side bar away, giving the
/// code the room, and tapping again brings it back. Badges say what is going on without opening anything.
/// </summary>
public sealed partial class ActivityBarViewModel : ObservableObject
{
    /// <summary>The tool windows, in the bar's order, top to bottom; those at the bottom come after.</summary>
    public ObservableCollection<ActivityItem> Items { get; } = [];

    /// <summary>The tool window the side bar shows, or <see langword="null"/> before one is chosen.</summary>
    [ObservableProperty]
    public partial ActivityItem? SelectedItem { get; private set; }

    /// <summary>Whether the side bar is open; the bar itself always shows.</summary>
    [ObservableProperty]
    public partial bool IsSideBarOpen { get; set; } = true;

    /// <summary>Raised when an item is tapped and its tool window should show.</summary>
    public event EventHandler<ActivityItem>? Activated;

    /// <summary>Adds a tool window at the end of its group.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The item, for setting it up further.</returns>
    public ActivityItem Add(ActivityItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Find(item.Id) is not null)
        {
            throw new ArgumentException($"There is already an item '{item.Id}'.", nameof(item));
        }

        Items.Add(item);
        return item;
    }

    /// <summary>The item of a tool window, or <see langword="null"/>.</summary>
    /// <param name="id">Its identifier.</param>
    public ActivityItem? Find(string id) => Items.FirstOrDefault(item => item.Id == id);

    /// <summary>Shows a tool window without the toggling a tap does, as when a command opens it.</summary>
    /// <param name="id">Its identifier.</param>
    public void Select(string id)
    {
        if (Find(id) is { Command: null } item)
        {
            Choose(item);
            IsSideBarOpen = true;
        }
    }

    /// <summary>What a tap on an item does: shows its tool window, or folds the side bar when it is already shown.</summary>
    /// <param name="item">The tapped item.</param>
    [RelayCommand]
    private void Activate(ActivityItem? item)
    {
        if (item is null || !item.IsAvailable)
        {
            return;
        }

        if (item.Command is { } command)
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }

            return;
        }

        if (item == SelectedItem && IsSideBarOpen)
        {
            IsSideBarOpen = false;
            return;
        }

        Choose(item);
        IsSideBarOpen = true;
        Activated?.Invoke(this, item);
    }

    private void Choose(ActivityItem item)
    {
        if (SelectedItem is { } previous)
        {
            previous.IsSelected = false;
        }

        item.IsSelected = true;
        SelectedItem = item;
    }
}
