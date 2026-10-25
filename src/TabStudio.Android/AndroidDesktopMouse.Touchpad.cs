using Android.Views;

namespace TabStudio.Android;

/// <summary>
/// A captured touchpad, such as a keyboard cover's: Android gives it as fingers on a pad, not as a mouse, so
/// TabStudio makes a mouse of it. One finger moves the pointer, two fingers scroll, and a tap or a press of the pad
/// clicks.
/// </summary>
internal sealed partial class AndroidDesktopMouse
{
    /// <summary>How much of the display's width one stroke across the whole pad moves the pointer.</summary>
    private const double PadToDisplay = 0.8;

    /// <summary>How many notches of a mouse wheel two fingers scroll across the whole pad's height.</summary>
    private const double PadScrollNotches = 12;

    /// <summary>The longest touch that is a tap, a click, in milliseconds.</summary>
    private const long TapMilliseconds = 200;

    /// <summary>The farthest a tap's finger moves, as a share of the pad's width.</summary>
    private const double TapTravel = 0.02;

    private float _lastX;
    private float _lastY;
    private long _touchStart;
    private double _travel;
    private double _scroll;
    private bool _buttonDown;

    /// <summary>A captured touchpad event: moves the pointer, scrolls, or clicks.</summary>
    /// <returns>Whether it was used.</returns>
    private bool OnTouchpad(View display, MotionEvent e)
    {
        var width = Range(e, Axis.X);
        var height = Range(e, Axis.Y);
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                _touchStart = e.EventTime;
                _travel = 0;
                Remember(e);
                break;

            case MotionEventActions.PointerDown:
            case MotionEventActions.PointerUp:
                // A finger more or less: the strokes go on from where the fingers are now.
                _scroll = 0;
                _travel = double.MaxValue;
                Remember(e);
                break;

            case MotionEventActions.Move:
                var dx = Average(e, Axis.X) - _lastX;
                var dy = Average(e, Axis.Y) - _lastY;
                Remember(e);
                _travel += Math.Sqrt((dx * dx) + (dy * dy)) / width;
                if (e.PointerCount == 1)
                {
                    var pixels = _cursor.PixelPosition;
                    var across = PadToDisplay * display.Width / width;
                    _cursor.MoveTo(pixels.X + (dx * across), pixels.Y + (dy * across));
                    Send(display, MotionEventActions.Move, _buttonDown ? MotionEventButtonState.Primary : 0);
                }
                else
                {
                    ScrollBy(display, -dy / height * PadScrollNotches);
                }

                break;

            case MotionEventActions.Up:
                if (!_buttonDown && e.EventTime - _touchStart <= TapMilliseconds && _travel <= TapTravel)
                {
                    Click(display);
                }

                break;

            case MotionEventActions.ButtonPress:
                _buttonDown = true;
                Send(display, MotionEventActions.ButtonPress, MotionEventButtonState.Primary);
                break;

            case MotionEventActions.ButtonRelease:
                _buttonDown = false;
                Send(display, MotionEventActions.ButtonRelease, 0);
                break;

            default:
                return false;
        }

        return true;
    }

    private void Click(View display)
    {
        Send(display, MotionEventActions.ButtonPress, MotionEventButtonState.Primary);
        Send(display, MotionEventActions.ButtonRelease, 0);
    }

    /// <summary>Scrolls by whole notches, keeping what is left of a slow stroke for the next one.</summary>
    private void ScrollBy(View display, double notches)
    {
        _scroll += notches;
        if (Math.Abs(_scroll) >= 0.25)
        {
            Send(display, MotionEventActions.Scroll, 0, (float)_scroll);
            _scroll = 0;
        }
    }

    private void Remember(MotionEvent e)
    {
        _lastX = Average(e, Axis.X);
        _lastY = Average(e, Axis.Y);
    }

    /// <summary>Where the fingers on the pad are, on average, leaving out one that is lifting.</summary>
    private static float Average(MotionEvent e, Axis axis)
    {
        var lifting = e.ActionMasked == MotionEventActions.PointerUp ? e.ActionIndex : -1;
        var sum = 0f;
        var count = 0;
        for (var pointer = 0; pointer < e.PointerCount; pointer++)
        {
            if (pointer != lifting)
            {
                sum += e.GetAxisValue(axis, pointer);
                count++;
            }
        }

        return count == 0 ? 0 : sum / count;
    }

    /// <summary>The pad's size along an axis, in its own units.</summary>
    private static float Range(MotionEvent e, Axis axis) =>
        e.Device?.GetMotionRange(axis, e.Source)?.Range is float range && range > 0 ? range : 1;
}
