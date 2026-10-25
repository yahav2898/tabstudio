using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using TabStudio.Uno.Hosting;

namespace TabStudio.App.Previews;

/// <summary>
/// The app preview of an Uno Platform or WinUI 3 app: the frames Uno's Skia renderer draws for the app's main window,
/// shown at the pane's size and pixel density, with taps, the mouse, the pen, the keyboard and the on-screen keyboard
/// going to the app.
/// </summary>
/// <remarks>
/// Uno draws on threads of its own, so a frame is copied into this control's bitmap on the UI thread when Uno says
/// one is ready, at most once per frame TabStudio draws. The pane's size goes to Uno as the window's size.
/// </remarks>
public sealed class UnoPreviewControl : Control
{
    private readonly UnoRuntime _runtime;
    private readonly UnoTextInputClient _textInput;
    private WriteableBitmap? _frame;
    private int _framePending;
    private bool _editingText;

    /// <summary>Creates the preview of the apps a runtime runs.</summary>
    /// <param name="runtime">Uno Platform, running in TabStudio.</param>
    public UnoPreviewControl(UnoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
        _textInput = new UnoTextInputClient(this);
        Focusable = true;
        ClipToBounds = true;
        AddHandler(TextInputMethodClientRequestedEvent, OnTextInputMethodClientRequested);
    }

    /// <summary>The app whose input this preview forwards, while it runs.</summary>
    public UnoApp? App { get; set; }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        if (_frame is { } frame)
        {
            context.DrawImage(frame, new Rect(frame.Size), new Rect(Bounds.Size));
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _runtime.FrameReady += OnFrameReady;
        ActualThemeVariantChanged += OnThemeChanged;
        OnThemeChanged(this, EventArgs.Empty);
        SendSize(Bounds.Size);
        UpdateFrame();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _runtime.FrameReady -= OnFrameReady;
        ActualThemeVariantChanged -= OnThemeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        SendSize(e.NewSize);
    }

    /// <inheritdoc/>
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Send(UnoPointerAction.Entered, e);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Send(UnoPointerAction.Exited, e);
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Send(UnoPointerAction.Moved, e);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        e.Pointer.Capture(this);
        Send(UnoPointerAction.Pressed, e);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Send(UnoPointerAction.Released, e);
        e.Pointer.Capture(null);
        e.Handled = true;
        _ = FollowTextFocusAsync();
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _runtime.SendPointer(new UnoPointerEvent { Action = UnoPointerAction.Cancelled, PointerId = (uint)e.Pointer.Id, Device = DeviceOf(e.Pointer.Type) });
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var point = e.GetCurrentPoint(this);
        foreach (var (delta, horizontal) in new[] { (e.Delta.Y, false), (e.Delta.X, true) })
        {
            if (delta != 0)
            {
                _runtime.SendPointer(PointerOf(UnoPointerAction.Wheel, e, point) with { WheelDelta = (int)Math.Round(delta * 120), IsHorizontalWheel = horizontal });
            }
        }

        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (UnoVirtualKeys.From(e.Key) is { } key)
        {
            _runtime.SendKey(true, key, ModifiersOf(e.KeyModifiers), null);
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (UnoVirtualKeys.From(e.Key) is { } key)
        {
            _runtime.SendKey(false, key, ModifiersOf(e.KeyModifiers), null);
            e.Handled = true;
            _ = FollowTextFocusAsync();
        }
    }

    /// <inheritdoc/>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        // Keys such as Enter and Backspace arrive as key presses; the text is what the key or the keyboard typed.
        if (e.Text is { Length: > 0 } text && !text.All(char.IsControl))
        {
            _runtime.SendText(new string([.. text.Where(c => !char.IsControl(c))]), UnoKeyModifiers.None);
            e.Handled = true;
        }
    }

    private static UnoPointerDevice DeviceOf(PointerType type) => type switch
    {
        PointerType.Touch => UnoPointerDevice.Touch,
        PointerType.Pen => UnoPointerDevice.Pen,
        _ => UnoPointerDevice.Mouse,
    };

    private static UnoKeyModifiers ModifiersOf(KeyModifiers modifiers) =>
        (modifiers.HasFlag(KeyModifiers.Control) ? UnoKeyModifiers.Control : UnoKeyModifiers.None)
        | (modifiers.HasFlag(KeyModifiers.Alt) ? UnoKeyModifiers.Menu : UnoKeyModifiers.None)
        | (modifiers.HasFlag(KeyModifiers.Shift) ? UnoKeyModifiers.Shift : UnoKeyModifiers.None)
        | (modifiers.HasFlag(KeyModifiers.Meta) ? UnoKeyModifiers.Windows : UnoKeyModifiers.None);

    private UnoPointerEvent PointerOf(UnoPointerAction action, PointerEventArgs e, PointerPoint point) => new()
    {
        Action = action,
        PointerId = (uint)e.Pointer.Id,
        Device = DeviceOf(e.Pointer.Type),
        X = point.Position.X,
        Y = point.Position.Y,
        IsLeftPressed = point.Properties.IsLeftButtonPressed,
        IsRightPressed = point.Properties.IsRightButtonPressed,
        IsMiddlePressed = point.Properties.IsMiddleButtonPressed,
        Modifiers = ModifiersOf(e.KeyModifiers),

        // Avalonia counts milliseconds; WinUI counts microseconds.
        Timestamp = e.Timestamp * 1000,
    };

    private void Send(UnoPointerAction action, PointerEventArgs e) => _runtime.SendPointer(PointerOf(action, e, e.GetCurrentPoint(this)));

    private void SendSize(Size size)
    {
        if (size.Width >= 1 && size.Height >= 1)
        {
            _runtime.Resize(size.Width, size.Height, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e) => _runtime.SetTheme(ActualThemeVariant != ThemeVariant.Light);

    /// <summary>Called on Uno's renderer thread: copies the frame on the UI thread, once per frame TabStudio draws.</summary>
    private void OnFrameReady(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _framePending, 1) == 0)
        {
            Dispatcher.UIThread.Post(UpdateFrame, DispatcherPriority.Render);
        }
    }

    private void UpdateFrame()
    {
        Volatile.Write(ref _framePending, 0);
        var (width, height) = _runtime.FrameSize;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (_frame is not { } frame || frame.PixelSize.Width != width || frame.PixelSize.Height != height || Math.Abs(frame.Dpi.X - (96 * scale)) > 0.01)
        {
            _frame?.Dispose();
            frame = _frame = new WriteableBitmap(new PixelSize(width, height), new Vector(96 * scale, 96 * scale), PixelFormat.Bgra8888, AlphaFormat.Premul);
        }

        using (var buffer = frame.Lock())
        {
            if (!_runtime.CopyFrame(buffer.Address, width, height, buffer.RowBytes))
            {
                return;
            }
        }

        InvalidateVisual();
    }

    /// <summary>
    /// After a tap or a key, shows the on-screen keyboard when the app's focus moved into a text box, and hides it when
    /// the focus left one: Avalonia asks the focused control for its text input client again when it is focused anew.
    /// </summary>
    private async Task FollowTextFocusAsync()
    {
        if (App is not { IsClosed: false } app)
        {
            return;
        }

        bool editing;
        try
        {
            editing = await app.IsEditingTextAsync();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (editing == _editingText || !IsFocused)
        {
            return;
        }

        _editingText = editing;
        if (TopLevel.GetTopLevel(this)?.FocusManager is { } focus)
        {
            focus.ClearFocus();
            Focus();
        }

        if (editing)
        {
            _textInput.ShowInputPanel();
        }
    }

    private void OnTextInputMethodClientRequested(object? sender, TextInputMethodClientRequestedEventArgs e)
    {
        if (_editingText)
        {
            e.Client = _textInput;
            e.Handled = true;
        }
    }

    /// <summary>
    /// What the on-screen keyboard types into: the app's text box, whose text the app keeps, so it offers no
    /// surrounding text or composition and the keyboard commits each character as text.
    /// </summary>
    private sealed class UnoTextInputClient(Visual view) : TextInputMethodClient
    {
        public override Visual TextViewVisual => view;

        public override bool SupportsPreedit => false;

        public override bool SupportsSurroundingText => false;

        public override string SurroundingText => string.Empty;

        public override Rect CursorRectangle => default;

        public override TextSelection Selection { get; set; }
    }
}
