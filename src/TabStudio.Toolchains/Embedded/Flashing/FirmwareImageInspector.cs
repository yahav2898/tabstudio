using System.Buffers.Binary;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>Reads addressed firmware formats before erase, including checksums, address overflow and file bounds.</summary>
public static class FirmwareImageInspector
{
    /// <summary>Extracts programmed byte ranges. BIN files require an explicit load address; no address is guessed.</summary>
    public static IReadOnlyList<FlashAddressRange> Inspect(string path, ulong? binaryAddress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (info.Length is <= 0 or > 128 * 1024 * 1024)
        {
            throw new InvalidDataException("Firmware must contain 1 byte..128 MiB.");
        }
        var bytes = File.ReadAllBytes(path);
        return Inspect(bytes, Path.GetExtension(path), binaryAddress);
    }

    /// <summary>Inspects a stable in-memory image; the caller can hash these exact bytes to bind a safe upload plan.</summary>
    public static IReadOnlyList<FlashAddressRange> Inspect(ReadOnlySpan<byte> bytes, string extension, ulong? binaryAddress = null)
    {
        if (bytes.Length is <= 0 or > 128 * 1024 * 1024)
        {
            throw new InvalidDataException("Firmware must contain 1 byte..128 MiB.");
        }
        var ranges = extension.ToLowerInvariant() switch
        {
            ".bin" => binaryAddress is { } address ? new[] { new FlashAddressRange(address, (ulong)bytes.Length) } : throw new InvalidDataException("Raw binary has no load address."),
            ".hex" => IntelHex(System.Text.Encoding.ASCII.GetString(bytes)),
            ".uf2" => Uf2(bytes),
            ".elf" => Elf(bytes),
            _ => throw new InvalidDataException("Unsupported firmware format."),
        };
        return Normalize(ranges);
    }

    /// <summary>Validates Intel HEX record lengths, checksums, EOF and extended segment/linear addresses.</summary>
    public static IReadOnlyList<FlashAddressRange> IntelHex(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var ranges = new List<FlashAddressRange>();
        ulong baseAddress = 0;
        var ended = false;
        foreach (var source in text.Split('\n'))
        {
            var line = source.Trim();
            if (line.Length == 0)
            {
                continue;
            }
            if (ended || line[0] != ':' || line.Length < 11 || line.Length % 2 == 0)
            {
                throw new InvalidDataException("Malformed Intel HEX record or data after EOF.");
            }
            byte[] record;
            try
            {
                record = Convert.FromHexString(line[1..]);
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("Invalid Intel HEX digit.", ex);
            }
            if (record.Length != record[0] + 5 || record.Sum(x => x) % 256 != 0)
            {
                throw new InvalidDataException("Intel HEX record length or checksum is invalid.");
            }
            var address = ((uint)record[1] << 8) | record[2];
            switch (record[3])
            {
                case 0:
                    if (address + record[0] > 0x10000)
                    {
                        throw new InvalidDataException("Intel HEX data record crosses its address segment.");
                    }
                    if (record[0] > 0)
                    {
                        ranges.Add(new(checked(baseAddress + address), record[0]));
                    }
                    break;
                case 1 when record[0] == 0 && address == 0:
                    ended = true;
                    break;
                case 2 when record[0] == 2 && address == 0:
                    baseAddress = (ulong)((record[4] << 8) | record[5]) << 4;
                    break;
                case 4 when record[0] == 2 && address == 0:
                    baseAddress = (ulong)((record[4] << 8) | record[5]) << 16;
                    break;
                case 3 or 5 when record[0] == 4 && address == 0:
                    // Entry points are metadata, not programmed bytes.
                    break;
                default:
                    throw new InvalidDataException("Unsupported or malformed Intel HEX record type.");
            }
        }
        if (!ended)
        {
            throw new InvalidDataException("Intel HEX file has no EOF record.");
        }
        return Normalize(ranges);
    }

    /// <summary>Validates UF2 blocks and extracts only main-flash payloads. Extension/file-container blocks are refused.</summary>
    public static IReadOnlyList<FlashAddressRange> Uf2(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes.Length % 512 != 0)
        {
            throw new InvalidDataException("UF2 size must be a positive multiple of 512.");
        }
        var ranges = new List<FlashAddressRange>();
        var numbers = new HashSet<uint>();
        var expectedCount = (uint)(bytes.Length / 512);
        uint? familyId = null;
        for (var offset = 0; offset < bytes.Length; offset += 512)
        {
            var block = bytes.Slice(offset, 512).ToArray();
            uint U32(int index) => BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(index, 4));
            if (U32(0) != 0x0A324655 || U32(4) != 0x9E5D5157 || U32(508) != 0x0AB16F30)
            {
                throw new InvalidDataException("Invalid UF2 block magic.");
            }
            var flags = U32(8);
            var size = U32(16);
            if ((flags & ~0x2000U) != 0 || size is 0 or > 476 || U32(24) != expectedCount || !numbers.Add(U32(20)) || U32(20) >= expectedCount)
            {
                throw new InvalidDataException("Unsupported UF2 flags or inconsistent block count/payload.");
            }
            if ((flags & 0x2000) != 0)
            {
                if (familyId is { } prior && prior != U32(28))
                {
                    throw new InvalidDataException("Mixed UF2 family IDs.");
                }
                familyId = U32(28);
            }
            ranges.Add(new(U32(12), size));
        }
        return Normalize(ranges);
    }

    /// <summary>Extracts file-backed PT_LOAD physical addresses from 32/64-bit ELF files in either byte order.</summary>
    public static IReadOnlyList<FlashAddressRange> Elf(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 52 || !bytes[..4].SequenceEqual(new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' }) || bytes[6] != 1 || bytes[5] is not (1 or 2) || bytes[4] is not (1 or 2))
        {
            throw new InvalidDataException("Invalid ELF header.");
        }
        var data = bytes.ToArray();
        var little = data[5] == 1;
        var wide = data[4] == 2;
        if (wide && data.Length < 64)
        {
            throw new InvalidDataException("Truncated ELF64 header.");
        }
        ushort U16(int at) => little ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at, 2)) : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at, 2));
        uint U32(int at) => little ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at, 4)) : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at, 4));
        ulong U64(int at) => little ? BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(at, 8)) : BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(at, 8));
        var programOffset = wide ? U64(32) : U32(28);
        var entrySize = U16(wide ? 54 : 42);
        var count = U16(wide ? 56 : 44);
        if (entrySize < (wide ? 56 : 32) || count == 0 || programOffset > (ulong)data.Length || (ulong)entrySize * count > (ulong)data.Length - programOffset)
        {
            throw new InvalidDataException("ELF program header table is missing or outside the file.");
        }
        var ranges = new List<FlashAddressRange>();
        for (var index = 0; index < count; index++)
        {
            var at = checked((int)(programOffset + (ulong)(index * entrySize)));
            if (U32(at) != 1)
            {
                continue;
            }
            var fileOffset = wide ? U64(at + 8) : U32(at + 4);
            var physicalAddress = wide ? U64(at + 24) : U32(at + 12);
            var fileSize = wide ? U64(at + 32) : U32(at + 16);
            if (fileSize == 0)
            {
                continue;
            }
            if (fileOffset > (ulong)data.Length || fileSize > (ulong)data.Length - fileOffset)
            {
                throw new InvalidDataException("ELF load segment exceeds the file.");
            }
            ranges.Add(new(physicalAddress, fileSize));
        }
        return Normalize(ranges);
    }

    private static List<FlashAddressRange> Normalize(IEnumerable<FlashAddressRange> ranges)
    {
        var result = new List<FlashAddressRange>();
        foreach (var range in ranges.OrderBy(x => x.Start))
        {
            _ = range.End;
            if (range.Length == 0)
            {
                throw new InvalidDataException("Empty firmware segment.");
            }
            if (result.Count > 0 && result[^1].End > range.Start)
            {
                throw new InvalidDataException("Overlapping firmware segments.");
            }
            if (result.Count > 0 && result[^1].End == range.Start)
            {
                result[^1] = result[^1] with { Length = checked(result[^1].Length + range.Length) };
            }
            else
            {
                result.Add(range);
            }
        }
        return result.Count > 0 ? result : throw new InvalidDataException("Firmware contains no programmed bytes.");
    }
}
