namespace TabStudio.Compilation.Features;

/// <summary>A completion list.</summary>
/// <param name="Items">The items, sorted by <see cref="CompletionEntry.SortText"/>.</param>
/// <param name="SpanStart">Start of the word being completed. The text from here to the caret filters the list.</param>
/// <param name="SpanLength">Length of the word being completed when the list was computed.</param>
/// <param name="IsSuggestionMode">
/// Whether the user may be typing a new name, such as a lambda parameter. The editor should then select the best
/// match without committing it on Space or punctuation.
/// </param>
public sealed record CompletionResult(IReadOnlyList<CompletionEntry> Items, int SpanStart, int SpanLength, bool IsSuggestionMode)
{
    /// <summary>No completions.</summary>
    public static CompletionResult Empty { get; } = new([], 0, 0, false);
}

/// <summary>The change that committing a completion item makes.</summary>
/// <param name="Edits">
/// Edits in document order, with offsets into the current text of the document. Usually one edit that replaces
/// the typed word; more when the item also adds a <c>using</c> directive.
/// </param>
/// <param name="CaretPosition">Where to put the caret after applying the edits, or <see langword="null"/> to leave it at the end of the inserted text.</param>
/// <param name="IncludesCommitCharacter">Whether the edits already contain the commit character, so the editor must not type it again.</param>
public sealed record CompletionCommit(IReadOnlyList<TextEdit> Edits, int? CaretPosition, bool IncludesCommitCharacter);
