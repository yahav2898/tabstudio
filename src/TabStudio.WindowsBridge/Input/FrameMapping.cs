namespace TabStudio.WindowsBridge.Input;

/// <summary>
/// Where a window's picture is drawn in the tablet's pane, so a position in the pane becomes a position in the window
/// on the PC: the picture is scaled to fit and centered, and the window may be larger than the frames it is sent in.
/// </summary>
/// <param name="Left">The picture's left edge in the pane, in device-independent pixels.</param>
/// <param name="Top">The picture's top edge in the pane.</param>
/// <param name="DrawnWidth">The picture's width in the pane.</param>
/// <param name="DrawnHeight">The picture's height in the pane.</param>
/// <param name="WindowWidth">The window's width in the PC's pixels.</param>
/// <param name="WindowHeight">The window's height in the PC's pixels.</param>
public readonly record struct FrameMapping(double Left, double Top, double DrawnWidth, double DrawnHeight, int WindowWidth, int WindowHeight)
{
    /// <summary>Whether the picture is drawn at all.</summary>
    public bool IsEmpty => DrawnWidth <= 0 || DrawnHeight <= 0 || WindowWidth <= 0 || WindowHeight <= 0;

    /// <summary>The PC's pixels per device-independent pixel of the pane.</summary>
    public double WindowPixelsPerPanePixel => IsEmpty ? 1 : WindowWidth / DrawnWidth;

    /// <summary>
    /// Fits a window into a pane: as large as the pane allows without growing past the window's own size, centered.
    /// </summary>
    /// <param name="paneWidth">The pane's width.</param>
    /// <param name="paneHeight">The pane's height.</param>
    /// <param name="windowWidth">The window's width in the PC's pixels.</param>
    /// <param name="windowHeight">The window's height in the PC's pixels.</param>
    /// <param name="pixelsPerDip">How many of the PC's pixels make one of the pane's, so a window at 96 DPI is not drawn twice as large.</param>
    public static FrameMapping Fit(double paneWidth, double paneHeight, int windowWidth, int windowHeight, double pixelsPerDip = 1)
    {
        if (paneWidth <= 0 || paneHeight <= 0 || windowWidth <= 0 || windowHeight <= 0)
        {
            return default;
        }

        var naturalWidth = windowWidth / Math.Max(pixelsPerDip, 0.1);
        var naturalHeight = windowHeight / Math.Max(pixelsPerDip, 0.1);
        var scale = Math.Min(1, Math.Min(paneWidth / naturalWidth, paneHeight / naturalHeight));
        var width = naturalWidth * scale;
        var height = naturalHeight * scale;
        return new FrameMapping((paneWidth - width) / 2, (paneHeight - height) / 2, width, height, windowWidth, windowHeight);
    }

    /// <summary>Whether a position in the pane is on the picture.</summary>
    /// <param name="x">The position from the pane's left edge.</param>
    /// <param name="y">The position from the pane's top edge.</param>
    public bool Contains(double x, double y) =>
        !IsEmpty && x >= Left && y >= Top && x < Left + DrawnWidth && y < Top + DrawnHeight;

    /// <summary>The window's pixel under a position in the pane, kept inside the window.</summary>
    /// <param name="x">The position from the pane's left edge.</param>
    /// <param name="y">The position from the pane's top edge.</param>
    public (int X, int Y) ToWindow(double x, double y)
    {
        if (IsEmpty)
        {
            return (0, 0);
        }

        var wx = (int)Math.Floor((x - Left) * WindowWidth / DrawnWidth);
        var wy = (int)Math.Floor((y - Top) * WindowHeight / DrawnHeight);
        return (Math.Clamp(wx, 0, WindowWidth - 1), Math.Clamp(wy, 0, WindowHeight - 1));
    }
}
