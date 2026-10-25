namespace TabStudio.Link.Qr;

/// <summary>
/// The block structure of a QR code version at error correction level M (15 % of the symbol can be damaged), from
/// ISO/IEC 18004 table 9, and its alignment pattern positions from annex E.
/// </summary>
/// <param name="Number">The version, 1 to 20.</param>
/// <param name="ErrorCorrectionPerBlock">Error correction codewords in each block.</param>
/// <param name="ShortBlocks">How many blocks have <paramref name="ShortBlockData"/> data codewords.</param>
/// <param name="ShortBlockData">Data codewords in a short block.</param>
/// <param name="LongBlocks">How many blocks have one data codeword more.</param>
/// <param name="AlignmentPositions">The row and column centers of the alignment patterns.</param>
internal sealed record QrVersion(int Number, int ErrorCorrectionPerBlock, int ShortBlocks, int ShortBlockData, int LongBlocks, int[] AlignmentPositions)
{
    /// <summary>Versions 1 to 20 at level M: up to 666 bytes, far more than a TabStudio Link invitation needs.</summary>
    public static IReadOnlyList<QrVersion> All { get; } =
    [
        new(1, 10, 1, 16, 0, []),
        new(2, 16, 1, 28, 0, [6, 18]),
        new(3, 26, 1, 44, 0, [6, 22]),
        new(4, 18, 2, 32, 0, [6, 26]),
        new(5, 24, 2, 43, 0, [6, 30]),
        new(6, 16, 4, 27, 0, [6, 34]),
        new(7, 18, 4, 31, 0, [6, 22, 38]),
        new(8, 22, 2, 38, 2, [6, 24, 42]),
        new(9, 22, 3, 36, 2, [6, 26, 46]),
        new(10, 26, 4, 43, 1, [6, 28, 50]),
        new(11, 30, 1, 50, 4, [6, 30, 54]),
        new(12, 22, 6, 36, 2, [6, 32, 58]),
        new(13, 22, 8, 37, 1, [6, 34, 62]),
        new(14, 24, 4, 40, 5, [6, 26, 46, 66]),
        new(15, 24, 5, 41, 5, [6, 26, 48, 70]),
        new(16, 28, 7, 45, 3, [6, 26, 50, 74]),
        new(17, 28, 10, 46, 1, [6, 30, 54, 78]),
        new(18, 26, 9, 43, 4, [6, 30, 56, 82]),
        new(19, 26, 3, 44, 11, [6, 30, 58, 86]),
        new(20, 26, 3, 41, 13, [6, 34, 62, 90]),
    ];

    /// <summary>The symbol's width and height in modules.</summary>
    public int Size => (Number * 4) + 17;

    /// <summary>All data codewords.</summary>
    public int DataCodewords => (ShortBlocks * ShortBlockData) + (LongBlocks * (ShortBlockData + 1));

    /// <summary>The number of bits of the byte mode's character count.</summary>
    public int CountBits => Number < 10 ? 8 : 16;

    /// <summary>How many bytes fit in byte mode.</summary>
    public int ByteCapacity => ((DataCodewords * 8) - 4 - CountBits) / 8;
}
