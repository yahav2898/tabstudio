namespace TabStudio.Compilation.Features;

/// <summary>What a section of a Quick Info tooltip shows.</summary>
public enum QuickInfoSectionKind
{
    /// <summary>Anything else, such as a plain text message.</summary>
    Other,

    /// <summary>The signature of the symbol, such as <c>string Greeter.Greet(string name)</c>.</summary>
    Description,

    /// <summary>The summary from the documentation comment.</summary>
    Documentation,

    /// <summary>What a method returns, from the documentation comment.</summary>
    Returns,

    /// <summary>What a property holds, from the documentation comment.</summary>
    Value,

    /// <summary>Remarks from the documentation comment.</summary>
    Remarks,

    /// <summary>The type arguments of a generic, such as <c>T is int</c>.</summary>
    TypeParameters,

    /// <summary>The anonymous types the signature uses.</summary>
    AnonymousTypes,

    /// <summary>How an awaitable is used.</summary>
    Usage,

    /// <summary>The exceptions the member can throw.</summary>
    Exceptions,

    /// <summary>The variables a lambda or local function captures.</summary>
    Captures,
}

/// <summary>One section of a Quick Info tooltip.</summary>
/// <param name="Kind">What the section shows.</param>
/// <param name="Runs">The text, as runs to color like code.</param>
public sealed record QuickInfoSection(QuickInfoSectionKind Kind, IReadOnlyList<TaggedRun> Runs)
{
    /// <summary>The section as plain text.</summary>
    public string Text => string.Concat(Runs.Select(r => r.Text));
}

/// <summary>The Quick Info tooltip for the symbol under the pointer or caret.</summary>
/// <param name="SpanStart">Start of the text the tooltip is about, such as an identifier.</param>
/// <param name="SpanLength">Length of that text.</param>
/// <param name="Kind">The icon to show.</param>
/// <param name="Sections">The sections, in display order: the signature first, then documentation.</param>
public sealed record QuickInfoResult(int SpanStart, int SpanLength, SymbolGlyph Kind, IReadOnlyList<QuickInfoSection> Sections);
