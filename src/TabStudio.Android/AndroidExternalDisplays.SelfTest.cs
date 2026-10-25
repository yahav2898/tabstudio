using Android.OS;
using Android.Views;

namespace TabStudio.Android;

/// <summary>A mouse on the external display, for the desktop mode self test.</summary>
public sealed partial class AndroidExternalDisplays
{
    /// <summary>
    /// Clicks the IDE on the display with a mouse's left button: the events Android gives a window when a mouse
    /// on its display moves there and clicks, given to the presentation's view.
    /// </summary>
    /// <remarks>
    /// The emulator cannot click the display through <c>adb shell input</c>: its taps have no button press and release
    /// (and on Android 10 no pointer tool), which Avalonia, like Android's own views, needs from a mouse to click.
    /// </remarks>
    /// <param name="x">The point's distance from the display's left edge, in its pixels.</param>
    /// <param name="y">The point's distance from the display's top edge, in its pixels.</param>
    /// <returns>Whether the IDE is on a display to click.</returns>
    internal bool ClickWithMouse(float x, float y)
    {
        if (_view is not { } view)
        {
            return false;
        }

        var downTime = SystemClock.UptimeMillis();
        Send(view, MotionEventActions.HoverMove, downTime, x, y);
        Send(view, MotionEventActions.Down, downTime, x, y);
        Send(view, MotionEventActions.ButtonPress, downTime, x, y);
        Send(view, MotionEventActions.ButtonRelease, downTime, x, y);
        Send(view, MotionEventActions.Up, downTime, x, y);
        return true;
    }

    /// <summary>Gives the view one event of the mouse, as a touch event or a generic one, the way Android does.</summary>
    private static void Send(View view, MotionEventActions action, long downTime, float x, float y)
    {
        var pointer = new MotionEvent.PointerProperties { Id = 0, ToolType = MotionEventToolType.Mouse };
        var position = new MotionEvent.PointerCoords { X = x, Y = y, Pressure = 1, Size = 1 };

        // No meta keys, button state, edge flags, source or flags: the view reads the action, the tool and the position.
        using var motion = MotionEvent.Obtain(downTime, SystemClock.UptimeMillis(), default, 1, [pointer], [position], default, default, 1, 1, 0, default, default, default);
        if (motion is null)
        {
            return;
        }

        motion.Action = action;
        _ = action is MotionEventActions.Down or MotionEventActions.Up
            ? view.DispatchTouchEvent(motion)
            : view.DispatchGenericMotionEvent(motion);
    }
}
