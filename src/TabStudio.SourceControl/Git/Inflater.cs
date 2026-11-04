namespace TabStudio.SourceControl.Git;

/// <summary>
/// Inflates zlib data (RFC 1950 and 1951) and leaves its input exactly after the data's last byte. A pack stores its
/// objects back to back without their compressed lengths, so the next object starts where the inflater stopped;
/// <see cref="System.IO.Compression.ZLibStream"/> reads ahead and cannot say where that is.
/// </summary>
internal sealed class Inflater
{
    private const int WindowSize = 32 * 1024;
    private const int WindowMask = WindowSize - 1;

    private static readonly int[] LengthBase = [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258];
    private static readonly int[] LengthExtra = [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0];
    private static readonly int[] DistanceBase = [1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577];
    private static readonly int[] DistanceExtra = [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13];
    private static readonly int[] CodeLengthOrder = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15];
    private static readonly HuffmanTable FixedLiterals = FixedLiteralTable();
    private static readonly HuffmanTable FixedDistances = FixedDistanceTable();

    private readonly byte[] _window = new byte[WindowSize];
    private readonly byte[] _output = new byte[64 * 1024];
    private PackInput _input = null!;
    private Stream _sink = Stream.Null;
    private ulong _bits;
    private int _bitCount;
    private int _phantom;
    private int _windowPosition;
    private int _outputCount;
    private long _produced;
    private uint _adlerA;
    private uint _adlerB;

    /// <summary>
    /// Inflates one zlib stream from <paramref name="input"/> into <paramref name="output"/>, leaving the input right
    /// after the stream's checksum.
    /// </summary>
    /// <returns>How many bytes were written.</returns>
    /// <exception cref="InvalidDataException">The data is not valid zlib data.</exception>
    /// <exception cref="EndOfStreamException">The input ended first.</exception>
    public long Inflate(PackInput input, Stream output)
    {
        _input = input;
        _sink = output;
        _bits = 0;
        _bitCount = 0;
        _phantom = 0;
        _windowPosition = 0;
        _outputCount = 0;
        _produced = 0;
        _adlerA = 1;
        _adlerB = 0;
        try
        {
            var cmf = input.ReadByte();
            var flg = input.ReadByte();
            if ((cmf & 0x0F) != 8 || ((cmf << 8) | flg) % 31 != 0 || (flg & 0x20) != 0)
            {
                throw new InvalidDataException("A packed object is not zlib data.");
            }

            InflateBlocks();
            Flush();

            // Whole bytes read ahead for decoding belong to what follows; the partial byte was this stream's.
            var leftover = _bitCount - (_phantom * 8);
            if (leftover < 0)
            {
                throw new EndOfStreamException("The pack ended in the middle of an object.");
            }

            input.Position -= leftover / 8;
            Span<byte> trailer = stackalloc byte[4];
            input.ReadExactly(trailer);
            var expected = ((uint)trailer[0] << 24) | ((uint)trailer[1] << 16) | ((uint)trailer[2] << 8) | trailer[3];
            if (expected != ((_adlerB << 16) | _adlerA))
            {
                throw new InvalidDataException("A packed object's checksum does not match its content.");
            }

            return _produced;
        }
        finally
        {
            _input = null!;
            _sink = Stream.Null;
        }
    }

    private void InflateBlocks()
    {
        bool last;
        do
        {
            last = Bits(1) == 1;
            switch (Bits(2))
            {
                case 0:
                    Stored();
                    break;
                case 1:
                    Codes(FixedLiterals, FixedDistances);
                    break;
                case 2:
                    var (literals, distances) = DynamicTables();
                    Codes(literals, distances);
                    break;
                default:
                    throw new InvalidDataException("A packed object uses an unknown deflate block type.");
            }
        }
        while (!last);
    }

    private void Stored()
    {
        Drop(_bitCount % 8);
        var length = Bits(16);
        var complement = Bits(16);
        if ((length ^ 0xFFFF) != complement)
        {
            throw new InvalidDataException("A stored deflate block has a damaged length.");
        }

        for (var i = 0; i < length; i++)
        {
            Emit((byte)Bits(8));
        }
    }

    private void Codes(HuffmanTable literals, HuffmanTable distances)
    {
        while (true)
        {
            var symbol = Decode(literals);
            if (symbol < 256)
            {
                Emit((byte)symbol);
                continue;
            }

            if (symbol == 256)
            {
                return;
            }

            symbol -= 257;
            if (symbol >= LengthBase.Length)
            {
                throw new InvalidDataException("A deflate block has an invalid length code.");
            }

            var length = LengthBase[symbol] + Bits(LengthExtra[symbol]);
            var distanceSymbol = Decode(distances);
            if (distanceSymbol >= DistanceBase.Length)
            {
                throw new InvalidDataException("A deflate block has an invalid distance code.");
            }

            var distance = DistanceBase[distanceSymbol] + Bits(DistanceExtra[distanceSymbol]);
            if (distance > _produced + _outputCount || distance > WindowSize)
            {
                throw new InvalidDataException("A deflate block refers back before its data.");
            }

            var from = (_windowPosition - distance) & WindowMask;
            for (var i = 0; i < length; i++)
            {
                Emit(_window[from]);
                from = (from + 1) & WindowMask;
            }
        }
    }

    private (HuffmanTable Literals, HuffmanTable Distances) DynamicTables()
    {
        var literalCount = Bits(5) + 257;
        var distanceCount = Bits(5) + 1;
        var codeLengthCount = Bits(4) + 4;
        if (literalCount > 286 || distanceCount > 30)
        {
            throw new InvalidDataException("A deflate block has too many codes.");
        }

        var codeLengths = new byte[19];
        for (var i = 0; i < codeLengthCount; i++)
        {
            codeLengths[CodeLengthOrder[i]] = (byte)Bits(3);
        }

        var codeLengthTable = HuffmanTable.Build(codeLengths);
        var lengths = new byte[literalCount + distanceCount];
        var index = 0;
        while (index < lengths.Length)
        {
            var symbol = Decode(codeLengthTable);
            if (symbol < 16)
            {
                lengths[index++] = (byte)symbol;
                continue;
            }

            byte value = 0;
            int repeat;
            switch (symbol)
            {
                case 16:
                    if (index == 0)
                    {
                        throw new InvalidDataException("A deflate block repeats a code length before the first.");
                    }

                    value = lengths[index - 1];
                    repeat = 3 + Bits(2);
                    break;
                case 17:
                    repeat = 3 + Bits(3);
                    break;
                default:
                    repeat = 11 + Bits(7);
                    break;
            }

            if (index + repeat > lengths.Length)
            {
                throw new InvalidDataException("A deflate block has too many code lengths.");
            }

            lengths.AsSpan(index, repeat).Fill(value);
            index += repeat;
        }

        if (lengths[256] == 0)
        {
            throw new InvalidDataException("A deflate block has no end-of-block code.");
        }

        return (HuffmanTable.Build(lengths.AsSpan(0, literalCount)), HuffmanTable.Build(lengths.AsSpan(literalCount, distanceCount)));
    }

    private int Decode(HuffmanTable table)
    {
        Need(table.Bits);
        var entry = table.Entries[(int)(_bits & ((1UL << table.Bits) - 1))];
        var length = entry & 0xF;
        if (length == 0)
        {
            throw new InvalidDataException("A deflate block has an invalid Huffman code.");
        }

        Drop(length);
        return entry >> 4;
    }

    private int Bits(int count)
    {
        if (count == 0)
        {
            return 0;
        }

        Need(count);
        var value = (int)(_bits & ((1UL << count) - 1));
        Drop(count);
        return value;
    }

    private void Need(int count)
    {
        while (_bitCount < count)
        {
            if (!_input.TryReadByte(out var value))
            {
                // Peeking past the end is allowed; consuming what is not there is not (see Drop).
                value = 0;
                _phantom++;
            }

            _bits |= (ulong)value << _bitCount;
            _bitCount += 8;
        }
    }

    private void Drop(int count)
    {
        _bits >>= count;
        _bitCount -= count;
        if (_bitCount < _phantom * 8)
        {
            throw new EndOfStreamException("The pack ended in the middle of an object.");
        }
    }

    private void Emit(byte value)
    {
        _window[_windowPosition] = value;
        _windowPosition = (_windowPosition + 1) & WindowMask;
        _output[_outputCount++] = value;
        if (_outputCount == _output.Length)
        {
            Flush();
        }
    }

    private void Flush()
    {
        if (_outputCount == 0)
        {
            return;
        }

        var a = _adlerA;
        var b = _adlerB;
        var span = _output.AsSpan(0, _outputCount);
        while (span.Length > 0)
        {
            // 5552 bytes is the most that can be summed before the 32-bit sums must be reduced.
            var chunk = span[..Math.Min(span.Length, 5552)];
            foreach (var value in chunk)
            {
                a += value;
                b += a;
            }

            a %= 65521;
            b %= 65521;
            span = span[chunk.Length..];
        }

        _adlerA = a;
        _adlerB = b;
        _sink.Write(_output, 0, _outputCount);
        _produced += _outputCount;
        _outputCount = 0;
    }

    private static HuffmanTable FixedLiteralTable()
    {
        var lengths = new byte[288];
        lengths.AsSpan(0, 144).Fill(8);
        lengths.AsSpan(144, 112).Fill(9);
        lengths.AsSpan(256, 24).Fill(7);
        lengths.AsSpan(280, 8).Fill(8);
        return HuffmanTable.Build(lengths);
    }

    private static HuffmanTable FixedDistanceTable()
    {
        var lengths = new byte[30];
        lengths.AsSpan().Fill(5);
        return HuffmanTable.Build(lengths);
    }

    /// <summary>
    /// A canonical Huffman code as a lookup table indexed by the next <see cref="Bits"/> input bits (least significant
    /// first): each entry holds the symbol shifted left by 4 and the code's length, or 0 where no code starts.
    /// </summary>
    private sealed class HuffmanTable(int bits, int[] entries)
    {
        public int Bits { get; } = bits;

        public int[] Entries { get; } = entries;

        public static HuffmanTable Build(ReadOnlySpan<byte> lengths)
        {
            Span<int> count = stackalloc int[16];
            var longest = 0;
            foreach (var length in lengths)
            {
                if (length > 15)
                {
                    throw new InvalidDataException("A deflate block has a code longer than 15 bits.");
                }

                count[length]++;
                longest = Math.Max(longest, (int)length);
            }

            count[0] = 0;
            longest = Math.Max(longest, 1);

            // Over-subscribed codes are invalid; incomplete ones are allowed, and their unused entries stay 0.
            var left = 1;
            for (var length = 1; length <= 15; length++)
            {
                left = (left << 1) - count[length];
                if (left < 0)
                {
                    throw new InvalidDataException("A deflate block has an over-subscribed Huffman code.");
                }
            }

            Span<int> next = stackalloc int[16];
            var code = 0;
            for (var length = 1; length <= 15; length++)
            {
                code = (code + count[length - 1]) << 1;
                next[length] = code;
            }

            var entries = new int[1 << longest];
            for (var symbol = 0; symbol < lengths.Length; symbol++)
            {
                int length = lengths[symbol];
                if (length == 0)
                {
                    continue;
                }

                var reversed = Reverse(next[length]++, length);
                for (var i = reversed; i < entries.Length; i += 1 << length)
                {
                    entries[i] = (symbol << 4) | length;
                }
            }

            return new HuffmanTable(longest, entries);
        }

        private static int Reverse(int code, int length)
        {
            var result = 0;
            for (var i = 0; i < length; i++)
            {
                result = (result << 1) | (code & 1);
                code >>= 1;
            }

            return result;
        }
    }
}
