using TabStudio.Compilation.Wpf.Xaml;

namespace TabStudio.Compilation.Wpf.Editing;

/// <summary>Where the caret is in XAML, for completion, Quick Info and Go to Definition.</summary>
internal enum XamlCaretKind
{
    /// <summary>Nowhere XAML features apply: element content, a comment or text outside the root.</summary>
    None,

    /// <summary>In an element's name, or just after its <c>&lt;</c>.</summary>
    ElementName,

    /// <summary>In an end tag's name, or just after its <c>&lt;/</c>.</summary>
    EndTagName,

    /// <summary>In an attribute's name, or in a start tag where one can go.</summary>
    AttributeName,

    /// <summary>In an attribute's value.</summary>
    AttributeValue,
}

/// <summary>
/// The XAML around the caret: the element and attribute it is in, and the word it is on, which completion replaces.
/// </summary>
/// <param name="Kind">Where the caret is.</param>
/// <param name="Document">The parsed text.</param>
/// <param name="Offset">The caret.</param>
/// <param name="WordStart">The start of the word the caret is on, or the caret when it is on none.</param>
/// <param name="WordEnd">The end of the word.</param>
/// <param name="Element">The element whose tag the caret is in; for an end tag, the element it closes, when known.</param>
/// <param name="Attribute">The attribute the caret is in, for <see cref="XamlCaretKind.AttributeName"/> on a name and <see cref="XamlCaretKind.AttributeValue"/>.</param>
internal sealed record XamlCaret(
    XamlCaretKind Kind,
    XamlDocument Document,
    int Offset,
    int WordStart,
    int WordEnd,
    XamlElement? Element,
    XamlAttributeNode? Attribute)
{
    /// <summary>The word the caret is on.</summary>
    public string Word => Document.Text[WordStart..WordEnd];

    /// <summary>The part of the word before the caret, which filters the list.</summary>
    public string Typed => Document.Text[WordStart..Math.Clamp(Offset, WordStart, WordEnd)];

    /// <summary>The caret's offset in the attribute's value.</summary>
    public int OffsetInValue => Attribute is { } attribute ? Offset - attribute.ValueStart : 0;

    /// <summary>Finds what the caret is on.</summary>
    /// <param name="document">The parsed text.</param>
    /// <param name="offset">The caret.</param>
    public static XamlCaret At(XamlDocument document, int offset)
    {
        ArgumentNullException.ThrowIfNull(document);
        var text = document.Text;
        offset = Math.Clamp(offset, 0, text.Length);
        var none = new XamlCaret(XamlCaretKind.None, document, offset, offset, offset, null, null);
        if (document.IsInOtherMarkup(offset))
        {
            return none;
        }

        if (EndTagAt(document, offset) is { } endTag)
        {
            return endTag;
        }

        if (document.StartTagAt(offset) is not { } element || offset < element.NameStart)
        {
            return none;
        }

        if (offset <= element.NameEnd)
        {
            return new XamlCaret(XamlCaretKind.ElementName, document, offset, element.NameStart, element.NameEnd, element, null);
        }

        foreach (var attribute in element.Attributes)
        {
            if (attribute.HasValue && offset >= attribute.ValueStart && offset <= attribute.ValueEnd)
            {
                return new XamlCaret(XamlCaretKind.AttributeValue, document, offset, attribute.ValueStart, attribute.ValueEnd, element, attribute);
            }

            if (offset >= attribute.NameStart && offset <= attribute.NameEnd)
            {
                return new XamlCaret(XamlCaretKind.AttributeName, document, offset, attribute.NameStart, attribute.NameEnd, element, attribute);
            }

            if (offset > attribute.NameEnd && offset < attribute.End)
            {
                // Between a name and its value, at the = or a quote.
                return none;
            }
        }

        // In the tag between attributes: an attribute can start here once there is white space before it.
        return offset > 0 && char.IsWhiteSpace(text[offset - 1])
            ? new XamlCaret(XamlCaretKind.AttributeName, document, offset, offset, offset, element, null)
            : none;
    }

    /// <summary>The caret in an end tag's name, with the element the tag closes, or would close.</summary>
    private static XamlCaret? EndTagAt(XamlDocument document, int offset)
    {
        var text = document.Text;
        var lessThan = text.LastIndexOf('<', Math.Max(0, offset - 1));
        if (lessThan < 0 || offset == 0 || lessThan + 1 >= text.Length || text[lessThan + 1] != '/' || offset < lessThan + 2)
        {
            return null;
        }

        var nameEnd = lessThan + 2;
        while (nameEnd < text.Length && XamlText.IsNameChar(text[nameEnd]))
        {
            nameEnd++;
        }

        if (offset > nameEnd || text.AsSpan(lessThan + 2, offset - lessThan - 2).ContainsAny('>', '<'))
        {
            return null;
        }

        // The element this tag closes is the innermost one still open where the tag starts.
        XamlElement? closes = null;
        for (var element = document.ElementAt(lessThan); element is not null; element = element.Parent)
        {
            if (!element.IsSelfClosing && element.StartTagEnd >= 0 && element.StartTagEnd <= lessThan && (element.EndTagStart < 0 || element.EndTagStart >= lessThan))
            {
                closes = element;
                break;
            }
        }

        return new XamlCaret(XamlCaretKind.EndTagName, document, offset, lessThan + 2, nameEnd, closes, null);
    }
}
