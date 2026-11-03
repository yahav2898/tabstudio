namespace TabStudio.SourceControl.Git;

/// <summary>The CRC-32 (IEEE) that a pack index records for each packed object.</summary>
internal static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    /// <summary>Continues a CRC with more bytes; start with 0.</summary>
    public static uint Append(uint crc, ReadOnlySpan<byte> data)
    {
        var value = ~crc;
        foreach (var b in data)
        {
            value = Table[(value ^ b) & 0xFF] ^ (value >> 8);
        }

        return ~value;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
