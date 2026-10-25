using System.IO.Compression;
using System.Text;

namespace TabStudio.Testing.Extensions;

/// <summary>
/// Builds a VS Code extension package (.vsix) in memory, laid out as <c>vsce</c> lays them out: the extension's files
/// under <c>extension/</c>, with <c>extension.vsixmanifest</c> and <c>[Content_Types].xml</c> at the root. Entries are
/// stored with a fixed time, so the same files always give the same bytes.
/// </summary>
public sealed class VsixBuilder
{
    private static readonly DateTimeOffset FixedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly SortedDictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _rootFiles = new(StringComparer.Ordinal);

    /// <summary>Adds a text file of the extension, relative to its folder.</summary>
    public VsixBuilder Add(string path, string text) => Add(path, Encoding.UTF8.GetBytes(text));

    /// <summary>Adds a file of the extension, relative to its folder.</summary>
    public VsixBuilder Add(string path, byte[] bytes)
    {
        _files[path] = bytes;
        return this;
    }

    /// <summary>Adds a file at the root of the archive, outside the extension folder, as a malicious package might.</summary>
    public VsixBuilder AddAtRoot(string path, string text)
    {
        _rootFiles[path] = Encoding.UTF8.GetBytes(text);
        return this;
    }

    /// <summary>The package's bytes.</summary>
    public byte[] Build()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\".json\" ContentType=\"application/json\"/></Types>"));
            Write(zip, "extension.vsixmanifest", Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?><PackageManifest Version=\"2.0.0\" xmlns=\"http://schemas.microsoft.com/developer/vsx-schema/2011\"></PackageManifest>"));
            foreach (var (path, bytes) in _rootFiles)
            {
                Write(zip, path, bytes);
            }

            foreach (var (path, bytes) in _files)
            {
                Write(zip, "extension/" + path, bytes);
            }
        }

        return stream.ToArray();
    }

    /// <summary>Writes the package to a file and returns its path.</summary>
    public string WriteTo(string folder, string fileName = "extension.vsix")
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        File.WriteAllBytes(path, Build());
        return path;
    }

    private static void Write(ZipArchive zip, string path, byte[] bytes)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.NoCompression);
        entry.LastWriteTime = FixedTime;
        using var stream = entry.Open();
        stream.Write(bytes);
    }
}
