using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views.Swarm;

/// <summary>
/// The Device Matrix's view: tiles in rows that wrap, zoomed by pinching, Ctrl+wheel or the toolbar, a tap that
/// focuses a tile, a double tap on its header that shows it at full size, and Escape that closes the full-size view.
/// It tells the screens when the matrix's tab comes on view or goes, which Avalonia does not tell them.
/// </summary>
public sealed partial class DeviceMatrixView : UserControl
{
    /// <summary>The height a tile adds to its screen: its header, actions, bezel and margins.</summary>
    private const double TileChrome = 150;

    private DeviceMatrixViewModel? _matrix;
    private double _pinchStartZoom;

    /// <summary>Creates the view.</summary>
    public DeviceMatrixView()
    {
        InitializeComponent();
        FitButton.Click += OnFitClicked;
        TilesList.AddHandler(TappedEvent, OnTileTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        TilesList.AddHandler(DoubleTappedEvent, OnTileDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        TilesScroller.GestureRecognizers.Add(new PinchGestureRecognizer());
        TilesScroller.AddHandler(Gestures.PinchEvent, OnPinch);
        TilesScroller.AddHandler(Gestures.PinchEndedEvent, OnPinchEnded);
        TilesScroller.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        KeyDown += OnKeyDown;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_matrix is not null)
        {
            _matrix.BringIntoViewRequested -= OnBringIntoViewRequested;
        }

        _matrix = DataContext as DeviceMatrixViewModel;
        if (_matrix is not null)
        {
            _matrix.BringIntoViewRequested += OnBringIntoViewRequested;
        }

        UpdateOnView();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // The document tabs hide the views of the tabs behind the current one.
        if (change.Property == IsVisibleProperty)
        {
            UpdateOnView();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateOnView();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var tile in _matrix?.Tiles ?? [])
        {
            tile.SetOnView(false);
        }
    }

    /// <summary>Tells every screen and tile whether the matrix is on view.</summary>
    private void UpdateOnView()
    {
        var onView = IsVisible && this.GetVisualRoot() is not null;
        foreach (var content in this.GetVisualDescendants().OfType<ScreenTileContent>())
        {
            content.SetOnView(onView);
        }

        foreach (var tile in _matrix?.Tiles ?? [])
        {
            tile.SetOnView(onView);
        }
    }

    private void OnTileTapped(object? sender, TappedEventArgs e)
    {
        if (TileOf(e.Source) is { } tile)
        {
            _matrix?.Focus(tile);
        }
    }

    /// <summary>A double tap on a tile's header, not on the app inside it, shows the tile at full size.</summary>
    private void OnTileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TileOf(e.Source) is { } tile && (e.Source as Visual)?.FindAncestorOfType<ScreenTileContent>(includeSelf: true) is null && e.Source is not Button)
        {
            _matrix?.ShowFullSize(tile);
        }
    }

    private static ScreenTileViewModel? TileOf(object? source) =>
        (source as Visual)?.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("tile"))?.DataContext as ScreenTileViewModel;

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (_matrix is null)
        {
            return;
        }

        if (_pinchStartZoom <= 0)
        {
            _pinchStartZoom = _matrix.Zoom;
        }

        _matrix.Zoom = Math.Clamp(Math.Round(_pinchStartZoom * e.Scale, 2), DeviceMatrixViewModel.MinimumZoom, DeviceMatrixViewModel.MaximumZoom);
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e) => _pinchStartZoom = 0;

    /// <summary>Ctrl and the mouse wheel zoom, as in a browser.</summary>
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_matrix is null || !e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0)
        {
            return;
        }

        if (e.Delta.Y > 0)
        {
            _matrix.ZoomInCommand.Execute(null);
        }
        else
        {
            _matrix.ZoomOutCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _matrix?.IsFullSize == true)
        {
            _matrix.CloseFullSizeCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnFitClicked(object? sender, RoutedEventArgs e) =>
        _matrix?.FitTo(TilesScroller.Bounds.Size, TileChrome);

    private void OnBringIntoViewRequested(object? sender, ScreenTileViewModel tile) =>
        TilesList.ContainerFromItem(tile)?.BringIntoView();
}
