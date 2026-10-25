using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels.Extensions;

namespace TabStudio.App.Views.Extensions;

/// <summary>
/// The quick pick over the shell: the filter box has the keyboard, the arrow keys move through the list, Enter or a
/// tap picks, and Escape or a tap outside the list cancels.
/// </summary>
public sealed partial class QuickPickView : UserControl
{
    private QuickPickViewModel? _viewModel;

    /// <summary>Creates the view; its data context is the <see cref="QuickPickViewModel"/>.</summary>
    public QuickPickView()
    {
        InitializeComponent();
        FilterBox.AddHandler(KeyDownEvent, OnFilterKeyDown, RoutingStrategies.Tunnel);
        ItemList.AddHandler(TappedEvent, OnItemTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        Scrim.PointerPressed += (_, e) =>
        {
            _viewModel?.Cancel();
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

        _viewModel = DataContext as QuickPickViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(QuickPickViewModel.IsOpen) when _viewModel?.IsOpen == true:
                Dispatcher.UIThread.Post(() => FilterBox.Focus(), DispatcherPriority.Background);
                break;
            case nameof(QuickPickViewModel.SelectedItem) when _viewModel?.SelectedItem is { } selected:
                Dispatcher.UIThread.Post(() => ItemList.ScrollIntoView(selected), DispatcherPriority.Background);
                break;
        }
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { IsOpen: true } viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                viewModel.MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                viewModel.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.PageDown:
                viewModel.MoveSelection(8);
                e.Handled = true;
                break;
            case Key.PageUp:
                viewModel.MoveSelection(-8);
                e.Handled = true;
                break;
            case Key.Enter:
                viewModel.Accept();
                e.Handled = true;
                break;
            case Key.Escape:
                viewModel.Cancel();
                e.Handled = true;
                break;
        }
    }

    private void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control source && source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is QuickPickItem item)
        {
            _viewModel?.Accept(item);
        }
    }
}
