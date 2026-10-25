using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;

namespace TabStudio.WindowsBridge.Frames;

/// <summary>One encoded update of a stream, ready to send.</summary>
/// <param name="Data">The bytes, for a <see cref="FrameDecoder"/>.</param>
/// <param name="IsKeyframe">Whether it carries the whole picture, as the first frame and frames after a resize do.</param>
/// <param name="ChangedTiles">The tiles it carries because they changed.</param>
/// <param name="RefinedTiles">The tiles it carries again, exactly, because they were sent at a lower quality before.</param>
/// <param name="Dirty">The part of the picture it changes.</param>
public sealed record EncodedFrame(byte[] Data, bool IsKeyframe, int ChangedTiles, int RefinedTiles, PixelRect Dirty);

/// <summary>
/// Encodes the pictures of one window as updates: only the tiles that changed since the previous picture, each at
/// the quality level asked for, compressed together. Tiles that were sent at a lower quality are sent again exactly
/// once they stop changing, as the refinement budget allows.
/// </summary>
/// <remarks>
/// <para>The update format (little-endian):</para>
/// <list type="bullet">
/// <item>the header: the magic <c>TSFU</c>, version 1, flags (1 for a keyframe), the tile size (16 bits), the
/// picture's width and height (16 bits each) and the number of tiles (32 bits);</item>
/// <item>for each tile: its column and row (16 bits each), its <see cref="TileEncoding"/> and its quality level;</item>
/// <item>the tiles' bytes, in the same order, deflated as one stream.</item>
/// </list>
/// <para>Not thread-safe: one encoder belongs to one stream.</para>
/// </remarks>
public sealed class FrameEncoder
{
    /// <summary>The tile size: big enough to compress well, small enough that a caret's blink sends little.</summary>
    public const int DefaultTileSize = 64;

    /// <summary>The first four bytes of every update: <c>TSFU</c>.</summary>
    internal const uint Magic = 0x55465354;

    /// <summary>The version of the update format.</summary>
    internal const byte Version = 1;

    /// <summary>The length of the header.</summary>
    internal const int HeaderLength = 16;

    /// <summary>The length of one tile's entry in the directory.</summary>
    internal const int TileEntryLength = 6;

    /// <summary>A tile the tablet has not been sent.</summary>
    private const byte NotSent = byte.MaxValue;

    private readonly int _tileSize;
    private FrameBitmap? _previous;
    private byte[] _levels = [];
    private int _columns;
    private int _rows;

    /// <summary>Creates an encoder.</summary>
    /// <param name="tileSize">The tile size, from 8 to 256 pixels.</param>
    public FrameEncoder(int tileSize = DefaultTileSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tileSize, 8);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tileSize, 256);
        _tileSize = tileSize;
    }

    /// <summary>The tile size.</summary>
    public int TileSize => _tileSize;

    /// <summary>Whether some tiles the tablet has are not exact yet, so a refinement is due once the window is still.</summary>
    public bool HasRoughTiles => _levels.AsSpan().ContainsAnyExcept((byte)FrameQuality.Exact, NotSent);

    /// <summary>Makes the next update a keyframe, such as after the tablet lost track of the stream.</summary>
    public void Reset() => _previous = null;

    /// <summary>
    /// Encodes a picture as an update to the previous one.
    /// </summary>
    /// <param name="frame">The window's picture now.</param>
    /// <param name="level">The quality level of changed tiles, from <see cref="FrameQuality.Exact"/> to <see cref="FrameQuality.Roughest"/>.</param>
    /// <param name="maxRefinedTiles">How many unchanged rough tiles to send again exactly; 0 for none.</param>
    /// <returns>The update, or <see langword="null"/> when nothing changed and nothing needed refining.</returns>
    public EncodedFrame? Encode(FrameBitmap frame, int level, int maxRefinedTiles = 0)
    {
        ArgumentNullException.ThrowIfNull(frame);
        level = FrameQuality.Clamp(level);
        var keyframe = _previous is null || _previous.Width != frame.Width || _previous.Height != frame.Height;
        if (keyframe)
        {
            _previous = frame.Clone();
            _columns = (frame.Width + _tileSize - 1) / _tileSize;
            _rows = (frame.Height + _tileSize - 1) / _tileSize;
            _levels = new byte[_columns * _rows];
            _levels.AsSpan().Fill(NotSent);
        }

        var tiles = new List<(int Column, int Row, int Level)>();
        IReadOnlyList<(int Column, int Row)> changed = keyframe ? AllTiles() : FindChangedTiles(_previous!, frame, _tileSize);
        var dirty = PixelRect.Empty;
        foreach (var (column, row) in changed)
        {
            tiles.Add((column, row, level));
            var rect = TileRect(column, row, frame.Width, frame.Height, _tileSize);
            dirty = dirty.Union(rect);
            if (!keyframe)
            {
                _previous!.CopyFrom(frame, rect);
            }
        }

        var refined = 0;
        if (maxRefinedTiles > 0)
        {
            var changedSet = changed.Select(t => TileIndex(t.Column, t.Row)).ToHashSet();
            for (var i = 0; i < _levels.Length && refined < maxRefinedTiles; i++)
            {
                if (_levels[i] is not FrameQuality.Exact and not NotSent && !changedSet.Contains(i))
                {
                    var (column, row) = (i % _columns, i / _columns);
                    tiles.Add((column, row, FrameQuality.Exact));
                    dirty = dirty.Union(TileRect(column, row, frame.Width, frame.Height, _tileSize));
                    refined++;
                }
            }
        }

        return tiles.Count == 0 ? null : new EncodedFrame(Write(_previous!, tiles, keyframe), keyframe, changed.Count, refined, dirty);
    }

    /// <summary>The tiles whose pixels differ between two pictures of the same size, row by row.</summary>
    /// <param name="previous">The earlier picture.</param>
    /// <param name="current">The later picture.</param>
    /// <param name="tileSize">The tile size.</param>
    /// <returns>The changed tiles' columns and rows, top to bottom and left to right.</returns>
    public static IReadOnlyList<(int Column, int Row)> FindChangedTiles(FrameBitmap previous, FrameBitmap current, int tileSize)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (previous.Width != current.Width || previous.Height != current.Height)
        {
            throw new ArgumentException("The pictures differ in size.", nameof(current));
        }

        var columns = (current.Width + tileSize - 1) / tileSize;
        var rows = (current.Height + tileSize - 1) / tileSize;
        var changed = new List<(int, int)>();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var rect = TileRect(column, row, current.Width, current.Height, tileSize);
                var length = rect.Width * FrameBitmap.BytesPerPixel;
                for (var y = rect.Y; y < rect.Bottom; y++)
                {
                    var offset = (y * current.Stride) + (rect.X * FrameBitmap.BytesPerPixel);
                    if (!previous.Pixels.AsSpan(offset, length).SequenceEqual(current.Pixels.AsSpan(offset, length)))
                    {
                        changed.Add((column, row));
                        break;
                    }
                }
            }
        }

        return changed;
    }

    /// <summary>A tile's pixels in a picture: tiles on the right and bottom edges are cut to the picture.</summary>
    internal static PixelRect TileRect(int column, int row, int width, int height, int tileSize)
    {
        var x = column * tileSize;
        var y = row * tileSize;
        return new PixelRect(x, y, Math.Min(tileSize, width - x), Math.Min(tileSize, height - y));
    }

    private int TileIndex(int column, int row) => (row * _columns) + column;

    private List<(int Column, int Row)> AllTiles()
    {
        var all = new List<(int, int)>(_columns * _rows);
        for (var row = 0; row < _rows; row++)
        {
            for (var column = 0; column < _columns; column++)
            {
                all.Add((column, row));
            }
        }

        return all;
    }

    private byte[] Write(FrameBitmap frame, List<(int Column, int Row, int Level)> tiles, bool keyframe)
    {
        var raw = new ArrayBufferWriter<byte>();
        var directory = new byte[HeaderLength + (tiles.Count * TileEntryLength)];
        BinaryPrimitives.WriteUInt32LittleEndian(directory, Magic);
        directory[4] = Version;
        directory[5] = keyframe ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(directory.AsSpan(6), (ushort)_tileSize);
        BinaryPrimitives.WriteUInt16LittleEndian(directory.AsSpan(8), (ushort)frame.Width);
        BinaryPrimitives.WriteUInt16LittleEndian(directory.AsSpan(10), (ushort)frame.Height);
        BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(12), (uint)tiles.Count);
        var entry = HeaderLength;
        foreach (var (column, row, level) in tiles)
        {
            var rect = TileRect(column, row, frame.Width, frame.Height, _tileSize);
            BinaryPrimitives.WriteUInt16LittleEndian(directory.AsSpan(entry), (ushort)column);
            BinaryPrimitives.WriteUInt16LittleEndian(directory.AsSpan(entry + 2), (ushort)row);
            if (TileCodec.IsSolid(frame, rect, out var color))
            {
                // One color is exact at any level.
                directory[entry + 4] = (byte)TileEncoding.Solid;
                directory[entry + 5] = FrameQuality.Exact;
                _levels[TileIndex(column, row)] = FrameQuality.Exact;
                var span = raw.GetSpan(3);
                span[0] = (byte)color;
                span[1] = (byte)(color >> 8);
                span[2] = (byte)(color >> 16);
                raw.Advance(3);
            }
            else
            {
                directory[entry + 4] = (byte)TileEncoding.Pixels;
                directory[entry + 5] = (byte)level;
                _levels[TileIndex(column, row)] = (byte)level;
                TileCodec.WritePixels(frame, rect, level, raw);
            }

            entry += TileEntryLength;
        }

        using var output = new MemoryStream(directory.Length + (raw.WrittenCount / 4));
        output.Write(directory);
        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(raw.WrittenSpan);
        }

        return output.ToArray();
    }
}
