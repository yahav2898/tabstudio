using Avalonia;
using TabStudio.App.Platform;

namespace TabStudio.App.Tests.DesktopMode;

/// <summary>
/// A captured touchpad as the display's mouse: one finger moves TabStudio's pointer, two fingers scroll the way they
/// slide, a tap clicks, and the pad's button presses, drags and releases.
/// </summary>
public sealed class DesktopTouchpadTests
{
    // A pad 1000 × 600 of its own units, and a display 1920 × 1080: a stroke across the pad moves the pointer 1536 pixels.
    private const double PadWidth = 1000;
    private const double PadHeight = 600;
    private const double DisplayWidth = 1920;

    private readonly DesktopCursor _cursor = new();
    private readonly DesktopTouchpad _touchpad;

    public DesktopTouchpadTests()
    {
        _cursor.Reset(1920, 1080, 1);
        _touchpad = new DesktopTouchpad(_cursor);
    }

    [Fact]
    public void One_finger_moves_the_pointer_by_a_share_of_the_display_and_keeps_it_on_the_display()
    {
        _touchpad.Touch(0, 500, 300);

        Assert.Equal(TouchpadAction.Move, Slide(600, 250));
        Assert.Equal(960 + 153.6, _cursor.PixelPosition.X, 6);
        Assert.Equal(540 - 76.8, _cursor.PixelPosition.Y, 6);

        Assert.Equal(TouchpadAction.Move, Slide(-2000, -2000));
        Assert.Equal(new Point(0, 0), _cursor.PixelPosition);
    }

    [Fact]
    public void Two_fingers_scroll_the_way_they_slide_in_whole_steps_and_keep_the_rest()
    {
        _touchpad.Touch(0, 500, 300);
        _touchpad.FingersChanged(500, 300);
        var pointer = _cursor.PixelPosition;

        // Down by a tenth of the pad: the content follows, as the wheel turned away from the user does.
        Assert.Equal(TouchpadAction.Scroll, Slide(500, 360, fingers: 2));
        Assert.Equal(1.2, _touchpad.ScrollNotches, 6);

        // Too little to send, then enough with what was kept; up scrolls the other way.
        Assert.Equal(TouchpadAction.None, Slide(500, 352, fingers: 2));
        Assert.Equal(TouchpadAction.Scroll, Slide(500, 340, fingers: 2));
        Assert.Equal(-0.4, _touchpad.ScrollNotches, 6);
        Assert.Equal(pointer, _cursor.PixelPosition);
    }

    [Fact]
    public void A_short_still_touch_is_a_click_but_not_a_long_one_a_moving_one_or_one_of_two_fingers()
    {
        _touchpad.Touch(1000, 500, 300);
        Slide(505, 302);
        Assert.Equal(TouchpadAction.Click, _touchpad.Lift(1000 + DesktopTouchpad.TapMilliseconds));

        _touchpad.Touch(2000, 500, 300);
        Assert.Equal(TouchpadAction.None, _touchpad.Lift(2000 + DesktopTouchpad.TapMilliseconds + 1));

        _touchpad.Touch(3000, 500, 300);
        Slide(560, 300);
        Assert.Equal(TouchpadAction.None, _touchpad.Lift(3050));

        _touchpad.Touch(4000, 500, 300);
        _touchpad.FingersChanged(500, 300);
        Assert.Equal(TouchpadAction.None, _touchpad.Lift(4050));
    }

    [Fact]
    public void The_pad_s_button_presses_drags_and_releases_without_a_click()
    {
        _touchpad.Touch(0, 500, 300);
        Assert.Equal(TouchpadAction.Press, _touchpad.PressButton());
        Assert.True(_touchpad.IsButtonDown);
        Assert.Equal(TouchpadAction.Drag, Slide(520, 300));
        Assert.Equal(TouchpadAction.None, _touchpad.Lift(50));
        Assert.Equal(TouchpadAction.Release, _touchpad.ReleaseButton());
        Assert.False(_touchpad.IsButtonDown);
        Assert.Equal(TouchpadAction.Move, Slide(530, 300));
    }

    private TouchpadAction Slide(double x, double y, int fingers = 1) =>
        _touchpad.Slide(x, y, fingers, PadWidth, PadHeight, DisplayWidth);
}
