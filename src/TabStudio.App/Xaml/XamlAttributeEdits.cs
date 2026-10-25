using System.Text;
using TabStudio.App.Previews;

namespace TabStudio.App.Xaml;

/// <summary>
/// Sets and removes attributes of an element in XAML text, as the Properties panel does: a value replaces the one
/// written, a new attribute goes after the last one (on a line of its own when the attributes are written one per
/// line), and every other character of the file stays as it is.
/// </summary>
internal static class XamlAttributeEdits
{
    /// <summary>The change that gives an element an attribute with a value, adding the attribute when it has none.</summary>
    /// <param name="text">The XAML text the element was parsed from.</param>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute, such as <c>Content</c> or <c>Grid.Row</c>.</param>
    /// <param name="value">The value, as the user typed it; it is escaped for XML.</param>
    public static XamlTextChange Set(string text, XamlElement element, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        if (element.FindAttribute(name) is { } existing)
        {
            return existing.Quote == '\0'
                ? new XamlTextChange(existing.Start, existing.End - existing.Start, Format(name, value))
                : new XamlTextChange(existing.ValueStart, existing.ValueEnd - existing.ValueStart, Escape(value, existing.Quote));
        }

        if (element.Attributes.Count == 0)
        {
            return new XamlTextChange(element.NameEnd, 0, " " + Format(name, value));
        }

        // Attributes written one per line get the new one on a line of its own, lined up with the last one.
        var last = element.Attributes[^1];
        var ownLine = XamlTextLayout.StartsLine(text, last.Start)
            && XamlTextLayout.LineStart(text, last.Start) > element.Start;
        var separator = ownLine
            ? XamlTextLayout.NewLine(text) + text[XamlTextLayout.LineStart(text, last.Start)..last.Start]
            : " ";
        return new XamlTextChange(last.End, 0, separator + Format(name, value));
    }

    /// <summary>The change that removes an attribute and the space in front of it.</summary>
    /// <param name="text">The XAML text the element was parsed from.</param>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute.</param>
    /// <returns>The change, or <see langword="null"/> when the element does not have the attribute.</returns>
    public static XamlTextChange? Remove(string text, XamlElement element, string name)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(element);
        if (element.FindAttribute(name) is not { } attribute)
        {
            return null;
        }

        var start = attribute.Start;
        while (start > element.NameEnd && char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        return new XamlTextChange(start, attribute.End - start, string.Empty);
    }

    /// <summary>Whether a name can be written as an attribute, such as <c>Width</c>, <c>x:Name</c> or <c>Grid.Row</c>.</summary>
    /// <param name="name">The name.</param>
    public static bool IsValidName(string name) =>
        !string.IsNullOrEmpty(name) && XamlMarkup.IsNameStart(name[0]) && name.All(XamlMarkup.IsNameChar)
        && !name.EndsWith(':') && !name.EndsWith('.');

    /// <summary>Escapes a value for an attribute in the given quotes.</summary>
    /// <param name="value">The value.</param>
    /// <param name="quote">The quote character around it.</param>
    public static string Escape(string value, char quote)
    {
        ArgumentNullException.ThrowIfNull(value);
        var escaped = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var reference = c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '"' when quote == '"' => "&quot;",
                '\'' when quote == '\'' => "&apos;",
                '\n' => "&#10;",
                '\r' => "&#13;",
                '\t' => "&#9;",
                _ => null,
            };

            if (reference is null)
            {
                escaped.Append(c);
            }
            else
            {
                escaped.Append(reference);
            }
        }

        return escaped.ToString();
    }

    private static string Format(string name, string value) => $"{name}=\"{Escape(value, '"')}\"";
}
