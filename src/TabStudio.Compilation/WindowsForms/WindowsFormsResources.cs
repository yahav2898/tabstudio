using System.Text;
using System.Xml.Linq;

namespace TabStudio.Compilation.WindowsForms;

/// <summary>
/// Compiles a Windows Forms project's .resx file that holds more than strings: the images, icons and image lists
/// (<c>imageList1.ImageStream</c>) the designer stores, files the .resx refers to, and values written as text with
/// their type, such as a <c>Point</c>. The result is a <c>.resources</c> file whose header names the Forms layer's
/// reader, which turns each value back into its object at run time, so <c>resources.GetObject</c> returns a
/// <c>Bitmap</c> or an <c>ImageListStreamer</c> as on Windows.
/// </summary>
/// <remarks>
/// Values the .NET Framework stored with its binary formatter are read here, once: an image list's data is taken out
/// of the serialized <c>ImageListStreamer</c>. Other binary-formatted values are left out, with a warning.
/// </remarks>
public static class WindowsFormsResources
{
    /// <summary>The reader the Forms layer reads these resources with.</summary>
    public const string ReaderType = "TabStudio.Forms.Resources.FormsResourceReader, TabStudio.Forms";

    private const int MagicNumber = unchecked((int)0xBEEFCACE);
    private const int Marker = 0x58525354;
    private const string ByteArrayMime = "application/x-microsoft.net.object.bytearray.base64";
    private const string BinaryMime = "application/x-microsoft.net.object.binary.base64";

    /// <summary>One resource of a .resx file.</summary>
    /// <param name="Name">The resource's name.</param>
    /// <param name="Kind">0 for a string, 1 for bytes, 2 for bytes of a type, 3 for text of a type.</param>
    /// <param name="Type">The type's assembly-qualified name, for kinds 2 and 3.</param>
    /// <param name="Text">The string or text.</param>
    /// <param name="Bytes">The bytes.</param>
    public sealed record Entry(string Name, byte Kind, string? Type, string? Text, byte[]? Bytes);

    /// <summary>
    /// Compiles a .resx file into the Forms layer's resources format when it holds anything but strings.
    /// </summary>
    /// <param name="path">The .resx file.</param>
    /// <param name="warnings">Receives a line for each value that could not be kept.</param>
    /// <returns>The compiled resources, or <see langword="null"/> when the file holds only strings.</returns>
    public static MemoryStream? TryCompile(string path, ICollection<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        var entries = Read(path, warnings);
        if (entries.All(entry => entry.Kind == 0))
        {
            return null;
        }

        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            using var header = new MemoryStream();
            using (var headerWriter = new BinaryWriter(header, Encoding.UTF8, leaveOpen: true))
            {
                headerWriter.Write(ReaderType);
                headerWriter.Write("System.Resources.ResourceSet");
            }

            writer.Write(MagicNumber);
            writer.Write(1);
            writer.Write((int)header.Length);
            writer.Write(header.ToArray());
            writer.Write(Marker);
            writer.Write(1);
            writer.Write(entries.Count);
            foreach (var entry in entries)
            {
                writer.Write(entry.Name);
                writer.Write(entry.Kind);
                switch (entry.Kind)
                {
                    case 0:
                        writer.Write(entry.Text ?? string.Empty);
                        break;
                    case 1:
                        writer.Write(entry.Bytes!.Length);
                        writer.Write(entry.Bytes);
                        break;
                    case 2:
                        writer.Write(entry.Type!);
                        writer.Write(entry.Bytes!.Length);
                        writer.Write(entry.Bytes);
                        break;
                    default:
                        writer.Write(entry.Type!);
                        writer.Write(entry.Text ?? string.Empty);
                        break;
                }
            }
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>The resources of a .resx file, each as it is compiled.</summary>
    /// <param name="path">The .resx file.</param>
    /// <param name="warnings">Receives a line for each value that could not be kept.</param>
    public static IReadOnlyList<Entry> Read(string path, ICollection<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        XDocument document;
        try
        {
            document = XDocument.Load(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            warnings?.Add($"{Path.GetFileName(path)} could not be read: {ex.Message}");
            return [];
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        var entries = new List<Entry>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var data in document.Root?.Elements("data") ?? [])
        {
            var name = data.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name) || !names.Add(name))
            {
                continue;
            }

            var value = data.Element("value")?.Value ?? string.Empty;
            var type = data.Attribute("type")?.Value;
            var mime = data.Attribute("mimetype")?.Value;
            var entry = ReadEntry(name, value, type, mime, folder, warnings);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>
    /// The data of the image list a .NET Framework binary formatter serialized as an <c>ImageListStreamer</c>: the
    /// bytes of its <c>Data</c> member, a single array of bytes in the serialization stream.
    /// </summary>
    /// <param name="serialized">The serialization stream.</param>
    /// <returns>The image list's data, or <see langword="null"/> when there is no such array.</returns>
    public static byte[]? ImageListData(byte[] serialized)
    {
        ArgumentNullException.ThrowIfNull(serialized);

        // ArraySinglePrimitive record: type 15, the object's id, the length, the primitive type (2 for bytes).
        for (var i = 0; i + 10 <= serialized.Length; i++)
        {
            if (serialized[i] != 15 || serialized[i + 9] != 2)
            {
                continue;
            }

            var length = BitConverter.ToInt32(serialized, i + 5);
            var start = i + 10;
            if (length < 4 || start + length > serialized.Length)
            {
                continue;
            }

            // Windows Forms' compressed form starts with MSFt; the raw image list with IL.
            var head = serialized.AsSpan(start, 4);
            if (head.SequenceEqual("MSFt"u8) || (head[0] == (byte)'I' && head[1] == (byte)'L'))
            {
                return serialized.AsSpan(start, length).ToArray();
            }
        }

        return null;
    }

    private static Entry? ReadEntry(string name, string value, string? type, string? mime, string folder, ICollection<string>? warnings)
    {
        var typeName = type?.Split(',')[0].Trim();
        if (mime is null && (type is null || typeName == "System.String"))
        {
            return new Entry(name, 0, null, value, null);
        }

        if (typeName == "System.Resources.ResXFileRef")
        {
            return FileReference(name, value, folder, warnings);
        }

        if (mime == ByteArrayMime)
        {
            var bytes = Base64(value);
            return typeName is null or "System.Byte[]" ? new Entry(name, 1, null, null, bytes) : new Entry(name, 2, type, null, bytes);
        }

        if (mime == BinaryMime)
        {
            var serialized = Base64(value);
            if ((typeName == "System.Windows.Forms.ImageListStreamer" || Contains(serialized, "System.Windows.Forms.ImageListStreamer"))
                && ImageListData(serialized) is { } list)
            {
                return new Entry(name, 2, "System.Windows.Forms.ImageListStreamer", null, list);
            }

            warnings?.Add($"Resource '{name}' was saved with .NET Framework's binary formatter, which .NET no longer reads; it is left out.");
            return null;
        }

        if (mime is not null)
        {
            warnings?.Add($"Resource '{name}' has the format {mime}, which is not supported; it is left out.");
            return null;
        }

        return new Entry(name, 3, type, value, null);
    }

    /// <summary>A resource that is a file next to the .resx file: <c>path;type[;encoding]</c>.</summary>
    private static Entry? FileReference(string name, string value, string folder, ICollection<string>? warnings)
    {
        var parts = value.Split(';');
        var relative = parts[0].Trim().Replace('\\', Path.DirectorySeparatorChar);
        var file = Path.GetFullPath(Path.Combine(folder, relative));
        var type = parts.Length > 1 ? parts[1].Trim() : "System.Byte[]";
        if (!File.Exists(file))
        {
            warnings?.Add($"Resource '{name}' refers to {relative}, which does not exist.");
            return null;
        }

        var typeName = type.Split(',')[0].Trim();
        if (typeName == "System.String")
        {
            var encoding = parts.Length > 2 ? EncodingNamed(parts[2].Trim()) : Encoding.UTF8;
            return new Entry(name, 0, null, File.ReadAllText(file, encoding), null);
        }

        var bytes = File.ReadAllBytes(file);
        return typeName == "System.Byte[]" ? new Entry(name, 1, null, null, bytes) : new Entry(name, 2, type, null, bytes);
    }

    private static Encoding EncodingNamed(string name)
    {
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static byte[] Base64(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return [];
        }
    }

    private static bool Contains(byte[] bytes, string text) => bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;
}
