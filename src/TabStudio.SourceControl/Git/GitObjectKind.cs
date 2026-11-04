namespace TabStudio.SourceControl.Git;

/// <summary>The kinds of Git objects, with the numbers a pack gives them.</summary>
public enum GitObjectKind
{
    /// <summary>A commit.</summary>
    Commit = 1,

    /// <summary>A tree: a folder's entries.</summary>
    Tree = 2,

    /// <summary>A blob: a file's content.</summary>
    Blob = 3,

    /// <summary>An annotated tag.</summary>
    Tag = 4,
}
