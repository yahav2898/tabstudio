using TabStudio.Core.Previews;

namespace TabStudio.Core.Tests;

public sealed class PreviewLayoutCalculatorTests
{
    [Fact]
    public void Fit_scales_a_window_larger_than_the_pane_down_until_all_of_it_shows()
    {
        var layout = PreviewLayoutCalculator.Compute(PreviewZoomMode.Fit, 420, 700, 1600, 900);

        Assert.Equal(0.262, layout.Scale);
        Assert.Equal(1600, layout.WindowWidth);
        Assert.Equal(900, layout.WindowHeight);
        Assert.True(layout.ShowsWholeWindow);
        Assert.False(layout.ScrollsHorizontally);
        Assert.False(layout.ScrollsVertically);

        // The narrow pane leaves room above and below, so the window is centered vertically.
        Assert.True(layout.Left * layout.Scale < 1);
        Assert.Equal(Math.Floor(((700 / 0.262) - 900) / 2), layout.Top);
        Assert.True((layout.Left + layout.WindowWidth) * layout.Scale <= 420);
        Assert.True((layout.Top + layout.WindowHeight) * layout.Scale <= 700);
    }

    [Fact]
    public void Fit_takes_the_tighter_of_width_and_height()
    {
        var layout = PreviewLayoutCalculator.Compute(PreviewZoomMode.Fit, 1000, 300, 800, 600);

        Assert.Equal(0.5, layout.Scale);
        Assert.Equal(0, layout.Top);
        Assert.Equal((2000 - 800) / 2, layout.Left);
        Assert.True(layout.ShowsWholeWindow);
    }

    [Fact]
    public void Fit_never_scales_a_small_window_up_and_centers_it()
    {
        var layout = PreviewLayoutCalculator.Compute(PreviewZoomMode.Fit, 1280, 800, 400, 300);

        Assert.Equal(1, layout.Scale);
        Assert.Equal(440, layout.Left);
        Assert.Equal(250, layout.Top);
        Assert.Equal(1280, layout.ViewportWidth);
        Assert.True(layout.ShowsWholeWindow);
    }

    [Fact]
    public void Fit_stops_at_the_smallest_legible_scale_and_then_scrolls()
    {
        var layout = PreviewLayoutCalculator.Compute(PreviewZoomMode.Fit, 400, 300, 4000, 600);

        Assert.Equal(PreviewLayoutCalculator.MinimumScale, layout.Scale);
        Assert.Equal(0, layout.Left);
        Assert.True(layout.ScrollsHorizontally);
        Assert.False(layout.ScrollsVertically);
        Assert.Equal(4000, layout.ExtentWidth);
    }

    [Fact]
    public void Actual_size_keeps_the_declared_size_and_scrolls_where_it_is_larger_than_the_pane()
    {
        var layout = PreviewLayoutCalculator.Compute(PreviewZoomMode.ActualSize, 420, 700, 1600, 600);

        Assert.Equal(1, layout.Scale);
        Assert.Equal(1, layout.MinimumScale);
        Assert.Equal(0, layout.Left);
        Assert.Equal(50, layout.Top);
        Assert.True(layout.ScrollsHorizontally);
        Assert.False(layout.ScrollsVertically);
        Assert.Equal(1600, layout.ExtentWidth);
        Assert.Equal(700, layout.ExtentHeight);
        Assert.Equal((1180.0, 0.0), layout.ScrollRange(1));
    }

    [Fact]
    public void Fill_gives_the_window_the_pane_whatever_it_declares()
    {
        var layout = PreviewLayoutCalculator.Compute(PreviewZoomMode.Fill, 420, 700, 1600, 900);

        Assert.Equal(1, layout.Scale);
        Assert.Equal((420.0, 700.0), (layout.WindowWidth, layout.WindowHeight));
        Assert.Equal((0.0, 0.0), (layout.Left, layout.Top));
        Assert.True(layout.ShowsWholeWindow);
    }

    [Theory]
    [InlineData(PreviewZoomMode.Fit)]
    [InlineData(PreviewZoomMode.ActualSize)]
    [InlineData(PreviewZoomMode.Fill)]
    public void A_window_that_declares_no_size_fills_the_pane_in_every_mode(PreviewZoomMode mode)
    {
        var layout = PreviewLayoutCalculator.Compute(mode, 640, 480, double.NaN, 300);

        Assert.Equal((640.0, 480.0), (layout.WindowWidth, layout.WindowHeight));
        Assert.Equal(1, layout.Scale);
        Assert.Equal(mode, layout.Mode);
    }

    [Fact]
    public void An_empty_pane_counts_as_one_pixel_rather_than_dividing_by_zero()
    {
        var layout = PreviewLayoutCalculator.Compute(PreviewZoomMode.Fit, 0, double.NaN, 800, 600);

        Assert.Equal(PreviewLayoutCalculator.MinimumScale, layout.Scale);
        Assert.True(double.IsFinite(layout.ViewportWidth));
    }

    [Fact]
    public void A_pinch_zooms_between_the_fit_and_the_maximum_and_scrolls_the_rest()
    {
        var layout = PreviewLayoutCalculator.Compute(PreviewZoomMode.Fit, 500, 500, 1000, 1000);
        Assert.Equal(0.5, layout.Scale);

        Assert.Equal(0.5, layout.ClampZoom(0.1));
        Assert.Equal(PreviewLayoutCalculator.MaximumScale, layout.ClampZoom(10));
        Assert.Equal(0.5, layout.ClampZoom(double.NaN));

        // At the fit there is nothing to scroll; zoomed in to 1, half of the window is out of view each way.
        Assert.Equal((0.0, 0.0), layout.ScrollRange(0.5));
        Assert.Equal((500.0, 500.0), layout.ScrollRange(1));
        Assert.Equal((875.0, 875.0), layout.ScrollRange(4));
    }

    [Fact]
    public void A_point_of_the_window_maps_to_the_pane_through_the_offset_and_the_scale()
    {
        var layout = PreviewLayoutCalculator.Compute(PreviewZoomMode.Fit, 400, 400, 800, 400);

        // Scale 0.5: the page is 800 by 800, the window 400 tall, centered 200 down.
        Assert.Equal(0.5, layout.Scale);
        Assert.Equal(200, layout.Top);
        Assert.Equal((400.0, 200.0), layout.ToPane(800, 200));
        Assert.Equal((0.0, 100.0), layout.ToPane(0, 0));
    }
}
