using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TabStudio.WindowsCompatibility.Display;
using TabStudio.WindowsCompatibility.Input;

namespace TabStudio.App.Views.WindowsCompatibility;

/// <summary>
/// The Windows desktop a Windows app runs on, in the app preview: Wine's virtual desktop drawn from the X server's
/// framebuffer as it changes, with touch, mouse, pen and keyboard input going back to the app.
/// </summary>
/// <remarks>
/// <para>
/// A tap clicks, a long press right-clicks, a drag drags and two fingers scroll (<see cref="TouchGestures"/>); a
/// mouse or pen is the desktop's mouse; keys that are not text go as keys, and text, from a hardware keyboard or the
/// on-screen one, is typed. Keys TabStudio itself uses while an app runs, such as Shift+F5, stay with the IDE.
/// </para>
/// <para>
/// The desktop has as many pixels as the view at the desktop's scale, so Windows apps that know their DPI, as WPF
/// and .NET's Windows Forms do, draw sharply. <see cref="DesktopSizeChanged"/> tells the session when the view's
/// size asks for another desktop size.
/// </para>
/// </remarks>
public sealed class WindowsDesktopView : Control, IDisposable
{
    private static readonly IBrush Backdrop = new SolidColorBrush(Color.FromRgb(0x24, 0x6E, 0x96));
    private static readonly Typeface StatusFace = new(FontFamily.Default);

    private readonly InputClient _client;
    private readonly DispatcherTimer _resize;
    private readonly Lock _gate = new();
    private WindowsDisplay? _display;
    private TouchGestures? _gestures;
    private WriteableBitmap? _bitmap;
    private byte[] _pixels = [];
    private ScreenRect _dirty;
    private bool _updateQueued;
    private string? _status;
    private (int Width, int Height) _requested;

    static WindowsDesktopView()
    {
        FocusableProperty.OverrideDefaultValue<WindowsDesktopView>(true);
        ClipToBoundsProperty.OverrideDefaultValue<WindowsDesktopView>(true);
    }

    /// <summary>Creates the view.</summary>
    public WindowsDesktopView()
    {
        _client = new InputClient(this);
        _resize = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _resize.Tick += (_, _) =>
        {
            _resize.Stop();
            RequestDesktopSize();
        };

        // The on-screen keyboard types into the app: no suggestions or corrections, and Enter sends Enter.
        TextInputOptions.SetShowSuggestions(this, false);
        TextInputOptions.SetAutoCapitalization(this, false);
        TextInputOptions.SetMultiline(this, true);
        TextInputMethodClientRequested += (_, e) => e.Client = _client;
    }

    /// <summary>The desktop's scale: desktop pixels per device-independent pixel of the view.</summary>
    public double Scale { get; set; } = 1;

    /// <summary>The desktop size the view's size asks for, in desktop pixels.</summary>
    public (int Width, int Height) DesktopSizeForBounds =>
        (Math.Max(320, (int)Math.Round(Bounds.Width * Scale)), Math.Max(240, (int)Math.Round(Bounds.Height * Scale)));

    /// <summary>Whether the view has been laid out, so its size is known.</summary>
    public bool HasSize => Bounds.Width > 1 && Bounds.Height > 1;

    /// <summary>The view's size asks for another desktop size, in desktop pixels.</summary>
    public event EventHandler<(int Width, int Height)>? DesktopSizeChanged;

    /// <summary>Shows a desktop, or none; the view takes its frames and sends it input.</summary>
    /// <param name="display">The desktop.</param>
    public void Attach(WindowsDisplay? display)
    {
        if (_display is { } old)
        {
            old.FrameChanged -= OnFrameChanged;
        }

        _gestures?.Dispose();
        _gestures = null;
        _display = display;
        if (display is not null)
        {
            _gestures = new TouchGestures(display) { Slop = 10 * Scale, ScrollStep = 24 * Scale };
            display.FrameChanged += OnFrameChanged;
            OnFrameChanged(display, new ScreenRect(0, 0, display.DesktopSize.Width, display.DesktopSize.Height));
        }

        InvalidateVisual();
    }

    /// <summary>A line over the desktop while it starts, such as "Setting up Windows", or none.</summary>
    /// <param name="status">The line.</param>
    public void ShowStatus(string? status)
    {
        _status = status;
        InvalidateVisual();
    }

    /// <summary>The frame shown now, as 32-bit BGRA pixels, for tests and screenshots.</summary>
    internal (int Width, int Height, byte[] Pixels) Snapshot()
    {
        lock (_gate)
        {
            var size = _display?.DesktopSize ?? (0, 0);
            return (size.Width, size.Height, (byte[])_pixels.Clone());
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Backdrop, bounds);
        if (_bitmap is { } bitmap)
        {
            var size = bitmap.PixelSize;
            var source = new Rect(0, 0, size.Width, size.Height);
            context.DrawImage(bitmap, source, new Rect(0, 0, size.Width / Scale, size.Height / Scale));
        }

        if (_status is { } status)
        {
            var text = new FormattedText(status, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, StatusFace, 14, Brushes.White);
            context.DrawText(text, new Point((bounds.Width - text.Width) / 2, (bounds.Height - text.Height) / 2));
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _resize.Stop();
        _resize.Start();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (_display is not { } display)
        {
            return;
        }

        var (x, y) = ToDesktop(e.GetPosition(this));
        if (e.Pointer.Type == PointerType.Touch)
        {
            _gestures?.Down(e.Pointer.Id, x, y);
        }
        else
        {
            display.MovePointer((int)x, (int)y);
            var properties = e.GetCurrentPoint(this).Properties;
            display.Button(properties.IsRightButtonPressed ? PointerButton.Right : properties.IsMiddleButtonPressed ? PointerButton.Middle : PointerButton.Left, pressed: true);
            e.Pointer.Capture(this);
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_display is not { } display)
        {
            return;
        }

        var (x, y) = ToDesktop(e.GetPosition(this));
        if (e.Pointer.Type == PointerType.Touch)
        {
            _gestures?.Move(e.Pointer.Id, x, y);
        }
        else
        {
            display.MovePointer((int)x, (int)y);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_display is not { } display)
        {
            return;
        }

        var (x, y) = ToDesktop(e.GetPosition(this));
        if (e.Pointer.Type == PointerType.Touch)
        {
            _gestures?.Up(e.Pointer.Id, x, y);
        }
        else
        {
            display.MovePointer((int)x, (int)y);
            display.Button(e.InitialPressMouseButton switch
            {
                MouseButton.Right => PointerButton.Right,
                MouseButton.Middle => PointerButton.Middle,
                _ => PointerButton.Left,
            }, pressed: false);
            e.Pointer.Capture(null);
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _gestures?.Cancel();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_display is { } display)
        {
            var (x, y) = ToDesktop(e.GetPosition(this));
            display.MovePointer((int)x, (int)y);
            display.Wheel((int)Math.Round(e.Delta.X), (int)Math.Round(e.Delta.Y));
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && _display is { } display && !IsIdeShortcut(e) && WindowsKeys.For(e.Key, e.KeyModifiers) is { } key)
        {
            display.Key(key, pressed: true);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (!e.Handled && _display is { } display && !IsIdeShortcut(e) && WindowsKeys.For(e.Key, e.KeyModifiers) is { } key)
        {
            display.Key(key, pressed: false);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!e.Handled && _display is { } display && !string.IsNullOrEmpty(e.Text))
        {
            display.Type(e.Text);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _resize.Stop();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Attach(null);
        _resize.Stop();
        lock (_gate)
        {
            _bitmap?.Dispose();
            _bitmap = null;
        }
    }

    /// <summary>The keys TabStudio keeps while an app runs: stopping, restarting and the debugger's.</summary>
    private static bool IsIdeShortcut(KeyEventArgs e) =>
        (e.Key == Key.F5 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        || (e.Key == Key.F4 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        || (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control));

    private (double X, double Y) ToDesktop(Point point) => (point.X * Scale, point.Y * Scale);

    private void RequestDesktopSize()
    {
        if (!HasSize)
        {
            return;
        }

        var size = DesktopSizeForBounds;
        if (size != _requested && _display is not null)
        {
            _requested = size;
            DesktopSizeChanged?.Invoke(this, size);
        }
    }

    private void OnFrameChanged(object? sender, ScreenRect area)
    {
        lock (_gate)
        {
            _dirty = _dirty.Union(area);
            if (_updateQueued)
            {
                return;
            }

            _updateQueued = true;
        }

        Dispatcher.UIThread.Post(ShowFrame, DispatcherPriority.Render);
    }

    /// <summary>Copies what changed from the framebuffer into the bitmap the view draws.</summary>
    private void ShowFrame()
    {
        if (_display is not { } display)
        {
            lock (_gate)
            {
                _updateQueued = false;
            }

            return;
        }

        var (width, height) = display.DesktopSize;
        ScreenRect area;
        lock (_gate)
        {
            _updateQueued = false;
            area = _dirty;
            _dirty = default;
            if (_bitmap is null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
            {
                _bitmap?.Dispose();
                _bitmap = new WriteableBitmap(new Avalonia.PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
                _pixels = new byte[width * height * 4];
                area = new ScreenRect(0, 0, width, height);
            }

            area = area.Intersect(new ScreenRect(0, 0, width, height));
            if (area.IsEmpty)
            {
                return;
            }

            display.CopyPixels(area, _pixels, width * 4);
        }

        using (var framebuffer = _bitmap.Lock())
        {
            for (var row = area.Y; row < area.Y + area.Height; row++)
            {
                var offset = (row * width * 4) + (area.X * 4);
                Marshal.Copy(_pixels, offset, framebuffer.Address + (row * framebuffer.RowBytes) + (area.X * 4), area.Width * 4);
            }
        }

        if (_status is not null)
        {
            _status = null;
        }

        InvalidateVisual();
    }

    private sealed class InputClient(WindowsDesktopView view) : TextInputMethodClient
    {
        public override Visual TextViewVisual => view;

        public override bool SupportsPreedit => false;

        public override bool SupportsSurroundingText => false;

        public override string SurroundingText => string.Empty;

        public override Rect CursorRectangle => default;

        public override TextSelection Selection { get; set; }
    }
}
