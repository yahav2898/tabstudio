using System.ComponentModel;
using System.Drawing.Imaging;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>An image: the base of <see cref="Bitmap"/>.</summary>
[ImmutableObject(true)]
public abstract class Image : MarshalByRefObject, ICloneable, IDisposable
{
    private ImageBuffer? _buffer;
    private PixelFormat _pixelFormat;

    private protected Image(ImageBuffer buffer, ImageFormat format, PixelFormat pixelFormat = PixelFormat.Format32bppArgb)
    {
        _buffer = buffer;
        RawFormat = format;
        _pixelFormat = pixelFormat;
        buffer.Opaque = (pixelFormat & PixelFormat.Alpha) == 0 && pixelFormat != PixelFormat.Undefined;
    }

    /// <summary>A callback GDI+ calls while making a thumbnail, to cancel it. Never called on this device.</summary>
    public delegate bool GetThumbnailImageAbort();

    /// <summary>The width in pixels.</summary>
    public int Width => Buffer.Width;

    /// <summary>The height in pixels.</summary>
    public int Height => Buffer.Height;

    /// <summary>The size in pixels.</summary>
    public Size Size => new(Width, Height);

    /// <summary>The size in pixels, as floating-point values.</summary>
    public SizeF PhysicalDimension => new(Width, Height);

    /// <summary>The horizontal resolution in pixels per inch: 96 unless set.</summary>
    public float HorizontalResolution { get; private protected set; } = 96;

    /// <summary>The vertical resolution in pixels per inch: 96 unless set.</summary>
    public float VerticalResolution { get; private protected set; } = 96;

    /// <summary>The file format the image was read from, or <see cref="ImageFormat.MemoryBmp"/> for one made in memory.</summary>
    public ImageFormat RawFormat { get; }

    /// <summary>The pixel format: <see cref="PixelFormat.Format32bppArgb"/> unless the image was made or read with another.</summary>
    public PixelFormat PixelFormat => _pixelFormat;

    /// <summary>Flags about the pixels, as GDI+ reports them: <c>ImageFlagsHasAlpha</c> (2) when the image has alpha.</summary>
    public int Flags => IsAlphaPixelFormat(_pixelFormat) ? 0x2 : 0;

    /// <summary>The identifiers of the image's metadata items: none.</summary>
    public int[] PropertyIdList => [];

    /// <summary>Data about the image.</summary>
    public object? Tag { get; set; }

    /// <summary>The pixels.</summary>
    /// <exception cref="ArgumentException">The image was disposed.</exception>
    internal ImageBuffer Buffer => _buffer ?? throw new ArgumentException("Parameter is not valid: the image was disposed.");

    /// <summary>Reads an image from a file: PNG, JPEG, BMP, GIF, ICO or WebP.</summary>
    /// <param name="filename">The file's path.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public static Image FromFile(string filename) => new Bitmap(filename);

    /// <summary>Reads an image from a file.</summary>
    /// <param name="filename">The file's path.</param>
    /// <param name="useEmbeddedColorManagement">Ignored.</param>
    public static Image FromFile(string filename, bool useEmbeddedColorManagement) => new Bitmap(filename);

    /// <summary>Reads an image from a stream.</summary>
    /// <param name="stream">The image's bytes.</param>
    public static Image FromStream(Stream stream) => new Bitmap(stream);

    /// <summary>Reads an image from a stream.</summary>
    /// <param name="stream">The image's bytes.</param>
    /// <param name="useEmbeddedColorManagement">Ignored.</param>
    public static Image FromStream(Stream stream, bool useEmbeddedColorManagement) => new Bitmap(stream);

    /// <summary>Reads an image from a stream.</summary>
    /// <param name="stream">The image's bytes.</param>
    /// <param name="useEmbeddedColorManagement">Ignored.</param>
    /// <param name="validateImageData">Ignored: the data is always validated.</param>
    public static Image FromStream(Stream stream, bool useEmbeddedColorManagement, bool validateImageData) => new Bitmap(stream);

    /// <summary>The number of bits per pixel of a pixel format.</summary>
    /// <param name="pixfmt">The pixel format.</param>
    public static int GetPixelFormatSize(PixelFormat pixfmt) => ((int)pixfmt >> 8) & 0xFF;

    /// <summary>Whether a pixel format has alpha.</summary>
    /// <param name="pixfmt">The pixel format.</param>
    public static bool IsAlphaPixelFormat(PixelFormat pixfmt) => (pixfmt & PixelFormat.Alpha) != 0;

    /// <summary>Whether a pixel format is 32 bits per pixel with alpha.</summary>
    /// <param name="pixfmt">The pixel format.</param>
    public static bool IsCanonicalPixelFormat(PixelFormat pixfmt) => (pixfmt & PixelFormat.Canonical) != 0;

    /// <summary>Whether a pixel format has 16 bits per channel.</summary>
    /// <param name="pixfmt">The pixel format.</param>
    public static bool IsExtendedPixelFormat(PixelFormat pixfmt) => (pixfmt & PixelFormat.Extended) != 0;

    /// <summary>Saves the image to a file in the format it was read in, or as PNG if it was made in memory.</summary>
    /// <param name="filename">The file's path.</param>
    public void Save(string filename) => Save(filename, RawFormat.Equals(ImageFormat.MemoryBmp) ? ImageFormat.Png : RawFormat);

    /// <summary>Saves the image to a file in a format: PNG, JPEG, BMP, ICO or WebP. GIF and TIFF are written as PNG.</summary>
    /// <param name="filename">The file's path.</param>
    /// <param name="format">The format.</param>
    public void Save(string filename, ImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(format);
        using var stream = File.Create(filename);
        Save(stream, format);
    }

    /// <summary>Saves the image to a stream in a format: PNG, JPEG, BMP, ICO or WebP. GIF and TIFF are written as PNG.</summary>
    /// <param name="stream">Where to write.</param>
    /// <param name="format">The format.</param>
    public void Save(Stream stream, ImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(format);
        Buffer.Save(stream, FileFormat(format));
    }

    /// <summary>Saves the image to a file with an encoder and its settings, such as a JPEG quality.</summary>
    /// <param name="filename">The file's path.</param>
    /// <param name="encoder">The encoder, from <see cref="ImageCodecInfo.GetImageEncoders"/>.</param>
    /// <param name="encoderParams">The settings, or <see langword="null"/>.</param>
    public void Save(string filename, ImageCodecInfo encoder, EncoderParameters? encoderParams)
    {
        ArgumentNullException.ThrowIfNull(filename);
        using var stream = File.Create(filename);
        Save(stream, encoder, encoderParams);
    }

    /// <summary>Saves the image to a stream with an encoder and its settings, such as a JPEG quality.</summary>
    /// <param name="stream">Where to write.</param>
    /// <param name="encoder">The encoder, from <see cref="ImageCodecInfo.GetImageEncoders"/>.</param>
    /// <param name="encoderParams">The settings, or <see langword="null"/>.</param>
    public void Save(Stream stream, ImageCodecInfo encoder, EncoderParameters? encoderParams)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(encoder);
        Buffer.Save(stream, FileFormat(new ImageFormat(encoder.FormatID)), encoderParams?.Quality ?? 75);
    }

    /// <summary>A copy of the image scaled to a size.</summary>
    /// <param name="thumbWidth">The width.</param>
    /// <param name="thumbHeight">The height.</param>
    /// <param name="callback">Ignored.</param>
    /// <param name="callbackData">Ignored.</param>
    public Image GetThumbnailImage(int thumbWidth, int thumbHeight, GetThumbnailImageAbort? callback, IntPtr callbackData) =>
        new Bitmap(this, thumbWidth, thumbHeight);

    /// <summary>The image's bounds, in pixels.</summary>
    /// <param name="pageUnit">Receives the unit: pixels.</param>
    public RectangleF GetBounds(ref GraphicsUnit pageUnit)
    {
        pageUnit = GraphicsUnit.Pixel;
        return new RectangleF(0, 0, Width, Height);
    }

    /// <summary>The number of frames of a dimension: 1, since only the first frame of an animation is read.</summary>
    /// <param name="dimension">The dimension.</param>
    public int GetFrameCount(FrameDimension dimension) => 1;

    /// <summary>Selects a frame: the only one.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="frameIndex">The frame.</param>
    public int SelectActiveFrame(FrameDimension dimension, int frameIndex) => 0;

    /// <summary>Rotates, flips, or rotates and flips the image.</summary>
    /// <param name="rotateFlipType">How.</param>
    public void RotateFlip(RotateFlipType rotateFlipType)
    {
        var buffer = Buffer;
        var (rotation, flip) = ((int)rotateFlipType & 3, (int)rotateFlipType >= 4);
        if (rotation == 0 && !flip)
        {
            return;
        }

        var (width, height) = (buffer.Width, buffer.Height);
        var source = buffer.Pixels();
        var (newWidth, newHeight) = rotation % 2 == 1 ? (height, width) : (width, height);
        var target = new int[source.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Rotate clockwise, then flip horizontally, as GDI+ does.
                var (nx, ny) = rotation switch
                {
                    1 => (height - 1 - y, x),
                    2 => (width - 1 - x, height - 1 - y),
                    3 => (y, width - 1 - x),
                    _ => (x, y),
                };
                if (flip)
                {
                    nx = newWidth - 1 - nx;
                }

                target[(ny * newWidth) + nx] = source[(y * width) + x];
            }
        }

        _buffer = ImageBuffer.FromPixels(newWidth, newHeight, target, buffer.Opaque);
    }

    /// <summary>Creates a copy of the image.</summary>
    public abstract object Clone();

    /// <summary>Releases the image's pixels.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the image's pixels.</summary>
    /// <param name="disposing">Whether this is called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        _buffer?.DiscardPendingDrawing();
        _buffer = null;
    }

    /// <summary>The pixels to draw now.</summary>
    internal ImageSnapshot Snapshot() => Buffer.Snapshot();

    /// <summary>Changes the pixel format, as making a color transparent does.</summary>
    /// <param name="format">The new format.</param>
    private protected void SetPixelFormat(PixelFormat format)
    {
        _pixelFormat = format;
        Buffer.Opaque = (format & PixelFormat.Alpha) == 0;
    }

    private static ImageFileFormat FileFormat(ImageFormat format)
    {
        if (format.Equals(ImageFormat.Jpeg))
        {
            return ImageFileFormat.Jpeg;
        }

        if (format.Equals(ImageFormat.Bmp) || format.Equals(ImageFormat.MemoryBmp))
        {
            return ImageFileFormat.Bmp;
        }

        if (format.Equals(ImageFormat.Icon))
        {
            return ImageFileFormat.Icon;
        }

        if (format.Equals(ImageFormat.Gif))
        {
            return ImageFileFormat.Gif;
        }

        return format.Equals(ImageFormat.Webp) ? ImageFileFormat.Webp : ImageFileFormat.Png;
    }
}
