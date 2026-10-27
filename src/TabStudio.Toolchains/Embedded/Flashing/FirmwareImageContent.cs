using System.Buffers.Binary;
using System.Text;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>Reads exact addressed bytes from a fully validated firmware image without inventing sparse-hole contents.</summary>
public static class FirmwareImageContent
{
    /// <summary>Returns up to 4096 addressed bytes; null means the image does not program that address.
    /// All image records/segments, including those outside the requested window, are validated before extraction.</summary>
    public static byte?[] ReadRange(ReadOnlySpan<byte> image, string extension, ulong address, int count, ulong? binaryAddress = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (count > 4096) throw new ArgumentOutOfRangeException(nameof(count), "Configuration inspection is limited to 4096 bytes.");
        _ = checked(address + (uint)count);
        _ = FirmwareImageInspector.Inspect(image, extension, binaryAddress);
        var data = image.ToArray();
        var result = new byte?[count];
        void Copy(ulong start, int offset, int length)
        {
            var first = Math.Max(start, address);
            var end = Math.Min(checked(start + (uint)length), checked(address + (uint)count));
            for (var current = first; current < end; current++)
                result[checked((int)(current - address))] = data[checked(offset + (int)(current - start))];
        }
        switch (extension.ToLowerInvariant())
        {
            case ".bin":
                Copy(binaryAddress!.Value, 0, data.Length);
                break;
            case ".hex":
                ulong basis = 0;
                foreach (var source in Encoding.ASCII.GetString(data).Split('\n'))
                {
                    var line = source.Trim();
                    if (line.Length == 0) continue;
                    var record = Convert.FromHexString(line[1..]);
                    switch (record[3])
                    {
                        case 0:
                            var start = checked(basis + ((uint)record[1] << 8) + record[2]);
                            for (var i = 0; i < record[0]; i++)
                            {
                                var current = checked(start + (uint)i);
                                if (current >= address && current - address < (uint)count) result[(int)(current - address)] = record[4 + i];
                            }
                            break;
                        case 2: basis = (ulong)((record[4] << 8) | record[5]) << 4; break;
                        case 4: basis = (ulong)((record[4] << 8) | record[5]) << 16; break;
                    }
                }
                break;
            case ".uf2":
                for (var at = 0; at < data.Length; at += 512)
                {
                    Copy(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 12)), at + 32,
                        checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 16))));
                }
                break;
            case ".elf":
                var little = data[5] == 1;
                var wide = data[4] == 2;
                ushort U16(int at) => little ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at, 2)) : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at, 2));
                uint U32(int at) => little ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at, 4)) : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at, 4));
                ulong U64(int at) => little ? BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(at, 8)) : BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(at, 8));
                var table = wide ? U64(32) : U32(28);
                var size = U16(wide ? 54 : 42);
                var entries = U16(wide ? 56 : 44);
                for (var i = 0; i < entries; i++)
                {
                    var at = checked((int)(table + (ulong)(i * size)));
                    if (U32(at) != 1) continue;
                    var length = wide ? U64(at + 32) : U32(at + 16);
                    if (length == 0) continue;
                    Copy(wide ? U64(at + 24) : U32(at + 12), checked((int)(wide ? U64(at + 8) : U32(at + 4))), checked((int)length));
                }
                break;
        }
        return result;
    }
}
