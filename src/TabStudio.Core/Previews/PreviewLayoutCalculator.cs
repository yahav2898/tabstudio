namespace TabStudio.Core.Previews;

/// <summary>
/// Lays a desktop window out in a preview pane for a <see cref="PreviewZoomMode"/>: <see cref="PreviewZoomMode.Fit"/>
/// scales the window down until all of it shows (never up), <see cref="PreviewZoomMode.ActualSize"/> keeps its declared
/// size and lets the pane scroll, and <see cref="PreviewZoomMode.Fill"/> gives it the pane's size. A window smaller than
/// the page is centered in it. The web preview of WPF apps applies the result inside the page (see <c>wpf.js</c> in
/// TabStudio.Wpf, which mirrors this calculation); any preview that draws windows can use it.
/// </summary>
public static class PreviewLayoutCalculator
{
    /// <summary>
    /// The smallest scale a window is drawn at, so its text stays legible, and the smallest a browser's page zoom goes; a
    /// window that needs less is drawn at this scale and scrolls.
    /// </summary>
    public const double MinimumScale = 0.25;

    /// <summary>How far a pinch may zoom in.</summary>
    public const double MaximumScale = 4;

    /// <summary>Lays a window out.</summary>
    /// <param name="mode">How to show it.</param>
    /// <param name="paneWidth">The pane's width; anything under one pixel counts as one.</param>
    /// <param name="paneHeight">The pane's height; anything under one pixel counts as one.</param>
    /// <param name="windowWidth">The window's declared width, or <see langword="null"/> (or NaN) when it declares none.</param>
    /// <param name="windowHeight">The window's declared height, or <see langword="null"/> (or NaN) when it declares none.</param>
    /// <returns>
    /// The layout. A window that does not declare both a width and a height fills the pane in every mode, as it did before
    /// there were modes.
    /// </returns>
    public static PreviewLayout Compute(PreviewZoomMode mode, double paneWidth, double paneHeight, double? windowWidth, double? windowHeight)
    {
        var pane = (Width: Sanitize(paneWidth), Height: Sanitize(paneHeight));
        if (mode == PreviewZoomMode.Fill || !IsSize(windowWidth) || !IsSize(windowHeight))
        {
            return new PreviewLayout(mode, pane.Width, pane.Height, pane.Width, pane.Height, 0, 0, 1, 1, MaximumScale);
        }

        var window = (Width: windowWidth!.Value, Height: windowHeight!.Value);
        if (mode == PreviewZoomMode.ActualSize)
        {
            return new PreviewLayout(
                mode, pane.Width, pane.Height, window.Width, window.Height,
                Center(pane.Width, window.Width), Center(pane.Height, window.Height), 1, 1, MaximumScale);
        }

        // Fit: the largest scale at which the whole window shows, rounded down to a thousandth so rounding never makes it
        // overflow by a hair, and never more than 1.
        var fit = Math.Min(1, Math.Min(pane.Width / window.Width, pane.Height / window.Height));
        var scale = Math.Max(MinimumScale, Math.Floor(fit * 1000) / 1000);
        return new PreviewLayout(
            mode, pane.Width, pane.Height, window.Width, window.Height,
            Center(pane.Width / scale, window.Width), Center(pane.Height / scale, window.Height), scale, scale, MaximumScale);
    }

    /// <summary>Where a length starts so that it is centered in a page, at whole pixels; 0 when it does not fit.</summary>
    private static double Center(double page, double length) => Math.Max(0, Math.Floor((page - length) / 2));

    private static bool IsSize(double? length) => length is { } value && double.IsFinite(value) && value >= 1;

    private static double Sanitize(double length) => double.IsFinite(length) ? Math.Max(1, length) : 1;
}
