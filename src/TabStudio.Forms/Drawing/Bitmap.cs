using System.Drawing.Imaging;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>An image made of pixels, read from a file or stream, or drawn pixel by pixel.</summary>
public sealed class Bitmap : Image
{
    /// <summary>Creates a bitmap of transparent black pixels.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public Bitmap(int width, int height)
        : base(Blank(width, height), ImageFormat.MemoryBmp)
    {
    }

    /// <summary>Creates a bitmap of transparent black pixels.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="format">The pixel format; every bitmap has 32 bits per pixel with alpha on this device.</param>
    public Bitmap(int width, int height, PixelFormat format)
        : this(width, height)
    {
    }

    /// <summary>Reads a bitmap from a file: PNG, JPEG, BMP, GIF (its first frame) or WebP.</summary>
    /// <param name="filename">The file's path.</param>
    public Bitmap(string filename)
        : base(ReadFile(filename), FormatOf(filename))
    {
    }

    /// <summary>Reads a bitmap from a file.</summary>
    /// <param name="filename">The file's path.</param>
    /// <param name="useIcm">Ignored.</param>
    public Bitmap(string filename, bool useIcm)
        : this(filename)
    {
    }

    /// <summary>Reads a bitmap from a stream.</summary>
    /// <param name="stream">The image's bytes.</param>
    public Bitmap(Stream stream)
        : base(ImageBuffer.Decode(stream ?? throw new ArgumentNullException(nameof(stream))), ImageFormat.Png)
    {
    }

    /// <summary>Reads a bitmap from a stream.</summary>
    /// <param name="stream">The image's bytes.</param>
    /// <param name="useIcm">Ignored.</param>
    public Bitmap(Stream stream, bool useIcm)
        : this(stream)
    {
    }

    /// <summary>Creates a copy of an image.</summary>
    /// <param name="original">The image to copy.</param>
    public Bitmap(Image original)
        : base((original ?? throw new ArgumentNullException(nameof(original))).Buffer.Clone(), original.RawFormat)
    {
    }

    /// <summary>Creates a copy of an image scaled to a size.</summary>
    /// <param name="original">The image to copy.</param>
    /// <param name="newSize">The new size in pixels.</param>
    public Bitmap(Image original, Size newSize)
        : this(original, newSize.Width, newSize.Height)
    {
    }

    /// <summary>Creates a copy of an image scaled to a size.</summary>
    /// <param name="original">The image to copy.</param>
    /// <param name="width">The new width in pixels.</param>
    /// <param name="height">The new height in pixels.</param>
    public Bitmap(Image original, int width, int height)
        : base((original ?? throw new ArgumentNullException(nameof(original))).Buffer.Scaled(width, height), ImageFormat.MemoryBmp)
    {
    }

    private Bitmap(ImageBuffer buffer, ImageFormat format)
        : base(buffer, format)
    {
    }

    /// <summary>The color of a pixel.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    public Color GetPixel(int x, int y) => Color.FromArgb(Buffer.GetPixel(x, y));

    /// <summary>Sets the color of a pixel.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="color">The color.</param>
    public void SetPixel(int x, int y, Color color) => Buffer.SetPixel(x, y, color.ToArgb());

    /// <summary>Makes the color of the lower-left pixel transparent everywhere, as Windows Forms does.</summary>
    public void MakeTransparent() => Buffer.MakeTransparent(Buffer.GetPixel(0, Height - 1));

    /// <summary>Makes a color transparent everywhere in the bitmap.</summary>
    /// <param name="transparentColor">The color.</param>
    public void MakeTransparent(Color transparentColor) => Buffer.MakeTransparent(transparentColor.ToArgb());

    /// <summary>Sets the resolution. Bitmaps are drawn pixel for pixel, so this only changes the reported values.</summary>
    /// <param name="xDpi">The horizontal resolution.</param>
    /// <param name="yDpi">The vertical resolution.</param>
    public void SetResolution(float xDpi, float yDpi)
    {
        HorizontalResolution = xDpi;
        VerticalResolution = yDpi;
    }

    /// <summary>Copies part of the bitmap into a new bitmap.</summary>
    /// <param name="rect">The part to copy, in pixels.</param>
    /// <param name="format">The pixel format; every bitmap has 32 bits per pixel with alpha on this device.</param>
    public Bitmap Clone(Rectangle rect, PixelFormat format)
    {
        var copy = new Bitmap(rect.Width, rect.Height);
        for (var y = 0; y < rect.Height; y++)
        {
            for (var x = 0; x < rect.Width; x++)
            {
                copy.Buffer.SetPixel(x, y, Buffer.GetPixel(rect.X + x, rect.Y + y));
            }
        }

        return copy;
    }

    /// <inheritdoc />
    public override object Clone() => new Bitmap(Buffer.Clone(), RawFormat);

    private static ImageBuffer Blank(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Parameter is not valid: the width and height must be positive.");
        }

        return new ImageBuffer(width, height);
    }

    private static ImageBuffer ReadFile(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        using var stream = File.OpenRead(filename);
        return ImageBuffer.Decode(stream);
    }

    private static ImageFormat FormatOf(string filename) => Path.GetExtension(filename).ToUpperInvariant() switch
    {
        ".JPG" or ".JPEG" => ImageFormat.Jpeg,
        ".BMP" => ImageFormat.Bmp,
        ".GIF" => ImageFormat.Gif,
        ".ICO" => ImageFormat.Icon,
        ".WEBP" => ImageFormat.Webp,
        _ => ImageFormat.Png,
    };
}
