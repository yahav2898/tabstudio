using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.Toolchains.Embedded.Storage;

/// <summary>A reviewed BOOTSEL UF2 copy, prepared without any device write.</summary>
public sealed class PreparedUf2Upload
{
    internal PreparedUf2Upload(FatVolume volume, FatFilePlan file, string boardInformation, uint familyId,
        string sha256, IReadOnlyList<FlashAddressRange> regions)
    {
        Volume = volume; File = file; BoardInformation = boardInformation; FamilyId = familyId;
        FirmwareSha256 = sha256; FlashRegions = regions;
    }
    internal FatVolume Volume { get; }
    internal FatFilePlan File { get; }
    /// <summary>Actual on-device BPB geometry.</summary>
    public FatVolumeInfo VolumeInfo => Volume.Info;
    /// <summary>Actual INFO_UF2.TXT, read before programming.</summary>
    public string BoardInformation { get; }
    /// <summary>Consistent, target-compatible family encoded in every UF2 block.</summary>
    public uint FamilyId { get; }
    /// <summary>Digest of the validated UF2 container.</summary>
    public string FirmwareSha256 { get; }
    /// <summary>New FAT root filename; existing files are never overwritten.</summary>
    public string FileName => File.Name;
    /// <summary>Parsed application-flash byte ranges.</summary>
    public IReadOnlyList<FlashAddressRange> FlashRegions { get; }
    /// <summary>UF2 container bytes to submit.</summary>
    public int FileBytes => File.Content.Length;
}

/// <summary>UF2 copy restricted to RP2040/RP2350 ROM BOOTSEL devices and their actual volume identity.</summary>
public static class Uf2BootselUpload
{
    /// <summary>Recognizes only the standard Raspberry Pi ROM USB identities, with a BOT/SCSI interface.</summary>
    public static bool IsSupportedDevice(UsbDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return device.VendorId == 0x2E8A && device.ProductId is 0x0003 or 0x000F
            && device.Interfaces.Any(i => i.Class == 8 && i.Subclass == 6 && i.Protocol == 0x50);
    }

    /// <summary>Validates USB identity, on-volume INFO_UF2, every UF2 block/family and explicit application bounds,
    /// then prepares FAT allocation and a new root entry. No sectors are written by this method.</summary>
    public static async Task<PreparedUf2Upload> PrepareAsync(FatVolume volume, UsbDeviceInfo device,
        ReadOnlyMemory<byte> firmware, IReadOnlyList<FlashAddressRange> applicationFlash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);
        if (!IsSupportedDevice(device)) throw new NotSupportedException("Select an actual RP2040/RP2350 ROM BOOTSEL BOT/SCSI device. Arbitrary USB disks are refused.");
        if (firmware.Length is <= 0 or > 128 * 1024 * 1024) throw new InvalidDataException("UF2 input must contain 1 byte..128 MiB.");
        cancellationToken.ThrowIfCancellationRequested();
        var infoBytes = await volume.ReadRootFileAsync("INFO_UF2.TXT", 4096, cancellationToken).ConfigureAwait(false);
        if (infoBytes.Any(value => value is > 0x7F or 0)) throw new InvalidDataException("INFO_UF2.TXT is not bounded plain ASCII.");
        var info = Encoding.ASCII.GetString(infoBytes);
        var board = info.Replace("\r", "", StringComparison.Ordinal).Split('\n')
            .Where(line => line.StartsWith("Board-ID:", StringComparison.Ordinal)).Select(line => line[9..].Trim()).ToArray();
        var expectedBoard = device.ProductId == 0x0003 ? "RPI-RP2" : "RP2350";
        if (!info.StartsWith("UF2 Bootloader ", StringComparison.Ordinal) || board.Length != 1 || board[0] != expectedBoard)
            throw new InvalidDataException("The actual INFO_UF2 board identity does not match the selected ROM USB identity. Custom/white-labeled bootloaders require a separately reviewed route.");
        var image = firmware.ToArray();
        var ranges = FirmwareImageInspector.Uf2(image);
        FlashSafety.ValidateFirmware(ranges, applicationFlash);
        // Ordinary XIP application flash only. This conservative route excludes RAM, OTP, aliases and >16 MiB layouts.
        FlashSafety.ValidateFirmware(applicationFlash, [new(0x10000000, 0x01000000)]);
        uint? family = null;
        var addresses = new HashSet<uint>();
        for (var at = 0; at < image.Length; at += 512)
        {
            var flags = U32(image, at + 8);
            var address = U32(image, at + 12);
            var current = U32(image, at + 28);
            if (flags != 0x2000 || U32(image, at + 16) != 256 || (address & 255) != 0 || !addresses.Add(address))
                throw new InvalidDataException("RP ROM UF2 requires family-tagged, unique aligned 256-byte main-flash blocks; extension/container/RAM blocks are refused.");
            if (family is uint previous && previous != current) throw new InvalidDataException("UF2 family changes between blocks.");
            family = current;
            if (device.ProductId == 0x0003 ? current != 0xE48BFF56 : current is not (0xE48BFF59 or 0xE48BFF5A or 0xE48BFF5B))
                throw new InvalidDataException("The UF2 family does not match the identified RP ROM target.");
        }
        var name = "TS" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + ".UF2";
        var file = await volume.PrepareFileAsync(name, image, cancellationToken).ConfigureAwait(false);
        return new(volume, file, info, family!.Value, Convert.ToHexStringLower(SHA256.HashData(image)), ranges);
    }

    /// <summary>Copies a validated plan after the permissioned block worker has been enabled for writes.
    /// Returned filesystem/transport evidence never establishes actual target-flash readback verification.</summary>
    public static Task<FatFileWriteResult> ExecuteAsync(PreparedUf2Upload plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (Convert.ToHexStringLower(SHA256.HashData(plan.File.Content)) != plan.FirmwareSha256)
            throw new InvalidDataException("UF2 container changed after validation.");
        return plan.Volume.ExecuteAsync(plan.File, cancellationToken);
    }

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
}
