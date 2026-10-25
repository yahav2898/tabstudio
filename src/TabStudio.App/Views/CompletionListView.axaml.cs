using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The completion list popup: the matching items with their icons and the description of the selected one. A tap
/// commits an item on a touch screen; with a mouse, a click selects and a double-click commits, as in Visual Studio.
/// </summary>
public sealed partial class CompletionListView : UserControl
{
    /// <summary>Creates the view.</summary>
    public CompletionListView()
    {
        InitializeComponent();
        ItemList.AddHandler(TappedEvent, OnTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        ItemList.AddHandler(DoubleTappedEvent, OnDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Raised when the user picks an item with a tap or a double-click.</summary>
    public event EventHandler<CompletionItemViewModel>? ItemChosen;

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (ItemAt(e) is not { } item || DataContext is not CompletionListViewModel list)
        {
            return;
        }

        if (e.Pointer.Type == PointerType.Mouse)
        {
            list.Select(item);
        }
        else
        {
            ItemChosen?.Invoke(this, item);
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && ItemAt(e) is { } item)
        {
            ItemChosen?.Invoke(this, item);
        }
    }

    private static CompletionItemViewModel? ItemAt(RoutedEventArgs e) =>
        (e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as CompletionItemViewModel;
}
