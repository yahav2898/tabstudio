using System.Windows.Forms;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using TabStudio.Forms.Runtime;
using AvaloniaCursor = Avalonia.Input.Cursor;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace TabStudio.Forms.Hosting;

/// <summary>
/// The pointer on the desktop: presses in a client area go to the app as mouse input (a finger as the left button),
/// the title bar drags its window, the edges and corners of a sizable window resize it, and the caption buttons
/// minimize, maximize and close. A long press asks for a context menu.
/// </summary>
internal sealed partial class FormsDesktop
{
    /// <summary>How long a finger is held still for a long press.</summary>
    private static readonly TimeSpan LongPress = TimeSpan.FromMilliseconds(550);

    /// <summary>How far a finger may move and still be held still, in the app's pixels.</summary>
    private const int LongPressSlop = 8;

    private readonly DispatcherTimer _longPressTimer = new() { Interval = LongPress };
    private readonly DispatcherTimer _flashTimer = new() { Interval = TimeSpan.FromMilliseconds(90) };
    private Gesture? _gesture;
    private int _hoveredWindow;
    private int _flashWindow;
    private int _flashesLeft;
    private volatile bool _movePending;
    private volatile bool _resizePending;

    /// <summary>What a press on the desktop is doing until it is released.</summary>
    private enum GestureKind
    {
        /// <summary>Mouse input for the app's controls in a window's client area or its drop-downs.</summary>
        Client,

        /// <summary>Dragging a window by its title bar.</summary>
        Move,

        /// <summary>Dragging an edge or corner of a window.</summary>
        Resize,

        /// <summary>Pressing a caption button.</summary>
        Caption,

        /// <summary>Pressing a minimized window's button.</summary>
        Minimized,
    }

    private void InitializeInput()
    {
        _longPressTimer.Tick += (_, _) => OnLongPress();
        _flashTimer.Tick += (_, _) => OnFlash();
        _canvas.PointerPressed += OnPointerPressed;
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += OnPointerReleased;
        _canvas.PointerCaptureLost += OnPointerCaptureLost;
        _canvas.PointerWheelChanged += OnPointerWheelChanged;
        _canvas.PointerExited += (_, _) => LeaveHovered();
    }

    /// <summary>The windows changed: the hover and a gesture on a window that closed are forgotten.</summary>
    private void OnWindowsChanged()
    {
        if (_gesture is { } gesture && !_views.ContainsKey(gesture.WindowId))
        {
            _gesture = null;
            _longPressTimer.Stop();
        }

        if (_hoveredWindow != 0 && !_views.ContainsKey(_hoveredWindow))
        {
            _hoveredWindow = 0;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsFromEditor(e.Source) || _gesture is not null || Runtime is not { } runtime)
        {
            return;
        }

        e.Handled = true;
        var point = DesktopPoint(e);
        var touch = e.Pointer.Type != PointerType.Mouse;
        var properties = e.GetCurrentPoint(_canvas).Properties;
        var button = properties.PointerUpdateKind switch
        {
            PointerUpdateKind.RightButtonPressed => MouseButtons.Right,
            PointerUpdateKind.MiddleButtonPressed => MouseButtons.Middle,
            PointerUpdateKind.XButton1Pressed => MouseButtons.XButton1,
            PointerUpdateKind.XButton2Pressed => MouseButtons.XButton2,
            _ => MouseButtons.Left,
        };
        var clicks = Math.Clamp(e.ClickCount, 1, 2);

        // The desktop takes the keyboard; the native text box keeps it if it has it, and gets it once a field has focus.
        if (!IsKeyboardFocusWithin)
        {
            Focus(NavigationMethod.Pointer);
        }

        // Minimized windows' buttons are above everything.
        foreach (var (minimized, bounds) in _overlay.Minimized)
        {
            if (bounds.Contains(point))
            {
                var part = MinimizedPart(minimized, bounds, point);
                Begin(e, new Gesture(GestureKind.Minimized, minimized.Id, part, point, touch, button));
                _overlay.SetHot(minimized.Id, part, ButtonState.Pressed);
                return;
            }
        }

        // While a menu or drop-down is open, a press inside it goes to it, and any other press only closes it.
        if (ViewsFromTop().FirstOrDefault(v => v.Frame.Popups.Count > 0) is { } withPopups)
        {
            DeliverPress(e, withPopups.Frame, point, button, clicks, touch);
            return;
        }

        foreach (var view in ViewsFromTop())
        {
            var frame = view.Frame;
            if (frame.IsMinimized)
            {
                continue;
            }

            var part = ChromeGeometry.HitTest(frame, point, touch);
            if (part == WindowPart.None)
            {
                continue;
            }

            if (frame.IsBlocked)
            {
                // A modal dialog keeps the input: the press brings it to the front, and its frame flashes.
                PostTo(frame.Id, window => runtime.Activate(window));
                FlashModal();
                return;
            }

            if (!frame.IsActive)
            {
                PostTo(frame.Id, window => runtime.Activate(window));
            }

            switch (part)
            {
                case WindowPart.Client:
                    DeliverPress(e, frame, point, button, clicks, touch);
                    return;
                case WindowPart.Title when clicks == 2 && frame.Chrome.MaximizeBox && frame.Chrome.ControlBox:
                    PostTo(frame.Id, window => window.ToggleMaximize());
                    return;
                case WindowPart.Title:
                    Begin(e, new Gesture(GestureKind.Move, frame.Id, part, point, touch, button));
                    return;
                case WindowPart.Minimize or WindowPart.Maximize or WindowPart.Close:
                    if (ChromeGeometry.IsEnabled(frame, part))
                    {
                        Begin(e, new Gesture(GestureKind.Caption, frame.Id, part, point, touch, button));
                        view.SetHot(part, ButtonState.Pressed);
                    }

                    return;
                default:
                    Begin(e, new Gesture(GestureKind.Resize, frame.Id, part, point, touch, button) { StartClient = frame.Client });
                    return;
            }
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = DesktopPoint(e);
        if (_gesture is not { } gesture)
        {
            if (!IsFromEditor(e.Source) && e.Pointer.Type == PointerType.Mouse)
            {
                Hover(point);
            }

            return;
        }

        if (e.Pointer != gesture.Pointer)
        {
            return;
        }

        if (Math.Abs(point.X - gesture.Start.X) > LongPressSlop || Math.Abs(point.Y - gesture.Start.Y) > LongPressSlop)
        {
            _longPressTimer.Stop();
        }

        gesture.Last = point;
        switch (gesture.Kind)
        {
            case GestureKind.Client when !gesture.LongPressed:
                PostTo(gesture.WindowId, window => window.PointerMoved(ClientPoint(window, point), gesture.Touch));
                break;
            case GestureKind.Move:
                QueueMove(gesture);
                break;
            case GestureKind.Resize:
                QueueResize(gesture);
                break;
            case GestureKind.Caption when _views.TryGetValue(gesture.WindowId, out var view):
                view.SetHot(gesture.Part, ChromeGeometry.Button(view.Frame, gesture.Part).Contains(point) ? ButtonState.Pressed : ButtonState.Hot);
                break;
            case GestureKind.Minimized:
                var inside = _overlay.Minimized.Any(m => m.Frame.Id == gesture.WindowId && MinimizedPartBounds(m.Bounds, gesture.Part).Contains(point));
                _overlay.SetHot(gesture.WindowId, gesture.Part, inside ? ButtonState.Pressed : ButtonState.Hot);
                break;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_gesture is not { } gesture || e.Pointer != gesture.Pointer)
        {
            return;
        }

        e.Handled = true;
        var point = DesktopPoint(e);
        End(e.Pointer);
        switch (gesture.Kind)
        {
            case GestureKind.Client:
                PostTo(gesture.WindowId, window => window.PointerReleased(ClientPoint(window, point), gesture.Button, gesture.Touch));
                break;
            case GestureKind.Caption when _views.TryGetValue(gesture.WindowId, out var view):
                view.SetHot(WindowPart.None, ButtonState.Normal);
                if (ChromeGeometry.Button(view.Frame, gesture.Part).Contains(point))
                {
                    PostTo(gesture.WindowId, gesture.Part switch
                    {
                        WindowPart.Minimize => window => window.Minimize(),
                        WindowPart.Maximize => window => window.ToggleMaximize(),
                        _ => window => window.CloseRequested(),
                    });
                }

                break;
            case GestureKind.Minimized:
                _overlay.SetHot(0, WindowPart.None, ButtonState.Normal);
                var released = _overlay.Minimized.FirstOrDefault(m => m.Frame.Id == gesture.WindowId);
                if (released.Frame is not null && MinimizedPartBounds(released.Bounds, gesture.Part).Contains(point))
                {
                    PostTo(gesture.WindowId, gesture.Part == WindowPart.Close ? window => window.CloseRequested() : window => window.Restore());
                }

                break;
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_gesture is { } gesture && e.Pointer == gesture.Pointer)
        {
            End(null);
            if (gesture.Kind == GestureKind.Client)
            {
                var point = gesture.Last;
                PostTo(gesture.WindowId, window => window.PointerReleased(ClientPoint(window, point), gesture.Button, gesture.Touch));
            }

            if (_views.TryGetValue(gesture.WindowId, out var view))
            {
                view.SetHot(WindowPart.None, ButtonState.Normal);
            }

            _overlay.SetHot(0, WindowPart.None, ButtonState.Normal);
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (IsFromEditor(e.Source))
        {
            return;
        }

        var point = DesktopPoint(e);
        var target = ViewsFromTop().FirstOrDefault(v => v.Frame.Popups.Count > 0)
            ?? ViewsFromTop().FirstOrDefault(v => !v.Frame.IsMinimized && !v.Frame.IsBlocked && v.Frame.Client.Contains(point));
        if (target is null)
        {
            return;
        }

        e.Handled = true;
        var delta = (int)Math.Round(e.Delta.Y * 120);
        if (delta != 0)
        {
            PostTo(target.Frame.Id, window => window.PointerWheel(ClientPoint(window, point), delta));
        }
    }

    /// <summary>Starts a gesture: the desktop gets the pointer until it is released.</summary>
    private void Begin(PointerPressedEventArgs e, Gesture gesture)
    {
        gesture.Pointer = e.Pointer;
        _gesture = gesture;
        e.Pointer.Capture(_canvas);
        if (gesture.Touch && gesture.Kind == GestureKind.Client)
        {
            _longPressTimer.Stop();
            _longPressTimer.Start();
        }
    }

    /// <summary>Ends the gesture and lets go of the pointer.</summary>
    private void End(IPointer? pointer)
    {
        _gesture = null;
        _longPressTimer.Stop();
        if (pointer?.Captured == _canvas)
        {
            pointer.Capture(null);
        }
    }

    /// <summary>A press in a client area, or in a window's menus and drop-downs, goes to the app.</summary>
    private void DeliverPress(PointerPressedEventArgs e, WindowFrame frame, DrawingPoint point, MouseButtons button, int clicks, bool touch)
    {
        Begin(e, new Gesture(GestureKind.Client, frame.Id, WindowPart.Client, point, touch, button));
        PostTo(frame.Id, window => window.PointerPressed(ClientPoint(window, point), button, clicks, touch));
    }

    /// <summary>A finger held still asks the control under it for its context menu.</summary>
    private void OnLongPress()
    {
        _longPressTimer.Stop();
        if (_gesture is { Kind: GestureKind.Client, LongPressed: false } gesture)
        {
            gesture.LongPressed = true;
            var point = gesture.Start;
            PostTo(gesture.WindowId, window => window.ContextRequested(ClientPoint(window, point)));
        }
    }

    /// <summary>Moves the window being dragged, once per frame of the app however fast the pointer moves.</summary>
    private void QueueMove(Gesture gesture)
    {
        if (_movePending)
        {
            return;
        }

        _movePending = true;
        PostTo(gesture.WindowId, window =>
        {
            _movePending = false;
            var target = gesture.Last;
            var dx = target.X - gesture.Moved.X;
            var dy = target.Y - gesture.Moved.Y;
            gesture.Moved = target;
            if (dx != 0 || dy != 0)
            {
                window.MovedBy(dx, dy);
            }
        });
    }

    /// <summary>Resizes the window whose edge is dragged, once per frame of the app.</summary>
    private void QueueResize(Gesture gesture)
    {
        if (_resizePending)
        {
            return;
        }

        _resizePending = true;
        PostTo(gesture.WindowId, window =>
        {
            _resizePending = false;
            var (left, top, right, bottom) = ChromeGeometry.Edges(gesture.Part);
            var start = gesture.StartClient;
            var dx = gesture.Last.X - gesture.Start.X;
            var dy = gesture.Last.Y - gesture.Start.Y;
            var client = DrawingRectangle.FromLTRB(
                start.Left + (left ? dx : 0),
                start.Top + (top ? dy : 0),
                start.Right + (right ? dx : 0),
                start.Bottom + (bottom ? dy : 0));
            window.ResizedTo(client, keepRight: left, keepBottom: top);
        });
    }

    /// <summary>The mouse moved without a button: hover in the app, on caption buttons, and the resize cursors.</summary>
    private void Hover(DrawingPoint point)
    {
        AvaloniaCursor? cursor = null;
        var hoveredWindow = 0;
        var hotView = (WindowView?)null;
        var hotPart = WindowPart.None;
        var minimized = _overlay.Minimized.FirstOrDefault(m => m.Bounds.Contains(point));
        if (minimized.Frame is not null)
        {
            _overlay.SetHot(minimized.Frame.Id, MinimizedPart(minimized.Frame, minimized.Bounds, point), ButtonState.Hot);
        }
        else
        {
            _overlay.SetHot(0, WindowPart.None, ButtonState.Normal);
            if (ViewsFromTop().FirstOrDefault(v => v.Frame.Popups.Count > 0) is { } withPopups)
            {
                hoveredWindow = withPopups.Frame.Id;
            }
            else
            {
                foreach (var view in ViewsFromTop())
                {
                    if (view.Frame.IsMinimized)
                    {
                        continue;
                    }

                    var part = ChromeGeometry.HitTest(view.Frame, point, touch: false);
                    if (part == WindowPart.None)
                    {
                        continue;
                    }

                    if (part == WindowPart.Client && !view.Frame.IsBlocked)
                    {
                        hoveredWindow = view.Frame.Id;
                    }
                    else if (part is WindowPart.Minimize or WindowPart.Maximize or WindowPart.Close && !view.Frame.IsBlocked)
                    {
                        hotView = view;
                        hotPart = part;
                    }

                    cursor = ResizeCursor(part);
                    break;
                }
            }
        }

        foreach (var view in _views.Values)
        {
            view.SetHot(view == hotView ? hotPart : WindowPart.None, view == hotView ? ButtonState.Hot : ButtonState.Normal);
        }

        if (_hoveredWindow != hoveredWindow)
        {
            LeaveHovered();
            _hoveredWindow = hoveredWindow;
        }

        if (hoveredWindow != 0)
        {
            PostTo(hoveredWindow, window => window.PointerMoved(ClientPoint(window, point), touch: false));
        }

        _canvas.Cursor = cursor;
    }

    /// <summary>Tells the window the mouse was over that it left.</summary>
    private void LeaveHovered()
    {
        if (_hoveredWindow != 0)
        {
            PostTo(_hoveredWindow, window => window.PointerLeft());
            _hoveredWindow = 0;
        }

        if (_gesture is null)
        {
            foreach (var view in _views.Values)
            {
                view.SetHot(WindowPart.None, ButtonState.Normal);
            }

            _overlay.SetHot(0, WindowPart.None, ButtonState.Normal);
        }
    }

    /// <summary>Flashes the frame of the topmost modal dialog, as Windows does when a window it blocks is pressed.</summary>
    private void FlashModal()
    {
        if (ViewsFromTop().FirstOrDefault(v => v.Frame.IsModal && !v.Frame.IsBlocked) is not { } modal)
        {
            return;
        }

        if (_flashWindow != 0 && _views.TryGetValue(_flashWindow, out var previous))
        {
            previous.SetFlashed(false);
        }

        _flashWindow = modal.Frame.Id;
        _flashesLeft = 6;
        _flashTimer.Stop();
        _flashTimer.Start();
        OnFlash();
    }

    private void OnFlash()
    {
        if (!_views.TryGetValue(_flashWindow, out var view) || _flashesLeft <= 0)
        {
            _flashTimer.Stop();
            if (view is not null)
            {
                view.SetFlashed(false);
            }

            return;
        }

        _flashesLeft--;
        view.SetFlashed(_flashesLeft % 2 == 1);
    }

    /// <summary>The cursor over an edge or corner that resizes a window.</summary>
    private static AvaloniaCursor? ResizeCursor(WindowPart part) => part switch
    {
        WindowPart.Left or WindowPart.Right => new AvaloniaCursor(StandardCursorType.SizeWestEast),
        WindowPart.Top or WindowPart.Bottom => new AvaloniaCursor(StandardCursorType.SizeNorthSouth),
        WindowPart.TopLeft => new AvaloniaCursor(StandardCursorType.TopLeftCorner),
        WindowPart.TopRight => new AvaloniaCursor(StandardCursorType.TopRightCorner),
        WindowPart.BottomLeft => new AvaloniaCursor(StandardCursorType.BottomLeftCorner),
        WindowPart.BottomRight => new AvaloniaCursor(StandardCursorType.BottomRightCorner),
        _ => null,
    };

    /// <summary>The part of a minimized window's button at a point: its restore button, its close button, or the rest.</summary>
    private static WindowPart MinimizedPart(WindowFrame frame, DrawingRectangle bounds, DrawingPoint point) =>
        frame.Chrome.ControlBox && ChromePainter.MinimizedButton(bounds, WindowPart.Close).Contains(point) ? WindowPart.Close
        : ChromePainter.MinimizedButton(bounds, WindowPart.Maximize).Contains(point) ? WindowPart.Maximize
        : WindowPart.Title;

    /// <summary>Where a part of a minimized window's button is.</summary>
    private static DrawingRectangle MinimizedPartBounds(DrawingRectangle bounds, WindowPart part) =>
        part == WindowPart.Title ? bounds : ChromePainter.MinimizedButton(bounds, part);

    /// <summary>A point on the desktop in a window's client coordinates, read on the app's thread where the window is now.</summary>
    private static DrawingPoint ClientPoint(FormWindow window, DrawingPoint point)
    {
        var origin = window.Form.ClientOriginOnScreen;
        return new DrawingPoint(point.X - origin.X, point.Y - origin.Y);
    }

    /// <summary>Where the pointer is on the desktop, in the app's pixels.</summary>
    private DrawingPoint DesktopPoint(PointerEventArgs e)
    {
        var position = e.GetPosition(_canvas);
        return new DrawingPoint((int)Math.Floor(position.X), (int)Math.Floor(position.Y));
    }

    /// <summary>A press on the desktop, followed until it is released.</summary>
    private sealed class Gesture(GestureKind kind, int windowId, WindowPart part, DrawingPoint start, bool touch, MouseButtons button)
    {
        public GestureKind Kind { get; } = kind;

        public int WindowId { get; } = windowId;

        public WindowPart Part { get; } = part;

        public DrawingPoint Start { get; } = start;

        public bool Touch { get; } = touch;

        public MouseButtons Button { get; } = button;

        public IPointer? Pointer { get; set; }

        /// <summary>Where the pointer is now.</summary>
        public DrawingPoint Last { get; set; } = start;

        /// <summary>Where the pointer was when the window was last moved, on the app's thread.</summary>
        public DrawingPoint Moved { get; set; } = start;

        /// <summary>The window's client area when an edge was pressed.</summary>
        public DrawingRectangle StartClient { get; init; }

        /// <summary>Whether a long press asked for a context menu, after which the finger does nothing.</summary>
        public bool LongPressed { get; set; }
    }
}
