namespace TabStudio.App.Xaml;

/// <summary>An attribute of an element in XAML text, as it is written.</summary>
/// <param name="Name">The name as written, such as <c>Content</c>, <c>x:Name</c> or <c>Grid.Row</c>.</param>
/// <param name="Value">The value with character references decoded: <c>Save &amp; close</c> for <c>Save &amp;amp; close</c>.</param>
/// <param name="Start">The offset of the name's first character.</param>
/// <param name="End">The offset just past the attribute: past the closing quote, or the value when it has no quotes.</param>
/// <param name="ValueStart">The offset of the value's first character, inside the quotes.</param>
/// <param name="ValueEnd">The offset just past the value's last character, before the closing quote.</param>
/// <param name="Quote">The quote around the value, or <c>'\0'</c> while a value without quotes is being typed.</param>
internal sealed record XamlAttribute(string Name, string Value, int Start, int End, int ValueStart, int ValueEnd, char Quote)
{
    /// <summary>Whether this declares an XML namespace, such as <c>xmlns:x</c>, rather than setting a property.</summary>
    public bool IsNamespaceDeclaration =>
        Name == "xmlns" || Name.StartsWith("xmlns:", StringComparison.Ordinal);
}

/// <summary>
/// An element in XAML text: where its tags are, its attributes, and the elements inside it. Built by
/// <see cref="XamlMarkup.Parse"/>, which also accepts XAML that is being typed and is not well formed yet.
/// </summary>
internal sealed class XamlElement
{
    private readonly List<XamlElement> _children = [];

    /// <summary>Creates an element whose start tag begins at <paramref name="start"/>.</summary>
    /// <param name="name">The name as written, such as <c>Button</c>, <c>local:Card</c> or <c>Grid.RowDefinitions</c>.</param>
    /// <param name="start">The offset of its <c>&lt;</c>.</param>
    /// <param name="parent">The element it is in, or <see langword="null"/> for the root.</param>
    public XamlElement(string name, int start, XamlElement? parent)
    {
        Name = name;
        Start = start;
        Parent = parent;
        parent?._children.Add(this);
    }

    /// <summary>The name as written.</summary>
    public string Name { get; }

    /// <summary>The namespace prefix, such as <c>local</c>; empty for the default namespace.</summary>
    public string Prefix => Name.IndexOf(':', StringComparison.Ordinal) is var colon and >= 0 ? Name[..colon] : string.Empty;

    /// <summary>The name without its prefix, such as <c>Button</c> or <c>Grid.RowDefinitions</c>.</summary>
    public string LocalName => Name.IndexOf(':', StringComparison.Ordinal) is var colon and >= 0 ? Name[(colon + 1)..] : Name;

    /// <summary>Whether this sets a property of its parent, such as <c>&lt;Grid.RowDefinitions&gt;</c>, rather than being an object.</summary>
    public bool IsPropertyElement => LocalName.Contains('.', StringComparison.Ordinal);

    /// <summary>The element it is in, or <see langword="null"/> for the root.</summary>
    public XamlElement? Parent { get; }

    /// <summary>The elements directly inside it, in document order.</summary>
    public IReadOnlyList<XamlElement> Children => _children;

    /// <summary>The offset of the start tag's <c>&lt;</c>.</summary>
    public int Start { get; }

    /// <summary>The offset just past the name in the start tag.</summary>
    public int NameEnd => Start + 1 + Name.Length;

    /// <summary>The offset just past the start tag's <c>&gt;</c>, or where an unfinished start tag stops.</summary>
    public int StartTagEnd { get; internal set; }

    /// <summary>Whether the start tag ends with <c>/&gt;</c>, so the element has no content.</summary>
    public bool IsSelfClosing { get; internal set; }

    /// <summary>Whether the start tag is finished with <c>&gt;</c> or <c>/&gt;</c>.</summary>
    public bool IsStartTagComplete { get; internal set; }

    /// <summary>The offset of the end tag's <c>&lt;/</c>, or <see langword="null"/> when it has none.</summary>
    public int? EndTagStart { get; internal set; }

    /// <summary>The offset just past the element: past its end tag, or its start tag when it has no content.</summary>
    public int End { get; internal set; }

    /// <summary>The attributes of the start tag, in the order they are written.</summary>
    public IReadOnlyList<XamlAttribute> Attributes { get; internal set; } = [];

    /// <summary>Where its content begins: just past the start tag.</summary>
    public int ContentStart => StartTagEnd;

    /// <summary>Where its content ends: at the end tag, or at the element's end when it has none.</summary>
    public int ContentEnd => EndTagStart ?? End;

    /// <summary>Whether it can hold elements: its start tag is finished and does not close it.</summary>
    public bool CanHaveContent => IsStartTagComplete && !IsSelfClosing;

    /// <summary>The attribute with this name as written, if the element has it.</summary>
    /// <param name="name">The name, such as <c>Content</c> or <c>x:Name</c>.</param>
    public XamlAttribute? FindAttribute(string name)
    {
        foreach (var attribute in Attributes)
        {
            if (string.Equals(attribute.Name, name, StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }
}
