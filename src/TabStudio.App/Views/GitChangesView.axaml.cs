using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The Git Changes window. Tapping a changed file compares it with its unmodified version; a long-press or
/// right-click offers the other actions. Ctrl+Enter in the message box commits, as in Visual Studio.
/// </summary>
public sealed partial class GitChangesView : UserControl
{
    private GitChangesViewModel? _viewModel;

    /// <summary>Creates the view.</summary>
    public GitChangesView()
    {
        InitializeComponent();

        // Tunnel, so the shortcut wins over the text box's own handling of Enter.
        CommitMessageBox.AddHandler(KeyDownEvent, OnCommitMessageKeyDown, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.CommitMessageFocusRequested -= OnCommitMessageFocusRequested;
        }

        _viewModel = DataContext as GitChangesViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CommitMessageFocusRequested += OnCommitMessageFocusRequested;
        }
    }

    private void OnCommitMessageFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => CommitMessageBox.Focus(), DispatcherPriority.Background);

    private void OnCommitMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control
            && _viewModel is { } viewModel && viewModel.CommitCommand.CanExecute(null))
        {
            viewModel.CommitCommand.Execute(null);
            e.Handled = true;
        }
    }
}
