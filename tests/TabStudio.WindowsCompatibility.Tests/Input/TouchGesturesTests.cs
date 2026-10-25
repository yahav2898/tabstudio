using TabStudio.WindowsCompatibility.Input;
using TabStudio.WindowsCompatibility.Tests.Infrastructure;

namespace TabStudio.WindowsCompatibility.Tests.Input;

/// <summary>Touch on the preview becomes the mouse a Windows app expects.</summary>
public sealed class TouchGesturesTests
{
    private readonly RecordingInput _input = new();
    private readonly ManualTime _time = new();

    [Fact]
    public void A_tap_clicks_where_the_finger_touched()
    {
        using var gestures = new TouchGestures(_input, _time);

        gestures.Down(1, 100, 50);
        gestures.Up(1, 102, 51);

        Assert.Equal(["move 100,50", "left down", "left up"], _input.Events);
    }

    [Fact]
    public void Nothing_is_pressed_until_the_finger_lifts_or_moves()
    {
        using var gestures = new TouchGestures(_input, _time);

        gestures.Down(1, 100, 50);
        gestures.Move(1, 104, 52);

        Assert.Empty(_input.Events);
    }

    [Fact]
    public void A_drag_presses_where_it_started_and_releases_where_it_ends()
    {
        using var gestures = new TouchGestures(_input, _time);

        gestures.Down(1, 10, 10);
        gestures.Move(1, 40, 10);
        gestures.Move(1, 80, 30);
        gestures.Up(1, 90, 30);

        Assert.Equal(["move 10,10", "left down", "move 40,10", "move 80,30", "move 90,30", "left up"], _input.Events);
    }

    [Fact]
    public void A_long_press_right_clicks_once_and_the_lift_does_nothing_more()
    {
        using var gestures = new TouchGestures(_input, _time);
        var pressed = 0;
        gestures.LongPressed += (_, _) => pressed++;

        gestures.Down(1, 200, 120);
        _time.Advance(TimeSpan.FromMilliseconds(600));
        gestures.Up(1, 200, 120);

        Assert.Equal(["move 200,120", "right down", "right up"], _input.Events);
        Assert.Equal(1, pressed);
    }

    [Fact]
    public void A_finger_that_moves_away_before_the_long_press_drags_instead()
    {
        using var gestures = new TouchGestures(_input, _time);

        gestures.Down(1, 200, 120);
        gestures.Move(1, 260, 120);
        _time.Advance(TimeSpan.FromSeconds(1));
        gestures.Up(1, 260, 120);

        Assert.DoesNotContain("right down", _input.Events);
        Assert.Equal("left up", _input.Events[^1]);
    }

    [Fact]
    public void Two_fingers_moving_together_turn_the_wheel_without_clicking()
    {
        using var gestures = new TouchGestures(_input, _time) { ScrollStep = 20 };

        gestures.Down(1, 100, 100);
        gestures.Down(2, 140, 100);
        gestures.Move(1, 100, 150);
        gestures.Move(2, 140, 150);
        gestures.Up(1, 100, 150);
        gestures.Up(2, 140, 150);

        Assert.DoesNotContain("left down", _input.Events);
        Assert.DoesNotContain("right down", _input.Events);
        var notches = _input.Events.Where(e => e.StartsWith("wheel", StringComparison.Ordinal)).Select(e => int.Parse(e.Split(',')[1], System.Globalization.CultureInfo.InvariantCulture)).Sum();

        // The fingers moved 50 pixels down: two notches of 20 pull the content down, as on a touchpad.
        Assert.Equal(2, notches);
    }

    [Fact]
    public void Cancel_releases_a_drag()
    {
        using var gestures = new TouchGestures(_input, _time);

        gestures.Down(1, 10, 10);
        gestures.Move(1, 60, 10);
        gestures.Cancel();

        Assert.Equal("left up", _input.Events[^1]);
    }

    private sealed class RecordingInput : IDesktopInput
    {
        public List<string> Events { get; } = [];

        public void MovePointer(int x, int y) => Events.Add($"move {x},{y}");

        public void Button(PointerButton button, bool pressed) => Events.Add($"{button.ToString().ToLowerInvariant()} {(pressed ? "down" : "up")}");

        public void Wheel(int horizontal, int vertical) => Events.Add($"wheel {horizontal},{vertical}");

        public void Key(WindowsKey key, bool pressed) => Events.Add($"key {key} {pressed}");

        public void Type(string text) => Events.Add($"type {text}");
    }
}
