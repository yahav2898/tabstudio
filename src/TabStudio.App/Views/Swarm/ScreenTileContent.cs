using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Swarm;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views.Swarm;

/// <summary>
/// The screen inside a Device Matrix tile, at the matrix's zoom: a virtual device's own frame, live and touchable;
/// the app preview, drawn live from the preview pane; or a phone's latest picture. The element of the finding being
/// shown is outlined over it.
/// </summary>
/// <remarks>
/// A virtual device's frame is a single control, so only one place shows it: the latest content that claimed it, such
/// as the full-size view over the tile. While no content on view claims it, it waits on the window's
/// <see cref="ScreenStage"/>, so it keeps running and taking mirrored input.
/// </remarks>
public sealed class ScreenTileContent : Decorator
{
    /// <summary>The tile whose screen this shows.</summary>
    public static readonly StyledProperty<ScreenTileViewModel?> TileProperty =
        AvaloniaProperty.Register<ScreenTileContent, ScreenTileViewModel?>(nameof(Tile));

    /// <summary>The zoom to draw at instead of the matrix's, such as the full-size view's; NaN for the matrix's.</summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<ScreenTileContent, double>(nameof(Zoom), double.NaN);

    /// <summary>How often the app preview's live picture is drawn again, while on view.</summary>
    private static readonly TimeSpan MirrorInterval = TimeSpan.FromMilliseconds(120);

    private static readonly IBrush HighlightStroke = new ImmutableSolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
    private static readonly IBrush HighlightFill = new ImmutableSolidColorBrush(Color.FromArgb(0x30, 0xF4, 0x43, 0x36));

    private readonly LayoutTransformControl _zoom = new();
    private readonly Panel _screen = new() { ClipToBounds = true };
    private readonly Decorator _frameHost = new();
    private readonly Border _mirror = new();
    private readonly Image _snapshot = new() { Stretch = Stretch.Uniform };
    private readonly Canvas _highlights = new() { IsHitTestVisible = false };
    private readonly DispatcherTimer _mirrorTimer = new() { Interval = MirrorInterval };
    private readonly VisualBrush _mirrorBrush = new() { Stretch = Stretch.Uniform };
    private ScreenTileViewModel? _tile;
    private ScreenStage? _stage;
    private bool _onView = true;
    private bool _claimed;

    /// <summary>Creates the content.</summary>
    public ScreenTileContent()
    {
        _mirror.Background = _mirrorBrush;
        _screen.Children.Add(_frameHost);
        _screen.Children.Add(_mirror);
        _screen.Children.Add(_snapshot);
        _screen.Children.Add(_highlights);
        _zoom.Child = _screen;
        Child = _zoom;
        _mirrorTimer.Tick += (_, _) => RefreshMirror();
    }

    /// <inheritdoc cref="TileProperty"/>
    public ScreenTileViewModel? Tile
    {
        get => GetValue(TileProperty);
        set => SetValue(TileProperty, value);
    }

    /// <inheritdoc cref="ZoomProperty"/>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>
    /// Whether the content is on view: the Device Matrix tells it when its tab comes forward or goes behind another,
    /// which Avalonia does not tell a control.
    /// </summary>
    /// <param name="onView">Whether it is on view.</param>
    public void SetOnView(bool onView)
    {
        _onView = onView;
        UpdateClaim();
        UpdateMirror();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TileProperty)
        {
            if (_tile is not null)
            {
                _tile.PropertyChanged -= OnTilePropertyChanged;
                Release();
            }

            _tile = change.GetNewValue<ScreenTileViewModel?>();
            if (_tile is not null)
            {
                _tile.PropertyChanged += OnTilePropertyChanged;
            }

            Update();
        }
        else if (change.Property == ZoomProperty)
        {
            UpdateSize();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _stage = ScreenStage.Of(this);

        // A tile added while the matrix's tab is behind another is not on view.
        _onView = this.FindAncestorOfType<DeviceMatrixView>() is not { } matrix || matrix.IsVisible;
        Update();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Release();
        _mirrorTimer.Stop();
    }

    private void OnTilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ScreenTileViewModel.Profile) or nameof(ScreenTileViewModel.Zoom):
                UpdateSize();
                UpdateHighlights();
                break;
            case nameof(ScreenTileViewModel.Highlights):
                UpdateHighlights();
                break;
            case nameof(ScreenTileViewModel.Snapshot):
                _snapshot.Source = _tile?.Snapshot;
                break;
            case nameof(ScreenTileViewModel.IsRunning):
                UpdateMirror();
                break;
        }
    }

    private void Update()
    {
        UpdateSize();
        _snapshot.Source = _tile?.Snapshot;
        _snapshot.IsVisible = _tile?.IsPictured == true;
        _mirror.IsVisible = _tile?.Preview is not null;
        _frameHost.IsVisible = _tile?.IsVirtual == true;
        UpdateClaim();
        UpdateMirror();
        UpdateHighlights();
    }

    private double EffectiveZoom => double.IsNaN(Zoom) ? _tile?.Zoom ?? 1 : Zoom;

    private void UpdateSize()
    {
        if (_tile is null)
        {
            return;
        }

        var zoom = EffectiveZoom;
        _screen.Width = _tile.Profile.Width;
        _screen.Height = _tile.Profile.Height;
        _zoom.LayoutTransform = new ScaleTransform(zoom, zoom);
        _highlights.Width = _tile.Profile.Width;
        _highlights.Height = _tile.Profile.Height;
    }

    /// <summary>A virtual device's frame comes here while this is on view, and goes back to the stage when not.</summary>
    private void UpdateClaim()
    {
        var wanted = _onView && _tile?.Virtual is not null && this.GetVisualRoot() is not null;
        if (wanted && !_claimed)
        {
            _claimed = true;
            FrameClaims.Claim(_tile!.Virtual!.Frame, this);
        }
        else if (!wanted && _claimed)
        {
            Release();
        }
    }

    private void Release()
    {
        if (_claimed && _tile?.Virtual is { } device)
        {
            _claimed = false;
            FrameClaims.Release(device.Frame, this);
        }
    }

    /// <summary>Shows a frame this content has claimed.</summary>
    internal void Show(Control frame) => ScreenPlacement.Show(frame, _frameHost);

    /// <summary>Where frames wait while nothing on view shows them.</summary>
    internal ScreenStage? Stage => _stage;

    /// <summary>The app preview is drawn live while this is on view.</summary>
    private void UpdateMirror()
    {
        if (_tile?.Preview is not null && _onView && this.GetVisualRoot() is not null)
        {
            RefreshMirror();
            _mirrorTimer.Start();
        }
        else
        {
            _mirrorTimer.Stop();
        }
    }

    private void RefreshMirror()
    {
        if (_tile?.Preview is not { } preview || preview.Surface is not { } surface)
        {
            _mirrorBrush.Visual = null;
            return;
        }

        // The preview pane may be hidden behind another side bar panel, where Avalonia does not lay it out.
        preview.EnsureLayout();
        _mirrorBrush.Visual = null;
        _mirrorBrush.Visual = surface;
        _mirror.InvalidateVisual();
    }

    private void UpdateHighlights()
    {
        _highlights.Children.Clear();
        if (_tile is null)
        {
            return;
        }

        var thickness = 3 / Math.Max(0.1, EffectiveZoom);
        foreach (var bounds in _tile.Highlights)
        {
            var outline = new Rectangle
            {
                Width = bounds.Width + (2 * thickness),
                Height = bounds.Height + (2 * thickness),
                Stroke = HighlightStroke,
                Fill = HighlightFill,
                StrokeThickness = thickness,
                RadiusX = 2 * thickness,
                RadiusY = 2 * thickness,
            };
            Canvas.SetLeft(outline, bounds.X - thickness);
            Canvas.SetTop(outline, bounds.Y - thickness);
            _highlights.Children.Add(outline);
        }
    }
}
