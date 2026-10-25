using TabStudio.Notebooks.Formats;

namespace TabStudio.Notebooks;

/// <summary>
/// A notebook: an ordered list of cells and the notebook's metadata, independent of the file format.
/// </summary>
/// <remarks>
/// <para>
/// Documents are immutable. The editing methods return a new document and leave this one unchanged, which
/// makes undo and saving in the background safe.
/// </para>
/// <para>
/// Read and save documents with <see cref="NotebookSerializer"/>. Everything a file contains that TabStudio
/// does not interpret is kept in <see cref="Metadata"/> and the <c>AdditionalProperties</c> of the document,
/// its cells and outputs, so a notebook saved in the format it was read from changes only where it was edited.
/// </para>
/// </remarks>
public sealed record NotebookDocument
{
    /// <summary>The cells, in order.</summary>
    public IReadOnlyList<NotebookCell> Cells { get; init; } = [];

    /// <summary>
    /// The notebook's metadata as stored by the format it was read from: the Jupyter <c>metadata</c> object for
    /// <c>.ipynb</c>, or the <c>#!meta</c> object for <c>.dib</c>.
    /// </summary>
    public NotebookMetadata Metadata { get; init; } = NotebookMetadata.Empty;

    /// <summary>Other top-level properties of an <c>.ipynb</c> file, including <c>nbformat</c> and <c>nbformat_minor</c>.</summary>
    public NotebookMetadata AdditionalProperties { get; init; } = NotebookMetadata.Empty;

    /// <summary>The format the notebook was read from, or <see langword="null"/> for a notebook created in memory.</summary>
    public NotebookFormat? Format { get; init; }

    /// <summary>The language of code cells that do not name one, as a kernel name. C# unless the metadata says otherwise.</summary>
    public string DefaultLanguage => KernelInfoMetadata.GetDefaultLanguage(Metadata);

    /// <summary>Creates a new C# notebook, with the metadata Polyglot Notebooks expects.</summary>
    /// <param name="cells">The cells.</param>
    public static NotebookDocument Create(params IEnumerable<NotebookCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        return new NotebookDocument
        {
            Cells = cells.ToList(),
            Metadata = NotebookMetadata.Empty.With(KernelInfoMetadata.KernelInfoKey, KernelInfoMetadata.CreateKernelInfo(NotebookLanguages.CSharp)),
        };
    }

    /// <summary>Finds the position of a cell.</summary>
    /// <param name="cellId">The cell's <see cref="NotebookCell.Id"/>.</param>
    /// <returns>The zero-based index, or -1 when there is no such cell.</returns>
    public int IndexOf(string cellId)
    {
        ArgumentNullException.ThrowIfNull(cellId);
        for (var i = 0; i < Cells.Count; i++)
        {
            if (Cells[i].Id == cellId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds a cell by id.</summary>
    /// <param name="cellId">The cell's <see cref="NotebookCell.Id"/>.</param>
    /// <returns>The cell, or <see langword="null"/> when there is no such cell.</returns>
    public NotebookCell? FindCell(string cellId)
    {
        var index = IndexOf(cellId);
        return index < 0 ? null : Cells[index];
    }

    /// <summary>Returns the notebook with the cell that has the same id replaced.</summary>
    /// <param name="cell">The new version of the cell.</param>
    /// <exception cref="ArgumentException">The notebook has no cell with that id.</exception>
    public NotebookDocument ReplaceCell(NotebookCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var index = IndexOf(cell.Id);
        if (index < 0)
        {
            throw new ArgumentException($"The notebook has no cell with id '{cell.Id}'.", nameof(cell));
        }

        var cells = Cells.ToList();
        cells[index] = cell;
        return this with { Cells = cells };
    }

    /// <summary>Returns the notebook with a cell inserted.</summary>
    /// <param name="index">Where to insert the cell, from 0 to the number of cells.</param>
    /// <param name="cell">The cell to insert.</param>
    public NotebookDocument InsertCell(int index, NotebookCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, Cells.Count);

        var cells = Cells.ToList();
        cells.Insert(index, cell);
        return this with { Cells = cells };
    }

    /// <summary>Returns the notebook without a cell.</summary>
    /// <param name="cellId">The id of the cell to remove. Nothing changes if there is no such cell.</param>
    public NotebookDocument RemoveCell(string cellId)
    {
        var index = IndexOf(cellId);
        if (index < 0)
        {
            return this;
        }

        var cells = Cells.ToList();
        cells.RemoveAt(index);
        return this with { Cells = cells };
    }

    /// <summary>Returns the notebook with a cell moved to a new position.</summary>
    /// <param name="cellId">The id of the cell to move.</param>
    /// <param name="newIndex">Its position afterwards, from 0 to the number of cells minus one.</param>
    /// <exception cref="ArgumentException">The notebook has no cell with that id.</exception>
    public NotebookDocument MoveCell(string cellId, int newIndex)
    {
        var index = IndexOf(cellId);
        if (index < 0)
        {
            throw new ArgumentException($"The notebook has no cell with id '{cellId}'.", nameof(cellId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(newIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(newIndex, Cells.Count);

        var cells = Cells.ToList();
        var cell = cells[index];
        cells.RemoveAt(index);
        cells.Insert(newIndex, cell);
        return this with { Cells = cells };
    }

    /// <summary>Returns the notebook with every cell's outputs and execution count cleared.</summary>
    public NotebookDocument WithoutOutputs() => this with { Cells = Cells.Select(c => c.WithoutOutputs()).ToList() };
}
