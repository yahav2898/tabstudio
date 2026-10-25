namespace TabStudio.WindowsBridge.Frames;

/// <summary>
/// A picture of a window: 32-bit pixels in blue, green, red, alpha order, row after row with no padding, as Windows
/// captures them and as Avalonia's <c>Bgra8888</c> bitmaps hold them. Alpha is ignored: windows are opaque.
/// </summary>
public sealed class FrameBitmap
{
    /// <summary>The bytes of one pixel.</summary>
    public const int BytesPerPixel = 4;

    /// <summary>Creates a black picture.</summary>
    /// <param name="width">The width in pixels, at least 1.</param>
    /// <param name="height">The height in pixels, at least 1.</param>
    public FrameBitmap(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        Width = width;
        Height = height;
        Pixels = new byte[checked(width * height * BytesPerPixel)];
    }

    /// <summary>The width in pixels.</summary>
    public int Width { get; }

    /// <summary>The height in pixels.</summary>
    public int Height { get; }

    /// <summary>The bytes of one row.</summary>
    public int Stride => Width * BytesPerPixel;

    /// <summary>The pixels, row after row.</summary>
    public byte[] Pixels { get; }

    /// <summary>The bytes of one row.</summary>
    /// <param name="y">The row, from the top.</param>
    public Span<byte> Row(int y) => Pixels.AsSpan(y * Stride, Stride);

    /// <summary>Reads a pixel as <c>0xAARRGGBB</c>.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    public uint GetPixel(int x, int y)
    {
        var i = (y * Width + x) * BytesPerPixel;
        return (uint)(Pixels[i] | (Pixels[i + 1] << 8) | (Pixels[i + 2] << 16) | (Pixels[i + 3] << 24));
    }

    /// <summary>Sets a pixel from <c>0xAARRGGBB</c>.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="argb">The color.</param>
    public void SetPixel(int x, int y, uint argb)
    {
        var i = (y * Width + x) * BytesPerPixel;
        Pixels[i] = (byte)argb;
        Pixels[i + 1] = (byte)(argb >> 8);
        Pixels[i + 2] = (byte)(argb >> 16);
        Pixels[i + 3] = (byte)(argb >> 24);
    }

    /// <summary>Paints a rectangle with one color, clipped to the picture.</summary>
    /// <param name="area">The rectangle.</param>
    /// <param name="argb">The color, <c>0xAARRGGBB</c>.</param>
    public void Fill(PixelRect area, uint argb)
    {
        var clipped = area.Intersect(Bounds);
        for (var y = clipped.Y; y < clipped.Bottom; y++)
        {
            for (var x = clipped.X; x < clipped.Right; x++)
            {
                SetPixel(x, y, argb);
            }
        }
    }

    /// <summary>The whole picture as a rectangle.</summary>
    public PixelRect Bounds => new(0, 0, Width, Height);

    /// <summary>A copy of the picture.</summary>
    public FrameBitmap Clone()
    {
        var copy = new FrameBitmap(Width, Height);
        Pixels.CopyTo(copy.Pixels, 0);
        return copy;
    }

    /// <summary>Copies a rectangle of another picture of the same size into the same place in this one.</summary>
    /// <param name="source">The other picture.</param>
    /// <param name="area">The rectangle.</param>
    public void CopyFrom(FrameBitmap source, PixelRect area)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Width != Width || source.Height != Height)
        {
            throw new ArgumentException("The pictures differ in size.", nameof(source));
        }

        var clipped = area.Intersect(Bounds);
        var length = clipped.Width * BytesPerPixel;
        for (var y = clipped.Y; y < clipped.Bottom; y++)
        {
            var offset = y * Stride + clipped.X * BytesPerPixel;
            Buffer.BlockCopy(source.Pixels, offset, Pixels, offset, length);
        }
    }

    /// <summary>
    /// The picture scaled down to fit a size, keeping its proportions, by averaging the pixels each new pixel covers;
    /// the picture itself when it already fits.
    /// </summary>
    /// <param name="maxWidth">The most pixels wide.</param>
    /// <param name="maxHeight">The most pixels high.</param>
    /// <param name="reuse">A picture of the scaled size to draw into, from the previous call, if any.</param>
    public FrameBitmap ScaleToFit(int maxWidth, int maxHeight, FrameBitmap? reuse = null)
    {
        if (maxWidth <= 0 || maxHeight <= 0 || (Width <= maxWidth && Height <= maxHeight))
        {
            return this;
        }

        var scale = Math.Min((double)maxWidth / Width, (double)maxHeight / Height);
        var width = Math.Max(1, (int)Math.Round(Width * scale));
        var height = Math.Max(1, (int)Math.Round(Height * scale));
        var target = reuse is { } existing && existing.Width == width && existing.Height == height ? existing : new FrameBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            var top = y * Height / height;
            var bottom = Math.Max(top + 1, (y + 1) * Height / height);
            for (var x = 0; x < width; x++)
            {
                var left = x * Width / width;
                var right = Math.Max(left + 1, (x + 1) * Width / width);
                int b = 0, g = 0, r = 0, count = 0;
                for (var sy = top; sy < bottom; sy++)
                {
                    var i = (sy * Width + left) * BytesPerPixel;
                    for (var sx = left; sx < right; sx++, i += BytesPerPixel)
                    {
                        b += Pixels[i];
                        g += Pixels[i + 1];
                        r += Pixels[i + 2];
                        count++;
                    }
                }

                var o = (y * width + x) * BytesPerPixel;
                target.Pixels[o] = (byte)(b / count);
                target.Pixels[o + 1] = (byte)(g / count);
                target.Pixels[o + 2] = (byte)(r / count);
                target.Pixels[o + 3] = 255;
            }
        }

        return target;
    }
}
