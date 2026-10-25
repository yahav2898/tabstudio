using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The Test Explorer. A tap selects a row and shows its details; tapping a row's chevron expands or collapses it,
/// and a double tap opens a test in the editor. On a keyboard, Enter does the same and the arrow keys expand and
/// collapse.
/// </summary>
public sealed partial class TestExplorerView : UserControl
{
    /// <summary>Creates the view.</summary>
    public TestExplorerView()
    {
        InitializeComponent();
        Tree.AddHandler(TappedEvent, OnTreeTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        Tree.AddHandler(DoubleTappedEvent, OnTreeDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        Tree.KeyDown += OnTreeKeyDown;
    }

    private TestExplorerViewModel? ViewModel => DataContext as TestExplorerViewModel;

    private static TestNodeViewModel? NodeAt(object? source) =>
        (source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as TestNodeViewModel;

    private static bool IsOnChevron(object? source) =>
        source is Control control && control.GetSelfAndVisualAncestors().OfType<Control>().TakeWhile(c => c is not ListBoxItem).Any(c => c.Classes.Contains("chevron"));

    private void OnTreeTapped(object? sender, TappedEventArgs e)
    {
        if (IsOnChevron(e.Source) && NodeAt(e.Source) is { } node)
        {
            ViewModel?.Toggle(node);
        }
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (NodeAt(e.Source) is { } node)
        {
            Activate(node);
        }
    }

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { SelectedNode: { } node } viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                Activate(node);
                e.Handled = true;
                break;
            case Key.Right when node.IsExpandable && !node.IsExpanded:
            case Key.Left when node.IsExpanded:
                viewModel.Toggle(node);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Expands or collapses a group, or opens a test in the editor.</summary>
    private void Activate(TestNodeViewModel node)
    {
        if (node.IsExpandable)
        {
            ViewModel?.Toggle(node);
        }
        else if (node.GoToTestCommand.CanExecute(null))
        {
            node.GoToTestCommand.Execute(null);
        }
    }
}
