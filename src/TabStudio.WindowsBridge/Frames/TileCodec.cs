using System.Buffers;

namespace TabStudio.WindowsBridge.Frames;

/// <summary>How a tile's pixels are carried.</summary>
internal enum TileEncoding : byte
{
    /// <summary>One color fills the tile: three bytes, blue, green and red, always exact.</summary>
    Solid = 0,

    /// <summary>
    /// The tile's pixels at its level: each channel cut to the level's bits, the difference from the pixel on its
    /// left (so flat and smooth areas become runs of zeros), blue, green and red, row after row.
    /// </summary>
    Pixels = 1,
}

/// <summary>Turns tiles of a picture into bytes and back, at a quality level. The bytes are compressed together afterwards.</summary>
internal static class TileCodec
{
    /// <summary>Whether every pixel of a tile has the same color, which is then returned.</summary>
    public static bool IsSolid(FrameBitmap frame, PixelRect tile, out uint color)
    {
        var pixels = frame.Pixels;
        var first = (tile.Y * frame.Width + tile.X) * FrameBitmap.BytesPerPixel;
        color = (uint)(pixels[first] | (pixels[first + 1] << 8) | (pixels[first + 2] << 16));
        for (var y = tile.Y; y < tile.Bottom; y++)
        {
            var i = (y * frame.Width + tile.X) * FrameBitmap.BytesPerPixel;
            for (var x = 0; x < tile.Width; x++, i += FrameBitmap.BytesPerPixel)
            {
                if (pixels[i] != (byte)color || pixels[i + 1] != (byte)(color >> 8) || pixels[i + 2] != (byte)(color >> 16))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>The bytes a tile's pixels take at a level, before compression.</summary>
    public static int PixelBytes(PixelRect tile, int level) =>
        StoredWidth(tile.Width, level) * StoredHeight(tile.Height, level) * 3;

    /// <summary>Writes a tile's pixels at a level.</summary>
    public static void WritePixels(FrameBitmap frame, PixelRect tile, int level, IBufferWriter<byte> output)
    {
        var bits = FrameQuality.BitsPerChannel(level);
        var shift = 8 - bits;
        var half = FrameQuality.IsHalfResolution(level);
        var width = StoredWidth(tile.Width, level);
        var height = StoredHeight(tile.Height, level);
        var span = output.GetSpan(width * height * 3);
        var o = 0;
        var pixels = frame.Pixels;
        for (var y = 0; y < height; y++)
        {
            int leftB = 0, leftG = 0, leftR = 0;
            for (var x = 0; x < width; x++)
            {
                int b, g, r;
                if (half)
                {
                    (b, g, r) = Average(frame, tile.X + (x * 2), tile.Y + (y * 2), tile.Right, tile.Bottom);
                }
                else
                {
                    var i = ((tile.Y + y) * frame.Width + tile.X + x) * FrameBitmap.BytesPerPixel;
                    b = pixels[i];
                    g = pixels[i + 1];
                    r = pixels[i + 2];
                }

                b >>= shift;
                g >>= shift;
                r >>= shift;
                span[o++] = (byte)(b - leftB);
                span[o++] = (byte)(g - leftG);
                span[o++] = (byte)(r - leftR);
                leftB = b;
                leftG = g;
                leftR = r;
            }
        }

        output.Advance(o);
    }

    /// <summary>Paints a tile from the bytes <see cref="WritePixels"/> wrote.</summary>
    public static void ReadPixels(ReadOnlySpan<byte> data, FrameBitmap frame, PixelRect tile, int level)
    {
        var bits = FrameQuality.BitsPerChannel(level);
        var shift = 8 - bits;
        var half = FrameQuality.IsHalfResolution(level);
        var width = StoredWidth(tile.Width, level);
        var height = StoredHeight(tile.Height, level);
        var mask = (1 << bits) - 1;
        var pixels = frame.Pixels;
        var o = 0;
        for (var y = 0; y < height; y++)
        {
            int b = 0, g = 0, r = 0;
            for (var x = 0; x < width; x++)
            {
                b = (b + data[o++]) & mask;
                g = (g + data[o++]) & mask;
                r = (r + data[o++]) & mask;
                var color = (byte)Expand(b, bits, shift) | ((uint)Expand(g, bits, shift) << 8) | ((uint)Expand(r, bits, shift) << 16) | 0xFF000000;
                if (half)
                {
                    for (var dy = 0; dy < 2; dy++)
                    {
                        var py = tile.Y + (y * 2) + dy;
                        for (var dx = 0; dx < 2 && py < tile.Bottom; dx++)
                        {
                            var px = tile.X + (x * 2) + dx;
                            if (px < tile.Right)
                            {
                                Put(pixels, (py * frame.Width + px) * FrameBitmap.BytesPerPixel, color);
                            }
                        }
                    }
                }
                else
                {
                    Put(pixels, ((tile.Y + y) * frame.Width + tile.X + x) * FrameBitmap.BytesPerPixel, color);
                }
            }
        }
    }

    /// <summary>Paints a tile with one color.</summary>
    public static void Fill(FrameBitmap frame, PixelRect tile, uint color)
    {
        var argb = color | 0xFF000000;
        for (var y = tile.Y; y < tile.Bottom; y++)
        {
            var i = (y * frame.Width + tile.X) * FrameBitmap.BytesPerPixel;
            for (var x = 0; x < tile.Width; x++, i += FrameBitmap.BytesPerPixel)
            {
                Put(frame.Pixels, i, argb);
            }
        }
    }

    private static int StoredWidth(int width, int level) => FrameQuality.IsHalfResolution(level) ? (width + 1) / 2 : width;

    private static int StoredHeight(int height, int level) => FrameQuality.IsHalfResolution(level) ? (height + 1) / 2 : height;

    /// <summary>A channel cut to some bits, widened back to eight by repeating its top bits, so white stays white.</summary>
    private static int Expand(int value, int bits, int shift) => bits == 8 ? value : (value << shift) | (value >> (bits - shift));

    private static (int B, int G, int R) Average(FrameBitmap frame, int left, int top, int right, int bottom)
    {
        int b = 0, g = 0, r = 0, count = 0;
        for (var y = top; y < Math.Min(top + 2, bottom); y++)
        {
            for (var x = left; x < Math.Min(left + 2, right); x++)
            {
                var i = (y * frame.Width + x) * FrameBitmap.BytesPerPixel;
                b += frame.Pixels[i];
                g += frame.Pixels[i + 1];
                r += frame.Pixels[i + 2];
                count++;
            }
        }

        return (b / count, g / count, r / count);
    }

    private static void Put(byte[] pixels, int i, uint argb)
    {
        pixels[i] = (byte)argb;
        pixels[i + 1] = (byte)(argb >> 8);
        pixels[i + 2] = (byte)(argb >> 16);
        pixels[i + 3] = (byte)(argb >> 24);
    }
}
