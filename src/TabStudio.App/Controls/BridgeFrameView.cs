using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TabStudio.App.ViewModels.WindowsBridge;
using TabStudio.WindowsBridge.Client;
using TabStudio.WindowsBridge.Input;

namespace TabStudio.App.Controls;

/// <summary>
/// The picture of a window on the PC, streamed through the Windows Bridge, as large as the pane allows: touch on it
/// becomes the mouse on the PC (tap, long press, drag, two-finger scroll), a mouse or keyboard on the tablet works as
/// it would at the PC, and the on-screen keyboard types into the program.
/// </summary>
public sealed class BridgeFrameView : Control, IDisposable
{
    /// <summary>The stream to draw.</summary>
    public static readonly StyledProperty<BridgeStreamReceiver?> StreamProperty =
        AvaloniaProperty.Register<BridgeFrameView, BridgeStreamReceiver?>(nameof(Stream));

    /// <summary>Where the input goes.</summary>
    public static readonly StyledProperty<IBridgeInputSink?> InputSinkProperty =
        AvaloniaProperty.Register<BridgeFrameView, IBridgeInputSink?>(nameof(InputSink));

    /// <summary>The PC's pixels per device-independent pixel of the window, so it is drawn at its own size.</summary>
    public static readonly StyledProperty<double> WindowScaleProperty =
        AvaloniaProperty.Register<BridgeFrameView, double>(nameof(WindowScale), 1);

    /// <summary>Whether the picture is dimmed, while the PC is away.</summary>
    public static readonly StyledProperty<bool> IsDimmedProperty =
        AvaloniaProperty.Register<BridgeFrameView, bool>(nameof(IsDimmed));

    private static readonly IBrush Backdrop = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16));
    private static readonly IBrush Dim = new SolidColorBrush(Color.FromArgb(0xA0, 0x10, 0x10, 0x12));
    private static readonly IPen Frame = new Pen(new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)), 1);

    private readonly TouchGestureRecognizer _touch = new();
    private readonly DispatcherTimer _longPress;
    private readonly BridgeImeClient _ime;
    private readonly Dictionary<IPointer, BridgePointerButton> _mouseButtons = [];
    private WriteableBitmap? _bitmap;
    private FrameMapping _map;
    private int _frameWidth;
    private int _frameHeight;

    /// <summary>Creates the view.</summary>
    public BridgeFrameView()
    {
        Focusable = true;
        ClipToBounds = true;
        _ime = new BridgeImeClient(this);
        _longPress = new DispatcherTimer(TimeSpan.FromMilliseconds(60), DispatcherPriority.Input, (_, _) => OnLongPressTick());
        TextInputOptions.SetShowSuggestions(this, false);
        TextInputOptions.SetAutoCapitalization(this, false);
        TextInputOptions.SetMultiline(this, true);
        AddHandler(TextInputMethodClientRequestedEvent, (_, e) => e.Client = _ime);
    }

    static BridgeFrameView()
    {
        AffectsRender<BridgeFrameView>(StreamProperty, WindowScaleProperty, IsDimmedProperty);
    }

    /// <summary>The stream to draw.</summary>
    public BridgeStreamReceiver? Stream
    {
        get => GetValue(StreamProperty);
        set => SetValue(StreamProperty, value);
    }

    /// <summary>Where the input goes.</summary>
    public IBridgeInputSink? InputSink
    {
        get => GetValue(InputSinkProperty);
        set => SetValue(InputSinkProperty, value);
    }

    /// <summary>The PC's pixels per device-independent pixel of the window.</summary>
    public double WindowScale
    {
        get => GetValue(WindowScaleProperty);
        set => SetValue(WindowScaleProperty, value);
    }

    /// <summary>Whether the picture is dimmed.</summary>
    public bool IsDimmed
    {
        get => GetValue(IsDimmedProperty);
        set => SetValue(IsDimmedProperty, value);
    }

    /// <summary>Where the window's picture is drawn, for input and tests.</summary>
    public FrameMapping Mapping => _map;

    /// <summary>Copies the stream's newest picture into the view and draws it.</summary>
    /// <returns>Whether there was a picture to draw.</returns>
    public bool Refresh()
    {
        if (Stream is not { } stream)
        {
            return false;
        }

        var drawn = stream.Read(frame =>
        {
            if (_bitmap is null || _frameWidth != frame.Width || _frameHeight != frame.Height)
            {
                _bitmap?.Dispose();
                _bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
                _frameWidth = frame.Width;
                _frameHeight = frame.Height;
            }

            using var target = _bitmap.Lock();
            var row = frame.Width * TabStudio.WindowsBridge.Frames.FrameBitmap.BytesPerPixel;
            for (var y = 0; y < frame.Height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(frame.Pixels, y * row, target.Address + (y * target.RowBytes), row);
            }

            return true;
        });
        InvalidateVisual();
        return drawn;
    }

    /// <summary>Releases the picture's bitmap.</summary>
    public void Dispose()
    {
        _longPress.Stop();
        _bitmap?.Dispose();
        _bitmap = null;
    }

    /// <summary>Shows the on-screen keyboard for typing into the program.</summary>
    public void ShowKeyboard()
    {
        // The keyboard opens when the focus arrives at a view that takes text, so focus the view anew.
        if (IsFocused)
        {
            TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        }

        Focus();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.FillRectangle(Backdrop, new Rect(Bounds.Size));
        var stream = Stream;
        if (_bitmap is null || stream is null || stream.WindowWidth <= 0)
        {
            _map = default;
            return;
        }

        _map = FrameMapping.Fit(Bounds.Width, Bounds.Height, stream.WindowWidth, stream.WindowHeight, WindowScale);
        var destination = new Rect(_map.Left, _map.Top, _map.DrawnWidth, _map.DrawnHeight);
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
        {
            context.DrawImage(_bitmap, new Rect(0, 0, _frameWidth, _frameHeight), destination);
        }

        context.DrawRectangle(null, Frame, destination.Inflate(0.5));
        if (IsDimmed)
        {
            context.FillRectangle(Dim, new Rect(Bounds.Size));
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == StreamProperty)
        {
            // A new stream starts with its own first picture.
            _bitmap?.Dispose();
            _bitmap = null;
            Send(_touch.Cancel(_map));
            _longPress.Stop();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Dispose();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerPressed(e);
        Focus();
        var position = e.GetPosition(this);
        if (_map.IsEmpty || !_map.Contains(position.X, position.Y))
        {
            return;
        }

        e.Handled = true;
        e.Pointer.Capture(this);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            var button = e.GetCurrentPoint(this).Properties.PointerUpdateKind switch
            {
                PointerUpdateKind.RightButtonPressed => BridgePointerButton.Right,
                PointerUpdateKind.MiddleButtonPressed => BridgePointerButton.Middle,
                _ => BridgePointerButton.Left,
            };
            _mouseButtons[e.Pointer] = button;
            var (x, y) = _map.ToWindow(position.X, position.Y);
            Send([BridgeInputEvent.Mouse(BridgeInputKind.PointerMove, x, y), BridgeInputEvent.Mouse(BridgeInputKind.PointerDown, x, y, button)]);
            return;
        }

        Send(_touch.Down(e.Pointer.Id, position.X, position.Y, Now(), _map));
        _longPress.Start();
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerMoved(e);
        if (_map.IsEmpty)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            if (_mouseButtons.TryGetValue(e.Pointer, out var held) || _map.Contains(position.X, position.Y))
            {
                var (x, y) = _map.ToWindow(position.X, position.Y);
                Send([BridgeInputEvent.Mouse(BridgeInputKind.PointerMove, x, y, held)]);
                e.Handled = held != BridgePointerButton.None;
            }

            return;
        }

        if (_touch.IsActive)
        {
            Send(_touch.Move(e.Pointer.Id, position.X, position.Y, Now(), _map));
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerReleased(e);
        var position = e.GetPosition(this);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            if (_mouseButtons.Remove(e.Pointer, out var button))
            {
                var (x, y) = _map.ToWindow(position.X, position.Y);
                Send([BridgeInputEvent.Mouse(BridgeInputKind.PointerUp, x, y, button)]);
                e.Handled = true;
            }

            e.Pointer.Capture(null);
            return;
        }

        if (_touch.IsActive)
        {
            Send(_touch.Up(e.Pointer.Id, position.X, position.Y, Now(), _map));
            e.Handled = true;
        }

        if (!_touch.IsActive)
        {
            _longPress.Stop();
            e.Pointer.Capture(null);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerCaptureLost(e);
        if (e.Pointer.Type != PointerType.Mouse && _touch.IsActive)
        {
            Send(_touch.Cancel(_map));
            _longPress.Stop();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerWheelChanged(e);
        var position = e.GetPosition(this);
        if (_map.IsEmpty || !_map.Contains(position.X, position.Y))
        {
            return;
        }

        // Avalonia's horizontal delta is positive to the left; Windows' is positive to the right.
        var (x, y) = _map.ToWindow(position.X, position.Y);
        Send([BridgeInputEvent.Wheel(x, y, (int)Math.Round(-e.Delta.X * BridgeInputEvent.WheelNotch), (int)Math.Round(e.Delta.Y * BridgeInputEvent.WheelNotch))]);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);
        if (ToVirtualKey(e) is { } key)
        {
            Send([BridgeInputEvent.Key(key, down: true)]);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyUp(e);
        if (ToVirtualKey(e) is { } key)
        {
            Send([BridgeInputEvent.Key(key, down: false)]);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnTextInput(e);
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        e.Handled = true;
        var events = new List<BridgeInputEvent>();
        var text = new System.Text.StringBuilder();
        foreach (var c in e.Text)
        {
            // The on-screen keyboard types Enter and Tab as characters: they are keys to Windows programs.
            var key = c switch
            {
                '\n' or '\r' => VirtualKeys.Enter,
                '\t' => VirtualKeys.Tab,
                '\b' => VirtualKeys.Back,
                _ => 0,
            };
            if (key == 0)
            {
                if (!char.IsControl(c))
                {
                    text.Append(c);
                }

                continue;
            }

            if (text.Length > 0)
            {
                events.Add(BridgeInputEvent.Typed(text.ToString()));
                text.Clear();
            }

            events.Add(BridgeInputEvent.Key(key, down: true));
            events.Add(BridgeInputEvent.Key(key, down: false));
        }

        if (text.Length > 0)
        {
            events.Add(BridgeInputEvent.Typed(text.ToString()));
        }

        Send(events);
    }

    /// <summary>
    /// The key to send for a key event: keys that are not characters (Enter, the arrows, F1), modifiers, and keys
    /// pressed with Ctrl, Alt or Windows. Characters arrive as text instead, so a key is not typed twice.
    /// </summary>
    private static int? ToVirtualKey(KeyEventArgs e)
    {
        if (VirtualKeys.Find(e.Key.ToString()) is not { } key)
        {
            return null;
        }

        var shortcut = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0;
        return VirtualKeys.IsNonCharacter(key) || VirtualKeys.IsModifier(key) || shortcut ? key : null;
    }

    private static TimeSpan Now() => TimeSpan.FromMilliseconds(Environment.TickCount64);

    private void OnLongPressTick()
    {
        if (!_touch.IsActive)
        {
            _longPress.Stop();
            return;
        }

        Send(_touch.Tick(Now(), _map));
    }

    private void Send(IReadOnlyList<BridgeInputEvent> events)
    {
        if (events.Count > 0)
        {
            InputSink?.Send(events);
        }
    }

    /// <summary>
    /// What the on-screen keyboard talks to: no text of its own and no suggestions, so every key and committed word
    /// goes straight to the program on the PC.
    /// </summary>
    private sealed class BridgeImeClient(BridgeFrameView view) : TextInputMethodClient
    {
        public override Visual TextViewVisual => view;

        public override bool SupportsPreedit => false;

        public override bool SupportsSurroundingText => false;

        public override string SurroundingText => string.Empty;

        public override Rect CursorRectangle => new(view.Bounds.Width / 2, view.Bounds.Height - 2, 1, 1);

        public override TextSelection Selection { get; set; }
    }
}
