using System.Drawing.Drawing2D;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>Drawing images and icons.</summary>
public sealed partial class Graphics
{
    /// <summary>Draws an image at its size, with its top left at a point.</summary>
    /// <param name="image">The image.</param>
    /// <param name="point">The top left.</param>
    public void DrawImage(Image image, PointF point) => DrawImage(image, point.X, point.Y);

    /// <summary>Draws an image at its size, with its top left at a point.</summary>
    /// <param name="image">The image.</param>
    /// <param name="point">The top left.</param>
    public void DrawImage(Image image, Point point) => DrawImage(image, point.X, point.Y);

    /// <summary>Draws an image at its size, with its top left at a point.</summary>
    /// <param name="image">The image.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    public void DrawImage(Image image, float x, float y)
    {
        ArgumentNullException.ThrowIfNull(image);
        DrawImage(image, new RectangleF(x, y, image.Width, image.Height));
    }

    /// <summary>Draws an image at its size, with its top left at a point.</summary>
    /// <param name="image">The image.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    public void DrawImage(Image image, int x, int y) => DrawImage(image, (float)x, y);

    /// <summary>Draws an image scaled into a rectangle.</summary>
    /// <param name="image">The image.</param>
    /// <param name="rect">The rectangle.</param>
    public void DrawImage(Image image, RectangleF rect)
    {
        ArgumentNullException.ThrowIfNull(image);
        Record(new ImageCommand(image.Snapshot(), rect, null, Smooth));
    }

    /// <summary>Draws an image scaled into a rectangle.</summary>
    /// <param name="image">The image.</param>
    /// <param name="rect">The rectangle.</param>
    public void DrawImage(Image image, Rectangle rect) => DrawImage(image, (RectangleF)rect);

    /// <summary>Draws an image scaled into a rectangle.</summary>
    /// <param name="image">The image.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void DrawImage(Image image, int x, int y, int width, int height) => DrawImage(image, new RectangleF(x, y, width, height));

    /// <summary>Draws an image scaled into a rectangle.</summary>
    /// <param name="image">The image.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void DrawImage(Image image, float x, float y, float width, float height) => DrawImage(image, new RectangleF(x, y, width, height));

    /// <summary>Draws part of an image into a rectangle.</summary>
    /// <param name="image">The image.</param>
    /// <param name="destRect">Where it is drawn.</param>
    /// <param name="srcRect">The part of the image, in pixels.</param>
    /// <param name="srcUnit">The unit of <paramref name="srcRect"/>: pixels.</param>
    public void DrawImage(Image image, RectangleF destRect, RectangleF srcRect, GraphicsUnit srcUnit)
    {
        ArgumentNullException.ThrowIfNull(image);
        Record(new ImageCommand(image.Snapshot(), destRect, srcRect, Smooth));
    }

    /// <summary>Draws part of an image into a rectangle.</summary>
    /// <param name="image">The image.</param>
    /// <param name="destRect">Where it is drawn.</param>
    /// <param name="srcRect">The part of the image, in pixels.</param>
    /// <param name="srcUnit">The unit of <paramref name="srcRect"/>: pixels.</param>
    public void DrawImage(Image image, Rectangle destRect, Rectangle srcRect, GraphicsUnit srcUnit) =>
        DrawImage(image, (RectangleF)destRect, srcRect, srcUnit);

    /// <summary>Draws part of an image into a rectangle.</summary>
    /// <param name="image">The image.</param>
    /// <param name="destRect">Where it is drawn.</param>
    /// <param name="srcX">The part's left, in pixels.</param>
    /// <param name="srcY">The part's top, in pixels.</param>
    /// <param name="srcWidth">The part's width, in pixels.</param>
    /// <param name="srcHeight">The part's height, in pixels.</param>
    /// <param name="srcUnit">The unit of the part: pixels.</param>
    public void DrawImage(Image image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit) =>
        DrawImage(image, (RectangleF)destRect, new RectangleF(srcX, srcY, srcWidth, srcHeight), srcUnit);

    /// <summary>Draws part of an image into a rectangle.</summary>
    /// <param name="image">The image.</param>
    /// <param name="destRect">Where it is drawn.</param>
    /// <param name="srcX">The part's left, in pixels.</param>
    /// <param name="srcY">The part's top, in pixels.</param>
    /// <param name="srcWidth">The part's width, in pixels.</param>
    /// <param name="srcHeight">The part's height, in pixels.</param>
    /// <param name="srcUnit">The unit of the part: pixels.</param>
    public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit) =>
        DrawImage(image, (RectangleF)destRect, new RectangleF(srcX, srcY, srcWidth, srcHeight), srcUnit);

    /// <summary>Draws part of an image at its size, with its top left at a point.</summary>
    /// <param name="image">The image.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="srcRect">The part of the image, in pixels.</param>
    /// <param name="srcUnit">The unit of <paramref name="srcRect"/>: pixels.</param>
    public void DrawImage(Image image, int x, int y, Rectangle srcRect, GraphicsUnit srcUnit) =>
        DrawImage(image, new RectangleF(x, y, srcRect.Width, srcRect.Height), srcRect, srcUnit);

    /// <summary>Draws an image into a parallelogram given by its upper-left, upper-right and lower-left corners.</summary>
    /// <param name="image">The image.</param>
    /// <param name="destPoints">The three corners.</param>
    public void DrawImage(Image image, PointF[] destPoints)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bounds = new RectangleF(0, 0, image.Width, image.Height);
        using var matrix = new Matrix(bounds, destPoints);
        var saved = _transform;
        _transform = matrix.Value * _transform;
        DrawImage(image, bounds);
        _transform = saved;
    }

    /// <summary>Draws an image into a parallelogram given by its upper-left, upper-right and lower-left corners.</summary>
    /// <param name="image">The image.</param>
    /// <param name="destPoints">The three corners.</param>
    public void DrawImage(Image image, Point[] destPoints)
    {
        ArgumentNullException.ThrowIfNull(destPoints);
        DrawImage(image, [.. destPoints.Select(p => (PointF)p)]);
    }

    /// <summary>Draws an image at its size.</summary>
    /// <param name="image">The image.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    public void DrawImageUnscaled(Image image, int x, int y) => DrawImage(image, x, y);

    /// <summary>Draws an image at its size.</summary>
    /// <param name="image">The image.</param>
    /// <param name="point">The top left.</param>
    public void DrawImageUnscaled(Image image, Point point) => DrawImage(image, point.X, point.Y);

    /// <summary>Draws an image at its size, with its top left at a rectangle's.</summary>
    /// <param name="image">The image.</param>
    /// <param name="rect">The rectangle.</param>
    public void DrawImageUnscaled(Image image, Rectangle rect) => DrawImage(image, rect.X, rect.Y);

    /// <summary>Draws an image at its size, clipped to a rectangle.</summary>
    /// <param name="image">The image.</param>
    /// <param name="rect">The rectangle.</param>
    public void DrawImageUnscaledAndClipped(Image image, Rectangle rect)
    {
        ArgumentNullException.ThrowIfNull(image);
        var width = Math.Min(rect.Width, image.Width);
        var height = Math.Min(rect.Height, image.Height);
        DrawImage(image, new RectangleF(rect.X, rect.Y, width, height), new RectangleF(0, 0, width, height), GraphicsUnit.Pixel);
    }

    /// <summary>Draws an icon at its size.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    public void DrawIcon(Icon icon, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(icon);
        Record(new ImageCommand(icon.Snapshot(), new RectangleF(x, y, icon.Width, icon.Height), null, Smooth));
    }

    /// <summary>Draws an icon scaled into a rectangle.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="targetRect">The rectangle.</param>
    public void DrawIcon(Icon icon, Rectangle targetRect)
    {
        ArgumentNullException.ThrowIfNull(icon);
        Record(new ImageCommand(icon.Snapshot(), targetRect, null, Smooth));
    }

    /// <summary>Draws an icon at its size, clipped to a rectangle.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="targetRect">The rectangle.</param>
    public void DrawIconUnstretched(Icon icon, Rectangle targetRect)
    {
        ArgumentNullException.ThrowIfNull(icon);
        DrawIcon(icon, new Rectangle(targetRect.X, targetRect.Y, Math.Min(icon.Width, targetRect.Width), Math.Min(icon.Height, targetRect.Height)));
    }

    private bool Smooth => InterpolationMode != InterpolationMode.NearestNeighbor;
}
