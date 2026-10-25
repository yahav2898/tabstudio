using Android.App;
using Android.OS;
using Android.Views;
using TabStudio.App.Platform;

namespace TabStudio.Android;

/// <summary>
/// The tablet's mouse on the external display in desktop mode, on every Android version TabStudio runs on.
/// </summary>
/// <remarks>
/// <para>
/// Before Android 16's connected displays, Android keeps the mouse pointer on the device's own screen. So in desktop
/// mode, when the mouse moves on the tablet, TabStudio asks Android for pointer capture (Android 8 and later): the
/// system's pointer disappears and the mouse's raw movements, buttons and wheel come to TabStudio, which moves its own
/// pointer on the display (<see cref="DesktopCursor"/>) and gives the IDE there the mouse's events at that point.
/// Touching the tablet's screen gives the mouse back to the tablet; moving it again takes it back. Android ends the
/// capture itself when TabStudio loses the focus, and desktop mode ends it when it ends.
/// </para>
/// <para>
/// Where Android moves its pointer onto the display itself, the mouse's events reach the IDE there directly, and
/// TabStudio only passes on hovering as moves, which Avalonia's view does not read from hover events.
/// </para>
/// </remarks>
internal sealed partial class AndroidDesktopMouse : Java.Lang.Object, View.IOnCapturedPointerListener, View.IOnHoverListener
{
    /// <summary>How long Android may take to give TabStudio the mouse it asked for, in milliseconds.</summary>
    private const long CaptureAnswerMilliseconds = 1000;

    private readonly Func<Activity?> _activity;
    private readonly DesktopCursor _cursor;
    private View? _display;
    private View? _capturing;
    private bool _forwarding;

    // When TabStudio last asked for the mouse without getting it yet (uptime milliseconds, 0 for no question), and
    // whether Android refused it, so the mouse stays with the tablet rather than doing nothing anywhere.
    private long _requestedAt;
    private bool _refused;

    /// <summary>Creates the mouse for the activity's window and the pointer it moves.</summary>
    /// <param name="activity">The activity whose window captures the mouse, while there is one.</param>
    /// <param name="cursor">The pointer drawn on the display.</param>
    public AndroidDesktopMouse(Func<Activity?> activity, DesktopCursor cursor)
    {
        _activity = activity;
        _cursor = cursor;
    }

    /// <summary>Whether TabStudio has the mouse: its events come to the display's pointer.</summary>
    public bool HasCapture => _capturing?.HasPointerCapture == true;

    /// <summary>The view on the display whose IDE gets the mouse's events, while desktop mode shows one.</summary>
    internal View? Display => _display;

    /// <summary>Starts giving the mouse to the IDE on the display.</summary>
    /// <param name="display">The display's view.</param>
    /// <param name="pixelWidth">The display's width, in pixels.</param>
    /// <param name="pixelHeight">The display's height, in pixels.</param>
    /// <param name="scale">How many pixels one device-independent unit of the view takes.</param>
    public void Attach(View display, int pixelWidth, int pixelHeight, double scale)
    {
        _display = display;
        _refused = false;
        display.SetOnHoverListener(this);
        _cursor.Reset(pixelWidth, pixelHeight, scale);
    }

    /// <summary>Gives the mouse back to the tablet: desktop mode ended, or the display went away.</summary>
    public void Detach()
    {
        _display?.SetOnHoverListener(null);
        _display = null;
        _touchpad = null;
        Release();
        _cursor.Hide();
    }

    /// <summary>
    /// Sees an event of the tablet's window before its views do: a mouse moving on the tablet is captured for the
    /// display, and a finger on the tablet's screen gives the mouse back to the tablet.
    /// </summary>
    /// <param name="e">The event.</param>
    /// <returns>Whether the event was the mouse's, now the display's, so the tablet's views do not get it.</returns>
    public bool OnTabletEvent(MotionEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (_display is null)
        {
            return false;
        }

        if (e.IsFromSource(InputSourceType.Mouse))
        {
            return HasCapture || Capture();
        }

        if (e.ActionMasked == MotionEventActions.Down && e.IsFromSource(InputSourceType.Touchscreen))
        {
            Release();
        }

        return false;
    }

    /// <summary>
    /// Hears that the tablet's window got or lost the focus. Android ends pointer capture when the window loses the
    /// focus, so the display's pointer hides until the mouse moves on the tablet again, and with the focus back,
    /// TabStudio may ask for the mouse again.
    /// </summary>
    /// <remarks>
    /// Android's own callback for capture changes is a default method of <c>Window.Callback</c>, which .NET for
    /// Android does not let an activity override; the pointer shows instead when the first captured event arrives.
    /// </remarks>
    /// <param name="hasFocus">Whether the window has the focus.</param>
    public void OnWindowFocusChanged(bool hasFocus)
    {
        _requestedAt = 0;
        if (hasFocus)
        {
            _refused = false;
        }
        else
        {
            _cursor.Hide();
        }
    }

    /// <summary>A captured mouse or touchpad event: it moves the display's pointer, and the IDE there gets it.</summary>
    /// <param name="view">The tablet's view that captured the mouse.</param>
    /// <param name="e">The event, with relative movements for a mouse.</param>
    /// <returns>Whether it was used.</returns>
    public bool OnCapturedPointer(View? view, MotionEvent? e)
    {
        if (e is null)
        {
            return false;
        }

        if (_display is not { } display)
        {
            view?.ReleasePointerCapture();
            return false;
        }

        // TabStudio has the mouse: its pointer shows on the display.
        _requestedAt = 0;
        _cursor.Show();
        if (e.IsFromSource(InputSourceType.MouseRelative))
        {
            MoveAndForward(display, e);
            return true;
        }

        return e.IsFromSource(InputSourceType.Touchpad) && OnTouchpad(display, e);
    }

    /// <summary>
    /// The system's own pointer hovers over the display, where Android moves it there: the IDE gets the movement as
    /// a move, and TabStudio's pointer stays hidden but follows, for when TabStudio has the mouse again.
    /// </summary>
    /// <param name="v">The display's view.</param>
    /// <param name="e">The hover event.</param>
    /// <returns><see langword="false"/>: the view handles hovering as usual too.</returns>
    public bool OnHover(View? v, MotionEvent? e)
    {
        if (_forwarding || v is null || e is null || !e.IsFromSource(InputSourceType.Mouse) || HasCapture)
        {
            return false;
        }

        _cursor.MoveTo(e.GetX(), e.GetY());
        if (e.ActionMasked is MotionEventActions.HoverMove or MotionEventActions.HoverEnter && MotionEvent.ObtainNoHistory(e) is { } move)
        {
            using (move)
            {
                move.Action = MotionEventActions.Move;
                Forward(v, move);
            }
        }

        return false;
    }

    /// <summary>Moves the pointer by a relative mouse event's movement, and gives the IDE the event at the pointer.</summary>
    private void MoveAndForward(View display, MotionEvent e)
    {
        // Each sample of a batched event is a movement of its own.
        var dx = 0.0;
        var dy = 0.0;
        for (var sample = 0; sample < e.HistorySize; sample++)
        {
            dx += e.GetHistoricalX(sample);
            dy += e.GetHistoricalY(sample);
        }

        var at = _cursor.MoveBy(dx + e.GetX(), dy + e.GetY());
        if (MotionEvent.ObtainNoHistory(e) is not { } forwarded)
        {
            return;
        }

        using (forwarded)
        {
            // The same buttons, wheel and action button, as an ordinary mouse event at the pointer.
            forwarded.SetSource(InputSourceType.Mouse);
            forwarded.SetLocation((float)at.X, (float)at.Y);
            Forward(display, forwarded);
        }
    }

    /// <summary>Gives the display's view a mouse event made here, at the pointer.</summary>
    private void Send(View display, MotionEventActions action, MotionEventButtonState buttons, float verticalScroll = 0)
    {
        var at = _cursor.PixelPosition;
        var pointer = new MotionEvent.PointerProperties { Id = 0, ToolType = MotionEventToolType.Mouse };
        var position = new MotionEvent.PointerCoords { X = (float)at.X, Y = (float)at.Y, Pressure = 1, Size = 1 };
        if (verticalScroll != 0)
        {
            position.SetAxisValue(Axis.Vscroll, verticalScroll);
        }

        var now = SystemClock.UptimeMillis();
        using var motion = MotionEvent.Obtain(now, now, action, 1, [pointer], [position], 0, buttons, 1, 1, 0, 0, InputSourceType.Mouse, 0);
        if (motion is not null)
        {
            Forward(display, motion);
        }
    }

    /// <summary>
    /// Gives the display's view a mouse event as Android gives a window one: Avalonia's view reads moves, buttons
    /// and the wheel from generic pointer events.
    /// </summary>
    private void Forward(View display, MotionEvent e)
    {
        _forwarding = true;
        try
        {
            display.DispatchGenericMotionEvent(e);
        }
        finally
        {
            _forwarding = false;
        }
    }

    /// <summary>Asks Android for the mouse, for the focused view of the tablet's window, which gets its events.</summary>
    /// <returns>
    /// Whether the mouse is about to be TabStudio's, so the tablet's views do not get its event; <see langword="false"/>
    /// when the window has no focus, or Android did not give TabStudio the mouse it asked for: it stays with the tablet.
    /// </returns>
    private bool Capture()
    {
        var now = SystemClock.UptimeMillis();
        if (_requestedAt != 0)
        {
            // Android answers in a moment; a mouse still on the tablet a while after asking was refused to TabStudio.
            if (now - _requestedAt < CaptureAnswerMilliseconds)
            {
                return true;
            }

            _requestedAt = 0;
            _refused = true;
        }

        if (_refused || _activity() is not { HasWindowFocus: true } activity || FocusedView(activity) is not { } view)
        {
            return false;
        }

        if (!ReferenceEquals(_capturing, view))
        {
            _capturing?.SetOnCapturedPointerListener(null);
            view.SetOnCapturedPointerListener(this);
            _capturing = view;
        }

        view.RequestPointerCapture();
        _requestedAt = now;
        return true;
    }

    private void Release()
    {
        _requestedAt = 0;
        if (_capturing is { HasPointerCapture: true } view)
        {
            view.ReleasePointerCapture();
        }
    }

    /// <summary>The view with the focus in the activity's window, which Android gives captured events to.</summary>
    private static View? FocusedView(Activity activity)
    {
        if (activity.CurrentFocus is { } focused)
        {
            return focused;
        }

        return activity.FindViewById(global::Android.Resource.Id.Content) is ViewGroup { ChildCount: > 0 } content
            && content.GetChildAt(0) is { } root
            && root.RequestFocus()
            ? root
            : null;
    }
}
