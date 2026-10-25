using TabStudio.App.Input.Scrolling;

namespace TabStudio.App.Tests.Input;

public sealed class FlingSplineTests
{
    [Theory]
    [InlineData(1000, 194.3, 555.2)]
    [InlineData(4000, 2157.0, 1540.7)]
    [InlineData(5000, 3177.6, 1815.8)]
    [InlineData(8000, 7186.4, 2566.6)]
    public void A_fling_goes_as_far_and_as_long_as_on_Android(double velocity, double distance, double milliseconds)
    {
        var fling = FlingSpline.FromVelocity(velocity);

        Assert.Equal(distance, fling.Distance, 0.5);
        Assert.Equal(milliseconds, fling.Duration.TotalMilliseconds, 0.5);
    }

    [Fact]
    public void It_starts_at_the_release_velocity_and_slows_down_to_rest()
    {
        var fling = FlingSpline.FromVelocity(4000);

        Assert.Equal(4000.0, fling.VelocityAt(TimeSpan.Zero), 40.0);
        var previousPosition = 0.0;
        var previousVelocity = double.MaxValue;
        for (var t = TimeSpan.FromMilliseconds(8); t <= fling.Duration; t += TimeSpan.FromMilliseconds(8))
        {
            var position = fling.PositionAt(t);
            var velocity = fling.VelocityAt(t);
            Assert.True(position >= previousPosition, $"it moves forward at {t.TotalMilliseconds} ms");
            Assert.True(velocity <= previousVelocity + 1e-9, $"it slows down at {t.TotalMilliseconds} ms");
            previousPosition = position;
            previousVelocity = velocity;
        }

        // Duration is rounded to whole ticks, so the end is reached to within a hair.
        Assert.Equal(fling.Distance, fling.PositionAt(fling.Duration), 0.01);
        Assert.Equal(0.0, fling.VelocityAt(fling.Duration + TimeSpan.FromTicks(1)));
        Assert.Equal(fling.Distance, fling.PositionAt(fling.Duration + TimeSpan.FromTicks(1)));
        Assert.Equal(0.0, fling.PositionAt(TimeSpan.Zero));
    }

    [Fact]
    public void A_fling_up_mirrors_a_fling_down()
    {
        var down = FlingSpline.FromVelocity(3000);
        var up = FlingSpline.FromVelocity(-3000);

        Assert.Equal(-down.Distance, up.Distance, 1e-9);
        Assert.Equal(down.Duration, up.Duration);
        Assert.Equal(-down.PositionAt(TimeSpan.FromMilliseconds(300)), up.PositionAt(TimeSpan.FromMilliseconds(300)), 1e-9);
    }

    [Fact]
    public void A_slow_release_does_not_fling_and_a_fast_one_is_capped()
    {
        Assert.True(FlingSpline.FromVelocity(30).IsEmpty);
        Assert.True(FlingSpline.FromVelocity(-49).IsEmpty);
        Assert.True(FlingSpline.FromVelocity(double.NaN).IsEmpty);
        Assert.Equal(FlingSpline.FromVelocity(8000).Distance, FlingSpline.FromVelocity(20000).Distance, 1e-9);
    }

    [Fact]
    public void A_fling_that_reaches_the_end_stops_there_on_the_same_curve()
    {
        var free = FlingSpline.FromVelocity(4000);
        var clamped = free.ClampedTo(before: 0, after: 1000);

        Assert.Equal(1000.0, clamped.Distance);
        Assert.True(clamped.Duration < free.Duration);
        // Android finds that moment from its time table, which agrees with the distance table to a fraction of a dip.
        Assert.Equal(1000.0, clamped.PositionAt(clamped.Duration), 0.5);
        Assert.Equal(1000.0, clamped.PositionAt(clamped.Duration + TimeSpan.FromTicks(1)));
        Assert.Equal(free.PositionAt(TimeSpan.FromMilliseconds(100)), clamped.PositionAt(TimeSpan.FromMilliseconds(100)), 1e-9);

        // The curve reaches the edge just as the shortened fling ends.
        Assert.Equal(1000.0, free.PositionAt(clamped.Duration), 5.0);
        Assert.All(Enumerable.Range(0, 200), i => Assert.True(clamped.PositionAt(TimeSpan.FromMilliseconds(i * 10)) <= 1000));
    }

    [Fact]
    public void A_fling_with_no_room_does_not_move_and_one_with_room_enough_is_unchanged()
    {
        var up = FlingSpline.FromVelocity(-2000);

        Assert.True(up.ClampedTo(before: 0, after: 5000).IsEmpty);
        Assert.Equal(up, up.ClampedTo(before: 5000, after: 0));
    }
}
