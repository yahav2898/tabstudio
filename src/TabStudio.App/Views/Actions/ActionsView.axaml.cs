using Avalonia.Controls;
using Avalonia.Input;
using TabStudio.App.ViewModels.Actions;

namespace TabStudio.App.Views.Actions;

/// <summary>
/// The GitHub Actions window in the side bar: the workspace commit's checks, the runs with their filters, the
/// workflows with Run workflow..., watched repositories and notifications; a run's details replace them while
/// open. Pulling the list down refreshes it, as Refresh does.
/// </summary>
public sealed partial class ActionsView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ActionsView()
    {
        InitializeComponent();
        Refresher.RefreshRequested += OnRefreshRequested;
        WatchBox.KeyDown += OnWatchBoxKeyDown;
    }

    private ActionsViewModel? ViewModel => DataContext as ActionsViewModel;

    private async void OnRefreshRequested(object? sender, RefreshRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            if (ViewModel is { } viewModel && viewModel.RefreshCommand.CanExecute(null))
            {
                await viewModel.RefreshCommand.ExecuteAsync(null);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnWatchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel is { } viewModel && viewModel.AddWatchedCommand.CanExecute(null))
        {
            viewModel.AddWatchedCommand.Execute(null);
            e.Handled = true;
        }
    }
}
