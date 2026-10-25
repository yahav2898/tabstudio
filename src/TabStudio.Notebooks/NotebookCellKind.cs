namespace TabStudio.Notebooks;

/// <summary>What a notebook cell holds.</summary>
public enum NotebookCellKind
{
    /// <summary>Code that a kernel runs, such as C#.</summary>
    Code,

    /// <summary>Markdown, shown as formatted text.</summary>
    Markdown,

    /// <summary>Text that is neither run nor rendered (a Jupyter raw cell).</summary>
    Raw,
}
