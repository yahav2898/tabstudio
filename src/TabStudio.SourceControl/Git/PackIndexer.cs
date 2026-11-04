using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace TabStudio.SourceControl.Git;

/// <summary>What indexing a pack found.</summary>
/// <param name="Checksum">The pack's trailing SHA-1, which names it.</param>
/// <param name="Ids">Every object in the pack.</param>
/// <param name="Index">The pack's <c>.idx</c> file, version 2, as Git writes it.</param>
internal sealed record IndexedPack(string Checksum, IReadOnlyList<string> Ids, byte[] Index);

/// <summary>
/// Does what <c>git index-pack</c> does: walks a pack object by object, resolves its deltas, names every object and
/// writes the index Git reads packs with. A pack that ended early, as a broken download leaves it, can still give up
/// the objects that arrived whole.
/// </summary>
internal static class PackIndexer
{
    /// <summary>How much resolved content is kept while deltas are applied.</summary>
    private const long CacheBudget = 96L << 20;

    /// <summary>Indexes a complete pack.</summary>
    /// <param name="packPath">The pack file.</param>
    /// <param name="externalBase">Finds a delta base that is not in the pack, as a thin pack's bases are not.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <exception cref="InvalidDataException">The pack is damaged, incomplete or needs a base that cannot be found.</exception>
    public static IndexedPack Index(string packPath, Func<string, GitObject?> externalBase, CancellationToken cancellationToken)
    {
        var scan = Scan(packPath, salvage: false, cancellationToken);
        Resolve(packPath, scan.Entries, externalBase, salvage: false, sink: null, cancellationToken);
        ComputeCrcs(packPath, scan.Entries);
        return new IndexedPack(scan.Checksum!, [.. scan.Entries.Select(e => e.Id!)], WriteIndex(scan.Entries, scan.Checksum!));
    }

    /// <summary>
    /// Gives each object of a pack that arrived whole, with its name, even when the pack ended early or is damaged
    /// further on; objects whose delta base did not arrive are left out.
    /// </summary>
    /// <param name="packPath">The pack file, complete or not.</param>
    /// <param name="externalBase">Finds a delta base that is not in the pack.</param>
    /// <param name="sink">Receives each object.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Whether the pack was complete.</returns>
    public static bool Salvage(string packPath, Func<string, GitObject?> externalBase, Action<string, GitObject> sink, CancellationToken cancellationToken)
    {
        var scan = Scan(packPath, salvage: true, cancellationToken);
        Resolve(packPath, scan.Entries, externalBase, salvage: true, sink, cancellationToken);
        return scan.Checksum is not null;
    }

    private static ScanResult Scan(string packPath, bool salvage, CancellationToken cancellationToken)
    {
        var entries = new List<Entry>();
        using var file = new FileStream(packPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1);
        var input = new PackInput(file);
        uint count;
        try
        {
            count = PackFormat.ReadPackHeader(input);
        }
        catch (EndOfStreamException) when (salvage)
        {
            return new ScanResult(entries, null);
        }

        var inflater = new Inflater();
        for (var i = 0u; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = input.Position;
            try
            {
                var (type, size) = PackFormat.ReadObjectHeader(input);
                var entry = new Entry { Offset = start, Type = type, Size = size };
                if (type == PackFormat.OffsetDelta)
                {
                    entry.BaseOffset = start - PackFormat.ReadDeltaDistance(input);
                    if (entry.BaseOffset < PackFormat.HeaderSize || entry.BaseOffset >= start)
                    {
                        throw new InvalidDataException("A packed delta's base is outside the pack.");
                    }
                }
                else if (type == PackFormat.ReferenceDelta)
                {
                    var id = new byte[GitHash.Size];
                    input.ReadExactly(id);
                    entry.BaseId = GitHash.ToHex(id);
                }
                else if (!PackFormat.IsObjectKind(type))
                {
                    throw new InvalidDataException("A pack holds an object of an unknown type.");
                }

                entry.DataOffset = input.Position;
                if (PackFormat.IsObjectKind(type))
                {
                    entry.Kind = (GitObjectKind)type;
                    using var hasher = new HashingStream(entry.Kind, size);
                    if (inflater.Inflate(input, hasher) != size)
                    {
                        throw new InvalidDataException("A packed object is not the size its header gives.");
                    }

                    entry.Id = hasher.Finish();
                }
                else if (inflater.Inflate(input, Stream.Null) != size)
                {
                    throw new InvalidDataException("A packed delta is not the size its header gives.");
                }

                entry.End = input.Position;
                entries.Add(entry);
            }
            catch (Exception ex) when (salvage && ex is EndOfStreamException or InvalidDataException)
            {
                return new ScanResult(entries, null);
            }
        }

        var end = input.Position;
        var trailer = new byte[GitHash.Size];
        try
        {
            input.ReadExactly(trailer);
        }
        catch (EndOfStreamException) when (salvage)
        {
            return new ScanResult(entries, null);
        }

        var checksum = GitHash.ToHex(trailer);
        if (!string.Equals(checksum, HashPrefix(file, end), StringComparison.Ordinal))
        {
            return salvage ? new ScanResult(entries, null) : throw new InvalidDataException("The pack's checksum does not match its content.");
        }

        return new ScanResult(entries, checksum);
    }

    private static void Resolve(string packPath, List<Entry> entries, Func<string, GitObject?> externalBase, bool salvage, Action<string, GitObject>? sink, CancellationToken cancellationToken)
    {
        using var file = new FileStream(packPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var byOffset = entries.ToDictionary(e => e.Offset);
        var byId = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(e => e.Id is not null))
        {
            byId.TryAdd(entry.Id!, entry);
        }

        var cache = new Dictionary<long, GitObject>();
        long cached = 0;

        if (sink is not null)
        {
            foreach (var entry in entries.Where(e => e.Id is not null))
            {
                cancellationToken.ThrowIfCancellationRequested();
                sink(entry.Id!, new GitObject(entry.Kind, InflateAt(file, entry.DataOffset, entry.Size)));
            }
        }

        // A delta resolved on the way to another stays pending until its own turn, so the sink sees every object once.
        var pending = entries.Where(e => e.Id is null).ToList();
        var done = new HashSet<Entry>();
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var entry in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Load(entry, 0) is { } resolved)
                {
                    progressed = true;
                    done.Add(entry);
                    sink?.Invoke(entry.Id!, resolved);
                }
            }

            pending.RemoveAll(done.Contains);
            if (!progressed)
            {
                break;
            }
        }

        if (pending.Count > 0 && !salvage)
        {
            throw new InvalidDataException($"The pack needs {pending.Count} objects it does not hold, such as {pending[0].BaseId ?? "an earlier object"}.");
        }

        GitObject? Load(Entry entry, int depth)
        {
            if (depth > 10_000)
            {
                throw new InvalidDataException("A packed delta chain is too long.");
            }

            if (cache.TryGetValue(entry.Offset, out var hit))
            {
                return hit;
            }

            if (PackFormat.IsObjectKind(entry.Type))
            {
                return Remember(entry, new GitObject(entry.Kind, InflateAt(file, entry.DataOffset, entry.Size)));
            }

            GitObject? baseObject;
            if (entry.BaseOffset >= 0)
            {
                baseObject = byOffset.TryGetValue(entry.BaseOffset, out var baseEntry)
                    ? Load(baseEntry, depth + 1)
                    : throw new InvalidDataException("A packed delta's base is not where the pack says.");
            }
            else if (byId.TryGetValue(entry.BaseId!, out var named))
            {
                baseObject = Load(named, depth + 1);
            }
            else
            {
                baseObject = externalBase(entry.BaseId!);
            }

            if (baseObject is null)
            {
                return null;
            }

            var content = DeltaPatch.Apply(baseObject.Content, InflateAt(file, entry.DataOffset, entry.Size));
            var result = new GitObject(baseObject.Kind, content);
            if (entry.Id is null)
            {
                entry.Kind = result.Kind;
                entry.Id = GitHash.Of(result.Kind, content);
                byId.TryAdd(entry.Id, entry);
            }

            return Remember(entry, result);
        }

        GitObject Remember(Entry entry, GitObject value)
        {
            if (cached + value.Content.Length > CacheBudget)
            {
                cache.Clear();
                cached = 0;
            }

            if (value.Content.Length <= CacheBudget / 4)
            {
                cache[entry.Offset] = value;
                cached += value.Content.Length;
            }

            return value;
        }
    }

    /// <summary>Inflates the object whose compressed data starts at an offset.</summary>
    internal static byte[] InflateAt(FileStream file, long dataOffset, long size)
    {
        if (size > Array.MaxLength)
        {
            throw new InvalidDataException("A packed object is too large to read into memory.");
        }

        file.Position = dataOffset;
        using var inflate = new ZLibStream(file, CompressionMode.Decompress, leaveOpen: true);
        var content = new byte[size];
        inflate.ReadExactly(content);
        return content;
    }

    private static void ComputeCrcs(string packPath, List<Entry> entries)
    {
        using var file = new FileStream(packPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[64 * 1024];
        foreach (var entry in entries)
        {
            file.Position = entry.Offset;
            var remaining = entry.End - entry.Offset;
            uint crc = 0;
            while (remaining > 0)
            {
                var read = file.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read == 0)
                {
                    throw new InvalidDataException("The pack ended in the middle of an object.");
                }

                crc = Crc32.Append(crc, buffer.AsSpan(0, read));
                remaining -= read;
            }

            entry.Crc = crc;
        }
    }

    private static string HashPrefix(FileStream file, long length)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        var buffer = new byte[64 * 1024];
        file.Position = 0;
        var remaining = length;
        while (remaining > 0)
        {
            var read = file.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                break;
            }

            sha.AppendData(buffer, 0, read);
            remaining -= read;
        }

        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private static byte[] WriteIndex(List<Entry> entries, string checksum)
    {
        var sorted = entries.OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
        using var output = new MemoryStream();
        Span<byte> word = stackalloc byte[8];

        output.Write(PackFile.IndexHeader);
        var fanout = new uint[256];
        foreach (var entry in sorted)
        {
            fanout[Convert.ToByte(entry.Id![..2], 16)]++;
        }

        uint running = 0;
        for (var i = 0; i < 256; i++)
        {
            running += fanout[i];
            BinaryPrimitives.WriteUInt32BigEndian(word, running);
            output.Write(word[..4]);
        }

        foreach (var entry in sorted)
        {
            output.Write(GitHash.FromHex(entry.Id!));
        }

        foreach (var entry in sorted)
        {
            BinaryPrimitives.WriteUInt32BigEndian(word, entry.Crc);
            output.Write(word[..4]);
        }

        var large = new List<long>();
        foreach (var entry in sorted)
        {
            if (entry.Offset < 0x80000000L)
            {
                BinaryPrimitives.WriteUInt32BigEndian(word, (uint)entry.Offset);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(word, 0x80000000u | (uint)large.Count);
                large.Add(entry.Offset);
            }

            output.Write(word[..4]);
        }

        foreach (var offset in large)
        {
            BinaryPrimitives.WriteUInt64BigEndian(word, (ulong)offset);
            output.Write(word);
        }

        output.Write(GitHash.FromHex(checksum));
        output.Write(SHA1Of(output.GetBuffer().AsSpan(0, (int)output.Length)));
        return output.ToArray();
    }

    private static byte[] SHA1Of(ReadOnlySpan<byte> data)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        sha.AppendData(data);
        return sha.GetHashAndReset();
    }

    private sealed record ScanResult(List<Entry> Entries, string? Checksum);

    private sealed class Entry
    {
        public long Offset { get; init; }

        public int Type { get; init; }

        public long Size { get; init; }

        public long DataOffset { get; set; }

        public long End { get; set; }

        public long BaseOffset { get; set; } = -1;

        public string? BaseId { get; set; }

        public string? Id { get; set; }

        public GitObjectKind Kind { get; set; }

        public uint Crc { get; set; }
    }
}
