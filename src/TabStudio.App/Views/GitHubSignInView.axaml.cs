using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>The GitHub account dialog. The token box takes focus when it opens; Enter signs in and Escape cancels.</summary>
public sealed partial class GitHubSignInView : UserControl
{
    private GitHubAccountViewModel? _viewModel;

    /// <summary>Creates the view.</summary>
    public GitHubSignInView()
    {
        InitializeComponent();
        TokenBox.AddHandler(KeyDownEvent, OnTokenKeyDown, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as GitHubAccountViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GitHubAccountViewModel.IsOpen) && _viewModel is { IsOpen: true, IsSignedIn: false })
        {
            Dispatcher.UIThread.Post(() => TokenBox.Focus(), DispatcherPriority.Background);
        }
    }

    private void OnTokenKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.Key == Key.Enter && _viewModel.SignInCommand.CanExecute(null))
        {
            _viewModel.SignInCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }
}
