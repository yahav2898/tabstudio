namespace TabStudio.Notebooks;

/// <summary>The file formats a notebook can be read from and saved to.</summary>
public enum NotebookFormat
{
    /// <summary>A Jupyter notebook (<c>.ipynb</c>), nbformat 4: JSON with cells, outputs and metadata.</summary>
    Jupyter,

    /// <summary>A Polyglot Notebooks file (<c>.dib</c>): plain text with <c>#!csharp</c>-style cell markers and no outputs.</summary>
    Polyglot,
}
