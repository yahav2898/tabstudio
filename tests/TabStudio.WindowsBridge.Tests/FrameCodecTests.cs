using TabStudio.WindowsBridge.Frames;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>The frame codec on synthetic pictures: what changed is found exactly, and what arrives is what was sent.</summary>
public sealed class FrameCodecTests
{
    [Fact]
    public void A_keyframe_arrives_exactly()
    {
        var picture = Pictures.Window(300, 200);
        var encoder = new FrameEncoder();
        var decoder = new FrameDecoder();

        var encoded = encoder.Encode(picture, FrameQuality.Exact)!;
        var dirty = decoder.Apply(encoded.Data);

        Assert.True(encoded.IsKeyframe);
        Assert.Equal(picture.Bounds, dirty);
        Assert.Equal(picture.Pixels, decoder.Frame!.Pixels);
    }

    [Fact]
    public void Only_the_tiles_that_changed_are_found()
    {
        var before = Pictures.Window(256, 192);
        var after = before.Clone();
        after.Fill(new PixelRect(70, 10, 4, 4), 0xFFFF0000);
        after.Fill(new PixelRect(130, 130, 60, 2), 0xFF00FF00);

        var changed = FrameEncoder.FindChangedTiles(before, after, 64);

        Assert.Equal([(1, 0), (2, 2)], changed);
    }

    [Fact]
    public void Tiles_on_the_edges_are_cut_to_the_picture()
    {
        var before = Pictures.Window(130, 70);
        var after = before.Clone();
        after.SetPixel(129, 69, 0xFF123456);

        var changed = FrameEncoder.FindChangedTiles(before, after, 64);
        var encoder = new FrameEncoder();
        var decoder = new FrameDecoder();
        decoder.Apply(encoder.Encode(before, 0)!.Data);
        var update = encoder.Encode(after, 0)!;
        var dirty = decoder.Apply(update.Data);

        Assert.Equal([(2, 1)], changed);
        Assert.Equal(new PixelRect(128, 64, 2, 6), dirty);
        Assert.Equal(after.Pixels, decoder.Frame!.Pixels);
    }

    [Fact]
    public void An_update_carries_only_the_changed_tiles_and_is_small()
    {
        var before = Pictures.Window(1280, 800);
        var after = before.Clone();
        after.Fill(new PixelRect(400, 300, 9, 18), 0xFF000000);
        var encoder = new FrameEncoder();
        var keyframe = encoder.Encode(before, 0)!;

        var update = encoder.Encode(after, 0)!;

        Assert.False(update.IsKeyframe);
        Assert.Equal(1, update.ChangedTiles);
        Assert.True(update.Data.Length < 2048, $"A caret-sized change took {update.Data.Length} bytes.");
        Assert.True(keyframe.Data.Length < 1280 * 800 * 4 / 20, $"A user interface keyframe took {keyframe.Data.Length} bytes.");
    }

    [Fact]
    public void Nothing_is_sent_when_nothing_changed()
    {
        var picture = Pictures.Window(200, 100);
        var encoder = new FrameEncoder();
        encoder.Encode(picture, 0);

        Assert.Null(encoder.Encode(picture.Clone(), 0));
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 8)]
    [InlineData(3, 16)]
    public void Rough_levels_stay_close_to_the_picture(int level, int tolerance)
    {
        var picture = Pictures.Gradient(160, 96);
        var decoder = new FrameDecoder();

        decoder.Apply(new FrameEncoder().Encode(picture, level)!.Data);

        Assert.True(Pictures.MaxDifference(picture, decoder.Frame!) <= tolerance);
    }

    [Fact]
    public void The_roughest_level_sends_half_the_resolution()
    {
        var smooth = Pictures.Gradient(256, 128);
        var noisy = Pictures.Noise(256, 128);
        var exact = new FrameEncoder().Encode(noisy, FrameQuality.Exact)!;
        var roughest = new FrameEncoder().Encode(noisy, FrameQuality.Roughest)!;
        var decoder = new FrameDecoder();

        decoder.Apply(new FrameEncoder().Encode(smooth, FrameQuality.Roughest)!.Data);

        Assert.True(roughest.Data.Length * 4 < exact.Data.Length, $"{roughest.Data.Length} bytes against {exact.Data.Length} exact.");
        Assert.Equal(smooth.Width, decoder.Frame!.Width);
        Assert.True(Pictures.MaxDifference(smooth, decoder.Frame) <= 24);
    }

    [Fact]
    public void Rough_tiles_are_sent_again_exactly_once_the_window_is_still()
    {
        var picture = Pictures.Gradient(192, 128);
        var encoder = new FrameEncoder();
        var decoder = new FrameDecoder();
        decoder.Apply(encoder.Encode(picture, 3)!.Data);
        Assert.True(encoder.HasRoughTiles);

        var first = encoder.Encode(picture, 3, maxRefinedTiles: 4)!;
        decoder.Apply(first.Data);
        var rest = encoder.Encode(picture, 3, maxRefinedTiles: 100)!;
        decoder.Apply(rest.Data);

        Assert.Equal(0, first.ChangedTiles);
        Assert.Equal(4, first.RefinedTiles);
        Assert.Equal(2, rest.RefinedTiles);
        Assert.False(encoder.HasRoughTiles);
        Assert.Null(encoder.Encode(picture, 3, maxRefinedTiles: 100));
        Assert.Equal(picture.Pixels, decoder.Frame!.Pixels);
    }

    [Fact]
    public void One_color_tiles_are_exact_at_any_level()
    {
        var picture = new FrameBitmap(128, 64);
        picture.Fill(picture.Bounds, 0xFF336699);
        var encoder = new FrameEncoder();
        var decoder = new FrameDecoder();

        decoder.Apply(encoder.Encode(picture, FrameQuality.Roughest)!.Data);

        Assert.False(encoder.HasRoughTiles);
        Assert.Equal(picture.Pixels, decoder.Frame!.Pixels);
    }

    [Fact]
    public void A_resized_window_is_sent_as_a_keyframe()
    {
        var encoder = new FrameEncoder();
        var decoder = new FrameDecoder();
        decoder.Apply(encoder.Encode(Pictures.Window(200, 100), 0)!.Data);
        var larger = Pictures.Window(240, 150);

        var update = encoder.Encode(larger, 0)!;
        var dirty = decoder.Apply(update.Data);

        Assert.True(update.IsKeyframe);
        Assert.Equal(larger.Bounds, dirty);
        Assert.Equal(larger.Pixels, decoder.Frame!.Pixels);
    }

    [Fact]
    public void A_decoder_refuses_an_update_before_its_keyframe()
    {
        var encoder = new FrameEncoder();
        var picture = Pictures.Window(100, 100);
        encoder.Encode(picture, 0);
        var changed = picture.Clone();
        changed.SetPixel(5, 5, 0xFFFFFFFF);
        var update = encoder.Encode(changed, 0)!;

        Assert.Throws<InvalidDataException>(() => new FrameDecoder().Apply(update.Data));
        Assert.Throws<InvalidDataException>(() => new FrameDecoder().Apply([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17]));
    }

    [Fact]
    public void A_reset_starts_the_stream_over_with_a_keyframe()
    {
        var encoder = new FrameEncoder();
        var picture = Pictures.Window(100, 80);
        encoder.Encode(picture, 0);

        encoder.Reset();
        var again = encoder.Encode(picture, 0)!;

        Assert.True(again.IsKeyframe);
        var decoder = new FrameDecoder();
        decoder.Apply(again.Data);
        Assert.Equal(picture.Pixels, decoder.Frame!.Pixels);
    }

    [Fact]
    public void Scaling_down_keeps_the_proportions_and_averages_the_pixels()
    {
        var picture = new FrameBitmap(400, 200);
        picture.Fill(new PixelRect(0, 0, 200, 200), 0xFFFFFFFF);

        var scaled = picture.ScaleToFit(100, 100);

        Assert.Equal((100, 50), (scaled.Width, scaled.Height));
        Assert.Equal(0xFFFFFFFFu, scaled.GetPixel(10, 10));
        Assert.Equal(0xFF000000u, scaled.GetPixel(90, 40));
        Assert.Same(picture, picture.ScaleToFit(800, 800));
    }
}
