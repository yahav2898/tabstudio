using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>The Error List. Tapping a row opens the file at the diagnostic's location.</summary>
public sealed partial class ErrorListView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ErrorListView()
    {
        InitializeComponent();
        Rows.AddHandler(TappedEvent, OnRowTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        Rows.KeyDown += OnRowsKeyDown;
    }

    private ErrorListViewModel? ViewModel => DataContext as ErrorListViewModel;

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is ErrorItemViewModel item)
        {
            // A code with a page, as language servers give, opens the page; the rest of the row opens the file.
            if (e.Source is TextBlock { Tag: "code" } && item.HasHelpLink)
            {
                ViewModel?.OpenHelpLinkCommand.Execute(item);
                return;
            }

            ViewModel?.NavigateCommand.Execute(item);
        }
    }

    private void OnRowsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Rows.SelectedItem is ErrorItemViewModel item)
        {
            ViewModel?.NavigateCommand.Execute(item);
            e.Handled = true;
        }
    }
}
