using System.Windows.Forms;
using TabStudio.Forms.Runtime;

namespace System.Drawing;

/// <summary>Copying from the screen, which for an app in TabStudio is the app preview's desktop.</summary>
public sealed partial class Graphics
{
    /// <summary>
    /// Copies a part of the screen: the app's windows on the app preview's desktop, as they are drawn, without their
    /// frames. Other apps' windows are not on this desktop.
    /// </summary>
    /// <param name="upperLeftSource">The top left of the part, on the desktop.</param>
    /// <param name="upperLeftDestination">Where it goes on this surface.</param>
    /// <param name="blockRegionSize">The size of the part.</param>
    public void CopyFromScreen(Point upperLeftSource, Point upperLeftDestination, Size blockRegionSize) =>
        CopyFromScreen(upperLeftSource.X, upperLeftSource.Y, upperLeftDestination.X, upperLeftDestination.Y, blockRegionSize, CopyPixelOperation.SourceCopy);

    /// <summary>Copies a part of the screen.</summary>
    /// <param name="upperLeftSource">The top left of the part, on the desktop.</param>
    /// <param name="upperLeftDestination">Where it goes on this surface.</param>
    /// <param name="blockRegionSize">The size of the part.</param>
    /// <param name="copyPixelOperation">How pixels combine; only copying is supported.</param>
    public void CopyFromScreen(Point upperLeftSource, Point upperLeftDestination, Size blockRegionSize, CopyPixelOperation copyPixelOperation) =>
        CopyFromScreen(upperLeftSource.X, upperLeftSource.Y, upperLeftDestination.X, upperLeftDestination.Y, blockRegionSize, copyPixelOperation);

    /// <summary>Copies a part of the screen.</summary>
    /// <param name="sourceX">The left of the part, on the desktop.</param>
    /// <param name="sourceY">The top of the part, on the desktop.</param>
    /// <param name="destinationX">Where it goes on this surface: the left.</param>
    /// <param name="destinationY">Where it goes on this surface: the top.</param>
    /// <param name="blockRegionSize">The size of the part.</param>
    public void CopyFromScreen(int sourceX, int sourceY, int destinationX, int destinationY, Size blockRegionSize) =>
        CopyFromScreen(sourceX, sourceY, destinationX, destinationY, blockRegionSize, CopyPixelOperation.SourceCopy);

    /// <summary>Copies a part of the screen.</summary>
    /// <param name="sourceX">The left of the part, on the desktop.</param>
    /// <param name="sourceY">The top of the part, on the desktop.</param>
    /// <param name="destinationX">Where it goes on this surface: the left.</param>
    /// <param name="destinationY">Where it goes on this surface: the top.</param>
    /// <param name="blockRegionSize">The size of the part.</param>
    /// <param name="copyPixelOperation">How pixels combine; only copying is supported.</param>
    /// <exception cref="InvalidOperationException">No app is running on this thread.</exception>
    public void CopyFromScreen(int sourceX, int sourceY, int destinationX, int destinationY, Size blockRegionSize, CopyPixelOperation copyPixelOperation)
    {
        if (blockRegionSize.Width <= 0 || blockRegionSize.Height <= 0)
        {
            return;
        }

        var runtime = FormsRuntime.Current ?? throw new InvalidOperationException(
            "Graphics.CopyFromScreen needs the app's UI thread, which Application.Run starts; the screen is the app preview's desktop.");
        using var screen = new Bitmap(blockRegionSize.Width, blockRegionSize.Height);
        var buffer = screen.Buffer;
        using (var background = FromImage(screen))
        {
            background.Clear(SystemColors.Desktop);
        }

        var area = new Rectangle(Point.Empty, blockRegionSize);
        foreach (var window in runtime.Windows)
        {
            var form = window.Form;
            if (form.WindowState == FormWindowState.Minimized)
            {
                continue;
            }

            var client = form.ClientOriginOnScreen;
            var origin = new Point(client.X - sourceX, client.Y - sourceY);
            var visible = Rectangle.Intersect(area, new Rectangle(origin, form.ClientSize));
            if (visible.Width > 0 && visible.Height > 0)
            {
                form.PaintClient(buffer.Recorder, visible, origin);
            }
        }

        buffer.Recorded();
        buffer.Flush();
        var mode = CompositingMode;
        CompositingMode = copyPixelOperation == CopyPixelOperation.SourceCopy ? Drawing2D.CompositingMode.SourceCopy : mode;
        try
        {
            DrawImageUnscaled(screen, destinationX, destinationY);
        }
        finally
        {
            CompositingMode = mode;
        }
    }
}

/// <summary>How copied pixels combine with the pixels they cover.</summary>
#pragma warning disable CA1027, CA1008 // The values are the Windows raster operation codes.
public enum CopyPixelOperation
{
    /// <summary>Black.</summary>
    Blackness = 0x00000042,

    /// <summary>The inverse of the source OR the destination.</summary>
    NotSourceErase = 0x001100A6,

    /// <summary>The inverse of the source.</summary>
    NotSourceCopy = 0x00330008,

    /// <summary>The source AND the inverse of the destination.</summary>
    SourceErase = 0x00440328,

    /// <summary>The inverse of the destination.</summary>
    DestinationInvert = 0x00550009,

    /// <summary>The pattern XOR the destination.</summary>
    PatInvert = 0x005A0049,

    /// <summary>The source XOR the destination.</summary>
    SourceInvert = 0x00660046,

    /// <summary>The source AND the destination.</summary>
    SourceAnd = 0x008800C6,

    /// <summary>The inverse of the source OR the destination.</summary>
    MergePaint = 0x00BB0226,

    /// <summary>The source AND the pattern.</summary>
    MergeCopy = 0x00C000CA,

    /// <summary>The source, as it is.</summary>
    SourceCopy = 0x00CC0020,

    /// <summary>The source OR the destination.</summary>
    SourcePaint = 0x00EE0086,

    /// <summary>The pattern.</summary>
    PatCopy = 0x00F00021,

    /// <summary>The pattern OR the inverse of the source OR the destination.</summary>
    PatPaint = 0x00FB0A09,

    /// <summary>White.</summary>
    Whiteness = 0x00FF0062,

    /// <summary>Windows that are layered on top of this window are included.</summary>
    CaptureBlt = 0x40000000,

    /// <summary>The bitmap is not mirrored.</summary>
    NoMirrorBitmap = unchecked((int)0x80000000),
}
#pragma warning restore CA1027, CA1008
