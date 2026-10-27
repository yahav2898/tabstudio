using System.Globalization;
using System.Text;

namespace TabStudio.Toolchains.Embedded.Debugging;

/// <summary>Formats a bounded static debugger memory snapshot as addresses, hex bytes and printable ASCII.</summary>
public static class EmbeddedMemoryDump
{
    /// <summary>Formats from 1 to 4096 bytes in 16-byte rows, preserving 64-bit addresses and rejecting range overflow.</summary>
    public static IReadOnlyList<string> Format(ulong address, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(bytes), "A memory snapshot must contain from 1 to 4096 bytes.");
        _ = checked(address + (ulong)bytes.Length);
        var rows = new List<string>((bytes.Length + 15) / 16);
        for (var offset = 0; offset < bytes.Length; offset += 16)
        {
            var count = Math.Min(16, bytes.Length - offset);
            var row = new StringBuilder("0x" + (address + (ulong)offset).ToString("X16", CultureInfo.InvariantCulture) + "  ");
            for (var i = 0; i < 16; i++)
                row.Append(i < count ? bytes[offset + i].ToString("X2", CultureInfo.InvariantCulture) : "  ").Append(' ');
            row.Append(" |");
            for (var i = 0; i < count; i++)
            {
                var value = bytes[offset + i];
                row.Append(value is >= 32 and <= 126 ? (char)value : '.');
            }
            rows.Add(row.Append('|').ToString());
        }
        return rows;
    }
}
