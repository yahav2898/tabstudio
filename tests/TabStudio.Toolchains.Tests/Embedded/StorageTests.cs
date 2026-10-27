using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Embedded.Storage;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class StorageTests
{
    private static readonly FlashAddressRange[] Application = [new(0x10000000, 4096)];

    [Theory]
    [InlineData(12, false)]
    [InlineData(16, false)]
    [InlineData(32, false)]
    [InlineData(12, true)]
    [InlineData(16, true)]
    [InlineData(32, true)]
    public async Task Fat_types_and_mbr_create_new_uf2_file_without_touching_existing_files_or_boot_sectors(int bits, bool mbr)
    {
        var disk = new Disk(bits, mbr);
        var beforeBoot = disk.Bytes(disk.Start);
        var beforeMbr = disk.Bytes(0);
        var volume = await FatVolume.OpenAsync(disk);
        Assert.Equal("FAT" + bits, volume.Info.Kind);
        var firmware = Uf2(3);
        var plan = await Uf2BootselUpload.PrepareAsync(volume, Device(), firmware, Application);
        Assert.Empty(disk.Writes);
        Assert.Contains("Board-ID: RPI-RP2", plan.BoardInformation, StringComparison.Ordinal);
        var result = await Uf2BootselUpload.ExecuteAsync(plan);
        Assert.True(result.AllCommandsAcknowledged);
        Assert.True(result.FileReadbackMatched);
        Assert.False(result.ResetDuringFinalStatus);
        var independentlyReopened = await FatVolume.OpenAsync(disk);
        Assert.Equal(firmware, await independentlyReopened.ReadRootFileAsync(plan.FileName));
        Assert.Equal(Enumerable.Repeat((byte)0x5A, 512), await independentlyReopened.ReadRootFileAsync("KEEP.BIN"));
        Assert.Equal(beforeBoot, disk.Bytes(disk.Start));
        Assert.Equal(beforeMbr, disk.Bytes(0));
        Assert.Equal(disk.FatBytes(0), disk.FatBytes(1));
        Assert.All(disk.Writes, sector => Assert.InRange(sector, disk.Start + 1, (uint)disk.SectorCount - 1));
        if (Environment.GetEnvironmentVariable("TABSTUDIO_FAT_PROOF") is { Length: > 0 } proof)
        {
            Directory.CreateDirectory(proof);
            var stem = "fat" + bits + (mbr ? "-mbr" : "-superfloppy");
            var image = Path.Combine(proof, stem + ".img");
            disk.Export(image);
            File.WriteAllBytes(Path.Combine(proof, stem + ".uf2"), firmware);
            File.WriteAllText(Path.Combine(proof, stem + ".json"), JsonSerializer.Serialize(new
            {
                image, expected = Path.Combine(proof, stem + ".uf2"), file = plan.FileName, offset = disk.Start * 512ul,
            }));
        }
    }

    [Fact]
    public async Task Fat12_packed_entry_crossing_sector_boundary_preserves_neighbor_nibbles()
    {
        var disk = new Disk(12);
        for (uint cluster = 4; cluster < 341; cluster++) disk.SetFat(cluster, 0xFFF);
        var volume = await FatVolume.OpenAsync(disk);
        var plan = await Uf2BootselUpload.PrepareAsync(volume, Device(), Uf2(2), Application);
        await Uf2BootselUpload.ExecuteAsync(plan);
        Assert.Equal(0xFFFu, disk.GetFat(340));
        Assert.Equal(342u, disk.GetFat(341));
        Assert.Equal(0xFFFu, disk.GetFat(342));
        Assert.Equal(0u, disk.GetFat(343));
        Assert.Equal(disk.FatBytes(0), disk.FatBytes(1));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(32)]
    public async Task Rom_sized_four_kib_clusters_preserve_whole_uf2_across_cluster_boundaries(int bits)
    {
        var disk = new Disk(bits, mbr: true, sectorsPerCluster: 8);
        var volume = await FatVolume.OpenAsync(disk);
        Assert.Equal(4096, volume.Info.ClusterBytes);
        var image = Uf2(12);
        var plan = await Uf2BootselUpload.PrepareAsync(volume, Device(), image, Application);
        await Uf2BootselUpload.ExecuteAsync(plan);
        Assert.Equal(image, await (await FatVolume.OpenAsync(disk)).ReadRootFileAsync(plan.FileName));
    }

    [Fact]
    public async Task Fat32_updates_primary_and_backup_fsinfo_with_actual_allocated_count()
    {
        var disk = new Disk(32);
        var plan = await Uf2BootselUpload.PrepareAsync(await FatVolume.OpenAsync(disk), Device(), Uf2(3), Application);
        await Uf2BootselUpload.ExecuteAsync(plan);
        Assert.Equal(disk.Clusters - 6, Read32(disk.Bytes(disk.Start + 1), 488)); // root, info, keep + three new clusters.
        Assert.Equal(disk.Bytes(disk.Start + 1), disk.Bytes(disk.Start + 7));
        Assert.Equal(uint.MaxValue, Read32(disk.Bytes(disk.Start + 1), 492));
    }

    [Fact]
    public async Task Fat32_disabled_mirroring_edits_only_the_explicit_active_fat()
    {
        var disk = new Disk(32);
        var boot = disk.Bytes(disk.Start); Write16(boot, 40, 0x81); disk.Put(disk.Start, boot);
        var inactive = disk.FatBytes(0);
        var plan = await Uf2BootselUpload.PrepareAsync(await FatVolume.OpenAsync(disk), Device(), Uf2(2), Application);
        await Uf2BootselUpload.ExecuteAsync(plan);
        Assert.Equal(inactive, disk.FatBytes(0));
        Assert.NotEqual(inactive, disk.FatBytes(1));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public async Task Fat12_bad_marker_is_an_ordinary_cluster_number_in_wider_fats(int bits)
    {
        var disk = new Disk(bits);
        var first = bits == 32 ? 4u : 3u;
        disk.SetFat(first, 0xFF7);
        disk.SetFat(0xFF7, bits == 16 ? 0xFFFFu : 0x0FFFFFFFu);
        var last = Enumerable.Repeat((byte)0xA5, 512).ToArray();
        disk.Put(disk.DataStart + 0xFF7 - 2, last);
        var root = disk.Bytes(disk.Root); Write32(root, 32 + 28, 1024); disk.Put(disk.Root, root);

        var file = await (await FatVolume.OpenAsync(disk)).ReadRootFileAsync("KEEP.BIN");

        Assert.Equal(Enumerable.Repeat((byte)0x5A, 512).Concat(last), file);
    }

    [Theory]
    [InlineData(12, 0xFF7u)]
    [InlineData(16, 0xFFF7u)]
    [InlineData(32, 0x0FFFFFF7u)]
    public async Task A_short_file_on_a_free_reserved_or_bad_cluster_is_refused_before_returning_bytes(int bits, uint bad)
    {
        foreach (var value in new[] { 0u, bad - 1, bad })
        {
            var disk = new Disk(bits);
            disk.SetFat(bits == 32 ? 4u : 3u, value);
            var volume = await FatVolume.OpenAsync(disk);

            await Assert.ThrowsAsync<InvalidDataException>(() => volume.ReadRootFileAsync("KEEP.BIN"));
            Assert.Empty(disk.Writes);
        }
    }

    [Fact]
    public async Task Arbitrary_usb_disks_wrong_board_info_wrong_family_and_protected_addresses_are_refused_before_writes()
    {
        var disk = new Disk(16);
        var volume = await FatVolume.OpenAsync(disk);
        await Assert.ThrowsAsync<NotSupportedException>(() => Uf2BootselUpload.PrepareAsync(volume, Device() with { VendorId = 0x1234 }, Uf2(1), Application));
        await Assert.ThrowsAsync<InvalidDataException>(() => Uf2BootselUpload.PrepareAsync(volume, Device(15), Uf2(1), Application));
        await Assert.ThrowsAsync<InvalidDataException>(() => Uf2BootselUpload.PrepareAsync(volume, Device(), Uf2(1, family: 0xE48BFF59), Application));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Uf2BootselUpload.PrepareAsync(volume, Device(), Uf2(1, address: 0x20000000), Application));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Uf2BootselUpload.PrepareAsync(volume, Device(), Uf2(1), [new(0, uint.MaxValue)]));
        Assert.Empty(disk.Writes);
    }

    [Fact]
    public async Task Rp2350_accepts_only_a_matching_actual_volume_and_explicit_supported_uf2_family()
    {
        var disk = new Disk(16, boardId: "RP2350");
        var volume = await FatVolume.OpenAsync(disk);
        var plan = await Uf2BootselUpload.PrepareAsync(volume, Device(15), Uf2(1, family: 0xE48BFF5A), Application);
        Assert.Equal(0xE48BFF5Au, plan.FamilyId);
        await Assert.ThrowsAsync<InvalidDataException>(() => Uf2BootselUpload.PrepareAsync(volume, Device(15), Uf2(1), Application));
        Assert.Empty(disk.Writes);
    }

    [Fact]
    public async Task Missing_family_duplicate_addresses_or_non_rom_payload_size_are_not_transferred()
    {
        var disk = new Disk(12);
        var volume = await FatVolume.OpenAsync(disk);
        var image = Uf2(2); Write32(image, 512 + 12, 0x10000000);
        await Assert.ThrowsAsync<InvalidDataException>(() => Uf2BootselUpload.PrepareAsync(volume, Device(), image, Application));
        image = Uf2(1); Write32(image, 8, 0);
        await Assert.ThrowsAsync<InvalidDataException>(() => Uf2BootselUpload.PrepareAsync(volume, Device(), image, Application));
        image = Uf2(1); Write32(image, 16, 128);
        await Assert.ThrowsAsync<InvalidDataException>(() => Uf2BootselUpload.PrepareAsync(volume, Device(), image, Application));
        Assert.Empty(disk.Writes);
    }

    [Fact]
    public async Task Cancellation_before_commit_writes_nothing_and_failed_payload_restores_metadata_when_possible()
    {
        var disk = new Disk(16);
        var before = disk.Snapshot();
        var plan = await Uf2BootselUpload.PrepareAsync(await FatVolume.OpenAsync(disk), Device(), Uf2(3), Application);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Uf2BootselUpload.ExecuteAsync(plan, cancellation.Token));
        Assert.Empty(disk.Writes);
        disk.FailBlockNumber = 1;
        var error = await Assert.ThrowsAsync<IOException>(() => Uf2BootselUpload.ExecuteAsync(plan));
        Assert.Contains("Metadata rollback was sent", error.Message, StringComparison.Ordinal);
        foreach (var (sector, bytes) in before.Where(row => row.Key < disk.DataStart)) Assert.Equal(bytes, disk.Bytes(sector));
    }

    [Fact]
    public async Task Final_data_accepted_without_csw_is_only_an_unconfirmed_submission()
    {
        var disk = new Disk(16) { ResetOnBlockNumber = 1 };
        var plan = await Uf2BootselUpload.PrepareAsync(await FatVolume.OpenAsync(disk), Device(), Uf2(2), Application);
        var result = await Uf2BootselUpload.ExecuteAsync(plan);
        Assert.False(result.AllCommandsAcknowledged);
        Assert.Null(result.FileReadbackMatched);
        Assert.True(result.ResetDuringFinalStatus);
        Assert.Equal(1024, result.BytesSubmitted);
    }

    [Fact]
    public async Task Mismatched_mirrors_bpb_outside_partition_and_allocation_races_are_rejected()
    {
        var disk = new Disk(16);
        var mirror = disk.Bytes(disk.FatStart + disk.FatSectors); mirror[20] = 0xAA; disk.Put(disk.FatStart + disk.FatSectors, mirror);
        await Assert.ThrowsAsync<InvalidDataException>(() => FatVolume.OpenAsync(disk));
        disk = new Disk(16, true);
        var mbr = disk.Bytes(0); Write32(mbr, 446 + 12, 8); disk.Put(0, mbr);
        await Assert.ThrowsAsync<InvalidDataException>(() => FatVolume.OpenAsync(disk));
        disk = new Disk(16);
        var plan = await Uf2BootselUpload.PrepareAsync(await FatVolume.OpenAsync(disk), Device(), Uf2(1), Application);
        disk.SetFat(4, 0xFFFF);
        await Assert.ThrowsAsync<IOException>(() => Uf2BootselUpload.ExecuteAsync(plan));
        Assert.Empty(disk.Writes);
    }

    [Fact]
    public async Task Fat_cluster_cycles_and_full_root_directory_are_not_repaired_or_formatted()
    {
        var disk = new Disk(16); disk.SetFat(2, 2);
        var root = disk.Bytes(disk.Root); Write32(root, 28, 1024); disk.Put(disk.Root, root);
        var volume = await FatVolume.OpenAsync(disk);
        await Assert.ThrowsAsync<InvalidDataException>(() => volume.ReadRootFileAsync("INFO_UF2.TXT"));
        disk = new Disk(16);
        for (uint sector = disk.Root; sector < disk.Root + 2; sector++)
        {
            var bytes = disk.Bytes(sector);
            for (var at = sector == disk.Root ? 64 : 0; at < 512; at += 32) { Encoding.ASCII.GetBytes("OCCUPIEDTXT").CopyTo(bytes, at); bytes[at + 11] = 0x20; }
            disk.Put(sector, bytes);
        }
        volume = await FatVolume.OpenAsync(disk);
        await Assert.ThrowsAsync<IOException>(() => Uf2BootselUpload.PrepareAsync(volume, Device(), Uf2(1), Application));
        Assert.Empty(disk.Writes);
    }

    internal static byte[] Uf2(int count, uint family = 0xE48BFF56, uint address = 0x10000000)
    {
        var bytes = new byte[count * 512];
        for (var i = 0; i < count; i++)
        {
            var at = i * 512; Write32(bytes, at, 0x0A324655); Write32(bytes, at + 4, 0x9E5D5157);
            Write32(bytes, at + 8, 0x2000); Write32(bytes, at + 12, address + (uint)i * 256); Write32(bytes, at + 16, 256);
            Write32(bytes, at + 20, (uint)i); Write32(bytes, at + 24, (uint)count); Write32(bytes, at + 28, family);
            bytes.AsSpan(at + 32, 256).Fill((byte)(i + 1)); Write32(bytes, at + 508, 0x0AB16F30);
        }
        return bytes;
    }
    private static UsbDeviceInfo Device(int product = 3) => new("rp-rom", 0x2E8A, product, "RP ROM",
        [new(0, 8, 6, 0x50, [new(0x81, 2, 64), new(0x02, 2, 64)])]);
    private static uint Read32(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static void Write32(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
    private static void Write16(byte[] bytes, int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at), value);

    private sealed class Disk : IBlockDevice
    {
        private readonly Dictionary<uint, byte[]> _sectors = [];
        private readonly int _bits;
        public Disk(int bits, bool mbr = false, string boardId = "RPI-RP2", int sectorsPerCluster = 1)
        {
            _bits = bits; Start = mbr ? 2048u : 0; Clusters = bits == 12 ? 4000u : bits == 16 ? 5000u : 65525u;
            FatSectors = bits == 12 ? 12u : bits == 16 ? 20u : 512u;
            var reserved = bits == 32 ? 32u : 1u;
            FatStart = Start + reserved; Root = FatStart + 2 * FatSectors;
            DataStart = Root + (bits == 32 ? 0u : 2u);
            var total = DataStart - Start + Clusters * (uint)sectorsPerCluster;
            SectorCount = Start + total;
            var boot = new byte[512]; boot[0] = 0xEB; boot[1] = 0x3C; boot[2] = 0x90;
            Write16(boot, 11, 512); boot[13] = (byte)sectorsPerCluster; Write16(boot, 14, (ushort)reserved); boot[16] = 2;
            Write16(boot, 17, (ushort)(bits == 32 ? 0 : 32)); boot[21] = 0xF8;
            Write16(boot, 24, 63); Write16(boot, 26, 255); Write32(boot, 28, Start);
            boot[bits == 32 ? 64 : 36] = 0x80; boot[bits == 32 ? 66 : 38] = 0x29;
            Encoding.ASCII.GetBytes(("FAT" + bits).PadRight(8)).CopyTo(boot, bits == 32 ? 82 : 54);
            if (total < 65536) Write16(boot, 19, (ushort)total); else Write32(boot, 32, total);
            if (bits == 32)
            {
                Write32(boot, 36, FatSectors); Write32(boot, 44, 2); Write16(boot, 48, 1); Write16(boot, 50, 6);
                foreach (var offset in new uint[] { 1, 7 })
                {
                    var fs = new byte[512]; Write32(fs, 0, 0x41615252); Write32(fs, 484, 0x61417272);
                    Write32(fs, 488, Clusters - 3); Write32(fs, 492, 5); Write32(fs, 508, 0xAA550000); Put(Start + offset, fs);
                }
            }
            else { Write16(boot, 22, (ushort)FatSectors); }
            Encoding.ASCII.GetBytes("RPI-RP2    ").CopyTo(boot, bits == 32 ? 71 : 43);
            Write16(boot, 510, 0xAA55); Put(Start, boot);
            if (mbr)
            {
                var sector = new byte[512]; sector[450] = (byte)(bits == 12 ? 1 : bits == 16 ? 6 : 12);
                Write32(sector, 454, Start); Write32(sector, 458, total); Write16(sector, 510, 0xAA55); Put(0, sector);
            }
            SetFat(0, bits == 12 ? 0xFF8u : bits == 16 ? 0xFFF8u : 0x0FFFFFF8u);
            SetFat(1, Eoc);
            var infoCluster = bits == 32 ? 3u : 2u;
            var keepCluster = infoCluster + 1;
            if (bits == 32) SetFat(2, Eoc);
            SetFat(infoCluster, Eoc); SetFat(keepCluster, Eoc);
            var info = Encoding.ASCII.GetBytes("UF2 Bootloader v3.0\nModel: Raspberry Pi RP2\nBoard-ID: " + boardId + "\n");
            var file = new byte[512]; info.CopyTo(file, 0); Put(DataStart + (infoCluster - 2) * (uint)sectorsPerCluster, file);
            file = new byte[512]; file.AsSpan().Fill(0x5A); Put(DataStart + (keepCluster - 2) * (uint)sectorsPerCluster, file);
            if (bits == 32) Root = DataStart;
            var directory = new byte[512];
            foreach (var (name, cluster, size, at) in new[] { ("INFO_UF2TXT", infoCluster, (uint)info.Length, 0), ("KEEP    BIN", keepCluster, 512u, 32) })
            {
                Encoding.ASCII.GetBytes(name).CopyTo(directory, at); directory[at + 11] = 1;
                Write16(directory, at + 26, (ushort)cluster); Write32(directory, at + 28, size);
            }
            Put(Root, directory);
        }
        public uint Start { get; }
        public uint FatStart { get; }
        public uint FatSectors { get; }
        public uint Root { get; private set; }
        public uint DataStart { get; }
        public uint Clusters { get; }
        public int SectorSize => 512;
        public ulong SectorCount { get; }
        public List<uint> Writes { get; } = [];
        public uint? FailBlockNumber { get; set; }
        public uint? ResetOnBlockNumber { get; set; }
        private uint Eoc => _bits == 12 ? 0xFFFu : _bits == 16 ? 0xFFFFu : 0x0FFFFFFFu;
        public byte[] Bytes(uint sector) => _sectors.TryGetValue(sector, out var bytes) ? bytes.ToArray() : new byte[512];
        public void Put(uint sector, byte[] bytes) => _sectors[sector] = bytes.ToArray();
        public Dictionary<uint, byte[]> Snapshot() => _sectors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        public void Export(string path)
        {
            using var stream = File.Create(path);
            stream.SetLength(checked((long)SectorCount * 512));
            foreach (var (sector, bytes) in _sectors) { stream.Position = sector * 512L; stream.Write(bytes); }
        }
        public byte[] FatBytes(uint copy) => Enumerable.Range(0, (int)FatSectors).SelectMany(i => Bytes(FatStart + copy * FatSectors + (uint)i)).ToArray();
        public uint GetFat(uint cluster)
        {
            var fat = FatBytes(0); var at = (int)(cluster * (uint)_bits / 8);
            return _bits == 12 ? (uint)((BinaryPrimitives.ReadUInt16LittleEndian(fat.AsSpan(at)) >> ((cluster & 1) == 0 ? 0 : 4)) & 0xFFF)
                : _bits == 16 ? BinaryPrimitives.ReadUInt16LittleEndian(fat.AsSpan(at)) : Read32(fat, at) & 0x0FFFFFFF;
        }
        public void SetFat(uint cluster, uint value)
        {
            var fat = FatBytes(0); var at = (int)(cluster * (uint)_bits / 8);
            if (_bits == 12)
            {
                var original = BinaryPrimitives.ReadUInt16LittleEndian(fat.AsSpan(at));
                Write16(fat, at, (ushort)((cluster & 1) == 0 ? ((uint)original & 0xF000u) | value : ((uint)original & 0xFu) | value << 4));
            }
            else if (_bits == 16) { Write16(fat, at, (ushort)value); }
            else { Write32(fat, at, value); }
            for (uint copy = 0; copy < 2; copy++)
                for (uint sector = 0; sector < FatSectors; sector++) Put(FatStart + copy * FatSectors + sector, fat.AsSpan((int)sector * 512, 512).ToArray());
        }
        public Task<byte[]> ReadBlocksAsync(uint sector, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True((ulong)sector + (uint)count <= SectorCount);
            return Task.FromResult(Enumerable.Range(0, count).SelectMany(i => Bytes(sector + (uint)i)).ToArray());
        }
        public Task WriteBlocksAsync(uint sector, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Writes.Add(sector);
            var bytes = data.ToArray();
            if (Read32(bytes, 0) == 0x0A324655)
            {
                var number = Read32(bytes, 20);
                if (number == FailBlockNumber) { FailBlockNumber = null; throw new IOException("Synthetic SCSI write failure."); }
                if (number == ResetOnBlockNumber) throw new BlockWriteDisconnectedException("Synthetic ROM reset during final CSW.", true);
            }
            Assert.Equal(512, bytes.Length); Put(sector, bytes); return Task.CompletedTask;
        }
    }
}
