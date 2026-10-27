using System.Runtime.InteropServices;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// Converts between the pixels a <see cref="System.Drawing.Bitmap"/> holds, unpremultiplied 0xAARRGGBB values, and
/// the premultiplied pixels the renderer draws into, in either byte order Skia uses on the platform.
/// </summary>
internal static class PixelConversion
{
    /// <summary>Converts premultiplied pixels to unpremultiplied 0xAARRGGBB values.</summary>
    /// <param name="source">The premultiplied pixels, 4 bytes each, rows without padding.</param>
    /// <param name="rgba">Whether the bytes are in R, G, B, A order; otherwise B, G, R, A.</param>
    /// <param name="target">Receives the pixels.</param>
    /// <param name="opaque">Whether to make every pixel opaque, for pixel formats without alpha.</param>
    public static void FromPremultiplied(ReadOnlySpan<byte> source, bool rgba, Span<int> target, bool opaque)
    {
        var count = Math.Min(target.Length, source.Length / 4);
        for (var i = 0; i < count; i++)
        {
            var o = i * 4;
            int r, g, b;
            var a = source[o + 3];
            if (rgba)
            {
                r = source[o];
                g = source[o + 1];
                b = source[o + 2];
            }
            else
            {
                b = source[o];
                g = source[o + 1];
                r = source[o + 2];
            }

            if (a == 0)
            {
                target[i] = opaque ? unchecked((int)0xFF000000) : 0;
                continue;
            }

            if (a != 255)
            {
                var half = a / 2;
                r = Math.Min(255, ((r * 255) + half) / a);
                g = Math.Min(255, ((g * 255) + half) / a);
                b = Math.Min(255, ((b * 255) + half) / a);
            }

            target[i] = ((opaque ? 255 : a) << 24) | (r << 16) | (g << 8) | b;
        }
    }

    /// <summary>Premultiplies one 0xAARRGGBB color by its alpha.</summary>
    /// <param name="argb">The unpremultiplied color.</param>
    public static uint Premultiply(uint argb)
    {
        var a = argb >> 24;
        if (a == 255)
        {
            return argb;
        }

        var r = (((argb >> 16) & 0xFF) * a + 127) / 255;
        var g = (((argb >> 8) & 0xFF) * a + 127) / 255;
        var b = ((argb & 0xFF) * a + 127) / 255;
        return (a << 24) | (r << 16) | (g << 8) | b;
    }

    /// <summary>Copies pixels of a rectangle into memory in a GDI+ pixel format, as <c>Bitmap.LockBits</c> hands them out.</summary>
    /// <param name="pixels">The bitmap's pixels.</param>
    /// <param name="width">The bitmap's width.</param>
    /// <param name="x">The rectangle's left.</param>
    /// <param name="y">The rectangle's top.</param>
    /// <param name="w">The rectangle's width.</param>
    /// <param name="h">The rectangle's height.</param>
    /// <param name="layout">The pixel format's layout.</param>
    /// <param name="scan0">The memory's first row.</param>
    /// <param name="stride">The bytes from one row to the next.</param>
    public static void CopyOut(int[] pixels, int width, int x, int y, int w, int h, PixelLayout layout, IntPtr scan0, int stride)
    {
        var row = new byte[stride];
        for (var r = 0; r < h; r++)
        {
            var start = ((y + r) * width) + x;
            var span = pixels.AsSpan(start, w);
            switch (layout)
            {
                case PixelLayout.Argb32:
                    MemoryMarshal.AsBytes(span).CopyTo(row);
                    break;
                case PixelLayout.Rgb32:
                    for (var i = 0; i < w; i++)
                    {
                        BitConverter.TryWriteBytes(row.AsSpan(i * 4), span[i] | unchecked((int)0xFF000000));
                    }

                    break;
                case PixelLayout.PArgb32:
                    for (var i = 0; i < w; i++)
                    {
                        BitConverter.TryWriteBytes(row.AsSpan(i * 4), Premultiply((uint)span[i]));
                    }

                    break;
                case PixelLayout.Rgb24:
                    for (var i = 0; i < w; i++)
                    {
                        var c = span[i];
                        row[i * 3] = (byte)c;
                        row[(i * 3) + 1] = (byte)(c >> 8);
                        row[(i * 3) + 2] = (byte)(c >> 16);
                    }

                    break;
            }

            Marshal.Copy(row, 0, scan0 + (r * stride), stride);
        }
    }

    /// <summary>Copies pixels back from memory in a GDI+ pixel format, as <c>Bitmap.UnlockBits</c> takes them in.</summary>
    /// <param name="pixels">The bitmap's pixels.</param>
    /// <param name="width">The bitmap's width.</param>
    /// <param name="x">The rectangle's left.</param>
    /// <param name="y">The rectangle's top.</param>
    /// <param name="w">The rectangle's width.</param>
    /// <param name="h">The rectangle's height.</param>
    /// <param name="layout">The pixel format's layout.</param>
    /// <param name="scan0">The memory's first row.</param>
    /// <param name="stride">The bytes from one row to the next.</param>
    public static void CopyIn(int[] pixels, int width, int x, int y, int w, int h, PixelLayout layout, IntPtr scan0, int stride)
    {
        var row = new byte[stride];
        for (var r = 0; r < h; r++)
        {
            Marshal.Copy(scan0 + (r * stride), row, 0, stride);
            var span = pixels.AsSpan(((y + r) * width) + x, w);
            for (var i = 0; i < w; i++)
            {
                span[i] = layout switch
                {
                    PixelLayout.Argb32 => BitConverter.ToInt32(row, i * 4),
                    PixelLayout.Rgb32 => BitConverter.ToInt32(row, i * 4) | unchecked((int)0xFF000000),
                    PixelLayout.PArgb32 => Unpremultiply(BitConverter.ToUInt32(row, i * 4)),
                    _ => unchecked((int)0xFF000000) | (row[(i * 3) + 2] << 16) | (row[(i * 3) + 1] << 8) | row[i * 3],
                };
            }
        }
    }

    private static int Unpremultiply(uint argb)
    {
        var a = argb >> 24;
        if (a is 0)
        {
            return 0;
        }

        if (a == 255)
        {
            return unchecked((int)argb);
        }

        var r = Math.Min(255, ((((argb >> 16) & 0xFF) * 255) + (a / 2)) / a);
        var g = Math.Min(255, ((((argb >> 8) & 0xFF) * 255) + (a / 2)) / a);
        var b = Math.Min(255, (((argb & 0xFF) * 255) + (a / 2)) / a);
        return unchecked((int)((a << 24) | (r << 16) | (g << 8) | b));
    }
}

/// <summary>How a GDI+ pixel format lays out a pixel in memory.</summary>
internal enum PixelLayout
{
    /// <summary>B, G, R, A bytes, unpremultiplied: <c>Format32bppArgb</c>.</summary>
    Argb32,

    /// <summary>B, G, R bytes and an unused byte: <c>Format32bppRgb</c>.</summary>
    Rgb32,

    /// <summary>B, G, R, A bytes, premultiplied: <c>Format32bppPArgb</c>.</summary>
    PArgb32,

    /// <summary>B, G, R bytes: <c>Format24bppRgb</c>.</summary>
    Rgb24,
}
