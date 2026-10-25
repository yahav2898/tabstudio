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
