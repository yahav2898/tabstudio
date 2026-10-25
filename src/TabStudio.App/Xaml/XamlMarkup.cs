using System.Globalization;
using System.Text;

namespace TabStudio.App.Xaml;

/// <summary>A stretch of XAML text, from <see cref="Start"/> up to but not including <see cref="End"/>.</summary>
/// <param name="Start">The offset of its first character.</param>
/// <param name="End">The offset just past its last character.</param>
internal readonly record struct XamlSpan(int Start, int End);

/// <summary>
/// The elements and attributes of XAML text, with the offsets they are written at, for the tools that edit XAML as
/// text: the Toolbox and the Properties panel. Unlike an XML parser it never gives up: XAML that is being typed, with
/// a tag or a quote not closed yet, still yields every element before and around the caret.
/// </summary>
internal sealed class XamlMarkup
{
    private readonly List<XamlElement> _elements;
    private readonly List<XamlSpan> _otherMarkup;

    private XamlMarkup(string text, List<XamlElement> elements, List<XamlSpan> otherMarkup)
    {
        Text = text;
        _elements = elements;
        _otherMarkup = otherMarkup;
        foreach (var element in elements)
        {
            if (element.Parent is null)
            {
                Root = element;
                break;
            }
        }
    }

    /// <summary>The text that was parsed.</summary>
    public string Text { get; }

    /// <summary>The first top-level element, or <see langword="null"/> when the text has none.</summary>
    public XamlElement? Root { get; }

    /// <summary>Every element, in document order.</summary>
    public IReadOnlyList<XamlElement> Elements => _elements;

    /// <summary>Reads XAML text.</summary>
    /// <param name="text">The XAML, finished or not.</param>
    public static XamlMarkup Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var elements = new List<XamlElement>();
        var otherMarkup = new List<XamlSpan>();
        var open = new List<XamlElement>();
        var position = 0;
        while (position < text.Length)
        {
            var lessThan = text.IndexOf('<', position);
            if (lessThan < 0 || lessThan == text.Length - 1)
            {
                break;
            }

            var rest = text.AsSpan(lessThan);
            var next = text[lessThan + 1];
            if (rest.StartsWith("<!--", StringComparison.Ordinal))
            {
                position = PastToken(text, "-->", lessThan + 4);
                otherMarkup.Add(new XamlSpan(lessThan, position));
            }
            else if (rest.StartsWith("<![CDATA[", StringComparison.Ordinal))
            {
                position = PastToken(text, "]]>", lessThan + 9);
                otherMarkup.Add(new XamlSpan(lessThan, position));
            }
            else if (next is '?' or '!')
            {
                position = PastToken(text, ">", lessThan + 2);
                otherMarkup.Add(new XamlSpan(lessThan, position));
            }
            else if (next == '/')
            {
                var nameEnd = NameEnd(text, lessThan + 2);
                position = EndTagEnd(text, nameEnd);
                Close(open, text[(lessThan + 2)..nameEnd], lessThan, position);
            }
            else if (IsNameStart(next))
            {
                var element = ReadStartTag(text, lessThan, open.Count > 0 ? open[^1] : null);
                elements.Add(element);
                if (element.CanHaveContent)
                {
                    open.Add(element);
                }
                else
                {
                    element.End = element.StartTagEnd;
                }

                position = element.StartTagEnd;
            }
            else
            {
                position = lessThan + 1;
            }
        }

        // Elements still open when the text ends run to its end.
        foreach (var element in open)
        {
            element.End = text.Length;
        }

        return new XamlMarkup(text, elements, otherMarkup);
    }

    /// <summary>
    /// The innermost element the offset is in, for showing its properties. An offset in the indentation in front of
    /// a start tag counts as that element's: the caret at the start of <c>&lt;Button /&gt;</c>'s line is on the button.
    /// </summary>
    /// <param name="offset">An offset in the text, such as the caret's.</param>
    public XamlElement? ElementAt(int offset)
    {
        offset = Math.Clamp(offset, 0, Text.Length);
        var lineStart = XamlTextLayout.LineStart(Text, offset);
        var tag = offset;
        while (tag < Text.Length && Text[tag] is ' ' or '\t')
        {
            tag++;
        }

        if (tag < Text.Length - 1 && Text[tag] == '<' && IsNameStart(Text[tag + 1]) && Text.AsSpan(lineStart, offset - lineStart).IsWhiteSpace())
        {
            offset = tag;
        }

        XamlElement? found = null;
        foreach (var element in _elements)
        {
            if (element.Start <= offset && offset < element.End)
            {
                found = element;
            }
        }

        return found;
    }

    /// <summary>
    /// The object the offset is in, for showing its properties: like <see cref="ElementAt"/>, but in a property
    /// element such as <c>&lt;Grid.RowDefinitions&gt;</c> it is the element that property belongs to.
    /// </summary>
    /// <param name="offset">An offset in the text.</param>
    public XamlElement? ObjectAt(int offset)
    {
        var element = ElementAt(offset);
        while (element is { IsPropertyElement: true })
        {
            element = element.Parent;
        }

        return element;
    }

    /// <summary>
    /// The innermost element that strictly contains the offset: the caret just before an element's <c>&lt;</c>, or
    /// just past its end, is in its parent. For deciding where new elements go.
    /// </summary>
    /// <param name="offset">An offset in the text.</param>
    public XamlElement? InnermostAround(int offset)
    {
        XamlElement? found = null;
        foreach (var element in _elements)
        {
            if (element.Start < offset && offset < element.End)
            {
                found = element;
            }
        }

        return found;
    }

    /// <summary>The comment, processing instruction or CDATA section the offset is inside, if any.</summary>
    /// <param name="offset">An offset in the text.</param>
    public XamlSpan? OtherMarkupAt(int offset)
    {
        foreach (var span in _otherMarkup)
        {
            if (span.Start < offset && offset < span.End)
            {
                return span;
            }
        }

        return null;
    }

    /// <summary>The XML namespace of an element, from the <c>xmlns</c> declarations of it and the elements around it.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The namespace, such as <c>https://github.com/avaloniaui</c>, or <see langword="null"/> when it is not declared.</returns>
    public static string? NamespaceOf(XamlElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var declaration = element.Prefix.Length == 0 ? "xmlns" : "xmlns:" + element.Prefix;
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current.FindAttribute(declaration) is { } attribute)
            {
                return attribute.Value;
            }
        }

        return null;
    }

    /// <summary>Whether a character can start an XML name.</summary>
    /// <param name="c">The character.</param>
    public static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';

    /// <summary>Whether a character can be part of an XML name, prefixes and property paths included.</summary>
    /// <param name="c">The character.</param>
    public static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or ':';

    /// <summary>Decodes the character references in an attribute value: <c>&amp;amp;</c>, <c>&amp;#38;</c> and the rest.</summary>
    /// <param name="value">The value as written.</param>
    public static string Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.Contains('&', StringComparison.Ordinal))
        {
            return value;
        }

        var decoded = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var semicolon = value[i] == '&' ? value.IndexOf(';', i) : -1;
            if (semicolon < 0 || DecodeReference(value[(i + 1)..semicolon]) is not { } character)
            {
                decoded.Append(value[i]);
                continue;
            }

            decoded.Append(character);
            i = semicolon;
        }

        return decoded.ToString();
    }

    private static string? DecodeReference(string name) => name switch
    {
        "amp" => "&",
        "lt" => "<",
        "gt" => ">",
        "quot" => "\"",
        "apos" => "'",
        _ when name.StartsWith("#x", StringComparison.Ordinal)
            && int.TryParse(name.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) && code is > 0 and <= 0x10FFFF => char.ConvertFromUtf32(code),
        _ when name.StartsWith('#')
            && int.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var code) && code is > 0 and <= 0x10FFFF => char.ConvertFromUtf32(code),
        _ => null,
    };

    /// <summary>Reads a start tag and its attributes, stopping early at a <c>&lt;</c> when the tag is not finished.</summary>
    private static XamlElement ReadStartTag(string text, int start, XamlElement? parent)
    {
        var nameEnd = NameEnd(text, start + 1);
        var element = new XamlElement(text[(start + 1)..nameEnd], start, parent);
        var attributes = new List<XamlAttribute>();
        var i = nameEnd;
        while (true)
        {
            i = SkipWhitespace(text, i);
            if (i >= text.Length || text[i] == '<')
            {
                element.StartTagEnd = i;
                break;
            }

            if (text[i] == '>')
            {
                element.StartTagEnd = i + 1;
                element.IsStartTagComplete = true;
                break;
            }

            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '>')
            {
                element.StartTagEnd = i + 2;
                element.IsStartTagComplete = true;
                element.IsSelfClosing = true;
                break;
            }

            if (!IsNameChar(text[i]))
            {
                i++;
                continue;
            }

            var attribute = ReadAttribute(text, i);
            attributes.Add(attribute);
            i = attribute.End;
            if (attribute.Quote != '\0' && attribute.ValueEnd >= text.Length)
            {
                // A quote that is never closed takes the rest of the text.
                element.StartTagEnd = text.Length;
                break;
            }
        }

        element.Attributes = attributes;
        return element;
    }

    private static XamlAttribute ReadAttribute(string text, int start)
    {
        var nameEnd = NameEnd(text, start);
        var name = text[start..nameEnd];
        var equals = SkipWhitespace(text, nameEnd);
        if (equals >= text.Length || text[equals] != '=')
        {
            // A name without a value yet, while it is being typed.
            return new XamlAttribute(name, string.Empty, start, nameEnd, nameEnd, nameEnd, '\0');
        }

        var value = SkipWhitespace(text, equals + 1);
        if (value < text.Length && text[value] is '"' or '\'')
        {
            var quote = text[value];
            var close = text.IndexOf(quote, value + 1);
            return close < 0
                ? new XamlAttribute(name, Decode(text[(value + 1)..]), start, text.Length, value + 1, text.Length, quote)
                : new XamlAttribute(name, Decode(text[(value + 1)..close]), start, close + 1, value + 1, close, quote);
        }

        // A value without quotes, while it is being typed: up to the next space or the end of the tag.
        var end = value;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not ('>' or '<') && !(text[end] == '/' && end + 1 < text.Length && text[end + 1] == '>'))
        {
            end++;
        }

        return new XamlAttribute(name, Decode(text[value..end]), start, end, value, end, '\0');
    }

    /// <summary>Closes the open element an end tag belongs to, and any elements inside it that were never closed.</summary>
    private static void Close(List<XamlElement> open, string name, int endTagStart, int endTagEnd)
    {
        for (var i = open.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(open[i].Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            for (var j = open.Count - 1; j > i; j--)
            {
                open[j].End = endTagStart;
            }

            open[i].EndTagStart = endTagStart;
            open[i].End = endTagEnd;
            open.RemoveRange(i, open.Count - i);
            return;
        }

        // An end tag without a start tag, such as one being typed: nothing to close.
    }

    private static int EndTagEnd(string text, int from)
    {
        for (var i = from; i < text.Length; i++)
        {
            if (text[i] == '>')
            {
                return i + 1;
            }

            if (text[i] == '<')
            {
                return i;
            }
        }

        return text.Length;
    }

    private static int PastToken(string text, string token, int from)
    {
        var index = from <= text.Length ? text.IndexOf(token, from, StringComparison.Ordinal) : -1;
        return index < 0 ? text.Length : index + token.Length;
    }

    private static int NameEnd(string text, int from)
    {
        var i = from;
        while (i < text.Length && IsNameChar(text[i]))
        {
            i++;
        }

        return i;
    }

    private static int SkipWhitespace(string text, int from)
    {
        var i = from;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }
}
