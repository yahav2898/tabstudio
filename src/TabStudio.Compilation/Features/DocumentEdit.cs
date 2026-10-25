namespace TabStudio.Compilation.Features;

/// <summary>The changes a rename or code action makes to one file.</summary>
/// <param name="FilePath">Full path of the file.</param>
/// <param name="Kind">Whether the file is changed, created or deleted.</param>
/// <param name="Edits">
/// Non-overlapping edits in document order, with offsets into the file's text before the change. Apply them
/// from last to first so earlier offsets stay valid.
/// </param>
public sealed record DocumentEdit(string FilePath, DocumentEditKind Kind, IReadOnlyList<TextEdit> Edits);
