using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using TabStudio.App.Theming;

namespace TabStudio.App.Swarm;

/// <summary>Pictures of screens: a control drawn into a PNG, at the screen's density, optionally with an element outlined.</summary>
internal static class ScreenCapture
{
    /// <summary>The longest side of a picture, in pixels: as much detail as Claude's vision uses, at a size phones can send.</summary>
    public const int MaxPixels = 1568;

    /// <summary>
    /// The outline drawn around an element in a picture, such as the one a layout issue is about: the palette's
    /// <c>Brush.Swarm.Highlight</c>, the same in both themes because the picture is the app's, not the IDE's.
    /// </summary>
    private static IPen Outline => new ImmutablePen(new ImmutableSolidColorBrush(ThemeBrushes.ColorOf("Brush.Swarm.Highlight", ThemeVariant.Dark)), 3);

    /// <summary>Draws a control as it is laid out now.</summary>
    /// <param name="visual">The control.</param>
    /// <param name="scale">Physical pixels per device-independent pixel; lowered when the picture would be too big.</param>
    /// <param name="highlight">An area to outline, in the control's coordinates, if any.</param>
    /// <returns>A PNG image, or <see langword="null"/> when the control has no size.</returns>
    public static byte[]? Png(Visual visual, double scale, Rect? highlight = null)
    {
        ArgumentNullException.ThrowIfNull(visual);
        var size = visual.Bounds.Size;
        if (size.Width < 1 || size.Height < 1)
        {
            return null;
        }

        scale = Math.Clamp(Math.Min(scale, MaxPixels / Math.Max(size.Width, size.Height)), 0.1, 4);
        var pixels = new PixelSize((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale));
        using var bitmap = new RenderTargetBitmap(pixels, new Vector(96 * scale, 96 * scale));
        bitmap.Render(visual);
        if (highlight is { } area)
        {
            using var context = bitmap.CreateDrawingContext(clear: false);
            context.DrawRectangle(null, Outline, area.Inflate(2), 4, 4);
        }

        using var png = new MemoryStream();
        bitmap.Save(png);
        return png.ToArray();
    }

    /// <summary>Outlines an area in a picture another screen took, such as the web preview's browser.</summary>
    /// <param name="picture">The picture, as PNG.</param>
    /// <param name="screenWidth">The screen's width in device-independent pixels, which the area is measured in.</param>
    /// <param name="area">The area to outline.</param>
    /// <returns>The picture with the outline, as PNG.</returns>
    public static byte[] Outlined(byte[] picture, double screenWidth, Rect area)
    {
        ArgumentNullException.ThrowIfNull(picture);
        using var stream = new MemoryStream(picture);
        using var source = new Bitmap(stream);
        var pixels = source.PixelSize;
        var scale = screenWidth > 0 ? pixels.Width / screenWidth : 1;
        using var bitmap = new RenderTargetBitmap(pixels);
        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawImage(source, new Rect(0, 0, pixels.Width, pixels.Height));
            var outlined = new Rect(area.X * scale, area.Y * scale, area.Width * scale, area.Height * scale);
            context.DrawRectangle(null, Outline, outlined.Inflate(2), 4, 4);
        }

        using var png = new MemoryStream();
        bitmap.Save(png);
        return png.ToArray();
    }
}
