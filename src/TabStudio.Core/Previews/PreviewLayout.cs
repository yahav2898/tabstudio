namespace TabStudio.Core.Previews;

/// <summary>
/// Where a window is drawn in a preview pane, and how large: the result of <see cref="PreviewLayoutCalculator"/>.
/// Lengths are in the page's pixels (CSS pixels in a browser, device-independent pixels elsewhere), before
/// <see cref="Scale"/> is applied; the pane shows the page at <see cref="Scale"/>.
/// </summary>
/// <param name="Mode">The mode it was computed for.</param>
/// <param name="PaneWidth">The pane's width, in the pane's own pixels.</param>
/// <param name="PaneHeight">The pane's height, in the pane's own pixels.</param>
/// <param name="WindowWidth">The width the window is laid out at: its declared width, or the pane's in <see cref="PreviewZoomMode.Fill"/>.</param>
/// <param name="WindowHeight">The height the window is laid out at.</param>
/// <param name="Left">The window's left edge in the page; it is centered when it is narrower than the page.</param>
/// <param name="Top">The window's top edge in the page; it is centered when it is shorter than the page.</param>
/// <param name="Scale">How large the page is drawn: 1 is one page pixel for one pane pixel, less is smaller.</param>
/// <param name="MinimumScale">How far a pinch may zoom out.</param>
/// <param name="MaximumScale">How far a pinch may zoom in.</param>
public sealed record PreviewLayout(
    PreviewZoomMode Mode,
    double PaneWidth,
    double PaneHeight,
    double WindowWidth,
    double WindowHeight,
    double Left,
    double Top,
    double Scale,
    double MinimumScale,
    double MaximumScale)
{
    /// <summary>How much less than a pixel still counts as fitting, for rounding.</summary>
    private const double Tolerance = 0.5;

    /// <summary>The width of the page the pane shows at <see cref="Scale"/>.</summary>
    public double ViewportWidth => PaneWidth / Scale;

    /// <summary>The height of the page the pane shows at <see cref="Scale"/>.</summary>
    public double ViewportHeight => PaneHeight / Scale;

    /// <summary>The width of the whole page, which scrolls when it is wider than the viewport.</summary>
    public double ExtentWidth => Math.Max(ViewportWidth, Left + WindowWidth);

    /// <summary>The height of the whole page, which scrolls when it is taller than the viewport.</summary>
    public double ExtentHeight => Math.Max(ViewportHeight, Top + WindowHeight);

    /// <summary>Whether the page scrolls sideways at <see cref="Scale"/>, so the pane shows a horizontal scroll bar.</summary>
    public bool ScrollsHorizontally => ExtentWidth > ViewportWidth + Tolerance;

    /// <summary>Whether the page scrolls up and down at <see cref="Scale"/>, so the pane shows a vertical scroll bar.</summary>
    public bool ScrollsVertically => ExtentHeight > ViewportHeight + Tolerance;

    /// <summary>Whether the whole window is in view at <see cref="Scale"/>, without scrolling.</summary>
    public bool ShowsWholeWindow => !ScrollsHorizontally && !ScrollsVertically;

    /// <summary>A zoom a pinch asks for, kept between <see cref="MinimumScale"/> and <see cref="MaximumScale"/>.</summary>
    /// <param name="scale">The scale the fingers ask for.</param>
    public double ClampZoom(double scale) => double.IsNaN(scale) ? Scale : Math.Clamp(scale, MinimumScale, MaximumScale);

    /// <summary>How far the page scrolls at a zoom: the largest scroll offset in each direction, in page pixels.</summary>
    /// <param name="scale">The zoom, which is clamped first.</param>
    /// <returns>Zero in a direction where the page fits.</returns>
    public (double Horizontal, double Vertical) ScrollRange(double scale)
    {
        var zoom = ClampZoom(scale);
        return (Math.Max(0, ExtentWidth - (PaneWidth / zoom)), Math.Max(0, ExtentHeight - (PaneHeight / zoom)));
    }

    /// <summary>Where a point of the window appears in the pane at <see cref="Scale"/>, scrolled to the top left.</summary>
    /// <param name="x">The point's distance from the window's left edge, in the window's pixels.</param>
    /// <param name="y">The point's distance from the window's top edge.</param>
    /// <returns>The point in the pane's pixels, from its top left corner.</returns>
    public (double X, double Y) ToPane(double x, double y) => ((Left + x) * Scale, (Top + y) * Scale);
}
