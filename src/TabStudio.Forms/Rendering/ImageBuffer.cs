using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using WriteableBitmap = Avalonia.Media.Imaging.WriteableBitmap;

namespace TabStudio.Forms.Rendering;

/// <summary>The pixels of an image at one moment, as a display list draws them. Immutable.</summary>
/// <param name="bitmap">The Avalonia bitmap. Owned by the snapshot's image buffer; never changed.</param>
internal sealed class ImageSnapshot(AvaloniaBitmap bitmap)
{
    /// <summary>The Avalonia bitmap to draw.</summary>
    public AvaloniaBitmap Bitmap { get; } = bitmap;

    /// <summary>The width in pixels.</summary>
    public int Width => Bitmap.PixelSize.Width;

    /// <summary>The height in pixels.</summary>
    public int Height => Bitmap.PixelSize.Height;
}

/// <summary>
/// The pixels behind a <see cref="System.Drawing.Image"/>: an image decoded from a file or stream, or pixels set one
/// by one, as <see cref="System.Drawing.Bitmap.SetPixel"/> does. Reading or writing a pixel of a decoded image copies
/// its pixels into an array first. Drawing takes a <see cref="Snapshot"/>, made again only after the pixels change.
/// </summary>
internal sealed class ImageBuffer
{
    private static readonly Vector Dpi = new(96, 96);

    private AvaloniaBitmap? _decoded;
    private int[]? _pixels;
    private ImageSnapshot? _snapshot;

    /// <summary>Creates a buffer over a decoded image.</summary>
    /// <param name="decoded">The decoded image.</param>
    public ImageBuffer(AvaloniaBitmap decoded)
    {
        _decoded = decoded;
        Width = decoded.PixelSize.Width;
        Height = decoded.PixelSize.Height;
    }

    /// <summary>Creates a buffer of transparent black pixels, as <c>new Bitmap(width, height)</c> does.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public ImageBuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        _pixels = new int[width * height];
    }

    private ImageBuffer(int width, int height, int[] pixels)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    /// <summary>The width in pixels.</summary>
    public int Width { get; }

    /// <summary>The height in pixels.</summary>
    public int Height { get; }

    /// <summary>Decodes an image file: PNG, JPEG, BMP, GIF (its first frame) or WebP.</summary>
    /// <param name="stream">The file's bytes.</param>
    /// <exception cref="ArgumentException">The bytes are not an image.</exception>
    public static ImageBuffer Decode(Stream stream)
    {
        try
        {
            return new ImageBuffer(new AvaloniaBitmap(stream));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not IOException)
        {
            throw new ArgumentException("Parameter is not valid: the data is not an image this device can decode.", nameof(stream), ex);
        }
    }

    /// <summary>A pixel as 0xAARRGGBB.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    public int GetPixel(int x, int y) => Pixels()[Index(x, y)];

    /// <summary>Sets a pixel.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="argb">The color as 0xAARRGGBB.</param>
    public void SetPixel(int x, int y, int argb)
    {
        Pixels()[Index(x, y)] = argb;
        Changed();
    }

    /// <summary>Makes every pixel of a color transparent.</summary>
    /// <param name="argb">The color as 0xAARRGGBB.</param>
    public void MakeTransparent(int argb)
    {
        var pixels = Pixels();
        for (var i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] == argb)
            {
                pixels[i] = 0;
            }
        }

        Changed();
    }

    /// <summary>The pixels to draw now. Made once per change.</summary>
    public ImageSnapshot Snapshot()
    {
        if (_snapshot is { } snapshot)
        {
            return snapshot;
        }

        if (_pixels is null && _decoded is { } decoded)
        {
            return _snapshot = new ImageSnapshot(decoded);
        }

        var handle = GCHandle.Alloc(_pixels!, GCHandleType.Pinned);
        try
        {
            var bitmap = new AvaloniaBitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, handle.AddrOfPinnedObject(), new PixelSize(Width, Height), Dpi, Width * 4);
            return _snapshot = new ImageSnapshot(bitmap);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>A copy with its own pixels.</summary>
    public ImageBuffer Clone() =>
        _pixels is { } pixels ? new ImageBuffer(Width, Height, (int[])pixels.Clone()) : new ImageBuffer(_decoded!);

    /// <summary>A copy scaled to a size, smoothly, as <c>new Bitmap(image, size)</c> makes.</summary>
    /// <param name="width">The new width.</param>
    /// <param name="height">The new height.</param>
    public ImageBuffer Scaled(int width, int height) =>
        width == Width && height == Height
            ? Clone()
            : new ImageBuffer(Snapshot().Bitmap.CreateScaledBitmap(new PixelSize(width, height)));

    /// <summary>Writes the image as a PNG file.</summary>
    /// <param name="stream">Where to write.</param>
    public void Save(Stream stream) => Snapshot().Bitmap.Save(stream);

    private void Changed() => _snapshot = null;

    private int Index(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "Parameter must be positive and < Width.");
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, "Parameter must be positive and < Height.");
        }

        return (y * Width) + x;
    }

    /// <summary>The pixel array, copied out of the decoded image the first time.</summary>
    private int[] Pixels()
    {
        if (_pixels is { } pixels)
        {
            return pixels;
        }

        var decoded = _decoded!;
        pixels = new int[Width * Height];
        using (var target = new WriteableBitmap(new PixelSize(Width, Height), Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul))
        {
            using var framebuffer = target.Lock();
            decoded.CopyPixels(framebuffer, AlphaFormat.Unpremul);
            for (var row = 0; row < Height; row++)
            {
                Marshal.Copy(framebuffer.Address + (row * framebuffer.RowBytes), pixels, row * Width, Width);
            }
        }

        _pixels = pixels;
        _decoded = null;
        _snapshot = null;
        return pixels;
    }
}
