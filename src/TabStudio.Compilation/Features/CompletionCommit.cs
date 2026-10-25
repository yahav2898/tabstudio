namespace TabStudio.Compilation.Features;

/// <summary>The change that committing a completion item makes.</summary>
/// <param name="Edits">
/// Edits in document order, with offsets into the current text of the document. Usually one edit that replaces
/// the typed word; more when the item also adds a <c>using</c> directive.
/// </param>
/// <param name="CaretPosition">Where to put the caret after applying the edits, or <see langword="null"/> to leave it at the end of the inserted text.</param>
/// <param name="IncludesCommitCharacter">Whether the edits already contain the commit character, so the editor must not type it again.</param>
public sealed record CompletionCommit(IReadOnlyList<TextEdit> Edits, int? CaretPosition, bool IncludesCommitCharacter)
{
    /// <summary>How much text after the caret to select, such as a snippet's first placeholder; 0 for none.</summary>
    public int SelectionLength { get; init; }
}
