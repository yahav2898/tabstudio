using System.Buffers.Binary;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// Writes a GIF file, as GDI+ does for an image of more than 8 bits per pixel: a picture of 256 colors or fewer keeps
/// its colors exactly; any other is mapped to the 216 colors of the web-safe palette. Pixels under half opacity are
/// transparent.
/// </summary>
internal static class GifWriter
{
    /// <summary>Writes unpremultiplied 0xAARRGGBB pixels as a GIF file.</summary>
    /// <param name="stream">Where to write.</param>
    /// <param name="pixels">The pixels, row by row.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public static void Write(Stream stream, int[] pixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(pixels);
        var (palette, indices, transparent) = Quantize(pixels);

        var bits = 1;
        while ((1 << bits) < palette.Count)
        {
            bits++;
        }

        var tableSize = 1 << bits;
        Span<byte> word = stackalloc byte[2];
        stream.Write("GIF89a"u8);
        BinaryPrimitives.WriteUInt16LittleEndian(word, (ushort)width);
        stream.Write(word);
        BinaryPrimitives.WriteUInt16LittleEndian(word, (ushort)height);
        stream.Write(word);
        stream.WriteByte((byte)(0x80 | ((bits - 1) << 4) | (bits - 1)));
        stream.WriteByte(0);
        stream.WriteByte(0);
        for (var i = 0; i < tableSize; i++)
        {
            var color = i < palette.Count ? palette[i] : 0;
            stream.WriteByte((byte)(color >> 16));
            stream.WriteByte((byte)(color >> 8));
            stream.WriteByte((byte)color);
        }

        if (transparent >= 0)
        {
            // Graphic control extension: the transparent color.
            stream.Write([0x21, 0xF9, 4, 1, 0, 0, (byte)transparent, 0]);
        }

        stream.WriteByte(0x2C);
        stream.Write([0, 0, 0, 0]);
        BinaryPrimitives.WriteUInt16LittleEndian(word, (ushort)width);
        stream.Write(word);
        BinaryPrimitives.WriteUInt16LittleEndian(word, (ushort)height);
        stream.Write(word);
        stream.WriteByte(0);
        var minimumCodeSize = Math.Max(2, bits);
        stream.WriteByte((byte)minimumCodeSize);
        WriteSubBlocks(stream, Lzw(indices, minimumCodeSize));
        stream.WriteByte(0);
        stream.WriteByte(0x3B);
    }

    private static (List<int> Palette, byte[] Indices, int Transparent) Quantize(int[] pixels)
    {
        var indices = new byte[pixels.Length];
        var palette = new List<int>();
        var lookup = new Dictionary<int, int>();
        var transparent = -1;
        var exact = true;
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            if ((uint)pixel >> 24 < 128)
            {
                if (transparent < 0)
                {
                    transparent = palette.Count;
                    palette.Add(0);
                }

                indices[i] = (byte)transparent;
                continue;
            }

            var rgb = pixel & 0xFFFFFF;
            if (!lookup.TryGetValue(rgb, out var index))
            {
                if (palette.Count == 256)
                {
                    exact = false;
                    break;
                }

                index = palette.Count;
                palette.Add(rgb);
                lookup[rgb] = index;
            }

            indices[i] = (byte)index;
        }

        if (exact)
        {
            return (palette, indices, transparent);
        }

        // The web-safe palette: six levels of each channel, and the transparent color after them.
        palette = [];
        for (var r = 0; r < 6; r++)
        {
            for (var g = 0; g < 6; g++)
            {
                for (var b = 0; b < 6; b++)
                {
                    palette.Add((r * 51 << 16) | (g * 51 << 8) | (b * 51));
                }
            }
        }

        transparent = -1;
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            if ((uint)pixel >> 24 < 128)
            {
                if (transparent < 0)
                {
                    transparent = palette.Count;
                    palette.Add(0);
                }

                indices[i] = (byte)transparent;
                continue;
            }

            static int Level(int channel) => (channel + 25) / 51;
            indices[i] = (byte)((Level((pixel >> 16) & 0xFF) * 36) + (Level((pixel >> 8) & 0xFF) * 6) + Level(pixel & 0xFF));
        }

        return (palette, indices, transparent);
    }

    /// <summary>Compresses color indices with GIF's variable-length LZW.</summary>
    private static byte[] Lzw(byte[] indices, int minimumCodeSize)
    {
        var output = new List<byte>(indices.Length / 2);
        var clear = 1 << minimumCodeSize;
        var end = clear + 1;
        var codeSize = minimumCodeSize + 1;
        var next = end + 1;
        var table = new Dictionary<int, int>();
        int buffer = 0, bufferBits = 0;
        void Emit(int code)
        {
            buffer |= code << bufferBits;
            bufferBits += codeSize;
            while (bufferBits >= 8)
            {
                output.Add((byte)buffer);
                buffer >>= 8;
                bufferBits -= 8;
            }
        }

        Emit(clear);
        if (indices.Length == 0)
        {
            Emit(end);
            if (bufferBits > 0)
            {
                output.Add((byte)buffer);
            }

            return [.. output];
        }

        var prefix = (int)indices[0];
        for (var i = 1; i < indices.Length; i++)
        {
            var symbol = indices[i];
            var key = (prefix << 8) | symbol;
            if (table.TryGetValue(key, out var code))
            {
                prefix = code;
                continue;
            }

            Emit(prefix);
            if (next < 4096)
            {
                table[key] = next++;
                if (next > (1 << codeSize) && codeSize < 12)
                {
                    codeSize++;
                }
            }
            else
            {
                Emit(clear);
                table.Clear();
                codeSize = minimumCodeSize + 1;
                next = end + 1;
            }

            prefix = symbol;
        }

        Emit(prefix);
        Emit(end);
        if (bufferBits > 0)
        {
            output.Add((byte)buffer);
        }

        return [.. output];
    }

    private static void WriteSubBlocks(Stream stream, byte[] data)
    {
        for (var offset = 0; offset < data.Length; offset += 255)
        {
            var length = Math.Min(255, data.Length - offset);
            stream.WriteByte((byte)length);
            stream.Write(data, offset, length);
        }
    }
}
