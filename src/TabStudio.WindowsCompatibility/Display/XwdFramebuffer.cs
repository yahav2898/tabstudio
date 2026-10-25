using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using TabStudio.WindowsCompatibility.Display.X11;

namespace TabStudio.WindowsCompatibility.Display;

/// <summary>
/// The screen of an Xvfb started with <c>-fbdir</c>: Xvfb keeps its framebuffer in a file in XWD format and maps
/// it into its own memory, so mapping the same file shows every pixel it draws, with no copy through a socket.
/// </summary>
/// <remarks>
/// The file is an XWD header (big-endian), a color map, and the pixels. TabStudio starts Xvfb with a 24-bit
/// screen, whose pixels are 32 bits each in the machine's byte order: blue, green, red and an unused byte, the
/// layout of a <c>Bgra8888</c> bitmap with opaque alpha.
/// </remarks>
public sealed class XwdFramebuffer : IDisposable
{
    private const int HeaderFields = 25;

    private readonly MemoryMappedFile _file;
    private readonly MemoryMappedViewAccessor _view;
    private readonly long _pixels;

    private XwdFramebuffer(MemoryMappedFile file, MemoryMappedViewAccessor view, int width, int height, int stride, long pixels)
    {
        _file = file;
        _view = view;
        Width = width;
        Height = height;
        Stride = stride;
        _pixels = pixels;
    }

    /// <summary>The screen's width in pixels.</summary>
    public int Width { get; }

    /// <summary>The screen's height in pixels.</summary>
    public int Height { get; }

    /// <summary>The bytes of one row of pixels.</summary>
    public int Stride { get; }

    /// <summary>The whole screen.</summary>
    public ScreenRect Bounds => new(0, 0, Width, Height);

    /// <summary>Maps an Xvfb framebuffer file.</summary>
    /// <param name="path">The file, <c>Xvfb_screen0</c> in the folder given to <c>-fbdir</c>.</param>
    /// <exception cref="InvalidDataException">The file is not a 32-bit XWD framebuffer.</exception>
    public static XwdFramebuffer Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        MemoryMappedFile? file = null;
        try
        {
            var header = new byte[HeaderFields * 4];
            stream.ReadExactly(header);
            var format = ReadHeader(header);
            var expected = format.Pixels + ((long)format.Stride * format.Height);
            if (stream.Length < expected)
            {
                throw new InvalidDataException($"The framebuffer file holds {stream.Length} bytes, but its header describes {expected}.");
            }

            file = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
            var view = file.CreateViewAccessor(0, expected, MemoryMappedFileAccess.Read);
            return new XwdFramebuffer(file, view, format.Width, format.Height, format.Stride, format.Pixels);
        }
        catch
        {
            if (file is null)
            {
                stream.Dispose();
            }
            else
            {
                file.Dispose();
            }

            throw;
        }
    }

    /// <summary>
    /// Copies part of the screen into a buffer of the same layout (32-bit blue, green, red, unused), one row of
    /// pixels after another.
    /// </summary>
    /// <param name="area">The part of the screen; it is clipped to the screen.</param>
    /// <param name="destination">The buffer: the pixels of a picture as big as the whole screen, or as the area when <paramref name="destinationIsArea"/>.</param>
    /// <param name="destinationStride">The bytes of one row of the buffer.</param>
    /// <param name="destinationIsArea">Whether the buffer starts at the area's corner rather than the screen's.</param>
    public void Copy(ScreenRect area, Span<byte> destination, int destinationStride, bool destinationIsArea = false)
    {
        var clipped = area.Intersect(Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        var rowBytes = clipped.Width * 4;
        var handle = _view.SafeMemoryMappedViewHandle;
        for (var row = 0; row < clipped.Height; row++)
        {
            var y = clipped.Y + row;
            var source = _pixels + ((long)y * Stride) + (clipped.X * 4);
            var target = destinationIsArea ? (row * destinationStride) + ((clipped.X - area.X) * 4) : (y * destinationStride) + (clipped.X * 4);
            handle.ReadSpan((ulong)(_view.PointerOffset + source), destination.Slice(target, rowBytes));
        }
    }

    /// <summary>A checksum of part of the screen, to find out whether it changed; it differs from one process to the next.</summary>
    /// <param name="area">The part of the screen.</param>
    public uint Checksum(ScreenRect area)
    {
        var clipped = area.Intersect(Bounds);
        var row = new byte[clipped.Width * 4];
        var hash = default(HashCode);
        for (var y = 0; y < clipped.Height; y++)
        {
            Copy(new ScreenRect(clipped.X, clipped.Y + y, clipped.Width, 1), row, row.Length, destinationIsArea: true);
            hash.AddBytes(row);
        }

        return (uint)hash.ToHashCode();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _view.Dispose();
        _file.Dispose();
    }

    /// <summary>Reads an XWD header and checks it describes a framebuffer TabStudio can show.</summary>
    internal static (int Width, int Height, int Stride, long Pixels) ReadHeader(ReadOnlySpan<byte> header)
    {
        var headerSize = Field(header, 0);
        var version = Field(header, 1);
        var width = (int)Field(header, 4);
        var height = (int)Field(header, 5);
        var byteOrder = Field(header, 7);
        var bitsPerPixel = Field(header, 11);
        var stride = (int)Field(header, 12);
        var redMask = Field(header, 14);
        var greenMask = Field(header, 15);
        var blueMask = Field(header, 16);
        var colors = Field(header, 19);
        if (version != 7)
        {
            throw new InvalidDataException($"The framebuffer file is not in XWD format (version {version}).");
        }

        if (bitsPerPixel != 32 || byteOrder != 0 || redMask != 0xFF0000 || greenMask != 0xFF00 || blueMask != 0xFF)
        {
            throw new InvalidDataException(
                $"The framebuffer has {bitsPerPixel}-bit pixels with masks {redMask:x}/{greenMask:x}/{blueMask:x}; TabStudio shows 32-bit BGRX ones. Start Xvfb with a 24-bit screen.");
        }

        if (width <= 0 || height <= 0 || stride < width * 4)
        {
            throw new InvalidDataException($"The framebuffer's size, {width}×{height} with {stride} bytes a row, is not valid.");
        }

        return (width, height, stride, headerSize + (colors * 12L));
    }

    private static uint Field(ReadOnlySpan<byte> header, int index) => BinaryPrimitives.ReadUInt32BigEndian(header[(index * 4)..]);
}
