using System.Buffers.Binary;
using System.IO.Compression;

namespace TabStudio.WindowsBridge.Frames;

/// <summary>
/// The tablet's side of a stream: applies the updates of a <see cref="FrameEncoder"/> to its picture of the window.
/// </summary>
/// <remarks>Not thread-safe: one decoder belongs to one stream.</remarks>
public sealed class FrameDecoder
{
    /// <summary>The window's picture as the updates so far paint it; <see langword="null"/> before the first keyframe.</summary>
    public FrameBitmap? Frame { get; private set; }

    /// <summary>Applies an update.</summary>
    /// <param name="update">The update's bytes.</param>
    /// <returns>The part of the picture that changed; all of it after a keyframe that resized it.</returns>
    /// <exception cref="InvalidDataException">The bytes are not an update, or an update that is not a keyframe arrived first.</exception>
    public PixelRect Apply(ReadOnlySpan<byte> update)
    {
        if (update.Length < FrameEncoder.HeaderLength || BinaryPrimitives.ReadUInt32LittleEndian(update) != FrameEncoder.Magic)
        {
            throw new InvalidDataException("This is not a Windows Bridge frame.");
        }

        if (update[4] != FrameEncoder.Version)
        {
            throw new InvalidDataException($"The frame has format {update[4]}, and this TabStudio reads format {FrameEncoder.Version}.");
        }

        var keyframe = (update[5] & 1) == 1;
        var tileSize = BinaryPrimitives.ReadUInt16LittleEndian(update[6..]);
        var width = BinaryPrimitives.ReadUInt16LittleEndian(update[8..]);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(update[10..]);
        var count = BinaryPrimitives.ReadUInt32LittleEndian(update[12..]);
        if (tileSize == 0 || width == 0 || height == 0)
        {
            throw new InvalidDataException("The frame has no size.");
        }

        var columns = (width + tileSize - 1) / tileSize;
        var rows = (height + tileSize - 1) / tileSize;
        if (count > (uint)(columns * rows) || update.Length < FrameEncoder.HeaderLength + (count * FrameEncoder.TileEntryLength))
        {
            throw new InvalidDataException("The frame's tile list is damaged.");
        }

        var frame = Frame;
        var resized = false;
        if (frame is null || frame.Width != width || frame.Height != height)
        {
            if (!keyframe)
            {
                throw new InvalidDataException("The frame changes a picture this device does not have.");
            }

            frame = new FrameBitmap(width, height);
            resized = true;
        }

        var directory = update.Slice(FrameEncoder.HeaderLength, (int)count * FrameEncoder.TileEntryLength);
        var data = Inflate(update[(FrameEncoder.HeaderLength + directory.Length)..], ExpectedLength(directory, tileSize, width, height));
        var offset = 0;
        var dirty = PixelRect.Empty;
        for (var i = 0; i < count; i++)
        {
            var entry = directory.Slice(i * FrameEncoder.TileEntryLength, FrameEncoder.TileEntryLength);
            var column = BinaryPrimitives.ReadUInt16LittleEndian(entry);
            var row = BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]);
            var rect = FrameEncoder.TileRect(column, row, width, height, tileSize);
            var level = entry[5];
            if ((TileEncoding)entry[4] == TileEncoding.Solid)
            {
                TileCodec.Fill(frame, rect, (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16)));
                offset += 3;
            }
            else
            {
                var length = TileCodec.PixelBytes(rect, level);
                TileCodec.ReadPixels(data.AsSpan(offset, length), frame, rect, level);
                offset += length;
            }

            dirty = dirty.Union(rect);
        }

        Frame = frame;
        return resized ? frame.Bounds : dirty;
    }

    /// <summary>Forgets the picture, so the next update must be a keyframe.</summary>
    public void Reset() => Frame = null;

    private static int ExpectedLength(ReadOnlySpan<byte> directory, int tileSize, int width, int height)
    {
        var total = 0;
        for (var i = 0; i < directory.Length; i += FrameEncoder.TileEntryLength)
        {
            var column = BinaryPrimitives.ReadUInt16LittleEndian(directory[i..]);
            var row = BinaryPrimitives.ReadUInt16LittleEndian(directory[(i + 2)..]);
            if (column * tileSize >= width || row * tileSize >= height || directory[i + 5] > FrameQuality.Roughest || directory[i + 4] > (byte)TileEncoding.Pixels)
            {
                throw new InvalidDataException("The frame names a tile outside the picture.");
            }

            total += (TileEncoding)directory[i + 4] == TileEncoding.Solid
                ? 3
                : TileCodec.PixelBytes(FrameEncoder.TileRect(column, row, width, height, tileSize), directory[i + 5]);
        }

        return total;
    }

    private static byte[] Inflate(ReadOnlySpan<byte> compressed, int length)
    {
        var data = new byte[length];
        using var input = new MemoryStream(compressed.ToArray());
        using var inflate = new DeflateStream(input, CompressionMode.Decompress);
        if (inflate.ReadAtLeast(data, length, throwOnEndOfStream: false) < length)
        {
            throw new InvalidDataException("The frame's pixels are cut short.");
        }

        return data;
    }
}
