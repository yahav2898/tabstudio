using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Porting.Xaml;

/// <summary>How a XAML file is laid out: its indentation and how it closes empty elements.</summary>
/// <param name="Indent">Spaces per level.</param>
/// <param name="SpaceBeforeSlash">Whether empty elements end with <c> /&gt;</c> rather than <c>/&gt;</c>.</param>
internal sealed record XamlFormat(int Indent, bool SpaceBeforeSlash)
{
    /// <summary>The layout of a file: four spaces and <c>/&gt;</c>, as Visual Studio writes WPF files, unless the file differs.</summary>
    /// <param name="text">The file's text.</param>
    public static XamlFormat Of(string text)
    {
        var indent = 4;
        foreach (var line in text.Split('\n'))
        {
            var spaces = line.Length - line.TrimStart(' ').Length;
            if (spaces > 0 && spaces < line.Length && line[spaces] == '<')
            {
                indent = spaces;
                break;
            }
        }

        var tight = text.Split("/>").Length - 1;
        var spaced = text.Split(" />").Length - 1;
        return new XamlFormat(indent, spaced * 2 > tight);
    }
}

/// <summary>Where a new attribute goes when the file is written: on the line of the attribute it replaced.</summary>
/// <param name="Line">The source line of the replaced attribute.</param>
internal sealed record AttributeLine(int Line);

/// <summary>
/// Writes XAML the way people lay it out: one element per line, attributes grouped on the lines they were on in
/// the original file, blank lines and comments kept, and text inside elements such as a <c>TextBlock</c>'s runs left
/// on one line. Remembers the line of every element and comment, so port notes can point at them.
/// </summary>
internal sealed class XamlWriter
{
    private readonly StringBuilder _text = new();
    private readonly XamlFormat _format;
    private readonly Dictionary<XObject, int> _lines = [];
    private int _line = 1;

    private XamlWriter(XamlFormat format) => _format = format;

    /// <summary>Writes a document.</summary>
    /// <param name="document">The XAML.</param>
    /// <param name="format">The indentation to use.</param>
    /// <param name="lines">The 1-based line of every element and comment.</param>
    public static string Write(XDocument document, XamlFormat format, out IReadOnlyDictionary<XObject, int> lines)
    {
        var writer = new XamlWriter(format);
        if (document.Declaration is { } declaration)
        {
            writer.Append(declaration.ToString());
            writer.NewLine();
        }

        foreach (var node in document.Nodes())
        {
            switch (node)
            {
                case XComment comment:
                    writer.WriteComment(comment, 0);
                    writer.NewLine();
                    break;
                case XElement element:
                    writer.WriteElement(element, 0);
                    writer.NewLine();
                    break;
            }
        }

        lines = writer._lines;
        return writer._text.ToString();
    }

    private void WriteElement(XElement element, int depth)
    {
        _lines[element] = _line;
        var name = QualifiedName(element, element.Name);
        var column = CurrentColumn();
        Append("<" + name);
        WriteAttributes(element, column + name.Length + 2);

        var children = element.Nodes().ToList();
        var meaningful = children.Where(n => n is not XText text || !string.IsNullOrWhiteSpace(text.Value)).ToList();
        if (meaningful.Count == 0)
        {
            if (children.OfType<XText>().Any(t => t.Value.Count(c => c == '\n') >= 2))
            {
                // An empty element with a blank line inside, as Visual Studio's templates leave for the first control.
                Append(">");
                NewLine();
                NewLine();
                Indent(depth);
                Append("</" + name + ">");
            }
            else
            {
                Append(_format.SpaceBeforeSlash ? " />" : "/>");
            }

            return;
        }

        Append(">");
        if (meaningful.Any(n => n is XText or XCData))
        {
            // Text or mixed content (runs of a TextBlock): kept on one line, as written.
            foreach (var node in children)
            {
                WriteInline(node);
            }

            Append("</" + name + ">");
            return;
        }

        var blankBefore = false;
        foreach (var node in children)
        {
            if (node is XText whitespace)
            {
                blankBefore |= whitespace.Value.Count(c => c == '\n') >= 2;
                continue;
            }

            NewLine();
            if (blankBefore && !ReferenceEquals(node, meaningful[0]))
            {
                NewLine();
            }

            blankBefore = false;
            Indent(depth + 1);
            if (node is XElement child)
            {
                WriteElement(child, depth + 1);
            }
            else if (node is XComment comment)
            {
                WriteComment(comment, depth + 1);
            }
        }

        NewLine();
        Indent(depth);
        Append("</" + name + ">");
    }

    /// <summary>Writes attributes, keeping the line breaks between them that the original file had.</summary>
    private void WriteAttributes(XElement element, int column)
    {
        var info = (IXmlLineInfo)element;
        var elementLine = info.HasLineInfo() ? info.LineNumber : 0;
        var previousLine = elementLine;
        var first = true;
        foreach (var attribute in element.Attributes())
        {
            var line = SourceLine(attribute, previousLine);
            if (!first && line > previousLine)
            {
                NewLine();
                Append(new string(' ', column));
            }
            else
            {
                Append(" ");
            }

            Append(QualifiedName(element, attribute.Name, isAttribute: true, attribute.IsNamespaceDeclaration));
            Append("=\"" + EscapeAttribute(attribute.Value) + "\"");
            previousLine = Math.Max(previousLine, line);
            first = false;
        }
    }

    private static int SourceLine(XAttribute attribute, int previousLine)
    {
        if (attribute.Annotation<AttributeLine>() is { } moved)
        {
            return moved.Line;
        }

        return ((IXmlLineInfo)attribute).HasLineInfo() ? ((IXmlLineInfo)attribute).LineNumber : previousLine;
    }

    private void WriteInline(XNode node)
    {
        switch (node)
        {
            case XCData data:
                Append("<![CDATA[" + data.Value + "]]>");
                break;
            case XText text:
                Append(EscapeText(text.Value));
                break;
            case XComment comment:
                _lines[comment] = _line;
                Append("<!--" + comment.Value + "-->");
                break;
            case XElement element:
                _lines[element] = _line;
                var name = QualifiedName(element, element.Name);
                Append("<" + name);
                foreach (var attribute in element.Attributes())
                {
                    Append(" " + QualifiedName(element, attribute.Name, isAttribute: true, attribute.IsNamespaceDeclaration) + "=\"" + EscapeAttribute(attribute.Value) + "\"");
                }

                if (!element.Nodes().Any())
                {
                    Append(_format.SpaceBeforeSlash ? " />" : "/>");
                    break;
                }

                Append(">");
                foreach (var child in element.Nodes())
                {
                    WriteInline(child);
                }

                Append("</" + name + ">");
                break;
        }
    }

    private void WriteComment(XComment comment, int depth)
    {
        _lines[comment] = _line;
        var lines = comment.Value.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        Append("<!--" + lines[0]);

        // Continuation lines keep their indentation relative to each other, under the comment's new indentation.
        var margin = lines.Skip(1).Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        for (var i = 1; i < lines.Length; i++)
        {
            NewLine();
            if (lines[i].Trim().Length > 0)
            {
                Indent(depth);
                Append(lines[i][Math.Min(margin, lines[i].Length)..]);
            }
            else if (i == lines.Length - 1)
            {
                Indent(depth);
            }
        }

        Append("-->");
    }

    /// <summary>A name with the prefix its namespace has where it is used.</summary>
    private static string QualifiedName(XElement scope, XName name, bool isAttribute = false, bool isNamespaceDeclaration = false)
    {
        if (isNamespaceDeclaration)
        {
            return name.Namespace == XNamespace.Xmlns ? "xmlns:" + name.LocalName : "xmlns";
        }

        if (name.Namespace == XNamespace.None)
        {
            return name.LocalName;
        }

        if (name.Namespace == XNamespace.Xml)
        {
            return "xml:" + name.LocalName;
        }

        var prefix = scope.GetPrefixOfNamespace(name.Namespace);
        if (prefix is null && !isAttribute && scope.GetDefaultNamespace() == name.Namespace)
        {
            return name.LocalName;
        }

        return prefix is null ? name.LocalName : prefix + ":" + name.LocalName;
    }

    private static string EscapeAttribute(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("\n", "&#10;", StringComparison.Ordinal)
            .Replace("\r", "&#13;", StringComparison.Ordinal)
            .Replace("\t", "&#9;", StringComparison.Ordinal);

    private static string EscapeText(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private int CurrentColumn()
    {
        for (var i = _text.Length - 1; i >= 0; i--)
        {
            if (_text[i] == '\n')
            {
                return _text.Length - i - 1;
            }
        }

        return _text.Length;
    }

    private void Indent(int depth) => _text.Append(' ', depth * _format.Indent);

    private void Append(string text) => _text.Append(text);

    private void NewLine()
    {
        // No spaces at the end of a line, as the repository's editorconfig asks.
        while (_text.Length > 0 && _text[^1] == ' ')
        {
            _text.Length--;
        }

        _text.Append('\n');
        _line++;
    }
}
