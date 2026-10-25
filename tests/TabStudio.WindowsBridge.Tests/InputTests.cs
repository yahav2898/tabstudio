using TabStudio.WindowsBridge.Input;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>Touch on the tablet becomes mouse input on the PC, at the right pixels, and keys become Windows' keys.</summary>
public sealed class InputTests
{
    // An 800 x 600 window drawn at half size, 20 pixels from the pane's left edge and 10 from its top.
    private static readonly FrameMapping Map = new(20, 10, 400, 300, 800, 600);

    [Fact]
    public void A_tap_is_a_click_where_the_finger_touched()
    {
        var touch = new TouchGestureRecognizer();

        var down = touch.Down(1, 120, 60, At(0), Map);
        var up = touch.Up(1, 123, 62, At(120), Map);

        Assert.Empty(down);
        Assert.Equal(
            [
                BridgeInputEvent.Mouse(BridgeInputKind.PointerMove, 200, 100),
                BridgeInputEvent.Mouse(BridgeInputKind.PointerDown, 200, 100, BridgePointerButton.Left),
                BridgeInputEvent.Mouse(BridgeInputKind.PointerUp, 200, 100, BridgePointerButton.Left),
            ],
            up);
        Assert.False(touch.IsActive);
    }

    [Fact]
    public void A_long_press_is_a_right_click_and_lifting_afterwards_does_nothing()
    {
        var touch = new TouchGestureRecognizer();
        touch.Down(1, 220, 160, At(0), Map);

        var early = touch.Tick(At(300), Map);
        var due = touch.Tick(At(600), Map);
        var lift = touch.Up(1, 220, 160, At(900), Map);

        Assert.Empty(early);
        Assert.Equal(BridgePointerButton.Right, due[1].Button);
        Assert.Equal((400, 300), (due[1].X, due[1].Y));
        Assert.Empty(lift);
    }

    [Fact]
    public void A_drag_holds_the_left_button_from_where_it_started_to_where_it_ends()
    {
        var touch = new TouchGestureRecognizer();
        touch.Down(1, 100, 100, At(0), Map);

        var start = touch.Move(1, 130, 100, At(50), Map);
        var middle = touch.Move(1, 160, 110, At(100), Map);
        var end = touch.Up(1, 170, 120, At(150), Map);

        Assert.Equal(BridgeInputKind.PointerDown, start[1].Kind);
        Assert.Equal((160, 180), (start[1].X, start[1].Y));
        Assert.Equal(BridgeInputEvent.Mouse(BridgeInputKind.PointerMove, 280, 200, BridgePointerButton.Left), middle[0]);
        Assert.Equal(BridgeInputEvent.Mouse(BridgeInputKind.PointerUp, 300, 220, BridgePointerButton.Left), end[0]);
    }

    [Fact]
    public void Two_fingers_turn_the_wheel_and_the_content_follows_them()
    {
        var touch = new TouchGestureRecognizer();
        touch.Down(1, 100, 200, At(0), Map);
        touch.Down(2, 140, 200, At(20), Map);

        var down = touch.Move(1, 100, 240, At(40), Map).Concat(touch.Move(2, 140, 240, At(40), Map)).ToList();
        var left = touch.Move(1, 60, 240, At(60), Map).Concat(touch.Move(2, 100, 240, At(60), Map)).ToList();

        // Fingers moving down show what is above: the wheel turns away from the user.
        Assert.Equal(BridgeInputEvent.WheelNotch, down.Sum(e => e.DeltaY));
        Assert.All(down, e => Assert.Equal(BridgeInputKind.Wheel, e.Kind));
        Assert.Equal(BridgeInputEvent.WheelNotch, left.Sum(e => e.DeltaX));
    }

    [Fact]
    public void Positions_outside_the_window_are_kept_on_its_edge()
    {
        Assert.Equal((0, 0), Map.ToWindow(0, 0));
        Assert.Equal((799, 599), Map.ToWindow(1000, 1000));
        Assert.False(Map.Contains(10, 5));
        Assert.True(Map.Contains(25, 15));
    }

    [Fact]
    public void A_window_is_fitted_into_the_pane_without_growing_past_its_size()
    {
        var small = FrameMapping.Fit(1000, 800, 300, 200, pixelsPerDip: 1.5);
        var large = FrameMapping.Fit(500, 400, 1600, 900);

        Assert.Equal((200.0, 133.33), (small.DrawnWidth, Math.Round(small.DrawnHeight, 2)));
        Assert.Equal((400.0, 333.33), (small.Left, Math.Round(small.Top, 2)));
        Assert.Equal(500, large.DrawnWidth);
        Assert.Equal(281.25, large.DrawnHeight);
        Assert.Equal(59.375, large.Top);
    }

    [Theory]
    [InlineData("Enter", 0x0D)]
    [InlineData("Return", 0x0D)]
    [InlineData("A", 0x41)]
    [InlineData("D7", 0x37)]
    [InlineData("NumPad3", 0x63)]
    [InlineData("F12", 0x7B)]
    [InlineData("PageDown", 0x22)]
    [InlineData("Next", 0x22)]
    [InlineData("OemComma", 0xBC)]
    [InlineData("Oem2", 0xBF)]
    [InlineData("LeftCtrl", 0xA2)]
    [InlineData("Delete", 0x2E)]
    public void Keys_are_Windows_virtual_keys(string name, int code)
    {
        Assert.Equal(code, VirtualKeys.Find(name));
    }

    [Fact]
    public void Modifiers_go_down_in_Windows_order_and_unknown_keys_have_no_code()
    {
        Assert.Equal([VirtualKeys.Control, VirtualKeys.Alt, VirtualKeys.Shift], VirtualKeys.Modifiers("Shift, Alt, Control"));
        Assert.Empty(VirtualKeys.Modifiers("None"));
        Assert.Null(VirtualKeys.Find("FnLeftArrow"));
        Assert.True(VirtualKeys.IsNonCharacter(0x25));
        Assert.False(VirtualKeys.IsNonCharacter(0x41));
    }

    private static TimeSpan At(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);
}
