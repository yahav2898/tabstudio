using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>An image made of pixels: read from a file or stream, drawn on with <see cref="Graphics.FromImage"/>, or set pixel by pixel.</summary>
public sealed class Bitmap : Image
{
    private readonly List<BitmapData> _locks = [];

    /// <summary>Creates a bitmap of transparent black pixels.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public Bitmap(int width, int height)
        : this(width, height, PixelFormat.Format32bppArgb)
    {
    }

    /// <summary>Creates a bitmap of black pixels in a pixel format, transparent when the format has alpha.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="format">The pixel format.</param>
    public Bitmap(int width, int height, PixelFormat format)
        : base(Blank(width, height, format), ImageFormat.MemoryBmp, format == PixelFormat.DontCare ? PixelFormat.Format32bppArgb : format)
    {
    }

    /// <summary>Creates a bitmap with the resolution of a surface.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="g">The surface.</param>
    public Bitmap(int width, int height, Graphics g)
        : this(width, height, PixelFormat.Format32bppPArgb)
    {
        ArgumentNullException.ThrowIfNull(g);
        SetResolution(g.DpiX, g.DpiY);
    }

    /// <summary>Creates a bitmap of pixels in memory, which are copied.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="stride">The bytes from one row to the next.</param>
    /// <param name="format">The pixel format: 32bppArgb, 32bppPArgb, 32bppRgb or 24bppRgb.</param>
    /// <param name="scan0">The first row.</param>
    public Bitmap(int width, int height, int stride, PixelFormat format, IntPtr scan0)
        : this(width, height, format)
    {
        if (scan0 != IntPtr.Zero)
        {
            var pixels = Buffer.Pixels();
            PixelConversion.CopyIn(pixels, width, 0, 0, width, height, Layout(format), scan0, stride);
            Buffer.PixelsChanged();
        }
    }

    /// <summary>Reads a bitmap from a file: PNG, JPEG, BMP, GIF (its first frame), ICO or WebP.</summary>
    /// <param name="filename">The file's path.</param>
    public Bitmap(string filename)
        : base(ReadFile(filename), FormatOf(filename), PixelFormatOf(FormatOf(filename)))
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
        : this(Sniff(stream ?? throw new ArgumentNullException(nameof(stream))))
    {
    }

    /// <summary>Reads a bitmap from a stream.</summary>
    /// <param name="stream">The image's bytes.</param>
    /// <param name="useIcm">Ignored.</param>
    public Bitmap(Stream stream, bool useIcm)
        : this(stream)
    {
    }

    /// <summary>Reads a bitmap embedded in an assembly as a manifest resource.</summary>
    /// <param name="type">A type of the assembly; the resource's name starts with its namespace.</param>
    /// <param name="resource">The resource's name after the namespace.</param>
    public Bitmap(Type type, string resource)
        : this(OpenResource(type, resource))
    {
    }

    /// <summary>Creates a copy of an image, with 32 bits per pixel and alpha.</summary>
    /// <param name="original">The image to copy.</param>
    public Bitmap(Image original)
        : base((original ?? throw new ArgumentNullException(nameof(original))).Buffer.Clone(), ImageFormat.MemoryBmp)
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

    private Bitmap(ImageBuffer buffer, ImageFormat format, PixelFormat pixelFormat)
        : base(buffer, format, pixelFormat)
    {
    }

    private Bitmap((ImageBuffer Buffer, ImageFormat Format) decoded)
        : base(decoded.Buffer, decoded.Format, PixelFormatOf(decoded.Format))
    {
    }

    /// <summary>The color of a pixel, with everything drawn on the bitmap so far.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    public Color GetPixel(int x, int y) => Color.FromArgb(Buffer.GetPixel(x, y));

    /// <summary>Sets the color of a pixel.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="color">The color; a pixel format without alpha keeps it opaque.</param>
    public void SetPixel(int x, int y, Color color) => Buffer.SetPixel(x, y, color.ToArgb());

    /// <summary>Makes the color of the lower-left pixel transparent everywhere, as Windows Forms does.</summary>
    public void MakeTransparent() => MakeTransparent(Color.FromArgb(Buffer.GetPixel(0, Height - 1)));

    /// <summary>Makes a color transparent everywhere in the bitmap; the bitmap then has alpha.</summary>
    /// <param name="transparentColor">The color.</param>
    public void MakeTransparent(Color transparentColor)
    {
        Buffer.MakeTransparent(transparentColor.ToArgb());
        SetPixelFormat(PixelFormat.Format32bppArgb);
    }

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
    /// <param name="format">The new bitmap's pixel format.</param>
    /// <exception cref="OutOfMemoryException">The part is not inside the bitmap, as GDI+ reports it.</exception>
    public Bitmap Clone(Rectangle rect, PixelFormat format)
    {
        var target = format == PixelFormat.DontCare ? PixelFormat : format;
        return new Bitmap(Buffer.Crop(rect.X, rect.Y, rect.Width, rect.Height), ImageFormat.MemoryBmp, target);
    }

    /// <summary>Copies part of the bitmap into a new bitmap.</summary>
    /// <param name="rect">The part to copy, in pixels; rounded to whole pixels.</param>
    /// <param name="format">The new bitmap's pixel format.</param>
    public Bitmap Clone(RectangleF rect, PixelFormat format) => Clone(Rectangle.Round(rect), format);

    /// <inheritdoc />
    public override object Clone() => new Bitmap(Buffer.Clone(), RawFormat, PixelFormat);

    /// <summary>
    /// Locks a rectangle of pixels into memory in a pixel format, to read or change them with
    /// <see cref="Marshal.Copy(IntPtr, byte[], int, int)"/>. Changes are written back by <see cref="UnlockBits"/>.
    /// </summary>
    /// <param name="rect">The rectangle, inside the bitmap.</param>
    /// <param name="flags">Whether the pixels are read, written or both.</param>
    /// <param name="format">The layout of the memory: 32bppArgb, 32bppPArgb, 32bppRgb or 24bppRgb.</param>
    /// <exception cref="ArgumentException">The rectangle is outside the bitmap, or the format cannot be locked.</exception>
    public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format) => LockBits(rect, flags, format, new BitmapData());

    /// <summary>Locks a rectangle of pixels into memory, into a <see cref="BitmapData"/> the caller gives.</summary>
    /// <param name="rect">The rectangle, inside the bitmap.</param>
    /// <param name="flags">Whether the pixels are read, written or both; with <see cref="ImageLockMode.UserInputBuffer"/>, into the caller's memory.</param>
    /// <param name="format">The layout of the memory.</param>
    /// <param name="bitmapData">Receives the memory's address and layout.</param>
    public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format, BitmapData bitmapData)
    {
        ArgumentNullException.ThrowIfNull(bitmapData);
        if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0 || rect.Right > Width || rect.Bottom > Height)
        {
            throw new ArgumentException("Parameter is not valid: the rectangle is not inside the bitmap.", nameof(rect));
        }

        if (_locks.Count > 0)
        {
            throw new InvalidOperationException("Bitmap region is already locked.");
        }

        var layout = Layout(format == PixelFormat.DontCare ? PixelFormat : format);
        var bytesPerPixel = layout == PixelLayout.Rgb24 ? 3 : 4;
        var stride = ((rect.Width * bytesPerPixel) + 3) & ~3;
        var user = (flags & ImageLockMode.UserInputBuffer) != 0 && bitmapData.Scan0 != IntPtr.Zero;
        if (!user)
        {
            bitmapData.Scan0 = Marshal.AllocHGlobal(stride * rect.Height);
            bitmapData.Stride = stride;
        }
        else if (bitmapData.Stride == 0)
        {
            bitmapData.Stride = stride;
        }

        bitmapData.Width = rect.Width;
        bitmapData.Height = rect.Height;
        bitmapData.PixelFormat = format == PixelFormat.DontCare ? PixelFormat : format;
        bitmapData.Rectangle = rect;
        bitmapData.Mode = flags;
        bitmapData.OwnsMemory = !user;

        // Write-only locks start from the current pixels too, so rows the app leaves alone keep them.
        PixelConversion.CopyOut(Buffer.Pixels(), Width, rect.X, rect.Y, rect.Width, rect.Height, layout, bitmapData.Scan0, bitmapData.Stride);
        _locks.Add(bitmapData);
        return bitmapData;
    }

    /// <summary>Unlocks pixels locked by <see cref="LockBits(Rectangle, ImageLockMode, PixelFormat)"/>, writing back any changes.</summary>
    /// <param name="bitmapdata">What <c>LockBits</c> returned.</param>
    public void UnlockBits(BitmapData bitmapdata)
    {
        ArgumentNullException.ThrowIfNull(bitmapdata);
        if (!_locks.Remove(bitmapdata))
        {
            throw new ArgumentException("Parameter is not valid: the bitmap data was not locked from this bitmap.", nameof(bitmapdata));
        }

        try
        {
            if ((bitmapdata.Mode & ImageLockMode.WriteOnly) != 0)
            {
                var rect = bitmapdata.Rectangle;
                var pixels = Buffer.Pixels();
                PixelConversion.CopyIn(pixels, Width, rect.X, rect.Y, rect.Width, rect.Height, Layout(bitmapdata.PixelFormat), bitmapdata.Scan0, bitmapdata.Stride);
                if (Buffer.Opaque)
                {
                    for (var row = rect.Y; row < rect.Bottom; row++)
                    {
                        for (var column = rect.X; column < rect.Right; column++)
                        {
                            pixels[(row * Width) + column] |= unchecked((int)0xFF000000);
                        }
                    }
                }

                Buffer.PixelsChanged();
            }
        }
        finally
        {
            if (bitmapdata.OwnsMemory)
            {
                Marshal.FreeHGlobal(bitmapdata.Scan0);
                bitmapdata.Scan0 = IntPtr.Zero;
            }
        }
    }

    /// <summary>GDI bitmap handles do not exist on this device.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IntPtr GetHbitmap() =>
        throw new NotSupportedException("Bitmap.GetHbitmap is not supported by TabStudio's Forms layer: there are no GDI handles on this device; draw the bitmap with Graphics.DrawImage.");

    /// <summary>GDI bitmap handles do not exist on this device.</summary>
    /// <param name="background">The background color.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IntPtr GetHbitmap(Color background) => GetHbitmap();

    /// <summary>Icon handles do not exist on this device.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IntPtr GetHicon() =>
        throw new NotSupportedException("Bitmap.GetHicon is not supported by TabStudio's Forms layer: there are no icon handles on this device; draw the bitmap itself, or save it with ImageFormat.Icon.");

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        foreach (var locked in _locks.Where(locked => locked.OwnsMemory))
        {
            Marshal.FreeHGlobal(locked.Scan0);
        }

        _locks.Clear();
        base.Dispose(disposing);
    }

    private static PixelLayout Layout(PixelFormat format) => format switch
    {
        PixelFormat.Format32bppArgb => PixelLayout.Argb32,
        PixelFormat.Format32bppPArgb => PixelLayout.PArgb32,
        PixelFormat.Format32bppRgb => PixelLayout.Rgb32,
        PixelFormat.Format24bppRgb => PixelLayout.Rgb24,
        _ => throw new ArgumentException($"Parameter is not valid: TabStudio's Forms layer locks bitmaps as Format32bppArgb, Format32bppPArgb, Format32bppRgb or Format24bppRgb, not {format}.", nameof(format)),
    };

    private static ImageBuffer Blank(int width, int height, PixelFormat format)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Parameter is not valid: the width and height must be positive.");
        }

        return new ImageBuffer(width, height, opaque: format != PixelFormat.DontCare && (format & PixelFormat.Alpha) == 0 && (format & PixelFormat.Indexed) == 0);
    }

    private static ImageBuffer ReadFile(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        using var stream = File.OpenRead(filename);
        return ImageBuffer.Decode(stream);
    }

    private static (ImageBuffer Buffer, ImageFormat Format) Sniff(Stream stream)
    {
        // The format is read from the first bytes, as GDI+ reports it in RawFormat.
        if (!stream.CanSeek)
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            return Sniff(copy);
        }

        var source = stream;
        var start = source.Position;
        Span<byte> header = stackalloc byte[4];
        var read = source.ReadAtLeast(header, 4, throwOnEndOfStream: false);
        source.Position = start;
        var format = read < 2 ? ImageFormat.Png : header switch
        {
            [0xFF, 0xD8, ..] => ImageFormat.Jpeg,
            [(byte)'B', (byte)'M', ..] => ImageFormat.Bmp,
            [(byte)'G', (byte)'I', (byte)'F', ..] => ImageFormat.Gif,
            [0, 0, 1, 0] => ImageFormat.Icon,
            [(byte)'R', (byte)'I', (byte)'F', (byte)'F'] => ImageFormat.Webp,
            _ => ImageFormat.Png,
        };
        return (ImageBuffer.Decode(source), format);
    }

    private static Stream OpenResource(Type type, string resource)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(resource);
        var assembly = type.Assembly;
        var name = type.Namespace is { Length: > 0 } ns ? $"{ns}.{resource}" : resource;
        return assembly.GetManifestResourceStream(name)
            ?? assembly.GetManifestResourceStream(resource)
            ?? throw new ArgumentException($"Resource '{resource}' cannot be found in class '{type.FullName}'.");
    }

    private static ImageFormat FormatOf(string filename) => Path.GetExtension(filename).ToUpperInvariant() switch
    {
        ".JPG" or ".JPEG" or ".JPE" or ".JFIF" => ImageFormat.Jpeg,
        ".BMP" or ".DIB" => ImageFormat.Bmp,
        ".GIF" => ImageFormat.Gif,
        ".ICO" => ImageFormat.Icon,
        ".WEBP" => ImageFormat.Webp,
        _ => ImageFormat.Png,
    };

    private static PixelFormat PixelFormatOf(ImageFormat format) =>
        format.Equals(ImageFormat.Jpeg) ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb;
}
