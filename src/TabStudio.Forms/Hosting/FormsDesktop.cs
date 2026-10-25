using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;
using DrawingSize = System.Drawing.Size;

namespace TabStudio.Forms.Hosting;

/// <summary>
/// The desktop a running Windows Forms app's windows are shown on, in the app preview: each window with its title bar
/// and border as on Windows 11, its menus and drop-downs above, and minimized windows as buttons along the bottom.
/// </summary>
/// <remarks>
/// <para>
/// The app runs on its own thread (see <see cref="FormsRuntime"/>), which hands the desktop frames of its windows as
/// they change; the desktop draws them, and sends the pointer, keys and typed text back. A window keeps its size in
/// the app's pixels: when the largest window does not fit the pane, the whole desktop is scaled down to fit it.
/// </para>
/// <para>
/// The text field with focus is edited in a native text box over it, so the device's keyboard and its text editing
/// work as in any other text box.
/// </para>
/// </remarks>
internal sealed partial class FormsDesktop : Control, IFormsPresenter
{
    /// <summary>
    /// The wallpaper behind the windows; Windows 11's light one when the host sets none. The IDE gives it the dark one
    /// in its dark theme, as Windows does in dark mode; the windows keep their light frames, as apps that do not ask for
    /// dark title bars do.
    /// </summary>
    public static readonly StyledProperty<IBrush?> WallpaperProperty = AvaloniaProperty.Register<FormsDesktop, IBrush?>(nameof(Wallpaper));

    /// <summary>
    /// The space kept around the largest window when the desktop is scaled to fit it: room for the margin a centered
    /// window keeps from the desktop's edges, and for its invisible resize borders.
    /// </summary>
    private const int FitMargin = 24;

    /// <summary>The smallest scale the desktop is drawn at, so text stays readable.</summary>
    private const double MinimumScale = 0.35;

    private static readonly IBrush LightWallpaper = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(0xDC, 0xE8, 0xF6), 0),
            new GradientStop(Color.FromRgb(0xC2, 0xD5, 0xEE), 0.55),
            new GradientStop(Color.FromRgb(0xA8, 0xC3, 0xE6), 1),
        },
    };

    private readonly DisplayListRenderer _renderer = new();
    private readonly DesktopCanvas _canvas = new();
    private readonly OverlayView _overlay;
    private readonly NativeTextEditor _editor = new();
    private readonly Dictionary<int, WindowView> _views = [];
    private readonly object _pendingGate = new();
    private readonly List<(IReadOnlyList<WindowFrame> Frames, IReadOnlyList<int> Closed, IReadOnlyList<int> Order)> _pending = [];
    private List<int> _order = [];
    private bool _flushScheduled;
    private long _desktopSize = Pack(new DrawingSize(1280, 800));
    private double _scale = 1;
    private Size _viewSize;

    static FormsDesktop()
    {
        AffectsRender<FormsDesktop>(WallpaperProperty);
    }

    /// <summary>Creates an empty desktop.</summary>
    public FormsDesktop()
    {
        _overlay = new OverlayView(_renderer);
        _canvas.Children.Add(_editor);
        _canvas.Children.Add(_overlay);
        _editor.ZIndex = int.MaxValue - 1;
        _overlay.ZIndex = int.MaxValue;
        _canvas.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        LogicalChildren.Add(_canvas);
        VisualChildren.Add(_canvas);
        ClipToBounds = true;
        Focusable = true;
        InitializeInput();
        InitializeKeyboard();
        InitializeEditor();
    }

    /// <inheritdoc cref="WallpaperProperty"/>
    public IBrush? Wallpaper
    {
        get => GetValue(WallpaperProperty);
        set => SetValue(WallpaperProperty, value);
    }

    /// <summary>Raised on the UI thread the first time the desktop knows its size, which the app's first window is placed in.</summary>
    public event EventHandler? SizeKnown;

    /// <summary>The app whose windows the desktop shows, once it starts.</summary>
    public FormsRuntime? Runtime { get; set; }

    /// <summary>Whether the desktop has been laid out, so it knows its size.</summary>
    public bool IsSizeKnown { get; private set; }

    /// <summary>The scale the desktop is drawn at: 1, or less when the largest window does not fit.</summary>
    public double Scale => _scale;

    /// <summary>The windows shown, from the bottom of the z-order.</summary>
    public IReadOnlyList<WindowFrame> Windows => [.. _order.Where(_views.ContainsKey).Select(id => _views[id].Frame)];

    /// <inheritdoc />
    public DrawingSize DesktopSize => Unpack(Interlocked.Read(ref _desktopSize));

    /// <inheritdoc />
    public void Present(IReadOnlyList<WindowFrame> frames, IReadOnlyList<int> closed, IReadOnlyList<int> order)
    {
        lock (_pendingGate)
        {
            _pending.Add((frames, closed, order));
            if (_flushScheduled)
            {
                return;
            }

            _flushScheduled = true;
        }

        Dispatcher.UIThread.Post(Flush, DispatcherPriority.Render);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) => context.FillRectangle(Wallpaper ?? LightWallpaper, new Rect(Bounds.Size));

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 1280 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 800 : availableSize.Height;
        var desktop = DesktopSize;
        _canvas.Measure(new Size(desktop.Width, desktop.Height));
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _viewSize = finalSize;
        UpdateScale();
        var desktop = DesktopSize;
        _canvas.Arrange(new Rect(0, 0, desktop.Width, desktop.Height));
        if (!IsSizeKnown && finalSize.Width > 0 && finalSize.Height > 0)
        {
            IsSizeKnown = true;
            SizeKnown?.Invoke(this, EventArgs.Empty);
        }

        return finalSize;
    }

    private static long Pack(DrawingSize size) => ((long)size.Width << 32) | (uint)size.Height;

    private static DrawingSize Unpack(long packed) => new((int)(packed >> 32), (int)(packed & 0xFFFFFFFF));

    /// <summary>Applies the frames the app presented since the last flush, in order.</summary>
    private void Flush()
    {
        List<(IReadOnlyList<WindowFrame> Frames, IReadOnlyList<int> Closed, IReadOnlyList<int> Order)> batches;
        lock (_pendingGate)
        {
            batches = [.. _pending];
            _pending.Clear();
            _flushScheduled = false;
        }

        foreach (var (frames, closed, order) in batches)
        {
            foreach (var id in closed)
            {
                if (_views.Remove(id, out var view))
                {
                    _canvas.Children.Remove(view);
                }
            }

            foreach (var frame in frames)
            {
                if (_views.TryGetValue(frame.Id, out var view))
                {
                    view.Update(frame);
                }
                else
                {
                    view = new WindowView(_renderer, frame);
                    _views.Add(frame.Id, view);
                    _canvas.Children.Add(view);
                }
            }

            _order = [.. order];
        }

        for (var i = 0; i < _order.Count; i++)
        {
            if (_views.TryGetValue(_order[i], out var view))
            {
                view.ZIndex = i;
            }
        }

        var windows = Windows;
        _overlay.Update(windows, DesktopSize);
        UpdateEditor();
        OnWindowsChanged();
        _canvas.InvalidateArrange();
        UpdateScale();
    }

    /// <summary>
    /// Scales the desktop so the largest window fits the pane, and tells the app when the desktop's size changed, so
    /// it can center or maximize its windows again.
    /// </summary>
    private void UpdateScale()
    {
        if (_viewSize.Width <= 0 || _viewSize.Height <= 0)
        {
            return;
        }

        // Maximized and minimized windows take the desktop's size rather than ask for one.
        var needWidth = 0;
        var needHeight = 0;
        foreach (var view in _views.Values)
        {
            if (!view.Frame.IsMaximized && !view.Frame.IsMinimized)
            {
                needWidth = Math.Max(needWidth, view.Frame.Frame.Width + (2 * FitMargin));
                needHeight = Math.Max(needHeight, view.Frame.Frame.Height + (2 * FitMargin));
            }
        }

        var scale = Math.Min(1, Math.Min(needWidth > 0 ? _viewSize.Width / needWidth : 1, needHeight > 0 ? _viewSize.Height / needHeight : 1));
        scale = Math.Max(MinimumScale, Math.Floor(scale * 100) / 100);
        var desktop = new DrawingSize((int)Math.Floor(_viewSize.Width / scale), (int)Math.Floor(_viewSize.Height / scale));
        var changed = desktop != DesktopSize;
        if (Math.Abs(scale - _scale) > 0.001 || _canvas.RenderTransform is null)
        {
            _scale = scale;
            _canvas.RenderTransform = new ScaleTransform(scale, scale);
        }

        if (changed)
        {
            Interlocked.Exchange(ref _desktopSize, Pack(desktop));
            _overlay.Update(Windows, desktop);
            InvalidateMeasure();
            if (Runtime is { } runtime)
            {
                runtime.Post(runtime.OnDesktopResized);
            }
        }
    }

    /// <summary>The window views from the top of the z-order down.</summary>
    private IEnumerable<WindowView> ViewsFromTop()
    {
        for (var i = _order.Count - 1; i >= 0; i--)
        {
            if (_views.TryGetValue(_order[i], out var view))
            {
                yield return view;
            }
        }
    }
}
