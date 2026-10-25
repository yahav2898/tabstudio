namespace TabStudio.Compilation.Features;

/// <summary>How a <see cref="DocumentEdit"/> changes its file.</summary>
public enum DocumentEditKind
{
    /// <summary>The file exists and its text changes by <see cref="DocumentEdit.Edits"/>.</summary>
    Modified,

    /// <summary>
    /// The file is new. <see cref="DocumentEdit.Edits"/> holds one edit, at offset 0, with its whole text.
    /// </summary>
    Added,

    /// <summary>The file is removed. <see cref="DocumentEdit.Edits"/> is empty.</summary>
    Removed,
}
