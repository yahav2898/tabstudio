using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using TabStudio.App.ViewModels.Extensions;

namespace TabStudio.App.Views.Extensions;

/// <summary>
/// The Extensions window in its tab: the lists on the left and the selected extension's details on the right. Where
/// the tab is too narrow for both, as in portrait or beside the side bar, it shows one at a time, with a way back.
/// </summary>
public sealed partial class ExtensionsView : UserControl, IDisposable
{
    // Below this width the lists and the details take turns.
    private const double NarrowWidth = 840;
    private const double ListWidth = 400;

    private ExtensionsViewModel? _viewModel;

    /// <summary>Creates the view; its data context is the <see cref="ExtensionsViewModel"/>.</summary>
    public ExtensionsView()
    {
        InitializeComponent();
        SearchBox.KeyDown += OnSearchKeyDown;
        RegistryBox.KeyDown += OnRegistryKeyDown;
        foreach (var list in new[] { BrowseList, InstalledList, UpdatesList })
        {
            list.ContainerPrepared += OnContainerPrepared;
        }
    }

    /// <summary>Whether the lists and the details take turns because the tab is narrow.</summary>
    public bool IsNarrow { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ExtensionsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateLayoutMode();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateLayoutMode();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExtensionsViewModel.HasDetails))
        {
            UpdateLayoutMode();
        }
    }

    /// <summary>Shows the lists and the details side by side, or one at a time when the tab is narrow.</summary>
    private void UpdateLayoutMode()
    {
        var width = Bounds.Width;
        IsNarrow = width > 0 && width < NarrowWidth;
        var showDetails = _viewModel?.HasDetails == true;
        var columns = Root.ColumnDefinitions;
        if (IsNarrow)
        {
            columns[0].Width = showDetails ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            columns[1].Width = showDetails ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        }
        else
        {
            columns[0].Width = new GridLength(Math.Min(ListWidth, Math.Max(320, width * 0.4)));
            columns[1].Width = new GridLength(1, GridUnitType.Star);
        }

        ListColumn.IsVisible = !IsNarrow || !showDetails;
        ColumnDivider.IsVisible = !IsNarrow;
        DetailsColumn.IsVisible = !IsNarrow || showDetails;
        BackBar.IsVisible = IsNarrow && showDetails;
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel is { } viewModel)
        {
            viewModel.SearchNowCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnRegistryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel is { } viewModel)
        {
            viewModel.ApplyRegistryCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>Loads a row's icon when the row is about to be shown, so long lists only load what is seen.</summary>
    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        var item = e.Container.DataContext as ExtensionItemViewModel ?? (sender as ItemsControl)?.ItemFromContainer(e.Container) as ExtensionItemViewModel;
        if (item is not null)
        {
            _ = item.LoadIconAsync();
        }
    }
}
