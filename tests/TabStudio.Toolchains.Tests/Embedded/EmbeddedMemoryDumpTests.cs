using TabStudio.Toolchains.Embedded.Debugging;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EmbeddedMemoryDumpTests
{
    [Fact]
    public void Static_memory_dump_preserves_hex_printable_ASCII_and_64_bit_address_carry()
    {
        var bytes = new byte[] { 0, 31, 32, 65, 126, 127, 255, 1, 2, 3, 4, 5, 6, 7, 8, 9, 66 };
        var rows = EmbeddedMemoryDump.Format(0xFFFFFFF0, bytes);
        Assert.Equal(2, rows.Count);
        Assert.StartsWith("0x00000000FFFFFFF0  00 1F 20 41 7E 7F FF", rows[0], StringComparison.Ordinal);
        Assert.EndsWith("|.. A~...........|", rows[0], StringComparison.Ordinal);
        Assert.StartsWith("0x0000000100000000  42 ", rows[1], StringComparison.Ordinal);
        Assert.EndsWith("|B|", rows[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Memory_dump_rejects_empty_oversized_and_overflowing_snapshots()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EmbeddedMemoryDump.Format(0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => EmbeddedMemoryDump.Format(0, new byte[4097]));
        Assert.Throws<OverflowException>(() => EmbeddedMemoryDump.Format(ulong.MaxValue, new byte[1]));
    }
}
