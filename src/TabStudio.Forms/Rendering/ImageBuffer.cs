using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace TabStudio.Forms.Rendering;

/// <summary>The pixels of an image at one moment, as a display list draws them. Immutable.</summary>
/// <param name="bitmap">The Avalonia bitmap. Owned by the snapshot's image buffer; never changed.</param>
internal sealed class ImageSnapshot(AvaloniaBitmap bitmap)
{
    private SKImage? _skia;

    /// <summary>The Avalonia bitmap to draw.</summary>
    public AvaloniaBitmap Bitmap { get; } = bitmap;

    /// <summary>The width in pixels.</summary>
    public int Width => Bitmap.PixelSize.Width;

    /// <summary>The height in pixels.</summary>
    public int Height => Bitmap.PixelSize.Height;

    /// <summary>The same pixels as a Skia image, for drawing into another image. Made once, on first use.</summary>
    public SKImage SkiaImage => _skia ??= CreateSkiaImage();

    private SKImage CreateSkiaImage()
    {
        var pixels = ImageBuffer.ReadPixels(Bitmap);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            return SKImage.FromPixelCopy(info, handle.AddrOfPinnedObject(), Width * 4);
        }
        finally
        {
            handle.Free();
        }
    }
}

/// <summary>
/// The pixels behind a <see cref="System.Drawing.Image"/>: an image decoded from a file or stream, pixels set one
/// by one as <see cref="System.Drawing.Bitmap.SetPixel"/> does, or drawing recorded by
/// <see cref="System.Drawing.Graphics.FromImage"/>.
/// </summary>
/// <remarks>
/// The pixels live in one of three places, and move only when something needs them elsewhere: the decoded image,
/// an array of unpremultiplied 0xAARRGGBB values (for pixel access, exact as GDI+ keeps them), or a drawing surface
/// (for drawing). Drawing is recorded and rasterized on the surface when anything reads the pixels, when the
/// <see cref="System.Drawing.Graphics"/> is flushed or disposed, or before the image is drawn, so drawing behaves as
/// if it were immediate. Drawing takes a <see cref="Snapshot"/>, made again only after the pixels change. Used on the
/// app's Forms thread.
/// </remarks>
internal sealed class ImageBuffer
{
    private static readonly Vector Dpi = new(96, 96);

    private readonly DisplayListBuilder _recorder = new();
    private AvaloniaBitmap? _decoded;
    private int[]? _pixels;
    private bool _pixelsCurrent;
    private RenderTargetBitmap? _surface;
    private bool _surfaceCurrent;
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
    /// <param name="opaque">Whether the pixel format has no alpha, so every pixel is opaque.</param>
    public ImageBuffer(int width, int height, bool opaque = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Opaque = opaque;
        _pixels = new int[width * height];
        if (opaque)
        {
            Array.Fill(_pixels, unchecked((int)0xFF000000));
        }

        _pixelsCurrent = true;
    }

    private ImageBuffer(int width, int height, int[] pixels, bool opaque)
    {
        Width = width;
        Height = height;
        Opaque = opaque;
        _pixels = pixels;
        _pixelsCurrent = true;
    }

    /// <summary>The width in pixels.</summary>
    public int Width { get; }

    /// <summary>The height in pixels.</summary>
    public int Height { get; }

    /// <summary>Whether every pixel is opaque, as in a pixel format without alpha such as <c>Format24bppRgb</c>.</summary>
    public bool Opaque { get; set; }

    /// <summary>Where a <see cref="System.Drawing.Graphics"/> of the image records its drawing.</summary>
    public DisplayListBuilder Recorder => _recorder;

    /// <summary>Whether drawing was recorded and not yet rasterized.</summary>
    public bool HasPendingDrawing => _recorder.Count > 0;

    /// <summary>Decodes an image file: PNG, JPEG, BMP, GIF (its first frame), ICO or WebP.</summary>
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

    /// <summary>Creates a buffer of pixels the caller hands over.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="pixels">The unpremultiplied 0xAARRGGBB pixels, row by row; owned by the buffer from now on.</param>
    /// <param name="opaque">Whether every pixel is opaque.</param>
    public static ImageBuffer FromPixels(int width, int height, int[] pixels, bool opaque) => new(width, height, pixels, opaque);

    /// <summary>Reads the pixels of an Avalonia bitmap as unpremultiplied 0xAARRGGBB values.</summary>
    /// <param name="bitmap">The bitmap.</param>
    public static int[] ReadPixels(AvaloniaBitmap bitmap)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        var pixels = new int[width * height];
        using var target = new WriteableBitmap(new PixelSize(width, height), Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var framebuffer = target.Lock();
        bitmap.CopyPixels(framebuffer, AlphaFormat.Unpremul);
        for (var row = 0; row < height; row++)
        {
            Marshal.Copy(framebuffer.Address + (row * framebuffer.RowBytes), pixels, row * width, width);
        }

        return pixels;
    }

    /// <summary>Rasterizes the drawing recorded so far into the pixels.</summary>
    public void Flush()
    {
        if (_recorder.Count == 0)
        {
            return;
        }

        var list = _recorder.Drain();
        var surface = Surface();
        BitmapRasterizer.Render(surface, list);
        _pixelsCurrent = false;
        _decoded = null;
        _snapshot = null;
    }

    /// <summary>Forgets drawing recorded and not yet rasterized, when the image is disposed.</summary>
    public void DiscardPendingDrawing() => _recorder.Clear();

    /// <summary>Called after drawing is recorded: the pixels the image shows have changed.</summary>
    public void Recorded() => _snapshot = null;

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
        var pixels = Pixels();
        pixels[Index(x, y)] = Opaque ? argb | unchecked((int)0xFF000000) : argb;
        PixelsChanged();
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

        Opaque = false;
        PixelsChanged();
    }

    /// <summary>The pixels, for code that reads or changes many of them. Call <see cref="PixelsChanged"/> after a change.</summary>
    public int[] Pixels()
    {
        Flush();
        if (_pixelsCurrent && _pixels is { } current)
        {
            return current;
        }

        _pixels = _surfaceCurrent && _surface is { } surface ? Download(surface) : ReadPixels(_decoded!);
        _pixelsCurrent = true;
        _decoded = null;
        return _pixels;
    }

    /// <summary>Called after the array <see cref="Pixels"/> returned was changed.</summary>
    public void PixelsChanged()
    {
        _surfaceCurrent = false;
        _decoded = null;
        _snapshot = null;
    }

    /// <summary>The pixels to draw now. Made once per change.</summary>
    public ImageSnapshot Snapshot()
    {
        Flush();
        if (_snapshot is { } snapshot)
        {
            return snapshot;
        }

        if (!_pixelsCurrent && !_surfaceCurrent && _decoded is { } decoded)
        {
            return _snapshot = new ImageSnapshot(decoded);
        }

        return _snapshot = new ImageSnapshot(FromPixelArray(Pixels()));
    }

    /// <summary>A copy with its own pixels.</summary>
    public ImageBuffer Clone()
    {
        Flush();
        return !_pixelsCurrent && !_surfaceCurrent && _decoded is { } decoded
            ? new ImageBuffer(decoded) { Opaque = Opaque }
            : new ImageBuffer(Width, Height, (int[])Pixels().Clone(), Opaque);
    }

    /// <summary>A copy of a rectangle of the pixels.</summary>
    /// <param name="x">The rectangle's left.</param>
    /// <param name="y">The rectangle's top.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    /// <exception cref="OutOfMemoryException">The rectangle is not inside the image, as GDI+ reports it.</exception>
    public ImageBuffer Crop(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > Width || y + height > Height)
        {
#pragma warning disable CA2201 // GDI+ reports this exact exception, and apps catch it.
            throw new OutOfMemoryException("Out of memory: the rectangle is not inside the image.");
#pragma warning restore CA2201
        }

        var pixels = Pixels();
        var copy = new int[width * height];
        for (var row = 0; row < height; row++)
        {
            Array.Copy(pixels, ((y + row) * Width) + x, copy, row * width, width);
        }

        return new ImageBuffer(width, height, copy, Opaque);
    }

    /// <summary>A copy scaled to a size, smoothly, as <c>new Bitmap(image, size)</c> makes.</summary>
    /// <param name="width">The new width.</param>
    /// <param name="height">The new height.</param>
    public ImageBuffer Scaled(int width, int height) =>
        width == Width && height == Height
            ? Clone()
            : new ImageBuffer(Snapshot().Bitmap.CreateScaledBitmap(new PixelSize(width, height))) { Opaque = Opaque };

    /// <summary>Writes the image in a format.</summary>
    /// <param name="stream">Where to write.</param>
    /// <param name="format">The file format.</param>
    /// <param name="quality">The JPEG or WebP quality from 0 to 100.</param>
    public void Save(Stream stream, ImageFileFormat format, int quality = 90) =>
        ImageEncoder.Encode(stream, Pixels(), Width, Height, Opaque, format, quality);

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

    /// <summary>The surface to draw on, holding the current pixels.</summary>
    private RenderTargetBitmap Surface()
    {
        if (_surface is { } current && _surfaceCurrent)
        {
            return current;
        }

        var surface = _surface ??= new RenderTargetBitmap(new PixelSize(Width, Height), Dpi);
        var source = _pixelsCurrent && _pixels is { } pixels ? FromPixelArray(pixels) : _decoded!;
        using (var context = surface.CreateDrawingContext(true))
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None, BitmapBlendingMode = BitmapBlendingMode.Source }))
        {
            context.DrawImage(source, new Rect(0, 0, Width, Height));
        }

        _surfaceCurrent = true;
        return surface;
    }

    private int[] Download(RenderTargetBitmap surface)
    {
        var stride = Width * 4;
        var bytes = new byte[stride * Height];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            surface.CopyPixels(new PixelRect(0, 0, Width, Height), handle.AddrOfPinnedObject(), bytes.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        var pixels = _pixels is { Length: var length } existing && length == Width * Height ? existing : new int[Width * Height];
        PixelConversion.FromPremultiplied(bytes, surface.Format == PixelFormat.Rgba8888, pixels, Opaque);
        return pixels;
    }

    private AvaloniaBitmap FromPixelArray(int[] pixels)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            return new AvaloniaBitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, handle.AddrOfPinnedObject(), new PixelSize(Width, Height), Dpi, Width * 4);
        }
        finally
        {
            handle.Free();
        }
    }
}
