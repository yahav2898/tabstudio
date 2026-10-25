namespace TabStudio.App.Platform;

/// <summary>What a touchpad gesture asks of the display's pointer.</summary>
public enum TouchpadAction
{
    /// <summary>Nothing yet, such as a scroll too small to turn the wheel.</summary>
    None,

    /// <summary>The pointer moved.</summary>
    Move,

    /// <summary>The pointer moved with the pad's button held.</summary>
    Drag,

    /// <summary>The wheel turned by <see cref="DesktopTouchpad.ScrollNotches"/>.</summary>
    Scroll,

    /// <summary>A tap: the left button pressed and released.</summary>
    Click,

    /// <summary>The pad's button went down.</summary>
    Press,

    /// <summary>The pad's button came up.</summary>
    Release,
}

/// <summary>
/// A touchpad, such as a keyboard cover's, as the mouse of the external display in desktop mode. Android gives a
/// captured touchpad as fingers on a pad rather than as a mouse, so this makes a mouse of them: one finger moves the
/// pointer, two fingers scroll the way they move, as Android scrolls with a touchpad, and a tap or a press of the pad
/// clicks.
/// </summary>
/// <remarks>Positions and sizes are in the pad's own units, times in milliseconds.</remarks>
/// <param name="cursor">The display's pointer, which one finger moves.</param>
public sealed class DesktopTouchpad(DesktopCursor cursor)
{
    /// <summary>How much of the display's width one stroke across the whole pad moves the pointer.</summary>
    public const double PadToDisplay = 0.8;

    /// <summary>How many notches of a mouse wheel two fingers scroll across the whole pad's height.</summary>
    public const double PadScrollNotches = 12;

    /// <summary>The longest touch that is a tap, a click, in milliseconds.</summary>
    public const long TapMilliseconds = 200;

    /// <summary>The farthest a tap's finger moves, as a share of the pad's width.</summary>
    public const double TapTravel = 0.02;

    /// <summary>The least scroll sent, in notches; less is kept for the next stroke.</summary>
    public const double ScrollStep = 0.25;

    private double _lastX;
    private double _lastY;
    private long _touchStart;
    private double _travel;
    private double _scroll;

    /// <summary>Whether the pad's button is held: moving drags.</summary>
    public bool IsButtonDown { get; private set; }

    /// <summary>How far the wheel turned for the last <see cref="TouchpadAction.Scroll"/>, in notches, positive away from the user.</summary>
    public double ScrollNotches { get; private set; }

    /// <summary>The first finger touched the pad.</summary>
    /// <param name="time">When.</param>
    /// <param name="x">Where, across the pad.</param>
    /// <param name="y">Where, down the pad.</param>
    public void Touch(long time, double x, double y)
    {
        _touchStart = time;
        _travel = 0;
        _scroll = 0;
        Remember(x, y);
    }

    /// <summary>A finger more or less: strokes go on from where the fingers are now, and the touch is no tap.</summary>
    /// <param name="x">Where the fingers that stay are, on average, across the pad.</param>
    /// <param name="y">Where they are, down the pad.</param>
    public void FingersChanged(double x, double y)
    {
        _scroll = 0;
        _travel = double.MaxValue;
        Remember(x, y);
    }

    /// <summary>The fingers slid: one moves the pointer, more scroll.</summary>
    /// <param name="x">Where the fingers are now, on average, across the pad.</param>
    /// <param name="y">Where they are, down the pad.</param>
    /// <param name="fingers">How many touch the pad.</param>
    /// <param name="padWidth">The pad's width.</param>
    /// <param name="padHeight">The pad's height.</param>
    /// <param name="displayWidth">The display's width, in pixels.</param>
    /// <returns><see cref="TouchpadAction.Move"/>, <see cref="TouchpadAction.Drag"/>, <see cref="TouchpadAction.Scroll"/> or none.</returns>
    public TouchpadAction Slide(double x, double y, int fingers, double padWidth, double padHeight, double displayWidth)
    {
        padWidth = Math.Max(1, padWidth);
        padHeight = Math.Max(1, padHeight);
        var dx = x - _lastX;
        var dy = y - _lastY;
        Remember(x, y);
        _travel += Math.Sqrt((dx * dx) + (dy * dy)) / padWidth;
        if (fingers <= 1)
        {
            var across = PadToDisplay * displayWidth / padWidth;
            var at = cursor.PixelPosition;
            cursor.MoveTo(at.X + (dx * across), at.Y + (dy * across));
            return IsButtonDown ? TouchpadAction.Drag : TouchpadAction.Move;
        }

        // The content follows the fingers: fingers sliding down show what is above, as the wheel turned away does.
        _scroll += dy / padHeight * PadScrollNotches;
        if (Math.Abs(_scroll) < ScrollStep)
        {
            return TouchpadAction.None;
        }

        ScrollNotches = _scroll;
        _scroll = 0;
        return TouchpadAction.Scroll;
    }

    /// <summary>The last finger left the pad: a short touch that hardly moved is a tap, a click.</summary>
    /// <param name="time">When.</param>
    /// <returns><see cref="TouchpadAction.Click"/> or none.</returns>
    public TouchpadAction Lift(long time) =>
        !IsButtonDown && time - _touchStart <= TapMilliseconds && _travel <= TapTravel ? TouchpadAction.Click : TouchpadAction.None;

    /// <summary>The pad's button went down.</summary>
    /// <returns><see cref="TouchpadAction.Press"/>.</returns>
    public TouchpadAction PressButton()
    {
        IsButtonDown = true;
        return TouchpadAction.Press;
    }

    /// <summary>The pad's button came up.</summary>
    /// <returns><see cref="TouchpadAction.Release"/>.</returns>
    public TouchpadAction ReleaseButton()
    {
        IsButtonDown = false;
        return TouchpadAction.Release;
    }

    private void Remember(double x, double y)
    {
        _lastX = x;
        _lastY = y;
    }
}
