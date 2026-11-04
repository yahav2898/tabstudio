using System.Buffers.Binary;

namespace TabStudio.SourceControl.Git;

/// <summary>An object read from Git's object database.</summary>
/// <param name="Kind">What kind of object.</param>
/// <param name="Content">Its content, without Git's header.</param>
internal sealed record GitObject(GitObjectKind Kind, byte[] Content);

/// <summary>The parts of Git's pack format that reading and writing share.</summary>
internal static class PackFormat
{
    /// <summary>A packed object stored as a delta from an object at an earlier offset in the same pack.</summary>
    public const int OffsetDelta = 6;

    /// <summary>A packed object stored as a delta from an object named by its hash.</summary>
    public const int ReferenceDelta = 7;

    /// <summary>The size of a pack's header.</summary>
    public const int HeaderSize = 12;

    /// <summary>Reads a pack's header and returns how many objects it holds.</summary>
    /// <exception cref="InvalidDataException">It is not a pack Git can read.</exception>
    public static uint ReadPackHeader(PackInput input)
    {
        Span<byte> header = stackalloc byte[HeaderSize];
        input.ReadExactly(header);
        if (!header[..4].SequenceEqual("PACK"u8) || BinaryPrimitives.ReadUInt32BigEndian(header[4..]) is not (2 or 3))
        {
            throw new InvalidDataException("The server did not send a Git pack.");
        }

        return BinaryPrimitives.ReadUInt32BigEndian(header[8..]);
    }

    /// <summary>Reads an object's type and size.</summary>
    public static (int Type, long Size) ReadObjectHeader(PackInput input)
    {
        int current = input.ReadByte();
        var type = (current >> 4) & 7;
        long size = current & 0x0F;
        var shift = 4;
        while ((current & 0x80) != 0)
        {
            current = input.ReadByte();
            size |= (long)(current & 0x7F) << shift;
            shift += 7;
            if (shift > 63)
            {
                throw new InvalidDataException("A packed object has an invalid size.");
            }
        }

        return (type, size);
    }

    /// <summary>Reads how far back the base of an offset delta is.</summary>
    public static long ReadDeltaDistance(PackInput input)
    {
        int current = input.ReadByte();
        long distance = current & 0x7F;
        while ((current & 0x80) != 0)
        {
            current = input.ReadByte();
            distance = ((distance + 1) << 7) | (long)(current & 0x7F);
            if (distance < 0)
            {
                throw new InvalidDataException("A packed delta has an invalid base offset.");
            }
        }

        return distance;
    }

    /// <summary>Writes an object's type and size.</summary>
    public static void WriteObjectHeader(Stream stream, GitObjectKind kind, long size)
    {
        Span<byte> buffer = stackalloc byte[16];
        var count = 0;
        var current = ((int)kind << 4) | (int)(size & 0x0F);
        size >>= 4;
        while (size > 0)
        {
            buffer[count++] = (byte)(current | 0x80);
            current = (int)(size & 0x7F);
            size >>= 7;
        }

        buffer[count++] = (byte)current;
        stream.Write(buffer[..count]);
    }

    /// <summary>Whether a pack type number is one of the four object kinds.</summary>
    public static bool IsObjectKind(int type) => type is >= 1 and <= 4;
}
