using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace TabStudio.Testing.Extensions;

/// <summary>
/// Draws simple, distinct PNG icons for extensions in the fake registry, so screenshots of the Extensions window
/// look like a real registry without shipping anyone's artwork.
/// </summary>
public static class TestIcons
{
    private const int Size = 64;

    private static readonly uint[] CrcTable = CreateCrcTable();

    /// <summary>A 64-pixel icon whose colors and shape follow from a seed, such as an extension's identifier.</summary>
    public static byte[] Create(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var hue = hash[0] * 360.0 / 256;
        var background = FromHue(hue, 0.55, 0.42);
        var foreground = FromHue((hue + 160) % 360, 0.75, 0.72);
        var shape = hash[1] % 3;

        var pixels = new byte[Size * ((Size * 4) + 1)];
        for (var y = 0; y < Size; y++)
        {
            var row = y * ((Size * 4) + 1);
            pixels[row] = 0;
            for (var x = 0; x < Size; x++)
            {
                var (r, g, b, a) = Pixel(x - 31.5, y - 31.5, shape, background, foreground);
                var i = row + 1 + (x * 4);
                (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (r, g, b, a);
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Size);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Size);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(png, "IHDR", header);
        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(pixels);
            }

            WriteChunk(png, "IDAT", compressed.ToArray());
        }

        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static (byte R, byte G, byte B, byte A) Pixel(double x, double y, int shape, (byte, byte, byte) background, (byte, byte, byte) foreground)
    {
        // A rounded square, with a circle, a diamond or two bars on it.
        const double Half = 31.5;
        const double Corner = 14;
        var dx = Math.Max(Math.Abs(x) - (Half - Corner), 0);
        var dy = Math.Max(Math.Abs(y) - (Half - Corner), 0);
        if ((dx * dx) + (dy * dy) > Corner * Corner)
        {
            return (0, 0, 0, 0);
        }

        var inside = shape switch
        {
            0 => (x * x) + (y * y) < 15 * 15,
            1 => Math.Abs(x) + Math.Abs(y) < 18,
            _ => Math.Abs(y) < 14 && (Math.Abs(x + 8) < 4 || Math.Abs(x - 8) < 4),
        };
        var (r, g, b) = inside ? foreground : background;
        return (r, g, b, 255);
    }

    private static (byte, byte, byte) FromHue(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var x = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        var m = lightness - (chroma / 2);
        var (r, g, b) = hue switch
        {
            < 60 => (chroma, x, 0.0),
            < 120 => (x, chroma, 0.0),
            < 180 => (0.0, chroma, x),
            < 240 => (0.0, x, chroma),
            < 300 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        return ((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        png.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        png.Write(typeBytes);
        png.Write(data);
        var crc = 0xFFFFFFFFu;
        foreach (var value in typeBytes.Concat(data))
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, crc ^ 0xFFFFFFFFu);
        png.Write(checksum);
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
