using TabStudio.WindowsBridge.Frames;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>The stream's pacing and quality: slow frames lower the quality at once, a run of fast ones raises it again.</summary>
public sealed class AdaptiveQualityTests
{
    private static readonly TimeSpan Target = TimeSpan.FromMilliseconds(200);

    [Fact]
    public void A_fast_link_stays_exact()
    {
        var quality = new AdaptiveQuality(Target);

        Deliver(quality, frames: 20, delivery: TimeSpan.FromMilliseconds(20), bytes: 30_000);

        Assert.Equal(FrameQuality.Exact, quality.Level);
        Assert.True(quality.BytesPerSecond > 1_000_000);
    }

    [Fact]
    public void Slow_frames_lower_the_quality_and_fast_ones_raise_it_again()
    {
        var quality = new AdaptiveQuality(Target);

        Deliver(quality, frames: 2, delivery: TimeSpan.FromMilliseconds(300), bytes: 200_000);
        var slow = quality.Level;
        Deliver(quality, frames: 1, delivery: TimeSpan.FromMilliseconds(900), bytes: 200_000);
        var slower = quality.Level;
        Deliver(quality, frames: 60, delivery: TimeSpan.FromMilliseconds(30), bytes: 5_000);

        Assert.Equal(2, slow);
        Assert.Equal(FrameQuality.Roughest, slower);
        Assert.Equal(FrameQuality.Exact, quality.Level);
    }

    [Fact]
    public void Only_a_few_frames_are_on_their_way_at_once()
    {
        var quality = new AdaptiveQuality(Target, maxInFlight: 2);

        quality.OnSent(1, 1000, TimeSpan.Zero);
        var afterOne = quality.CanSend;
        quality.OnSent(2, 1000, TimeSpan.FromMilliseconds(5));

        Assert.True(afterOne);
        Assert.False(quality.CanSend);
        Assert.Equal(0, quality.RefinementBudget);
        quality.OnShown(2, TimeSpan.FromMilliseconds(40));
        Assert.Equal(0, quality.InFlight);
        Assert.True(quality.CanSend);
        Assert.True(quality.RefinementBudget > 0);
    }

    private static void Deliver(AdaptiveQuality quality, int frames, TimeSpan delivery, int bytes)
    {
        var at = TimeSpan.FromSeconds(Random.Shared.Next(1, 100));
        for (var i = 0; i < frames; i++)
        {
            var sequence = (long)(at.TotalMilliseconds * 10) + i;
            quality.OnSent(sequence, bytes, at);
            at += delivery;
            quality.OnShown(sequence, at);
        }
    }
}
