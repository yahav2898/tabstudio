using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Resources;
using System.Text;
using System.Windows.Forms;

namespace TabStudio.Forms.Resources;

/// <summary>
/// Reads the resources TabStudio compiles from a Windows Forms project's .resx files when they hold more than
/// strings: images, icons, image lists, byte arrays and values written as text with their type, such as a
/// <see cref="Point"/>. The <c>.resources</c> header names this reader, so a <see cref="ResourceManager"/> or
/// <c>ComponentResourceManager</c> uses it as it would on Windows, without the binary formatter .NET no longer has.
/// </summary>
/// <remarks>
/// After the standard header (magic number, version, the names of this reader and of <see cref="ResourceSet"/>),
/// the format is: the marker <c>TSRX</c>, a version, the count, then per resource its name, a kind and its value:
/// a string; bytes; a type name and bytes; or a type name and text. TabStudio.Compilation's
/// <c>WindowsFormsResources</c> writes it.
/// </remarks>
internal sealed class FormsResourceReader : IResourceReader
{
    /// <summary>The marker after the header.</summary>
    internal const int Marker = 0x58525354;

    private const int MagicNumber = unchecked((int)0xBEEFCACE);

    private readonly Dictionary<string, object?> _resources = new(StringComparer.Ordinal);

    /// <summary>Reads resources from a stream, at its start or at its header.</summary>
    /// <param name="stream">The resources.</param>
    public FormsResourceReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadInt32() != MagicNumber)
        {
            throw new ArgumentException("Stream is not a valid resource file.", nameof(stream));
        }

        reader.ReadInt32();
        var skip = reader.ReadInt32();
        stream.Seek(skip, SeekOrigin.Current);
        if (reader.ReadInt32() != Marker)
        {
            throw new ArgumentException("Stream is not a resource file TabStudio wrote.", nameof(stream));
        }

        reader.ReadInt32();
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadString();
            var kind = reader.ReadByte();
            _resources[name] = kind switch
            {
                0 => reader.ReadString(),
                1 => reader.ReadBytes(reader.ReadInt32()),
                2 => FromBytes(reader.ReadString(), reader.ReadBytes(reader.ReadInt32())),
                3 => FromText(reader.ReadString(), reader.ReadString()),
                _ => throw new ArgumentException($"Resource '{name}' has an unknown kind {kind}.", nameof(stream)),
            };
        }
    }

    /// <inheritdoc />
    public IDictionaryEnumerator GetEnumerator() => _resources.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public void Close()
    {
        // Everything was read when the reader was made.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Everything was read when the reader was made.
    }

    /// <summary>A value stored as bytes: an image, an icon, an image list, or a stream.</summary>
    private static object? FromBytes(string type, byte[] bytes)
    {
        var name = TypeName(type);
        try
        {
            return name switch
            {
                "System.Drawing.Bitmap" or "System.Drawing.Image" => new Bitmap(new MemoryStream(bytes)),
                "System.Drawing.Icon" => new Icon(new MemoryStream(bytes)),
                "System.Windows.Forms.ImageListStreamer" => ImageListStreamer.Read(bytes),
                "System.IO.MemoryStream" or "System.IO.Stream" or "System.IO.UnmanagedMemoryStream" => new MemoryStream(bytes, writable: false),
                _ => bytes,
            };
        }
        catch (ArgumentException ex)
        {
            System.Diagnostics.Debug.WriteLine($"A resource of type {name} could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>A value stored as text with its type, read with the type's converter, as a .resx file stores a <see cref="Point"/>.</summary>
    private static object? FromText(string type, string text)
    {
        if (Resolve(TypeName(type)) is not { } resolved)
        {
            return text;
        }

        try
        {
            var converter = TypeDescriptor.GetConverter(resolved);
            return converter.CanConvertFrom(typeof(string)) ? converter.ConvertFromInvariantString(text) : text;
        }
        catch (Exception ex) when (ex is NotSupportedException or FormatException or ArgumentException)
        {
            System.Diagnostics.Debug.WriteLine($"A resource of type {resolved} could not be read from '{text}': {ex.Message}");
            return null;
        }
    }

    /// <summary>The full name of a type in an assembly-qualified name.</summary>
    private static string TypeName(string type) => type.Split(',')[0].Trim();

    /// <summary>A type by its full name: one of the Forms layer's, or of .NET's.</summary>
    private static Type? Resolve(string fullName) =>
        typeof(Control).Assembly.GetType(fullName)
        ?? typeof(Point).Assembly.GetType(fullName)
        ?? typeof(object).Assembly.GetType(fullName)
        ?? Type.GetType(fullName);
}
