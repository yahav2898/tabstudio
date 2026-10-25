using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks;

/// <summary>
/// One cell of a notebook: code, Markdown or raw text, with the outputs it produced when it last ran.
/// </summary>
/// <remarks>
/// Cells are immutable. Change one with a <see langword="with"/> expression and put it back with
/// <see cref="NotebookDocument.ReplaceCell(NotebookCell)"/>; <see cref="Id"/> identifies it.
/// </remarks>
public sealed record NotebookCell
{
    /// <summary>
    /// Identifies the cell within its notebook. Jupyter notebooks from format 4.5 on store it; cells read from
    /// files without ids get a new one each time they are loaded.
    /// </summary>
    public string Id { get; init; } = NewId();

    /// <summary>Whether the cell holds code, Markdown or raw text.</summary>
    public required NotebookCellKind Kind { get; init; }

    /// <summary>
    /// The language of a code cell, as a kernel name such as <see cref="NotebookLanguages.CSharp"/>.
    /// Markdown cells use <see cref="NotebookLanguages.Markdown"/> and raw cells <see cref="NotebookLanguages.Raw"/>.
    /// </summary>
    public string Language { get; init; } = NotebookLanguages.CSharp;

    /// <summary>The text of the cell.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>What the cell produced when it last ran. Only code cells have outputs.</summary>
    public IReadOnlyList<NotebookOutput> Outputs { get; init; } = [];

    /// <summary>The kernel's execution counter when the cell last ran, shown as <c>[3]</c>; <see langword="null"/> if it has not run.</summary>
    public int? ExecutionCount { get; init; }

    /// <summary>The cell's metadata, including what other tools stored there.</summary>
    public NotebookMetadata Metadata { get; init; } = NotebookMetadata.Empty;

    /// <summary>Other properties the cell had in its file, such as Jupyter attachments, kept so saving does not lose them.</summary>
    public NotebookMetadata AdditionalProperties { get; init; } = NotebookMetadata.Empty;

    /// <summary>Whether this is a code cell.</summary>
    public bool IsCode => Kind == NotebookCellKind.Code;

    /// <summary>
    /// Whether <see cref="Id"/> was made up while loading a file that had no id for the cell, so that saving the
    /// file again does not add one.
    /// </summary>
    internal bool HasGeneratedId { get; init; }

    /// <summary>Creates a code cell.</summary>
    /// <param name="source">The code.</param>
    /// <param name="language">The language, as a kernel name. C# unless given.</param>
    public static NotebookCell Code(string source, string language = NotebookLanguages.CSharp)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(language);
        return new NotebookCell { Kind = NotebookCellKind.Code, Language = language, Source = source };
    }

    /// <summary>Creates a Markdown cell.</summary>
    /// <param name="source">The Markdown text.</param>
    public static NotebookCell Markdown(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new NotebookCell { Kind = NotebookCellKind.Markdown, Language = NotebookLanguages.Markdown, Source = source };
    }

    /// <summary>Creates a raw cell.</summary>
    /// <param name="source">The text.</param>
    public static NotebookCell Raw(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new NotebookCell { Kind = NotebookCellKind.Raw, Language = NotebookLanguages.Raw, Source = source };
    }

    /// <summary>Creates a new cell id: eight hexadecimal digits, as VS Code writes them.</summary>
    public static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Returns the cell without outputs or execution count, as if it had never run.</summary>
    public NotebookCell WithoutOutputs() => this with { Outputs = [], ExecutionCount = null };
}
