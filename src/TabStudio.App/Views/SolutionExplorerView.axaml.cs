using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The Solution Explorer. A tap opens a file or expands a folder; on a keyboard, Enter does the same and the
/// arrow keys move through the tree.
/// </summary>
public sealed partial class SolutionExplorerView : UserControl
{
    /// <summary>Creates the view.</summary>
    public SolutionExplorerView()
    {
        InitializeComponent();
        Tree.AddHandler(TappedEvent, OnTreeTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        Tree.KeyDown += OnTreeKeyDown;
    }

    private SolutionExplorerViewModel? ViewModel => DataContext as SolutionExplorerViewModel;

    private void OnTreeTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is ExplorerItemViewModel item)
        {
            ViewModel?.Activate(item);
        }
    }

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { SelectedItem: { } item } viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                viewModel.Activate(item);
                e.Handled = true;
                break;
            case Key.Right when item.IsExpandable && !item.IsExpanded:
            case Key.Left when item.IsExpanded:
                viewModel.Toggle(item);
                e.Handled = true;
                break;
        }
    }
}
