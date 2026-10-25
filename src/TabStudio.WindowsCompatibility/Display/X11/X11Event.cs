using System.Buffers.Binary;
using System.Globalization;

namespace TabStudio.WindowsCompatibility.Display.X11;

/// <summary>An event from the X server, such as an extension's notification, as its 32 bytes (or more for a generic event).</summary>
/// <param name="Bytes">The event.</param>
internal readonly record struct X11Event(byte[] Bytes)
{
    /// <summary>The event's code, without the bit that marks a sent event.</summary>
    public byte Code => (byte)(Bytes[0] & 0x7F);

    /// <summary>Reads an unsigned 16-bit value at an offset.</summary>
    /// <param name="offset">The offset.</param>
    public ushort UInt16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan(offset));

    /// <summary>Reads a signed 16-bit value at an offset.</summary>
    /// <param name="offset">The offset.</param>
    public short Int16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan(offset));

    /// <summary>Reads a 32-bit value at an offset.</summary>
    /// <param name="offset">The offset.</param>
    public uint UInt32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan(offset));
}

/// <summary>An error the X server reported for a request.</summary>
/// <param name="Code">The error code, such as 2 for BadValue or 3 for BadWindow.</param>
/// <param name="Sequence">The request's sequence number.</param>
/// <param name="BadValue">The value the server rejected, such as a window id.</param>
/// <param name="MajorOpcode">The request's major opcode.</param>
/// <param name="MinorOpcode">The request's minor opcode, for an extension's request.</param>
public sealed record X11Error(byte Code, ushort Sequence, uint BadValue, byte MajorOpcode, ushort MinorOpcode)
{
    /// <summary>The error's name, such as <c>BadWindow</c>.</summary>
    public string Name => Code switch
    {
        1 => "BadRequest",
        2 => "BadValue",
        3 => "BadWindow",
        4 => "BadPixmap",
        5 => "BadAtom",
        8 => "BadMatch",
        9 => "BadDrawable",
        10 => "BadAccess",
        11 => "BadAlloc",
        14 => "BadIDChoice",
        16 => "BadLength",
        17 => "BadImplementation",
        _ => "error " + Code.ToString(CultureInfo.InvariantCulture),
    };

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Name} (value 0x{BadValue:x}) for request {MajorOpcode}.{MinorOpcode}");

    internal static X11Error Parse(byte[] bytes) => new(
        bytes[1],
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)),
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)),
        bytes[10],
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8)));
}

/// <summary>The X server refused a connection or a request, or the connection broke.</summary>
public sealed class X11Exception : Exception
{
    /// <summary>Creates the exception.</summary>
    public X11Exception()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public X11Exception(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public X11Exception(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for an error the server reported.</summary>
    /// <param name="error">The error.</param>
    public X11Exception(X11Error error)
        : base($"The X server reported {error}.")
    {
        Error = error;
    }

    /// <summary>The server's error, when it reported one.</summary>
    public X11Error? Error { get; }

    /// <summary>Whether the server refused the connection, such as for a wrong cookie, which trying again does not change.</summary>
    public bool Refused { get; init; }
}
