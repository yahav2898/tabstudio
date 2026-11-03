using System.Buffers.Binary;
using System.IO.Compression;

namespace TabStudio.SourceControl.Git;

/// <summary>A pack and its version 2 index, as Git keeps them in <c>objects/pack</c>, read by object name.</summary>
internal sealed class PackFile
{
    private readonly byte[] _index;
    private readonly int _count;

    private PackFile(string packPath, byte[] index)
    {
        PackPath = packPath;
        _index = index;
        _count = (int)BinaryPrimitives.ReadUInt32BigEndian(index.AsSpan(8 + (255 * 4)));
    }

    /// <summary>The first eight bytes of a version 2 pack index.</summary>
    internal static ReadOnlySpan<byte> IndexHeader => [0xFF, 0x74, 0x4F, 0x63, 0, 0, 0, 2];

    /// <summary>The pack file.</summary>
    public string PackPath { get; }

    /// <summary>How many objects the pack holds.</summary>
    public int Count => _count;

    /// <summary>Opens a pack by its index; <see langword="null"/> when the index is not version 2 or the pack is missing.</summary>
    public static PackFile? Open(string indexPath)
    {
        var packPath = Path.ChangeExtension(indexPath, ".pack");
        if (!File.Exists(packPath))
        {
            return null;
        }

        var index = File.ReadAllBytes(indexPath);
        if (index.Length < 8 + (256 * 4) + 40 || !index.AsSpan(0, 8).SequenceEqual(IndexHeader))
        {
            return null;
        }

        return new PackFile(packPath, index);
    }

    /// <summary>Every object name in the pack.</summary>
    public IEnumerable<string> Ids()
    {
        for (var i = 0; i < _count; i++)
        {
            yield return GitHash.ToHex(IdAt(i));
        }
    }

    /// <summary>Finds an object's offset in the pack.</summary>
    public bool TryFind(ReadOnlySpan<byte> id, out long offset)
    {
        offset = 0;
        var first = id[0];
        var low = first == 0 ? 0 : (int)Fanout(first - 1);
        var high = (int)Fanout(first) - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var comparison = IdAt(middle).SequenceCompareTo(id);
            if (comparison == 0)
            {
                offset = OffsetAt(middle);
                return true;
            }

            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return false;
    }

    /// <summary>Reads the object at an offset, resolving deltas.</summary>
    /// <param name="offset">Where the object starts.</param>
    /// <param name="external">Finds the base of a delta that names an object outside this pack.</param>
    public GitObject Read(long offset, Func<string, GitObject?> external)
    {
        using var file = new FileStream(PackPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1);
        return Read(file, offset, external, 0);
    }

    /// <summary>
    /// Opens the content of a blob stored whole (not as a delta) for reading as a stream, so a large file is written
    /// out without holding it in memory; <see langword="null"/> when it is stored as a delta.
    /// </summary>
    public Stream? OpenWhole(long offset, out GitObjectKind kind, out long size)
    {
        var file = new FileStream(PackPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1);
        try
        {
            var input = new PackInput(file) { Position = offset };
            var (type, length) = PackFormat.ReadObjectHeader(input);
            kind = (GitObjectKind)type;
            size = length;
            if (!PackFormat.IsObjectKind(type))
            {
                file.Dispose();
                return null;
            }

            file.Position = input.Position;
            return new ZLibStream(file, CompressionMode.Decompress, leaveOpen: false);
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    private GitObject Read(FileStream file, long offset, Func<string, GitObject?> external, int depth)
    {
        if (depth > 10_000)
        {
            throw new InvalidDataException("A packed delta chain is too long.");
        }

        var input = new PackInput(file) { Position = offset };
        var (type, size) = PackFormat.ReadObjectHeader(input);
        if (PackFormat.IsObjectKind(type))
        {
            return new GitObject((GitObjectKind)type, PackIndexer.InflateAt(file, input.Position, size));
        }

        GitObject baseObject;
        if (type == PackFormat.OffsetDelta)
        {
            var baseOffset = offset - PackFormat.ReadDeltaDistance(input);
            var dataOffset = input.Position;
            baseObject = Read(file, baseOffset, external, depth + 1);
            return new GitObject(baseObject.Kind, DeltaPatch.Apply(baseObject.Content, PackIndexer.InflateAt(file, dataOffset, size)));
        }

        if (type == PackFormat.ReferenceDelta)
        {
            var id = new byte[GitHash.Size];
            input.ReadExactly(id);
            var dataOffset = input.Position;
            baseObject = TryFind(id, out var local)
                ? Read(file, local, external, depth + 1)
                : external(GitHash.ToHex(id)) ?? throw new InvalidDataException($"A packed delta's base {GitHash.ToHex(id)} is missing.");
            return new GitObject(baseObject.Kind, DeltaPatch.Apply(baseObject.Content, PackIndexer.InflateAt(file, dataOffset, size)));
        }

        throw new InvalidDataException("A pack holds an object of an unknown type.");
    }

    private uint Fanout(int value) => BinaryPrimitives.ReadUInt32BigEndian(_index.AsSpan(8 + (value * 4)));

    private ReadOnlySpan<byte> IdAt(int position) => _index.AsSpan(8 + (256 * 4) + (position * GitHash.Size), GitHash.Size);

    private long OffsetAt(int position)
    {
        var offsets = 8 + (256 * 4) + (_count * (GitHash.Size + 4));
        var value = BinaryPrimitives.ReadUInt32BigEndian(_index.AsSpan(offsets + (position * 4)));
        if ((value & 0x80000000u) == 0)
        {
            return value;
        }

        var large = offsets + (_count * 4) + ((int)(value & 0x7FFFFFFFu) * 8);
        return (long)BinaryPrimitives.ReadUInt64BigEndian(_index.AsSpan(large));
    }
}
