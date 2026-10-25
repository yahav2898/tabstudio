using System.Text;

namespace TabStudio.Link.Qr;

/// <summary>
/// A QR code: a square of dark and light modules encoding text in byte mode with error correction level M, as
/// ISO/IEC 18004 specifies it. Small and dependency-free; it serves the host's pairing QR code.
/// </summary>
/// <remarks>
/// The smallest version that fits is used, and of the eight masks the one the standard's penalty rules score best.
/// Draw it with a quiet zone of four light modules around it.
/// </remarks>
public sealed class QrCode
{
    private const int PenaltyRun = 3;
    private const int PenaltyBlock = 3;
    private const int PenaltyFinderLike = 40;
    private const int PenaltyBalance = 10;

    private readonly bool[,] _modules;
    private readonly bool[,] _isFunction;
    private readonly QrVersion _version;

    private QrCode(QrVersion version)
    {
        _version = version;
        _modules = new bool[version.Size, version.Size];
        _isFunction = new bool[version.Size, version.Size];
    }

    /// <summary>The width and height in modules, without the quiet zone.</summary>
    public int Size => _version.Size;

    /// <summary>The version, 1 to 20.</summary>
    public int Version => _version.Number;

    /// <summary>The mask the encoder chose, 0 to 7.</summary>
    public int Mask { get; private set; }

    /// <summary>Whether the module at a column and row is dark.</summary>
    /// <param name="x">The column, from the left.</param>
    /// <param name="y">The row, from the top.</param>
    public bool this[int x, int y] => _modules[y, x];

    /// <summary>Encodes text as UTF-8.</summary>
    /// <param name="text">The text, such as a <c>tabstudio-link://</c> URI.</param>
    /// <exception cref="ArgumentException">The text does not fit in a version 20 symbol.</exception>
    public static QrCode Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Encode(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>Encodes bytes.</summary>
    /// <param name="data">The bytes.</param>
    /// <exception cref="ArgumentException">The data does not fit in a version 20 symbol.</exception>
    public static QrCode Encode(ReadOnlySpan<byte> data)
    {
        QrVersion? chosen = null;
        foreach (var version in QrVersion.All)
        {
            if (data.Length <= version.ByteCapacity)
            {
                chosen = version;
                break;
            }
        }

        if (chosen is null)
        {
            throw new ArgumentException($"{data.Length} bytes do not fit in a QR code of version 20.", nameof(data));
        }

        var code = new QrCode(chosen);
        code.DrawFunctionPatterns();
        code.DrawCodewords(Interleave(chosen, DataCodewords(chosen, data)));
        code.ChooseMask();
        return code;
    }

    /// <summary>The data codewords: mode, count, bytes, terminator and padding.</summary>
    private static byte[] DataCodewords(QrVersion version, ReadOnlySpan<byte> data)
    {
        var bits = new BitWriter(version.DataCodewords * 8);
        bits.Append(0b0100, 4);
        bits.Append(data.Length, version.CountBits);
        foreach (var value in data)
        {
            bits.Append(value, 8);
        }

        var capacity = version.DataCodewords * 8;
        bits.Append(0, Math.Min(4, capacity - bits.Length));
        bits.Append(0, (8 - (bits.Length % 8)) % 8);
        for (var pad = 0xEC; bits.Length < capacity; pad ^= 0xEC ^ 0x11)
        {
            bits.Append(pad, 8);
        }

        return bits.ToBytes();
    }

    /// <summary>Splits the data into blocks, adds each block's error correction and interleaves them.</summary>
    private static byte[] Interleave(QrVersion version, byte[] data)
    {
        var blockCount = version.ShortBlocks + version.LongBlocks;
        var blocks = new List<byte[]>(blockCount);
        var errorCorrection = new List<byte[]>(blockCount);
        var offset = 0;
        for (var i = 0; i < blockCount; i++)
        {
            var length = version.ShortBlockData + (i < version.ShortBlocks ? 0 : 1);
            var block = data.AsSpan(offset, length).ToArray();
            offset += length;
            blocks.Add(block);
            errorCorrection.Add(ReedSolomon.Compute(block, version.ErrorCorrectionPerBlock));
        }

        var result = new List<byte>(data.Length + (blockCount * version.ErrorCorrectionPerBlock));
        for (var i = 0; i <= version.ShortBlockData; i++)
        {
            foreach (var block in blocks)
            {
                if (i < block.Length)
                {
                    result.Add(block[i]);
                }
            }
        }

        for (var i = 0; i < version.ErrorCorrectionPerBlock; i++)
        {
            foreach (var block in errorCorrection)
            {
                result.Add(block[i]);
            }
        }

        return [.. result];
    }

    private void DrawFunctionPatterns()
    {
        var size = Size;
        for (var i = 0; i < size; i++)
        {
            SetFunction(6, i, i % 2 == 0);
            SetFunction(i, 6, i % 2 == 0);
        }

        DrawFinder(3, 3);
        DrawFinder(size - 4, 3);
        DrawFinder(3, size - 4);

        var positions = _version.AlignmentPositions;
        for (var i = 0; i < positions.Length; i++)
        {
            for (var j = 0; j < positions.Length; j++)
            {
                var isCorner = (i == 0 && j == 0) || (i == 0 && j == positions.Length - 1) || (i == positions.Length - 1 && j == 0);
                if (!isCorner)
                {
                    DrawAlignment(positions[i], positions[j]);
                }
            }
        }

        // Reserve the format areas now; ChooseMask draws the real bits.
        DrawFormatBits(0);
        DrawVersionBits();
    }

    private void DrawFinder(int centerX, int centerY)
    {
        for (var dy = -4; dy <= 4; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                int x = centerX + dx, y = centerY + dy;
                if (x >= 0 && x < Size && y >= 0 && y < Size)
                {
                    SetFunction(x, y, distance != 2 && distance != 4);
                }
            }
        }
    }

    private void DrawAlignment(int centerX, int centerY)
    {
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                SetFunction(centerX + dx, centerY + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
        }
    }

    /// <summary>The 15 format bits: level M and the mask, BCH-protected, in both copies.</summary>
    private void DrawFormatBits(int mask)
    {
        // Level M is 00.
        var data = mask;
        var remainder = data;
        for (var i = 0; i < 10; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);
        }

        var bits = ((data << 10) | remainder) ^ 0x5412;
        var size = Size;
        for (var i = 0; i <= 5; i++)
        {
            SetFunction(8, i, Bit(bits, i));
        }

        SetFunction(8, 7, Bit(bits, 6));
        SetFunction(8, 8, Bit(bits, 7));
        SetFunction(7, 8, Bit(bits, 8));
        for (var i = 9; i < 15; i++)
        {
            SetFunction(14 - i, 8, Bit(bits, i));
        }

        for (var i = 0; i < 8; i++)
        {
            SetFunction(size - 1 - i, 8, Bit(bits, i));
        }

        for (var i = 8; i < 15; i++)
        {
            SetFunction(8, size - 15 + i, Bit(bits, i));
        }

        SetFunction(8, size - 8, true);
    }

    /// <summary>The 18 version bits of versions 7 and up, BCH-protected, in both copies.</summary>
    private void DrawVersionBits()
    {
        if (Version < 7)
        {
            return;
        }

        var remainder = Version;
        for (var i = 0; i < 12; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >> 11) * 0x1F25);
        }

        var bits = (Version << 12) | remainder;
        for (var i = 0; i < 18; i++)
        {
            var bit = Bit(bits, i);
            var a = Size - 11 + (i % 3);
            var b = i / 3;
            SetFunction(a, b, bit);
            SetFunction(b, a, bit);
        }
    }

    /// <summary>Places the codewords in the zigzag order, two columns at a time from the bottom right.</summary>
    private void DrawCodewords(byte[] codewords)
    {
        var size = Size;
        var index = 0;
        for (var right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
            {
                right = 5;
            }

            for (var vertical = 0; vertical < size; vertical++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? size - 1 - vertical : vertical;
                    if (!_isFunction[y, x] && index < codewords.Length * 8)
                    {
                        _modules[y, x] = Bit(codewords[index >> 3], 7 - (index & 7));
                        index++;
                    }
                }
            }
        }
    }

    private void ChooseMask()
    {
        var best = 0;
        var bestPenalty = int.MaxValue;
        for (var mask = 0; mask < 8; mask++)
        {
            ApplyMask(mask);
            DrawFormatBits(mask);
            var penalty = Penalty();
            if (penalty < bestPenalty)
            {
                best = mask;
                bestPenalty = penalty;
            }

            // Masking twice undoes it.
            ApplyMask(mask);
        }

        Mask = best;
        ApplyMask(best);
        DrawFormatBits(best);
    }

    private void ApplyMask(int mask)
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (_isFunction[y, x])
                {
                    continue;
                }

                var invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => ((x / 3) + (y / 2)) % 2 == 0,
                    5 => ((x * y) % 2) + ((x * y) % 3) == 0,
                    6 => (((x * y) % 2) + ((x * y) % 3)) % 2 == 0,
                    _ => (((x + y) % 2) + ((x * y) % 3)) % 2 == 0,
                };
                _modules[y, x] ^= invert;
            }
        }
    }

    /// <summary>The standard's penalty score: long runs, 2×2 blocks, finder-like patterns and an unbalanced share of dark modules.</summary>
    private int Penalty()
    {
        var size = Size;
        var penalty = 0;
        for (var line = 0; line < size; line++)
        {
            penalty += LinePenalty(line, horizontal: true) + LinePenalty(line, horizontal: false);
        }

        var dark = 0;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (_modules[y, x])
                {
                    dark++;
                }

                if (x < size - 1 && y < size - 1)
                {
                    var color = _modules[y, x];
                    if (color == _modules[y, x + 1] && color == _modules[y + 1, x] && color == _modules[y + 1, x + 1])
                    {
                        penalty += PenaltyBlock;
                    }
                }
            }
        }

        var total = size * size;
        var deviation = ((Math.Abs((dark * 20) - (total * 10)) + total - 1) / total) - 1;
        return penalty + (Math.Max(0, deviation) * PenaltyBalance);
    }

    private int LinePenalty(int line, bool horizontal)
    {
        var size = Size;
        var penalty = 0;
        var run = 0;
        var previous = false;
        for (var i = 0; i < size; i++)
        {
            var color = horizontal ? _modules[line, i] : _modules[i, line];
            if (i > 0 && color == previous)
            {
                run++;
            }
            else
            {
                if (run >= 5)
                {
                    penalty += PenaltyRun + (run - 5);
                }

                run = 1;
                previous = color;
            }

            // 1:1:3:1:1 dark-light-dark-light-dark with four light modules on one side looks like a finder.
            if (i >= 10 && MatchesFinderLike(line, i - 10, horizontal))
            {
                penalty += PenaltyFinderLike;
            }
        }

        if (run >= 5)
        {
            penalty += PenaltyRun + (run - 5);
        }

        return penalty;
    }

    private bool MatchesFinderLike(int line, int start, bool horizontal)
    {
        bool At(int i) => horizontal ? _modules[line, start + i] : _modules[start + i, line];
        ReadOnlySpan<bool> forward = [true, false, true, true, true, false, true, false, false, false, false];
        var matchesForward = true;
        var matchesBackward = true;
        for (var i = 0; i < 11; i++)
        {
            matchesForward &= At(i) == forward[i];
            matchesBackward &= At(i) == forward[10 - i];
        }

        return matchesForward || matchesBackward;
    }

    private void SetFunction(int x, int y, bool dark)
    {
        _modules[y, x] = dark;
        _isFunction[y, x] = true;
    }

    private static bool Bit(int value, int index) => ((value >> index) & 1) != 0;

    /// <summary>Appends bits, most significant first.</summary>
    private sealed class BitWriter(int capacity)
    {
        private readonly List<bool> _bits = new(capacity);

        public int Length => _bits.Count;

        public void Append(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                _bits.Add(((value >> i) & 1) != 0);
            }
        }

        public byte[] ToBytes()
        {
            var bytes = new byte[_bits.Count / 8];
            for (var i = 0; i < bytes.Length * 8; i++)
            {
                if (_bits[i])
                {
                    bytes[i >> 3] |= (byte)(0x80 >> (i & 7));
                }
            }

            return bytes;
        }
    }
}
