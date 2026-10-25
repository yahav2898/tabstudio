using System.Buffers.Binary;
using System.Text;

namespace TabStudio.WindowsCompatibility.Display.X11;

/// <summary>The X11 wire format's helpers: padding, and a writer for requests in the client's byte order (LSB first).</summary>
internal static class X11Wire
{
    /// <summary>A length rounded up to the next multiple of four, as every X11 field list is.</summary>
    public static int Pad(int length) => (length + 3) & ~3;
}

/// <summary>Builds one X11 request: the opcode, a data byte and the length, then its fields, padded to four bytes.</summary>
internal sealed class X11Request
{
    private readonly byte[] _buffer;
    private int _length;

    /// <summary>Starts a request.</summary>
    /// <param name="opcode">The major opcode: a core request, or an extension's opcode.</param>
    /// <param name="data">The header's data byte: a parameter, or an extension's minor opcode.</param>
    /// <param name="capacity">How many bytes the request takes at most.</param>
    public X11Request(byte opcode, byte data, int capacity = 64)
    {
        _buffer = new byte[X11Wire.Pad(Math.Max(capacity, 4))];
        _buffer[0] = opcode;
        _buffer[1] = data;
        _length = 4;
    }

    /// <summary>Whether the server answers the request with a reply.</summary>
    public bool HasReply { get; init; }

    /// <summary>Appends a byte.</summary>
    public X11Request Byte(byte value)
    {
        _buffer[_length++] = value;
        return this;
    }

    /// <summary>Appends unused bytes.</summary>
    public X11Request Skip(int count)
    {
        _length += count;
        return this;
    }

    /// <summary>Appends a 16-bit value.</summary>
    public X11Request UInt16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_length), value);
        _length += 2;
        return this;
    }

    /// <summary>Appends a signed 16-bit value.</summary>
    public X11Request Int16(short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(_buffer.AsSpan(_length), value);
        _length += 2;
        return this;
    }

    /// <summary>Appends a 32-bit value.</summary>
    public X11Request UInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_length), value);
        _length += 4;
        return this;
    }

    /// <summary>Appends a string without a length, padded to four bytes.</summary>
    public X11Request Text(string value)
    {
        var count = Encoding.ASCII.GetBytes(value, _buffer.AsSpan(_length));
        _length += X11Wire.Pad(count);
        return this;
    }

    /// <summary>The request's bytes, with its length in four-byte units filled in.</summary>
    public ReadOnlyMemory<byte> ToBytes()
    {
        var length = X11Wire.Pad(_length);
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(2), (ushort)(length / 4));
        return _buffer.AsMemory(0, length);
    }
}

/// <summary>A reply from the X server: the 32-byte header and what follows it.</summary>
/// <param name="Bytes">The whole reply.</param>
internal readonly record struct X11Reply(byte[] Bytes)
{
    /// <summary>The header's data byte.</summary>
    public byte Data => Bytes[1];

    /// <summary>Reads an unsigned 16-bit value at an offset.</summary>
    public ushort UInt16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan(offset));

    /// <summary>Reads a signed 16-bit value at an offset.</summary>
    public short Int16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan(offset));

    /// <summary>Reads a 32-bit value at an offset.</summary>
    public uint UInt32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan(offset));
}
