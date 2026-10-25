using System.Buffers.Binary;

namespace TabStudio.WindowsCompatibility.Tests.Infrastructure;

/// <summary>
/// A framebuffer file like the one Xvfb keeps with <c>-fbdir</c>: an XWD header, an empty color map, then 32-bit
/// BGRX pixels, which a test paints to play the X server's part.
/// </summary>
public sealed class SyntheticFramebuffer : IDisposable
{
    private const int HeaderSize = 100 + 16;
    private readonly FileStream _file;

    public SyntheticFramebuffer(string path, int width, int height)
    {
        Path = path;
        Width = width;
        Height = height;
        Stride = width * 4;
        _file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        var header = new byte[HeaderSize];
        uint[] fields = [HeaderSize, 7, 2, 24, (uint)width, (uint)height, 0, 0, 32, 0, 32, 32, (uint)Stride, 4, 0xFF0000, 0xFF00, 0xFF, 8, 256, 0, (uint)width, (uint)height, 0, 0, 0];
        for (var i = 0; i < fields.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(i * 4), fields[i]);
        }

        "Xvfb test\0"u8.CopyTo(header.AsSpan(100));
        _file.Write(header);
        _file.SetLength(HeaderSize + ((long)Stride * height));
        _file.Flush();
    }

    public string Path { get; }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    /// <summary>Paints a rectangle in a color, given as blue, green and red.</summary>
    public void Fill(int x, int y, int width, int height, byte blue, byte green, byte red)
    {
        var row = new byte[width * 4];
        for (var i = 0; i < width; i++)
        {
            row[i * 4] = blue;
            row[(i * 4) + 1] = green;
            row[(i * 4) + 2] = red;
        }

        for (var line = y; line < y + height; line++)
        {
            _file.Position = HeaderSize + ((long)line * Stride) + (x * 4);
            _file.Write(row);
        }

        _file.Flush();
    }

    public void Dispose() => _file.Dispose();
}
