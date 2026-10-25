namespace TabStudio.Compilation.Features;

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
