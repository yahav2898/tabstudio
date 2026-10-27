using System.Buffers.Binary;
using System.Text;

namespace TabStudio.Toolchains.Embedded.Testing;

/// <summary>Reads embedded-test's nonallocated INFO metadata after the complete ELF has passed MemoryAnalyzer validation.</summary>
internal static class EmbeddedTestElfMetadata
{
    private sealed record Section(uint Name, uint Type, ulong Address, ulong Offset, ulong Size, uint Link, ulong EntrySize);

    /// <summary>Requires the actual version symbol in .embedded_test, including normal nonallocated INFO sections.</summary>
    /// <remarks>Upstream linker-script/embedded-test.x places .embedded_test at address 1 with INFO semantics.
    /// It is intentionally absent from the application's load segments and memory-usage totals.</remarks>
    public static uint RequireVersion(byte[] image)
    {
        var wide = image[4] == 2;
        var little = image[5] == 1;
        ushort U16(int at) => little ? BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(at, 2)) : BinaryPrimitives.ReadUInt16BigEndian(image.AsSpan(at, 2));
        uint U32(int at) => little ? BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(at, 4)) : BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(at, 4));
        ulong U64(int at) => little ? BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(at, 8)) : BinaryPrimitives.ReadUInt64BigEndian(image.AsSpan(at, 8));
        var offset = wide ? U64(40) : U32(32);
        var entrySize = U16(wide ? 58 : 46);
        var count = U16(wide ? 60 : 48);
        var namesIndex = U16(wide ? 62 : 50);
        var sections = new List<Section>(count);
        for (var index = 0; index < count; index++)
        {
            var at = checked((int)(offset + (ulong)index * entrySize));
            sections.Add(wide
                ? new(U32(at), U32(at + 4), U64(at + 16), U64(at + 24), U64(at + 32), U32(at + 40), U64(at + 56))
                : new(U32(at), U32(at + 4), U32(at + 12), U32(at + 16), U32(at + 20), U32(at + 24), U32(at + 36)));
        }
        string StringAt(Section table, uint at)
        {
            if (at >= table.Size) throw new InvalidDataException("Invalid embedded-test metadata string offset.");
            var start = checked((int)(table.Offset + at));
            var limit = checked((int)Math.Min(table.Size - at, 4096));
            var length = image.AsSpan(start, limit).IndexOf((byte)0);
            if (length < 0) throw new InvalidDataException("Unterminated/oversized embedded-test metadata name.");
            return Encoding.UTF8.GetString(image, start, length);
        }
        var metadataIndex = sections.FindIndex(section => section.Size >= 4 && StringAt(sections[namesIndex], section.Name) == ".embedded_test");
        if (metadataIndex < 0) throw new InvalidDataException("Format Rust on a board requires the ELF's actual .embedded_test INFO metadata.");
        var metadata = sections[metadataIndex];
        var found = new List<uint>();
        foreach (var table in sections.Where(section => section.Type == 2))
        {
            for (ulong position = 0; position < table.Size; position += table.EntrySize)
            {
                var at = checked((int)(table.Offset + position));
                if (StringAt(sections[(int)table.Link], U32(at)) != "EMBEDDED_TEST_VERSION") continue;
                var symbolSection = U16(at + (wide ? 6 : 14));
                var address = wide ? U64(at + 8) : U32(at + 4);
                var size = wide ? U64(at + 16) : U32(at + 8);
                if (symbolSection != metadataIndex || size != 4 || address < metadata.Address
                    || address - metadata.Address > metadata.Size - 4 || metadata.Type == 8)
                {
                    throw new InvalidDataException("EMBEDDED_TEST_VERSION does not identify four real bytes inside .embedded_test.");
                }
                found.Add(BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(checked((int)(metadata.Offset + address - metadata.Address)), 4)));
            }
        }
        if (found.Count != 1 || found[0] is not (0 or 1))
        {
            throw new InvalidDataException("One actual EMBEDDED_TEST_VERSION (0 or 1, supported by probe-rs 0.32) is required before board tests.");
        }
        return found[0];
    }
}
