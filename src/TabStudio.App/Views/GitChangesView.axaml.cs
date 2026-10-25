using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The Git Changes window. Tapping a changed file compares it with its unmodified version; a long-press or
/// right-click offers the other actions. Ctrl+Enter in the message box commits, as in Visual Studio.
/// </summary>
public sealed partial class GitChangesView : UserControl
{
    /// <summary>Creates the view.</summary>
    public GitChangesView()
    {
        InitializeComponent();

        // Tunnel, so the shortcut wins over the text box's own handling of Enter.
        CommitMessageBox.AddHandler(KeyDownEvent, OnCommitMessageKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnCommitMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control
            && DataContext is GitChangesViewModel viewModel && viewModel.CommitCommand.CanExecute(null))
        {
            viewModel.CommitCommand.Execute(null);
            e.Handled = true;
        }
    }
}
