using Avalonia;
using TabStudio.App.Input.Scrolling;

namespace TabStudio.App.Tests.Input;

public sealed class TouchVelocityTrackerTests
{
    [Fact]
    public void A_steady_finger_moves_at_its_speed()
    {
        var tracker = new TouchVelocityTracker();
        for (var i = 0; i <= 20; i++)
        {
            tracker.Add(TimeSpan.FromMilliseconds(i * 8), new Point(100 + (i * 2), 500 - (i * 6)));
        }

        var velocity = tracker.VelocityAt(TimeSpan.FromMilliseconds(164));

        Assert.Equal(250.0, velocity.X, 1.0);
        Assert.Equal(-750.0, velocity.Y, 1.0);
    }

    [Fact]
    public void An_accelerating_finger_has_the_speed_it_reached_when_it_lifts()
    {
        // y = 2000 t², so the speed at t = 0.096 s is 4000 · 0.096 = 384 dip/s.
        var tracker = new TouchVelocityTracker();
        for (var i = 0; i <= 12; i++)
        {
            var t = i * 0.008;
            tracker.Add(TimeSpan.FromSeconds(t), new Point(0, 2000 * t * t));
        }

        Assert.Equal(384.0, tracker.VelocityAt(TimeSpan.FromSeconds(0.1)).Y, 1.0);
    }

    [Fact]
    public void Only_the_last_100_ms_count()
    {
        var tracker = new TouchVelocityTracker();
        var y = 0.0;
        for (var i = 0; i < 15; i++)
        {
            tracker.Add(TimeSpan.FromMilliseconds(i * 20), new Point(0, y += 1));
        }

        // Then fast for 120 ms: the slow start is too old to count.
        for (var i = 1; i <= 15; i++)
        {
            tracker.Add(TimeSpan.FromMilliseconds(280 + (i * 8)), new Point(0, y += 40));
        }

        Assert.Equal(5000.0, tracker.VelocityAt(TimeSpan.FromMilliseconds(402)).Y, 1.0);
    }

    [Fact]
    public void A_finger_that_rested_before_lifting_has_no_speed()
    {
        var tracker = new TouchVelocityTracker();
        for (var i = 0; i < 10; i++)
        {
            tracker.Add(TimeSpan.FromMilliseconds(i * 8), new Point(0, i * 30));
        }

        Assert.Equal(default(Vector), tracker.VelocityAt(TimeSpan.FromMilliseconds(72 + 41)));
        Assert.NotEqual(default(Vector), tracker.VelocityAt(TimeSpan.FromMilliseconds(72 + 39)));
    }

    [Fact]
    public void Two_positions_give_their_line_and_one_gives_nothing()
    {
        var tracker = new TouchVelocityTracker();
        tracker.Add(TimeSpan.FromMilliseconds(0), new Point(0, 0));
        Assert.Equal(default(Vector), tracker.VelocityAt(TimeSpan.FromMilliseconds(1)));

        tracker.Add(TimeSpan.FromMilliseconds(10), new Point(0, 20));
        Assert.Equal(2000.0, tracker.VelocityAt(TimeSpan.FromMilliseconds(10)).Y, 1e-6);

        tracker.Clear();
        Assert.Equal(default(Vector), tracker.VelocityAt(TimeSpan.FromMilliseconds(10)));
    }
}
