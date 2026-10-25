using CommunityToolkit.Mvvm.Input;
using TabStudio.Notebooks;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>Adding, deleting, moving and converting cells, with undo.</summary>
public sealed partial class NotebookViewModel
{
    private readonly Stack<Action> _structureUndo = new();
    private readonly List<NotebookCellViewModel> _removedCells = [];
    private bool _lastChangeWasStructural;

    /// <summary>Inserts a new, empty cell, selects it and puts the keyboard in it.</summary>
    /// <param name="index">Where to insert it, from 0 to the number of cells.</param>
    /// <param name="kind">Code or Markdown.</param>
    /// <returns>The new cell.</returns>
    public NotebookCellViewModel InsertCell(int index, NotebookCellKind kind)
    {
        var model = kind switch
        {
            NotebookCellKind.Code => NotebookCell.Code(string.Empty),
            NotebookCellKind.Markdown => NotebookCell.Markdown(string.Empty),
            _ => NotebookCell.Raw(string.Empty),
        };

        var cell = new NotebookCellViewModel(this, model);
        Cells.Insert(Math.Clamp(index, 0, Cells.Count), cell);
        RecordStructureChange(() => Remove(cell));
        SelectedCell = cell;
        cell.IsEditing = cell.IsMarkdown;
        cell.RequestFocus();
        return cell;
    }

    /// <summary>Inserts a new cell right below another one.</summary>
    /// <param name="cell">The cell to insert below.</param>
    /// <param name="kind">Code or Markdown.</param>
    public NotebookCellViewModel InsertCellBelow(NotebookCellViewModel cell, NotebookCellKind kind)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var index = Cells.IndexOf(cell);
        return InsertCell(index < 0 ? Cells.Count : index + 1, kind);
    }

    /// <summary>Inserts a new cell right above another one.</summary>
    /// <param name="cell">The cell to insert above.</param>
    /// <param name="kind">Code or Markdown.</param>
    public NotebookCellViewModel InsertCellAbove(NotebookCellViewModel cell, NotebookCellKind kind)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return InsertCell(Math.Max(0, Cells.IndexOf(cell)), kind);
    }

    /// <summary>Deletes a cell. Undo brings it back with its text and outputs.</summary>
    /// <param name="cell">The cell.</param>
    public void DeleteCell(NotebookCellViewModel cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var index = Cells.IndexOf(cell);
        if (index < 0)
        {
            return;
        }

        RemoveFromQueue(cell);
        Cells.RemoveAt(index);
        _removedCells.Add(cell);
        RecordStructureChange(() =>
        {
            _removedCells.Remove(cell);
            Cells.Insert(Math.Min(index, Cells.Count), cell);
            SelectedCell = cell;
        });

        SelectedCell = Cells.Count == 0 ? null : Cells[Math.Min(index, Cells.Count - 1)];
        StatusMessage = "Deleted a cell. Undo (Ctrl+Z) brings it back.";
    }

    /// <summary>Moves a cell up or down.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="offset">-1 to move it up, 1 to move it down.</param>
    public void MoveCell(NotebookCellViewModel cell, int offset)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var index = Cells.IndexOf(cell);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= Cells.Count)
        {
            return;
        }

        Cells.Move(index, target);
        RecordStructureChange(() =>
        {
            var now = Cells.IndexOf(cell);
            if (now >= 0)
            {
                Cells.Move(now, Math.Min(index, Cells.Count - 1));
            }
        });

        SelectedCell = cell;
    }

    /// <summary>Turns a code cell into a Markdown cell or the other way round, keeping its text.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="kind">The new kind.</param>
    public void ChangeCellKind(NotebookCellViewModel cell, NotebookCellKind kind)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var previous = cell.Kind;
        if (previous == kind)
        {
            return;
        }

        RemoveFromQueue(cell);
        cell.ChangeKind(kind);
        RecordStructureChange(() => cell.ChangeKind(previous));
    }

    /// <summary>Removes a cell's outputs.</summary>
    /// <param name="cell">The cell.</param>
    public void ClearOutputs(NotebookCellViewModel cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        if (cell.ClearOutputs() && KeepsOutputs)
        {
            MarkChanged();
        }
    }

    /// <summary>Starts editing a cell: shows a Markdown cell's text and puts the keyboard in the editor.</summary>
    /// <param name="cell">The cell.</param>
    public void EditCell(NotebookCellViewModel cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        SelectedCell = cell;
        cell.IsEditing = cell.IsMarkdown;
        cell.RequestFocus();
    }

    /// <summary>Selects the next or previous cell.</summary>
    /// <param name="offset">1 for the next cell, -1 for the previous one.</param>
    public void SelectAdjacentCell(int offset)
    {
        if (Cells.Count == 0)
        {
            return;
        }

        var index = SelectedCell is null ? -1 : Cells.IndexOf(SelectedCell);
        SelectedCell = Cells[Math.Clamp(index + offset, 0, Cells.Count - 1)];
    }

    [RelayCommand]
    private void AddCodeCell() => InsertCell(InsertionIndex(), NotebookCellKind.Code);

    [RelayCommand]
    private void AddMarkdownCell() => InsertCell(InsertionIndex(), NotebookCellKind.Markdown);

    [RelayCommand]
    private void AppendCodeCell() => InsertCell(Cells.Count, NotebookCellKind.Code);

    [RelayCommand]
    private void AppendMarkdownCell() => InsertCell(Cells.Count, NotebookCellKind.Markdown);

    [RelayCommand]
    private void ClearAllOutputs()
    {
        var cleared = false;
        foreach (var cell in Cells)
        {
            cleared |= cell.ClearOutputs();
        }

        if (cleared && KeepsOutputs)
        {
            MarkChanged();
        }
    }

    private int InsertionIndex()
    {
        var index = SelectedCell is null ? -1 : Cells.IndexOf(SelectedCell);
        return index >= 0 ? index + 1 : Cells.Count;
    }

    private void Remove(NotebookCellViewModel cell)
    {
        RemoveFromQueue(cell);
        Cells.Remove(cell);
        _removedCells.Add(cell);
        if (ReferenceEquals(SelectedCell, cell))
        {
            SelectedCell = Cells.Count > 0 ? Cells[^1] : null;
        }
    }

    private void RecordStructureChange(Action undo)
    {
        _structureUndo.Push(undo);
        _lastChangeWasStructural = true;
        MarkChanged();
        UpdateUndoState();
    }

    /// <summary>Forgets the cell changes Undo could take back, when the cells are replaced as a whole.</summary>
    private void ForgetStructureChanges()
    {
        _structureUndo.Clear();
        foreach (var cell in _removedCells)
        {
            cell.Dispose();
        }

        _removedCells.Clear();
        _lastChangeWasStructural = false;
        UpdateUndoState();
    }

    private void UndoStructureChange()
    {
        var undo = _structureUndo.Pop();
        undo();
        _lastChangeWasStructural = _structureUndo.Count > 0;
        StatusMessage = null;
        MarkChanged();
        UpdateUndoState();
    }
}
