namespace TabStudio.Compilation.Wpf.Xaml;

/// <summary>
/// The elements and attributes of XAML text with the offsets they are written at, for the editor's XAML features.
/// Unlike an XML parser it never gives up: XAML being typed, with a tag or a quote not closed yet, still yields every
/// element before and around the caret.
/// </summary>
public sealed class XamlDocument
{
    private readonly List<XamlElement> _elements;
    private readonly List<(int Start, int End)> _otherMarkup;

    private XamlDocument(string text, List<XamlElement> elements, List<(int, int)> otherMarkup)
    {
        Text = text;
        _elements = elements;
        _otherMarkup = otherMarkup;
        Root = elements.Find(e => e.Parent is null && e.LocalName.Length > 0);
    }

    /// <summary>The text that was parsed.</summary>
    public string Text { get; }

    /// <summary>The first top-level element, or <see langword="null"/> when there is none.</summary>
    public XamlElement? Root { get; }

    /// <summary>Every element, in document order.</summary>
    public IReadOnlyList<XamlElement> Elements => _elements;

    /// <summary>Reads XAML text, finished or not.</summary>
    /// <param name="text">The text.</param>
    public static XamlDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var elements = new List<XamlElement>();
        var other = new List<(int, int)>();
        var open = new List<XamlElement>();
        var position = 0;
        while (position < text.Length)
        {
            var lessThan = text.IndexOf('<', position);
            if (lessThan < 0)
            {
                break;
            }

            var rest = text.AsSpan(lessThan);
            if (rest.StartsWith("<!--", StringComparison.Ordinal))
            {
                position = Past(text, "-->", lessThan + 4);
                other.Add((lessThan, position));
            }
            else if (rest.StartsWith("<![CDATA[", StringComparison.Ordinal))
            {
                position = Past(text, "]]>", lessThan + 9);
                other.Add((lessThan, position));
            }
            else if (rest.Length > 1 && rest[1] is '?' or '!')
            {
                position = Past(text, ">", lessThan + 2);
                other.Add((lessThan, position));
            }
            else if (rest.Length > 1 && rest[1] == '/')
            {
                position = ReadEndTag(text, lessThan, open);
            }
            else
            {
                position = ReadStartTag(text, lessThan, open, elements);
            }
        }

        return new XamlDocument(text, elements, other);
    }

    /// <summary>The innermost element an offset is in, or <see langword="null"/>.</summary>
    /// <param name="offset">The offset.</param>
    public XamlElement? ElementAt(int offset)
    {
        XamlElement? found = null;
        foreach (var element in _elements)
        {
            if (element.Start > offset)
            {
                break;
            }

            if (element.Contains(offset))
            {
                found = element;
            }
        }

        return found;
    }

    /// <summary>
    /// The element whose start tag an offset is in, from just after its <c>&lt;</c> to just before its <c>&gt;</c>, or
    /// to where the text or the next tag starts while the tag is not closed; <see langword="null"/> elsewhere.
    /// </summary>
    /// <param name="offset">The offset.</param>
    public XamlElement? StartTagAt(int offset)
    {
        // Tags never overlap, so only the last element that starts before the offset can have it in its tag.
        var last = _elements.FindLast(e => e.Start < offset);
        if (last is null)
        {
            return null;
        }

        var inTag = last.StartTagEnd >= 0 ? offset < last.StartTagEnd : last.End < 0 || offset <= last.End;
        return inTag ? last : null;
    }

    /// <summary>Whether an offset is in a comment, a CDATA section or a processing instruction.</summary>
    /// <param name="offset">The offset.</param>
    public bool IsInOtherMarkup(int offset) => _otherMarkup.Exists(m => offset > m.Start && offset < m.End);

    /// <summary>The element with an <c>x:Name</c> or <c>Name</c>, or <see langword="null"/>.</summary>
    /// <param name="name">The name.</param>
    public XamlElement? FindNamed(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _elements.Find(e => (e.Attribute(XamlNamespaces.Language, "Name") ?? e.Attribute("Name"))?.Value == name);
    }

    /// <summary>Converts an offset to a one-based line and column.</summary>
    /// <param name="offset">The offset.</param>
    public (int Line, int Column) LineAndColumn(int offset)
    {
        offset = Math.Clamp(offset, 0, Text.Length);
        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < offset; i++)
        {
            if (Text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, offset - lineStart + 1);
    }

    private static int Past(string text, string token, int from)
    {
        var index = text.IndexOf(token, Math.Min(from, text.Length), StringComparison.Ordinal);
        return index < 0 ? text.Length : index + token.Length;
    }

    private static int ReadName(string text, int start)
    {
        var end = start;
        while (end < text.Length && XamlText.IsNameChar(text[end]))
        {
            end++;
        }

        return end;
    }

    private static (string Prefix, string LocalName) Split(string name)
    {
        var colon = name.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? (string.Empty, name) : (name[..colon], name[(colon + 1)..]);
    }

    private static int ReadEndTag(string text, int lessThan, List<XamlElement> open)
    {
        var nameStart = lessThan + 2;
        var nameEnd = ReadName(text, nameStart);
        var name = text[nameStart..nameEnd];
        var greaterThan = text.IndexOf('>', nameEnd);
        var nextTag = text.IndexOf('<', nameEnd);
        var end = greaterThan < 0 || (nextTag >= 0 && nextTag < greaterThan) ? (nextTag < 0 ? text.Length : nextTag) : greaterThan + 1;

        // Close the matching element and everything opened inside it that was never closed.
        var match = open.FindLastIndex(e => e.Name == name);
        if (match >= 0)
        {
            for (var i = open.Count - 1; i > match; i--)
            {
                open[i].End = lessThan;
            }

            open[match].EndTagStart = lessThan;
            open[match].End = end;
            open.RemoveRange(match, open.Count - match);
        }

        return end;
    }

    private static int ReadStartTag(string text, int lessThan, List<XamlElement> open, List<XamlElement> elements)
    {
        var nameStart = lessThan + 1;
        var nameEnd = ReadName(text, nameStart);
        var (prefix, localName) = Split(text[nameStart..nameEnd]);
        var parent = open.Count > 0 ? open[^1] : null;
        var element = new XamlElement(parent, prefix, localName, lessThan, nameStart, nameEnd);
        parent?.Add(element);
        elements.Add(element);

        var position = nameEnd;
        while (position < text.Length)
        {
            var c = text[position];
            if (char.IsWhiteSpace(c))
            {
                position++;
            }
            else if (c == '>')
            {
                element.StartTagEnd = position + 1;
                open.Add(element);
                return position + 1;
            }
            else if (c == '/' && position + 1 < text.Length && text[position + 1] == '>')
            {
                element.StartTagEnd = position + 2;
                element.IsSelfClosing = true;
                element.End = position + 2;
                return position + 2;
            }
            else if (c == '<')
            {
                // A tag that is not closed yet, as while typing: it ends where the next tag starts.
                element.End = position;
                return position;
            }
            else if (XamlText.IsNameStart(c))
            {
                position = ReadAttribute(text, position, element);
            }
            else
            {
                position++;
            }
        }

        // The text ends inside the tag: it stays open, so everything up to the end is in it.
        return text.Length;
    }

    private static int ReadAttribute(string text, int nameStart, XamlElement element)
    {
        var nameEnd = ReadName(text, nameStart);
        var (prefix, localName) = Split(text[nameStart..nameEnd]);
        var attribute = new XamlAttribute(prefix, localName, nameStart, nameEnd) { ValueStart = nameEnd, ValueEnd = nameEnd };
        element.Add(attribute);

        var position = nameEnd;
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        if (position >= text.Length || text[position] != '=')
        {
            return nameEnd;
        }

        position++;
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        if (position >= text.Length || text[position] is not ('"' or '\''))
        {
            return position;
        }

        var quote = text[position];
        var valueStart = position + 1;
        var valueEnd = valueStart;
        while (valueEnd < text.Length && text[valueEnd] != quote && text[valueEnd] != '<')
        {
            valueEnd++;
        }

        attribute.HasValue = true;
        attribute.ValueStart = valueStart;
        attribute.ValueEnd = valueEnd;
        attribute.IsValueClosed = valueEnd < text.Length && text[valueEnd] == quote;
        attribute.RawValue = text[valueStart..valueEnd];
        return attribute.IsValueClosed ? valueEnd + 1 : valueEnd;
    }
}
