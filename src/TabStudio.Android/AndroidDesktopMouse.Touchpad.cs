using Android.Views;
using TabStudio.App.Platform;

namespace TabStudio.Android;

/// <summary>
/// A captured touchpad, such as a keyboard cover's: Android gives it as fingers on a pad, not as a mouse, so
/// <see cref="DesktopTouchpad"/> makes a mouse of it, and its moves, scrolls and clicks go to the display's view.
/// </summary>
internal sealed partial class AndroidDesktopMouse
{
    private DesktopTouchpad? _touchpad;

    /// <summary>A captured touchpad event: moves the pointer, scrolls, or clicks.</summary>
    /// <returns>Whether it was used.</returns>
    private bool OnTouchpad(View display, MotionEvent e)
    {
        var touchpad = _touchpad ??= new DesktopTouchpad(_cursor);
        var action = e.ActionMasked switch
        {
            MotionEventActions.Down => Touched(touchpad, e),
            MotionEventActions.PointerDown or MotionEventActions.PointerUp => FingersChanged(touchpad, e),
            MotionEventActions.Move => touchpad.Slide(Average(e, Axis.X), Average(e, Axis.Y), e.PointerCount, Range(e, Axis.X), Range(e, Axis.Y), display.Width),
            MotionEventActions.Up => touchpad.Lift(e.EventTime),
            MotionEventActions.ButtonPress => touchpad.PressButton(),
            MotionEventActions.ButtonRelease => touchpad.ReleaseButton(),
            _ => (TouchpadAction?)null,
        };

        switch (action)
        {
            case null:
                return false;
            case TouchpadAction.Move:
                Send(display, MotionEventActions.Move, 0);
                break;
            case TouchpadAction.Drag:
                Send(display, MotionEventActions.Move, MotionEventButtonState.Primary);
                break;
            case TouchpadAction.Scroll:
                Send(display, MotionEventActions.Scroll, 0, (float)touchpad.ScrollNotches);
                break;
            case TouchpadAction.Click:
                Send(display, MotionEventActions.ButtonPress, MotionEventButtonState.Primary);
                Send(display, MotionEventActions.ButtonRelease, 0);
                break;
            case TouchpadAction.Press:
                Send(display, MotionEventActions.ButtonPress, MotionEventButtonState.Primary);
                break;
            case TouchpadAction.Release:
                Send(display, MotionEventActions.ButtonRelease, 0);
                break;
        }

        return true;
    }

    private static TouchpadAction Touched(DesktopTouchpad touchpad, MotionEvent e)
    {
        touchpad.Touch(e.EventTime, Average(e, Axis.X), Average(e, Axis.Y));
        return TouchpadAction.None;
    }

    private static TouchpadAction FingersChanged(DesktopTouchpad touchpad, MotionEvent e)
    {
        touchpad.FingersChanged(Average(e, Axis.X), Average(e, Axis.Y));
        return TouchpadAction.None;
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
