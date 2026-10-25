using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels.Search;

namespace TabStudio.App.Views.Search;

/// <summary>
/// The search over the shell: the query box has the keyboard with its text selected, the arrow keys move through the
/// results, Tab and Shift+Tab change the scope, Enter or a tap opens, and Escape or a tap outside closes.
/// </summary>
public sealed partial class UnifiedSearchView : UserControl
{
    private UnifiedSearchViewModel? _viewModel;

    /// <summary>Creates the view; its data context is the <see cref="UnifiedSearchViewModel"/>.</summary>
    public UnifiedSearchView()
    {
        InitializeComponent();
        QueryBox.AddHandler(KeyDownEvent, OnQueryKeyDown, RoutingStrategies.Tunnel);
        ResultList.AddHandler(TappedEvent, OnResultTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        Scrim.PointerPressed += (_, e) =>
        {
            _viewModel?.CloseCommand.Execute(null);
            e.Handled = true;
        };
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as UnifiedSearchViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(UnifiedSearchViewModel.IsOpen) when _viewModel?.IsOpen == true:
                Dispatcher.UIThread.Post(
                    () =>
                    {
                        QueryBox.Focus();
                        QueryBox.SelectAll();
                    },
                    DispatcherPriority.Background);
                break;
            case nameof(UnifiedSearchViewModel.SelectedRow) when _viewModel?.SelectedRow is { } selected:
                Dispatcher.UIThread.Post(() => ResultList.ScrollIntoView(selected), DispatcherPriority.Background);
                break;
        }
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { IsOpen: true } viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                viewModel.MoveSelection(1);
                break;
            case Key.Up:
                viewModel.MoveSelection(-1);
                break;
            case Key.PageDown:
                viewModel.MoveSelection(8);
                break;
            case Key.PageUp:
                viewModel.MoveSelection(-8);
                break;
            case Key.Tab:
                MoveScope(viewModel, e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                break;
            case Key.Enter:
                _ = viewModel.AcceptCommand.ExecuteAsync(null);
                break;
            case Key.Escape:
                viewModel.CloseCommand.Execute(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private static void MoveScope(UnifiedSearchViewModel viewModel, int delta)
    {
        var scopes = viewModel.Scopes;
        var index = scopes.ToList().FindIndex(chip => chip.IsSelected);
        var next = scopes[((index + delta) % scopes.Count + scopes.Count) % scopes.Count];
        viewModel.ChooseScopeCommand.Execute(next);
    }

    private void OnResultTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control source && source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is SearchRow row)
        {
            _ = _viewModel?.AcceptCommand.ExecuteAsync(row);
        }
    }
}
