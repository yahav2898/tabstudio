namespace TabStudio.Compilation.Wpf.Xaml;

/// <summary>An attribute of a XAML element, with the offsets it is written at.</summary>
public sealed class XamlAttribute
{
    internal XamlAttribute(string prefix, string localName, int nameStart, int nameEnd)
    {
        Prefix = prefix;
        LocalName = localName;
        NameStart = nameStart;
        NameEnd = nameEnd;
    }

    /// <summary>The prefix before the colon, such as <c>x</c> in <c>x:Name</c>; empty when there is none.</summary>
    public string Prefix { get; }

    /// <summary>The name after the prefix, such as <c>Name</c>, or <c>Grid.Row</c> for an attached property.</summary>
    public string LocalName { get; }

    /// <summary>The name as written, such as <c>x:Name</c>.</summary>
    public string Name => Prefix.Length == 0 ? LocalName : $"{Prefix}:{LocalName}";

    /// <summary>The offset of the name's first character.</summary>
    public int NameStart { get; }

    /// <summary>The offset just past the name.</summary>
    public int NameEnd { get; }

    /// <summary>Whether the attribute has <c>=</c> and an opening quote.</summary>
    public bool HasValue { get; internal set; }

    /// <summary>The offset of the value's first character, just inside the quote; the name's end when there is no value.</summary>
    public int ValueStart { get; internal set; }

    /// <summary>The offset just past the value's last character, at the closing quote or where the text stops.</summary>
    public int ValueEnd { get; internal set; }

    /// <summary>Whether the value's closing quote is there.</summary>
    public bool IsValueClosed { get; internal set; }

    /// <summary>The value as written, entities undecoded.</summary>
    public string RawValue { get; internal set; } = string.Empty;

    /// <summary>The value with its character entities decoded, such as <c>&amp;amp;</c> to <c>&amp;</c>.</summary>
    public string Value => XamlText.Decode(RawValue);

    /// <summary>Whether this is an XML namespace declaration, <c>xmlns</c> or <c>xmlns:prefix</c>.</summary>
    public bool IsNamespaceDeclaration => (Prefix.Length == 0 && LocalName == "xmlns") || Prefix == "xmlns";

    /// <summary>The offset just past the attribute: its closing quote, or its name when it has no value.</summary>
    public int End => HasValue ? (IsValueClosed ? ValueEnd + 1 : ValueEnd) : NameEnd;
}

/// <summary>A XAML element, with the offsets it is written at and the elements and attributes in it.</summary>
public sealed class XamlElement
{
    private readonly List<XamlAttribute> _attributes = [];
    private readonly List<XamlElement> _children = [];

    internal XamlElement(XamlElement? parent, string prefix, string localName, int start, int nameStart, int nameEnd)
    {
        Parent = parent;
        Prefix = prefix;
        LocalName = localName;
        Start = start;
        NameStart = nameStart;
        NameEnd = nameEnd;
        StartTagEnd = -1;
        End = -1;
    }

    /// <summary>The element it is in, or <see langword="null"/> for the root.</summary>
    public XamlElement? Parent { get; }

    /// <summary>The prefix of its name, empty for the default namespace.</summary>
    public string Prefix { get; }

    /// <summary>Its name after the prefix, such as <c>Button</c>, or <c>Grid.RowDefinitions</c> for a property element.</summary>
    public string LocalName { get; }

    /// <summary>The name as written, such as <c>local:CustomerView</c>.</summary>
    public string Name => Prefix.Length == 0 ? LocalName : $"{Prefix}:{LocalName}";

    /// <summary>Whether it sets a property of its parent, as <c>&lt;Grid.RowDefinitions&gt;</c> does, rather than create an object.</summary>
    public bool IsPropertyElement => LocalName.Contains('.', StringComparison.Ordinal);

    /// <summary>The offset of its <c>&lt;</c>.</summary>
    public int Start { get; }

    /// <summary>The offset of its name.</summary>
    public int NameStart { get; }

    /// <summary>The offset just past its name.</summary>
    public int NameEnd { get; }

    /// <summary>The offset just past its start tag's <c>&gt;</c>, or -1 while the tag is not closed.</summary>
    public int StartTagEnd { get; internal set; }

    /// <summary>Whether it ends with <c>/&gt;</c>.</summary>
    public bool IsSelfClosing { get; internal set; }

    /// <summary>The offset of its end tag's <c>&lt;</c>, or -1 when it has none.</summary>
    public int EndTagStart { get; internal set; } = -1;

    /// <summary>The offset just past the element, or -1 while it is not closed: everything typed after it is inside.</summary>
    public int End { get; internal set; }

    /// <summary>Its attributes, in order.</summary>
    public IReadOnlyList<XamlAttribute> Attributes => _attributes;

    /// <summary>The elements directly in it, in order.</summary>
    public IReadOnlyList<XamlElement> Children => _children;

    /// <summary>The attribute with a name as written, such as <c>x:Name</c>, or <see langword="null"/>.</summary>
    /// <param name="name">The name.</param>
    public XamlAttribute? Attribute(string name)
    {
        foreach (var attribute in _attributes)
        {
            if (string.Equals(attribute.Name, name, StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>The attribute with a namespace and name, such as the XAML language namespace's <c>Name</c>.</summary>
    /// <param name="xmlNamespace">The namespace, empty for an attribute without a prefix.</param>
    /// <param name="localName">The name.</param>
    public XamlAttribute? Attribute(string xmlNamespace, string localName)
    {
        foreach (var attribute in _attributes)
        {
            if (string.Equals(attribute.LocalName, localName, StringComparison.Ordinal)
                && string.Equals(attribute.Prefix.Length == 0 ? string.Empty : NamespaceOf(attribute.Prefix), xmlNamespace, StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>The XML namespace of its name.</summary>
    public string XmlNamespace => NamespaceOf(Prefix) ?? string.Empty;

    /// <summary>The XML namespace a prefix stands for here, from this element's and its ancestors' declarations.</summary>
    /// <param name="prefix">The prefix, empty for the default namespace.</param>
    /// <returns>The namespace, or <see langword="null"/> when the prefix is not declared.</returns>
    public string? NamespaceOf(string prefix)
    {
        for (var element = this; element is not null; element = element.Parent)
        {
            foreach (var attribute in element._attributes)
            {
                if (!attribute.HasValue)
                {
                    continue;
                }

                var declares = prefix.Length == 0
                    ? attribute.Prefix.Length == 0 && attribute.LocalName == "xmlns"
                    : attribute.Prefix == "xmlns" && string.Equals(attribute.LocalName, prefix, StringComparison.Ordinal);
                if (declares)
                {
                    return attribute.Value;
                }
            }
        }

        return prefix switch
        {
            "xml" => "http://www.w3.org/XML/1998/namespace",
            _ => null,
        };
    }

    /// <summary>The prefixes declared here and above, each with its namespace, the nearest declaration winning.</summary>
    public IReadOnlyDictionary<string, string> NamespacesInScope()
    {
        var scope = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var element = this; element is not null; element = element.Parent)
        {
            foreach (var attribute in element._attributes)
            {
                if (attribute.IsNamespaceDeclaration && attribute.HasValue)
                {
                    scope.TryAdd(attribute.Prefix.Length == 0 ? string.Empty : attribute.LocalName, attribute.Value);
                }
            }
        }

        return scope;
    }

    /// <summary>Whether an offset is inside the element, from its <c>&lt;</c> to its end, or to the text's end while it is open.</summary>
    /// <param name="offset">The offset.</param>
    public bool Contains(int offset) => offset >= Start && (End < 0 || offset < End);

    /// <summary>Whether an offset is inside its start tag, after the name.</summary>
    /// <param name="offset">The offset.</param>
    public bool IsInStartTag(int offset) => offset > NameEnd - 1 && (StartTagEnd < 0 ? End < 0 || offset < End : offset < StartTagEnd);

    internal void Add(XamlAttribute attribute) => _attributes.Add(attribute);

    internal void Add(XamlElement child) => _children.Add(child);
}

/// <summary>Helpers for the characters of XAML text.</summary>
public static class XamlText
{
    /// <summary>Whether a character can start an XML name.</summary>
    /// <param name="c">The character.</param>
    public static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';

    /// <summary>Whether a character can be in an XML name, with the prefix's colon and an attached member's dot.</summary>
    /// <param name="c">The character.</param>
    public static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or ':';

    /// <summary>Decodes the character entities of XML text, such as <c>&amp;lt;</c> and <c>&amp;#x41;</c>.</summary>
    /// <param name="text">The text.</param>
    public static string Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Contains('&', StringComparison.Ordinal))
        {
            return text;
        }

        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var semicolon = text[i] == '&' ? text.IndexOf(';', i) : -1;
            if (semicolon < 0 || semicolon - i > 10)
            {
                result.Append(text[i]);
                continue;
            }

            var entity = text[(i + 1)..semicolon];
            string? decoded = entity switch
            {
                "lt" => "<",
                "gt" => ">",
                "amp" => "&",
                "quot" => "\"",
                "apos" => "'",
                _ when entity.StartsWith("#x", StringComparison.OrdinalIgnoreCase) && int.TryParse(entity[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hex) => FromCodePoint(hex),
                _ when entity.StartsWith('#') && int.TryParse(entity[1..], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number) => FromCodePoint(number),
                _ => null,
            };
            if (decoded is null)
            {
                result.Append(text[i]);
                continue;
            }

            result.Append(decoded);
            i = semicolon;
        }

        return result.ToString();
    }

    private static string? FromCodePoint(int value) =>
        value is >= 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF) ? char.ConvertFromUtf32(value) : null;
}
