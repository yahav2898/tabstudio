using System.ComponentModel;
using System.Drawing.Imaging;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>An image: the base of <see cref="Bitmap"/>.</summary>
[ImmutableObject(true)]
public abstract class Image : MarshalByRefObject, ICloneable, IDisposable
{
    private ImageBuffer? _buffer;

    private protected Image(ImageBuffer buffer, ImageFormat format)
    {
        _buffer = buffer;
        RawFormat = format;
    }

    /// <summary>The width in pixels.</summary>
    public int Width => Buffer.Width;

    /// <summary>The height in pixels.</summary>
    public int Height => Buffer.Height;

    /// <summary>The size in pixels.</summary>
    public Size Size => new(Width, Height);

    /// <summary>The size in pixels, as floating-point values.</summary>
    public SizeF PhysicalDimension => new(Width, Height);

    /// <summary>The horizontal resolution in pixels per inch: 96.</summary>
    public float HorizontalResolution { get; private protected set; } = 96;

    /// <summary>The vertical resolution in pixels per inch: 96.</summary>
    public float VerticalResolution { get; private protected set; } = 96;

    /// <summary>The file format the image was read from.</summary>
    public ImageFormat RawFormat { get; }

    /// <summary>The pixel format: 32 bits per pixel with alpha.</summary>
    public PixelFormat PixelFormat => PixelFormat.Format32bppArgb;

    /// <summary>Data about the image.</summary>
    public object? Tag { get; set; }

    /// <summary>The pixels.</summary>
    /// <exception cref="ArgumentException">The image was disposed.</exception>
    internal ImageBuffer Buffer => _buffer ?? throw new ArgumentException("Parameter is not valid: the image was disposed.");

    /// <summary>Reads an image from a file: PNG, JPEG, BMP, GIF or WebP.</summary>
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

    /// <summary>Saves the image to a file as a PNG.</summary>
    /// <param name="filename">The file's path.</param>
    public void Save(string filename) => Save(filename, ImageFormat.Png);

    /// <summary>Saves the image to a file. Every format is written as PNG on this device.</summary>
    /// <param name="filename">The file's path.</param>
    /// <param name="format">The format asked for.</param>
    public void Save(string filename, ImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(filename);
        using var stream = File.Create(filename);
        Save(stream, format);
    }

    /// <summary>Saves the image to a stream. Every format is written as PNG on this device.</summary>
    /// <param name="stream">Where to write.</param>
    /// <param name="format">The format asked for.</param>
    public void Save(Stream stream, ImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(format);
        Buffer.Save(stream);
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
    protected virtual void Dispose(bool disposing) => _buffer = null;

    /// <summary>The pixels to draw now.</summary>
    internal ImageSnapshot Snapshot() => Buffer.Snapshot();
}
