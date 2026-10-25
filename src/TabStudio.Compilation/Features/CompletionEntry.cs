namespace TabStudio.Compilation.Features;

/// <summary>
/// Identifies a <see cref="CompletionEntry"/> to the language service, so committing it or showing its description
/// can go back to the Roslyn item it came from. Treat it as opaque.
/// </summary>
/// <param name="Session">The completion request the entry belongs to.</param>
/// <param name="Index">The entry's position in that request.</param>
public readonly record struct CompletionHandle(long Session, int Index);

/// <summary>One item of a completion list.</summary>
/// <param name="Handle">Identifies the item for <see cref="CompletionFeature.GetCompletionChangeAsync"/> and <see cref="CompletionFeature.GetCompletionDescriptionAsync"/>.</param>
/// <param name="DisplayText">The text shown in the list, such as <c>WriteLine</c>.</param>
/// <param name="DisplayTextPrefix">Text shown before <paramref name="DisplayText"/>, usually empty.</param>
/// <param name="DisplayTextSuffix">Text shown after <paramref name="DisplayText"/>, such as <c>&lt;&gt;</c> for generic types.</param>
/// <param name="FilterText">The text matched against what the user types.</param>
/// <param name="SortText">The text the list is sorted by.</param>
/// <param name="InlineDescription">A short note shown at the right of the item, such as the namespace of a type that is not imported yet.</param>
/// <param name="Kind">The icon to show.</param>
/// <param name="IsPreselected">Whether the item should be selected when the list opens, such as the matching enum type after <c>=</c>.</param>
/// <param name="SpanStart">Start of the text the item replaces when committed.</param>
/// <param name="SpanLength">Length of the text the item replaces, as it was when the list was computed.</param>
public sealed record CompletionEntry(
    CompletionHandle Handle,
    string DisplayText,
    string DisplayTextPrefix,
    string DisplayTextSuffix,
    string FilterText,
    string SortText,
    string InlineDescription,
    SymbolGlyph Kind,
    bool IsPreselected,
    int SpanStart,
    int SpanLength);
