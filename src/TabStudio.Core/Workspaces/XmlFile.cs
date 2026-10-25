using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// An XML file loaded for editing: its whitespace, line endings and byte order mark are written back as they
/// were, so an edit changes only the lines it touches.
/// </summary>
/// <remarks>
/// XML parsers turn every <c>\r\n</c> into <c>\n</c>, so the document is edited with <c>\n</c> and the file's
/// own line ending is restored when it is saved.
/// </remarks>
internal sealed class XmlFile
{
    private XmlFile(string path, XDocument document, bool windowsLineEndings, bool byteOrderMark)
    {
        Path = path;
        Document = document;
        WindowsLineEndings = windowsLineEndings;
        ByteOrderMark = byteOrderMark;
    }

    public string Path { get; }

    public XDocument Document { get; }

    public XElement Root => Document.Root!;

    public bool WindowsLineEndings { get; }

    public bool ByteOrderMark { get; }

    /// <summary>Loads a file.</summary>
    /// <exception cref="ProjectLoadException">The file is not well-formed XML.</exception>
    public static XmlFile Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var text = Encoding.UTF8.GetString(bom ? bytes.AsSpan(3) : bytes);
        try
        {
            var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            return document.Root is null
                ? throw new ProjectLoadException(path, "it has no root element.", null)
                : new XmlFile(path, document, text.Contains("\r\n", StringComparison.Ordinal), bom);
        }
        catch (XmlException ex)
        {
            throw new ProjectLoadException(path, $"it is not valid XML: {ex.Message}", ex);
        }
    }

    /// <summary>Writes the document back with the file's line endings and byte order mark.</summary>
    public void Save()
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = Document.Declaration is null,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            NewLineHandling = NewLineHandling.None,
        };

        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, settings))
        {
            Document.Save(writer);
        }

        var text = Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (WindowsLineEndings)
        {
            text = text.Replace("\n", "\r\n", StringComparison.Ordinal);
        }

        File.WriteAllText(Path, text, new UTF8Encoding(ByteOrderMark));
    }
}
