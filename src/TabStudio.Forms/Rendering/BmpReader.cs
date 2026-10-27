using System.Buffers.Binary;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// Reads Windows bitmap files of 1, 4, 8, 24 and 32 bits per pixel, as image list streams hold them, with their
/// alpha bytes kept: 32-bit image lists keep alpha there, which general decoders ignore.
/// </summary>
internal static class BmpReader
{
    /// <summary>The pixels of a bitmap file.</summary>
    /// <param name="Width">The width.</param>
    /// <param name="Height">The height.</param>
    /// <param name="Pixels">The pixels as 0xAARRGGBB, rows from the top; alpha is 0 unless the file has it.</param>
    /// <param name="HasAlpha">Whether the file has 32 bits per pixel with some alpha set.</param>
    public sealed record Image(int Width, int Height, int[] Pixels, bool HasAlpha);

    /// <summary>Reads the bitmap file at an offset, and moves the offset past it.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="offset">Where the file starts; afterwards, where it ends.</param>
    /// <exception cref="ArgumentException">The bytes are not a bitmap file this reader understands.</exception>
    public static Image Read(byte[] data, ref int offset)
    {
        var file = data.AsSpan(offset);
        if (file.Length < 54 || file[0] != (byte)'B' || file[1] != (byte)'M')
        {
            throw new ArgumentException("Parameter is not valid: the image list's bitmap is not a BMP file.");
        }

        var bitsOffset = BinaryPrimitives.ReadInt32LittleEndian(file[10..]);
        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(file[14..]);
        var width = BinaryPrimitives.ReadInt32LittleEndian(file[18..]);
        var rawHeight = BinaryPrimitives.ReadInt32LittleEndian(file[22..]);
        var bits = BinaryPrimitives.ReadInt16LittleEndian(file[28..]);
        var compression = BinaryPrimitives.ReadInt32LittleEndian(file[30..]);
        var used = BinaryPrimitives.ReadInt32LittleEndian(file[46..]);
        if (compression is not (0 or 3) || width <= 0 || rawHeight == 0 || bits is not (1 or 4 or 8 or 24 or 32))
        {
            throw new ArgumentException($"Parameter is not valid: a {bits}-bit BMP with compression {compression} is not supported.");
        }

        var topDown = rawHeight < 0;
        var height = Math.Abs(rawHeight);
        var paletteCount = bits <= 8 ? (used > 0 ? used : 1 << bits) : 0;
        var palette = new int[paletteCount];
        var paletteStart = 14 + headerSize + (compression == 3 && headerSize == 40 ? 12 : 0);
        for (var i = 0; i < paletteCount && paletteStart + (i * 4) + 3 < file.Length; i++)
        {
            var p = file[(paletteStart + (i * 4))..];
            palette[i] = (p[2] << 16) | (p[1] << 8) | p[0];
        }

        var stride = ((width * bits) + 31) / 32 * 4;
        var pixels = new int[width * height];
        var hasAlpha = false;
        for (var row = 0; row < height; row++)
        {
            var start = bitsOffset + (row * stride);
            if (start + stride > file.Length)
            {
                break;
            }

            var line = file.Slice(start, stride);
            var y = topDown ? row : height - 1 - row;
            for (var x = 0; x < width; x++)
            {
                int color;
                switch (bits)
                {
                    case 32:
                        var a = line[(x * 4) + 3];
                        hasAlpha |= a != 0;
                        color = (a << 24) | (line[(x * 4) + 2] << 16) | (line[(x * 4) + 1] << 8) | line[x * 4];
                        break;
                    case 24:
                        color = (line[(x * 3) + 2] << 16) | (line[(x * 3) + 1] << 8) | line[x * 3];
                        break;
                    case 8:
                        color = Palette(palette, line[x]);
                        break;
                    case 4:
                        color = Palette(palette, (line[x / 2] >> (x % 2 == 0 ? 4 : 0)) & 0xF);
                        break;
                    default:
                        color = Palette(palette, (line[x / 8] >> (7 - (x % 8))) & 1);
                        break;
                }

                pixels[(y * width) + x] = color;
            }
        }

        // Image lists write the file size of the headers alone, so the end is where the pixels end.
        offset += bitsOffset + (stride * height);
        return new Image(width, height, pixels, hasAlpha);
    }

    private static int Palette(int[] palette, int index) => index < palette.Length ? palette[index] : 0;
}
