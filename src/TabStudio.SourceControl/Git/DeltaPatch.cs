namespace TabStudio.SourceControl.Git;

/// <summary>Applies Git's binary deltas, which packs use to store an object as changes to another.</summary>
internal static class DeltaPatch
{
    /// <summary>The size of the object a delta produces, read from its header.</summary>
    public static long ResultSize(ReadOnlySpan<byte> delta)
    {
        var position = 0;
        ReadSize(delta, ref position);
        return ReadSize(delta, ref position);
    }

    /// <summary>Builds an object from its base and a delta.</summary>
    /// <exception cref="InvalidDataException">The delta does not fit the base.</exception>
    public static byte[] Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        var position = 0;
        var sourceSize = ReadSize(delta, ref position);
        var resultSize = ReadSize(delta, ref position);
        if (sourceSize != source.Length || resultSize > Array.MaxLength)
        {
            throw new InvalidDataException("A packed delta does not fit the object it is based on.");
        }

        var result = new byte[resultSize];
        var written = 0;
        while (position < delta.Length)
        {
            int op = delta[position++];
            if ((op & 0x80) != 0)
            {
                long offset = 0;
                long size = 0;
                for (var i = 0; i < 4; i++)
                {
                    if ((op & (1 << i)) != 0)
                    {
                        offset |= (long)Next(delta, ref position) << (8 * i);
                    }
                }

                for (var i = 0; i < 3; i++)
                {
                    if ((op & (0x10 << i)) != 0)
                    {
                        size |= (long)Next(delta, ref position) << (8 * i);
                    }
                }

                if (size == 0)
                {
                    size = 0x10000;
                }

                if (offset + size > source.Length || written + size > result.Length)
                {
                    throw new InvalidDataException("A packed delta copies from outside the object it is based on.");
                }

                source.Slice((int)offset, (int)size).CopyTo(result.AsSpan(written));
                written += (int)size;
            }
            else if (op != 0)
            {
                if (position + op > delta.Length || written + op > result.Length)
                {
                    throw new InvalidDataException("A packed delta inserts more than it holds.");
                }

                delta.Slice(position, op).CopyTo(result.AsSpan(written));
                position += op;
                written += op;
            }
            else
            {
                throw new InvalidDataException("A packed delta has an invalid instruction.");
            }
        }

        if (written != result.Length)
        {
            throw new InvalidDataException("A packed delta is shorter than the object it describes.");
        }

        return result;
    }

    private static long ReadSize(ReadOnlySpan<byte> delta, ref int position)
    {
        long value = 0;
        var shift = 0;
        byte current;
        do
        {
            current = Next(delta, ref position);
            value |= (long)(current & 0x7F) << shift;
            shift += 7;
        }
        while ((current & 0x80) != 0 && shift < 63);

        return value;
    }

    private static byte Next(ReadOnlySpan<byte> delta, ref int position) =>
        position < delta.Length ? delta[position++] : throw new InvalidDataException("A packed delta ends too early.");
}
