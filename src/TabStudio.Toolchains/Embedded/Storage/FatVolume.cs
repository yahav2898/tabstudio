using System.Buffers.Binary;
using System.Text;

namespace TabStudio.Toolchains.Embedded.Storage;

/// <summary>A bounded FAT12/16/32 root-file reader/writer for 512-byte-sector BOOTSEL volumes.
/// Never formats media, overwrites an existing file, edits a boot sector or writes outside the parsed volume.</summary>
public sealed class FatVolume
{
    private sealed record DirectorySector(uint Sector, byte[] Bytes);
    private sealed record RootEntry(string Name, byte Attributes, uint Cluster, uint Size);
    private readonly IBlockDevice _device;
    private readonly byte[] _boot;
    private readonly byte[] _fat;
    private readonly uint _fatStart, _fatSectors, _dataStart, _rootStart, _rootSectors, _rootCluster;
    private readonly int _bits, _fatCopies, _sectorsPerCluster, _activeFat;
    private readonly bool _mirrored;
    private readonly ushort _fsInfo, _backupBoot;

    private FatVolume(IBlockDevice device, byte[] boot, FatVolumeInfo info, byte[] fat, uint fatStart,
        uint fatSectors, uint dataStart, uint rootStart, uint rootSectors, uint rootCluster,
        int bits, int copies, int sectorsPerCluster, int activeFat, bool mirrored, ushort fsInfo, ushort backupBoot)
    {
        _device = device; _boot = boot; Info = info; _fat = fat; _fatStart = fatStart; _fatSectors = fatSectors;
        _dataStart = dataStart; _rootStart = rootStart; _rootSectors = rootSectors; _rootCluster = rootCluster;
        _bits = bits; _fatCopies = copies; _sectorsPerCluster = sectorsPerCluster;
        _activeFat = activeFat; _mirrored = mirrored; _fsInfo = fsInfo; _backupBoot = backupBoot;
    }

    /// <summary>Actual filesystem geometry; it is never selected from a board-name guess.</summary>
    public FatVolumeInfo Info { get; }
    internal IBlockDevice BlockDevice => _device;

    /// <summary>Reads the BPB, FAT and applicable mirrors before exposing any write operation.</summary>
    public static async Task<FatVolume> OpenAsync(IBlockDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (device.SectorSize != 512 || device.SectorCount is 0 or > uint.MaxValue)
            throw new NotSupportedException("BOOTSEL FAT access requires an actual 512-byte-sector device within READ(10) addressing.");
        var boot = await Read(device, 0, 1, cancellationToken).ConfigureAwait(false);
        uint start = 0;
        var partitionCapacity = device.SectorCount;
        if (!LooksLikeBpb(boot))
        {
            if (U16(boot, 510) != 0xAA55) throw new InvalidDataException("No FAT BPB or valid MBR signature.");
            var partitions = new List<(uint Start, uint Count)>();
            for (var i = 0; i < 4; i++)
            {
                var p = 446 + i * 16;
                if (boot[p + 4] == 0) continue;
                if (boot[p + 4] is not (0x01 or 0x04 or 0x06 or 0x0B or 0x0C or 0x0E))
                    throw new NotSupportedException("Only a single ordinary FAT MBR partition is supported; GPT/extended partitions are refused.");
                partitions.Add((U32(boot, p + 8), U32(boot, p + 12)));
            }
            if (partitions.Count != 1 || partitions[0].Start == 0 || partitions[0].Count == 0
                || (ulong)partitions[0].Start + partitions[0].Count > device.SectorCount)
            {
                throw new InvalidDataException("A single valid FAT partition is required.");
            }
            start = partitions[0].Start;
            partitionCapacity = partitions[0].Count;
            boot = await Read(device, start, 1, cancellationToken).ConfigureAwait(false);
        }
        if (!LooksLikeBpb(boot) || U16(boot, 510) != 0xAA55) throw new InvalidDataException("Invalid FAT boot sector.");
        var spc = boot[13];
        var reserved = U16(boot, 14);
        var copies = boot[16];
        var rootEntries = U16(boot, 17);
        var total = U16(boot, 19) is var small && small != 0 ? small : U32(boot, 32);
        var fatSectors = U16(boot, 22) is var shortFat && shortFat != 0 ? shortFat : U32(boot, 36);
        if (spc is 0 or > 128 || (spc & (spc - 1)) != 0 || reserved == 0 || copies is < 1 or > 2 || total == 0 || fatSectors == 0)
            throw new InvalidDataException("Invalid FAT allocation geometry.");
        var rootSectors = ((uint)rootEntries * 32 + 511) / 512;
        var overhead = checked((ulong)reserved + (ulong)copies * fatSectors + rootSectors);
        if (overhead >= total || total > partitionCapacity || (ulong)start + total > device.SectorCount)
            throw new InvalidDataException("FAT volume exceeds its actual device capacity.");
        var clusters = (uint)((total - overhead) / spc);
        var bits = clusters < 4085 ? 12 : clusters < 65525 ? 16 : 32;
        if (clusters == 0 || clusters >= 0x0FFFFFF5 || fatSectors > 32768
            || ((ulong)clusters + 2) * (uint)bits > (ulong)fatSectors * 512 * 8)
        {
            throw new InvalidDataException("FAT table cannot represent its data clusters or exceeds the 16 MB table limit.");
        }
        if (bits == 32 ? rootEntries != 0 || shortFat != 0 || U16(boot, 42) != 0 : rootEntries == 0 || rootEntries % 16 != 0 || shortFat == 0)
            throw new InvalidDataException("BPB root-directory/FAT fields do not match the cluster-count FAT type.");
        var flags = bits == 32 ? U16(boot, 40) : 0;
        var mirrored = (flags & 0x80) == 0;
        var active = mirrored ? 0 : flags & 0x0F;
        if ((flags & ~0x8F) != 0 || active >= copies) throw new InvalidDataException("Invalid FAT32 active/mirroring flags.");
        var fatStart = checked(start + reserved);
        var fat = await Read(device, checked(fatStart + (uint)active * fatSectors), (int)fatSectors, cancellationToken).ConfigureAwait(false);
        if (mirrored)
        {
            for (var copy = 1; copy < copies; copy++)
            {
                var mirror = await Read(device, checked(fatStart + (uint)copy * fatSectors), (int)fatSectors, cancellationToken).ConfigureAwait(false);
                if (!fat.AsSpan().SequenceEqual(mirror)) throw new InvalidDataException("FAT mirrors disagree; no repair or format is attempted.");
            }
        }
        var rootStart = checked(fatStart + (uint)copies * fatSectors);
        var dataStart = checked(rootStart + rootSectors);
        var rootCluster = bits == 32 ? U32(boot, 44) & 0x0FFFFFFF : 0;
        if (bits == 32 && (rootCluster < 2 || rootCluster > clusters + 1)) throw new InvalidDataException("Invalid FAT32 root cluster.");
        var label = Encoding.ASCII.GetString(boot, bits == 32 ? 71 : 43, 11).Trim();
        var result = new FatVolume(device, boot, new("FAT" + bits, label, start, total, spc * 512, clusters), fat,
            fatStart, fatSectors, dataStart, rootStart, rootSectors, rootCluster, bits, copies, spc, active, mirrored,
            bits == 32 ? U16(boot, 48) : (ushort)0xFFFF, bits == 32 ? U16(boot, 50) : (ushort)0xFFFF);
        if ((result.FatEntry(fat, 0) & 255) != boot[21] || result.FatEntry(fat, 1) < result.EndOfChain - 7)
            throw new InvalidDataException("FAT reserved/media entries are invalid or the volume is marked unclean; no repair is attempted.");
        return result;
    }

    /// <summary>Reads a regular root file by short name, following the actual FAT chain with cycle/bounds checks.</summary>
    public async Task<byte[]> ReadRootFileAsync(string name, int maximumBytes = 65536, CancellationToken cancellationToken = default)
    {
        var encoded = ShortName(name);
        var sectors = await RootSectorsAsync(cancellationToken).ConfigureAwait(false);
        RootEntry? found = null;
        foreach (var sector in sectors)
        {
            for (var at = 0; at < 512; at += 32)
            {
                if (sector.Bytes[at] == 0) goto Complete;
                if (sector.Bytes[at] == 0xE5 || (sector.Bytes[at + 11] & 0x18) != 0 || sector.Bytes[at + 11] == 0x0F) continue;
                if (sector.Bytes.AsSpan(at, 11).SequenceEqual(encoded))
                {
                    if (found is not null) throw new InvalidDataException("Duplicate FAT root filename.");
                    found = Entry(sector.Bytes, at);
                }
            }
        }
    Complete:
        if (found is null) throw new FileNotFoundException("The selected FAT volume has no " + name + ".");
        if (found.Size > maximumBytes) throw new InvalidDataException("FAT file exceeds its bounded read limit.");
        return await ReadFileAsync(found, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Plans a new 8.3 file without writing. Existing root files and occupied clusters are preserved.</summary>
    internal async Task<FatFilePlan> PrepareFileAsync(string name, ReadOnlyMemory<byte> content, CancellationToken token)
    {
        if (content.Length == 0 || content.Length % 512 != 0) throw new ArgumentException("UF2 files must contain complete 512-byte blocks.", nameof(content));
        var encoded = ShortName(name);
        var roots = await RootSectorsAsync(token).ConfigureAwait(false);
        var existing = new HashSet<uint>(_bits == 32 ? Chain(_rootCluster) : []);
        (int Sector, int Offset)? slot = null;
        var endMarker = false;
        foreach (var (sector, index) in roots.Select((sector, index) => (sector, index)))
        {
            for (var at = 0; at < 512; at += 32)
            {
                var first = sector.Bytes[at];
                if (first == 0) { slot ??= (index, at); endMarker = slot == (index, at); goto FoundEnd; }
                if (first == 0xE5) { slot ??= (index, at); }
                else
                {
                    if (sector.Bytes.AsSpan(at, 11).SequenceEqual(encoded)) throw new IOException("The destination FAT file already exists; it will not be overwritten.");
                    var entry = Entry(sector.Bytes, at);
                    if (entry.Attributes == 0x0F || (entry.Attributes & 8) != 0) continue;
                    if ((entry.Attributes & 0x10) != 0) throw new NotSupportedException("BOOTSEL upload requires a regular ROM root directory; subdirectories are not rewritten.");
                    if (entry.Size == 0 && entry.Cluster == 0) continue;
                    var chain = Chain(entry.Cluster).ToArray();
                    if ((ulong)chain.Length * (uint)Info.ClusterBytes < entry.Size || chain.Any(cluster => !existing.Add(cluster)))
                        throw new InvalidDataException("Existing FAT files have a short or cross-linked allocation chain.");
                }
            }
        }
    FoundEnd:
        if (slot is null) throw new IOException("The FAT root directory has no free entry; no directory extension or formatting is attempted.");
        var needed = checked((content.Length + Info.ClusterBytes - 1) / Info.ClusterBytes);
        var allocated = new List<uint>(needed);
        for (uint cluster = 2; cluster <= Info.ClusterCount + 1 && allocated.Count < needed; cluster++)
            if (FatEntry(_fat, cluster) == 0) allocated.Add(cluster);
        if (allocated.Count != needed) throw new IOException("The FAT volume has insufficient free clusters.");
        var changedFat = _fat.ToArray();
        for (var i = 0; i < allocated.Count; i++) SetFatEntry(changedFat, allocated[i], i + 1 < allocated.Count ? allocated[i + 1] : EndOfChain);
        var metadata = new Dictionary<uint, (byte[] Before, byte[] After)>();
        for (var sector = 0; sector < _fatSectors; sector++)
        {
            var original = _fat.AsSpan((int)sector * 512, 512).ToArray();
            var replacement = changedFat.AsSpan((int)sector * 512, 512).ToArray();
            if (original.AsSpan().SequenceEqual(replacement)) continue;
            for (var copy = 0; copy < _fatCopies; copy++)
            {
                if (_mirrored || copy == _activeFat) metadata.Add(checked(_fatStart + (uint)copy * _fatSectors + (uint)sector), (original, replacement));
            }
        }
        await AddFsInfoAsync(metadata, allocated, token).ConfigureAwait(false);
        var root = roots[slot.Value.Sector];
        var updated = root.Bytes.ToArray();
        updated.AsSpan(slot.Value.Offset, 32).Clear();
        encoded.CopyTo(updated, slot.Value.Offset);
        updated[slot.Value.Offset + 11] = 0x20;
        W16(updated, slot.Value.Offset + 22, 0); W16(updated, slot.Value.Offset + 24, 0x0021); // 1980-01-01, no fabricated device clock.
        W16(updated, slot.Value.Offset + 26, (ushort)(allocated[0] & 0xFFFF));
        if (_bits == 32) W16(updated, slot.Value.Offset + 20, (ushort)(allocated[0] >> 16));
        W32(updated, slot.Value.Offset + 28, (uint)content.Length);
        metadata[root.Sector] = (root.Bytes, updated);
        if (endMarker)
        {
            var nextOffset = slot.Value.Offset + 32;
            var nextIndex = slot.Value.Sector;
            if (nextOffset == 512) { nextOffset = 0; nextIndex++; }
            if (nextIndex < roots.Count)
            {
                var next = roots[nextIndex];
                var value = metadata.TryGetValue(next.Sector, out var staged) ? staged.After.ToArray() : next.Bytes.ToArray();
                value[nextOffset] = 0;
                metadata[next.Sector] = (next.Bytes, value);
            }
        }
        var blocks = new List<uint>(content.Length / 512);
        foreach (var cluster in allocated)
            for (var sector = 0; sector < _sectorsPerCluster && blocks.Count < content.Length / 512; sector++) blocks.Add(checked(ClusterSector(cluster) + (uint)sector));
        return new(name, content.ToArray(), metadata, blocks);
    }

    /// <summary>Executes a prepared file plan; metadata is restored on ordinary failure when transport remains available.
    /// A final USB data phase without status is returned as unconfirmed submission, never flash verification.</summary>
    internal async Task<FatFileWriteResult> ExecuteAsync(FatFilePlan plan, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!(await Read(_device, Info.VolumeStart, 1, token).ConfigureAwait(false)).AsSpan().SequenceEqual(_boot))
            throw new IOException("The FAT volume changed after inspection.");
        foreach (var (sector, values) in plan.Metadata)
        {
            if (!(await Read(_device, sector, 1, token).ConfigureAwait(false)).AsSpan().SequenceEqual(values.Before))
                throw new IOException("FAT allocation metadata changed after inspection; prepare the upload again.");
        }
        var written = new List<uint>();
        long submitted = 0;
        try
        {
            // Commit the allocation before any UF2 data: the ROM may reboot on the final UF2 block.
            foreach (var (sector, values) in plan.Metadata)
            {
                token.ThrowIfCancellationRequested();
                written.Add(sector); // A failed command may still have changed the sector.
                await _device.WriteBlocksAsync(sector, values.After, token).ConfigureAwait(false);
            }
            for (var i = 0; i < plan.Blocks.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                try { await _device.WriteBlocksAsync(plan.Blocks[i], plan.Content.AsMemory(i * 512, 512), token).ConfigureAwait(false); }
                catch (BlockWriteDisconnectedException error) when (i == plan.Blocks.Count - 1 && error.DataTransferred)
                {
                    return new(plan.Name, submitted + 512, false, null, true);
                }
                submitted += 512;
            }
            bool? matches;
            try
            {
                matches = true;
                for (var i = 0; i < plan.Blocks.Count; i++)
                {
                    var block = await Read(_device, plan.Blocks[i], 1, token).ConfigureAwait(false);
                    if (!block.AsSpan().SequenceEqual(plan.Content.AsSpan(i * 512, 512))) { matches = false; break; }
                }
            }
            catch (IOException) { matches = null; } // ROM reset/readback absence is not success proof.
            return new(plan.Name, submitted, true, matches, false);
        }
        catch (Exception error) when (error is IOException or OperationCanceledException)
        {
            var restored = true;
            using var rollback = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            foreach (var sector in written.AsEnumerable().Reverse())
            {
                try { await _device.WriteBlocksAsync(sector, plan.Metadata[sector].Before, rollback.Token).ConfigureAwait(false); }
                catch (Exception restoreError) when (restoreError is IOException or OperationCanceledException) { restored = false; }
            }
            throw new IOException($"UF2 copy stopped after {submitted} acknowledged payload bytes. Metadata rollback {(restored ? "was sent" : "could not complete")}; target flash cannot be rolled back by filesystem metadata.", error);
        }
    }

    private async Task<List<DirectorySector>> RootSectorsAsync(CancellationToken token)
    {
        var result = new List<DirectorySector>();
        var sectors = _bits == 32 ? Chain(_rootCluster).SelectMany(cluster => Enumerable.Range(0, _sectorsPerCluster).Select(i => checked(ClusterSector(cluster) + (uint)i)))
            : Enumerable.Range(0, checked((int)_rootSectors)).Select(i => checked(_rootStart + (uint)i));
        foreach (var sector in sectors)
        {
            if (result.Count >= 4096) throw new InvalidDataException("FAT root directory exceeds its scan limit.");
            result.Add(new(sector, await Read(_device, sector, 1, token).ConfigureAwait(false)));
        }
        return result;
    }

    private async Task<byte[]> ReadFileAsync(RootEntry entry, CancellationToken token)
    {
        var result = new byte[checked((int)entry.Size)];
        var at = 0;
        if (result.Length == 0) return result;
        foreach (var cluster in Chain(entry.Cluster))
        {
            var allocation = FatEntry(_fat, cluster);
            if (allocation < 2 || allocation >= EndOfChain - 15 && allocation < EndOfChain - 7)
                throw new InvalidDataException("A FAT file refers to a free, reserved or bad cluster.");
            var bytes = await Read(_device, ClusterSector(cluster), _sectorsPerCluster, token).ConfigureAwait(false);
            var count = Math.Min(bytes.Length, result.Length - at);
            bytes.AsSpan(0, count).CopyTo(result.AsSpan(at)); at += count;
            if (at == result.Length) return result;
        }
        throw new InvalidDataException("FAT file ends before its declared byte size.");
    }

    private IEnumerable<uint> Chain(uint first)
    {
        var visited = new HashSet<uint>();
        var current = first;
        while (true)
        {
            if (current < 2 || current > Info.ClusterCount + 1 || !visited.Add(current)) throw new InvalidDataException("Invalid or cyclic FAT cluster chain.");
            yield return current;
            var next = FatEntry(_fat, current);
            if (next >= EndOfChain - 7) yield break;
            if (next >= EndOfChain - 15) throw new InvalidDataException("A FAT chain contains a reserved/bad allocation value.");
            current = next;
        }
    }

    private async Task AddFsInfoAsync(Dictionary<uint, (byte[] Before, byte[] After)> metadata, List<uint> allocated, CancellationToken token)
    {
        if (_bits != 32 || _fsInfo is 0 or 0xFFFF || _fsInfo >= _fatStart - Info.VolumeStart) return;
        List<uint> sectors = [checked(Info.VolumeStart + _fsInfo)];
        var backup = (uint)_backupBoot + _fsInfo;
        if (_backupBoot is not (0 or 0xFFFF) && backup < _fatStart - Info.VolumeStart) sectors.Add(checked(Info.VolumeStart + backup));
        foreach (var sector in sectors)
        {
            var before = await Read(_device, sector, 1, token).ConfigureAwait(false);
            if (U32(before, 0) != 0x41615252 || U32(before, 484) != 0x61417272 || U32(before, 508) != 0xAA550000) continue;
            var after = before.ToArray();
            var free = U32(before, 488);
            W32(after, 488, free <= Info.ClusterCount && free >= allocated.Count ? free - (uint)allocated.Count : uint.MaxValue);
            W32(after, 492, uint.MaxValue); // Unknown is valid; do not publish an unverified free-cluster hint.
            metadata.Add(sector, (before, after));
        }
    }

    private RootEntry Entry(byte[] bytes, int at) => new(Encoding.ASCII.GetString(bytes, at, 11), bytes[at + 11],
        U16(bytes, at + 26) | (_bits == 32 ? (uint)U16(bytes, at + 20) << 16 : 0), U32(bytes, at + 28));
    private uint ClusterSector(uint cluster) => checked(_dataStart + (cluster - 2) * (uint)_sectorsPerCluster);
    private uint EndOfChain => _bits == 12 ? 0xFFFu : _bits == 16 ? 0xFFFFu : 0x0FFFFFFFu;
    private uint FatEntry(byte[] fat, uint cluster)
    {
        var at = checked((int)(cluster * (uint)_bits / 8));
        return _bits switch { 12 => (uint)((U16(fat, at) >> ((cluster & 1) != 0 ? 4 : 0)) & 0xFFF), 16 => U16(fat, at), _ => U32(fat, at) & 0x0FFFFFFF };
    }
    private void SetFatEntry(byte[] fat, uint cluster, uint value)
    {
        var at = checked((int)(cluster * (uint)_bits / 8));
        if (_bits == 12) W16(fat, at, (ushort)((cluster & 1) != 0 ? ((uint)U16(fat, at) & 0xFu) | value << 4 : ((uint)U16(fat, at) & 0xF000u) | value));
        else if (_bits == 16) W16(fat, at, (ushort)value);
        else W32(fat, at, (U32(fat, at) & 0xF0000000) | value);
    }
    private static bool LooksLikeBpb(byte[] sector) => sector.Length == 512 && U16(sector, 11) == 512 && sector[0] is 0xEB or 0xE9;
    private static byte[] ShortName(string name)
    {
        var parts = name.ToUpperInvariant().Split('.');
        if (parts.Length is < 1 or > 2 || parts[0].Length is < 1 or > 8 || parts.Length == 2 && parts[1].Length is < 1 or > 3
            || parts.Any(part => part.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))))
        {
            throw new ArgumentException("Use an ASCII 8.3 root filename.", nameof(name));
        }
        return Encoding.ASCII.GetBytes(parts[0].PadRight(8) + (parts.Length == 2 ? parts[1] : "").PadRight(3));
    }
    private static async Task<byte[]> Read(IBlockDevice device, uint sector, int count, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (count <= 0 || (ulong)sector + (uint)count > device.SectorCount) throw new InvalidDataException("Block read exceeds actual device capacity.");
        var bytes = await device.ReadBlocksAsync(sector, count, token).ConfigureAwait(false);
        if (bytes.Length != checked(count * 512)) throw new InvalidDataException("Short block-device read.");
        return bytes;
    }
    private static ushort U16(byte[] bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at, 2));
    private static uint U32(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
    private static void W16(byte[] bytes, int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at, 2), value);
    private static void W32(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at, 4), value);
}

internal sealed record FatFilePlan(string Name, byte[] Content,
    Dictionary<uint, (byte[] Before, byte[] After)> Metadata, IReadOnlyList<uint> Blocks);
