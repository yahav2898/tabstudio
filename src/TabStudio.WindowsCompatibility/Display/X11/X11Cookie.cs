using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TabStudio.WindowsCompatibility.Display.X11;

/// <summary>
/// An X server's MIT-MAGIC-COOKIE-1: a random secret only clients that know it can connect with, kept in an
/// Xauthority file the server reads (<c>Xvfb -auth</c>) and clients find through <c>XAUTHORITY</c>.
/// </summary>
public sealed class X11Cookie
{
    /// <summary>The authorization protocol's name.</summary>
    public const string ProtocolName = "MIT-MAGIC-COOKIE-1";

    /// <summary>The Xauthority family that matches any address, so the entry works for every way of connecting.</summary>
    private const ushort FamilyWild = 0xFFFF;

    private readonly byte[] _data;

    private X11Cookie(byte[] data) => _data = data;

    /// <summary>The secret, 16 bytes.</summary>
    public ReadOnlySpan<byte> Data => _data;

    /// <summary>A new random cookie.</summary>
    public static X11Cookie Create() => new(RandomNumberGenerator.GetBytes(16));

    /// <summary>A cookie with a given secret, such as one read back from a file.</summary>
    /// <param name="data">The secret.</param>
    public static X11Cookie FromBytes(ReadOnlySpan<byte> data) => new(data.ToArray());

    /// <summary>
    /// Writes an Xauthority file with this cookie for a display, readable by its owner only.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="display">The display number, such as 5.</param>
    public void WriteAuthorityFile(string path, int display)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var number = Encoding.ASCII.GetBytes(display.ToString(CultureInfo.InvariantCulture));
        var name = Encoding.ASCII.GetBytes(ProtocolName);
        using var buffer = new MemoryStream();
        WriteUInt16(buffer, FamilyWild);
        WriteField(buffer, []);
        WriteField(buffer, number);
        WriteField(buffer, name);
        WriteField(buffer, _data);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, buffer.ToArray());
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <summary>Reads the cookie of an Xauthority file's first entry.</summary>
    /// <param name="path">The file.</param>
    public static X11Cookie ReadAuthorityFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var offset = 2;
        for (var field = 0; field < 3; field++)
        {
            offset += 2 + BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset));
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset));
        return new X11Cookie(bytes.AsSpan(offset + 2, length).ToArray());
    }

    private static void WriteField(MemoryStream buffer, ReadOnlySpan<byte> value)
    {
        WriteUInt16(buffer, (ushort)value.Length);
        buffer.Write(value);
    }

    private static void WriteUInt16(MemoryStream buffer, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        buffer.Write(bytes);
    }
}
