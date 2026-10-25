using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace System.Windows.Forms;

/// <summary>The current cell, the selection, sorting and copying.</summary>
public partial class DataGridView
{
    private readonly HashSet<DataGridViewCell> _selectedCells = [];
    private DataGridViewCell? _currentCell;
    private DataGridViewCell? _anchorCell;
    private DataGridViewColumn? _sortedColumn;
    private SortOrder _sortOrder;

    /// <summary>The cell with focus, whose row is the current row.</summary>
    public DataGridViewCell? CurrentCell
    {
        get => _currentCell;
        set
        {
            if (value == _currentCell)
            {
                return;
            }

            if (value is not null && (value.DataGridView != this || value.RowIndex < 0 || value.ColumnIndex < 0))
            {
                throw new ArgumentException("The current cell must be a cell of the grid.", nameof(value));
            }

            if (value is not null && !value.Visible)
            {
                throw new InvalidOperationException("Current cell cannot be set to an invisible cell.");
            }

            if (!EndEdit())
            {
                return;
            }

            ClearSelectionCore();
            SetCurrentCellCore(value, raiseEvents: true);
            if (value is not null)
            {
                SelectFromCurrent(value, extend: false, toggle: false);
            }

            Invalidate();
            OnSelectionChanged(EventArgs.Empty);
        }
    }

    /// <summary>The column and row of the current cell, or (-1, -1).</summary>
    public Point CurrentCellAddress => _currentCell is null ? new Point(-1, -1) : new Point(_currentCell.ColumnIndex, _currentCell.RowIndex);

    /// <summary>The row of the current cell.</summary>
    public DataGridViewRow? CurrentRow => _currentCell?.OwningRow;

    /// <summary>The selected cells.</summary>
    public DataGridViewSelectedCellCollection SelectedCells => new(_rows.Items.SelectMany(row => row.Cells.Items).Where(IsCellSelected));

    /// <summary>The selected rows.</summary>
    public DataGridViewSelectedRowCollection SelectedRows => new(_rows.Items.Where(row => row.Selected).Reverse());

    /// <summary>The selected columns.</summary>
    public DataGridViewSelectedColumnCollection SelectedColumns => new(_columns.Items.Where(column => column.Selected));

    /// <summary>The column the rows are sorted by, if any.</summary>
    public DataGridViewColumn? SortedColumn => _sortedColumn;

    /// <summary>The direction the rows are sorted in.</summary>
    public SortOrder SortOrder => _sortOrder;

    /// <summary>Whether all the cells are selected.</summary>
    /// <param name="includeInvisibleCells">Whether hidden cells count.</param>
    public bool AreAllCellsSelected(bool includeInvisibleCells) =>
        _rows.Items.Where(row => includeInvisibleCells || row.Visible).SelectMany(row => row.Cells.Items).All(IsCellSelected);

    /// <summary>Deselects every cell, row and column.</summary>
    public void ClearSelection()
    {
        ClearSelectionCore();
        Invalidate();
        OnSelectionChanged(EventArgs.Empty);
    }

    /// <summary>Selects every cell.</summary>
    public void SelectAll()
    {
        if (!MultiSelect)
        {
            return;
        }

        if (_selectionMode is DataGridViewSelectionMode.FullRowSelect or DataGridViewSelectionMode.RowHeaderSelect)
        {
            foreach (var row in _rows.Items)
            {
                row.SetSelectedInternal(true);
            }
        }
        else
        {
            foreach (var cell in _rows.Items.SelectMany(row => row.Cells.Items))
            {
                _selectedCells.Add(cell);
            }
        }

        Invalidate();
        OnSelectionChanged(EventArgs.Empty);
    }

    /// <summary>Sorts the rows by a column's values.</summary>
    /// <param name="dataGridViewColumn">The column.</param>
    /// <param name="direction">The direction.</param>
    public virtual void Sort(DataGridViewColumn dataGridViewColumn, ListSortDirection direction)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewColumn);
        if (!EndEdit())
        {
            return;
        }

        if (IsBound)
        {
            if (_boundList is IBindingList { SupportsSorting: true } bindingList && PropertyOf(dataGridViewColumn) is { } property)
            {
                bindingList.ApplySort(property, direction);
            }
            else
            {
                throw new InvalidOperationException("DataGridView control cannot be sorted if it is bound to an IBindingList that does not support sorting.");
            }
        }
        else
        {
            var column = dataGridViewColumn.Index;
            var sign = direction == ListSortDirection.Ascending ? 1 : -1;
            _rows.SortInternal((a, b) => sign * CompareCells(dataGridViewColumn, a, b, column));
        }

        SetSorted(dataGridViewColumn, direction == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending);
    }

    /// <summary>Sorts the rows with a comparer of rows.</summary>
    /// <param name="comparer">The comparer.</param>
    public virtual void Sort(IComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        if (IsBound)
        {
            throw new InvalidOperationException("A data-bound DataGridView control can only be sorted on data-bound columns.");
        }

        EndEdit();
        _rows.SortInternal((a, b) => comparer.Compare(a, b));
        SetSorted(null, SortOrder.None);
    }

    /// <summary>The selected cells as text, for the clipboard: tabs between cells, lines between rows.</summary>
    public virtual DataObject? GetClipboardContent()
    {
        if (ClipboardCopyMode == DataGridViewClipboardCopyMode.Disable)
        {
            return null;
        }

        var selected = _rows.Items.Where(row => row.Cells.Items.Exists(IsCellSelected)).ToList();
        if (selected.Count == 0)
        {
            return null;
        }

        var columns = _columnOrder.Where(column => column.Visible && selected.Exists(row => IsCellSelected(row.Cells[column.Index]))).ToList();
        var text = new StringBuilder();
        if (ClipboardCopyMode == DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
            || (ClipboardCopyMode == DataGridViewClipboardCopyMode.EnableWithAutoHeaderText && selected.Exists(row => row.Selected)))
        {
            text.AppendJoin('\t', columns.Select(column => column.HeaderText)).Append("\r\n");
        }

        foreach (var row in selected)
        {
            text.AppendJoin('\t', columns.Select(column => IsCellSelected(row.Cells[column.Index]) ? Convert.ToString(row.Cells[column.Index].FormattedValue, CultureInfo.CurrentCulture) : string.Empty)).Append("\r\n");
        }

        return new DataObject(DataFormats.Text, text.ToString());
    }

    /// <summary>Whether a cell is selected: itself, or its row or column.</summary>
    internal bool IsCellSelected(DataGridViewCell cell) =>
        _selectedCells.Contains(cell) || (cell.OwningRow?.Selected ?? false) || (cell.OwningColumn?.Selected ?? false);

    /// <summary>Selects or deselects a cell.</summary>
    internal void SetCellSelected(DataGridViewCell cell, bool selected)
    {
        if (selected ? _selectedCells.Add(cell) : _selectedCells.Remove(cell))
        {
            Invalidate();
            OnCellStateChanged(new DataGridViewCellStateChangedEventArgs(cell, DataGridViewElementStates.Selected));
            OnSelectionChanged(EventArgs.Empty);
        }
    }

    /// <summary>Makes the first cell current when there is none, as a grid does when it gets rows.</summary>
    internal void EnsureCurrentCell()
    {
        if (_currentCell is not null || _initializing > 0)
        {
            return;
        }

        var row = _rows.Items.Find(r => r.Visible);
        var column = _columnOrder.Find(c => c.Visible) ?? DisplayedColumns().FirstOrDefault(c => c.Visible);
        if (row is null || column is null || column.Index >= row.Cells.Count)
        {
            return;
        }

        SetCurrentCellCore(row.Cells[column.Index], raiseEvents: true);
        SelectFromCurrent(_currentCell!, extend: false, toggle: false);
        OnSelectionChanged(EventArgs.Empty);
    }

    /// <summary>Makes a cell current, with the Leave, Validating, Enter and CurrentCellChanged events.</summary>
    internal void SetCurrentCellCore(DataGridViewCell? cell, bool raiseEvents)
    {
        var previous = _currentCell;
        if (raiseEvents && previous is not null && previous.DataGridView == this)
        {
            OnCellLeave(new DataGridViewCellEventArgs(previous.ColumnIndex, previous.RowIndex));
            if (cell?.OwningRow != previous.OwningRow)
            {
                OnRowLeave(new DataGridViewCellEventArgs(previous.ColumnIndex, previous.RowIndex));
                var validating = new DataGridViewCellCancelEventArgs(previous.ColumnIndex, previous.RowIndex);
                OnRowValidating(validating);
                if (validating.Cancel)
                {
                    return;
                }

                OnRowValidated(new DataGridViewCellEventArgs(previous.ColumnIndex, previous.RowIndex));
            }
        }

        _currentCell = cell;
        if (cell is not null)
        {
            ScrollIntoView(cell);
        }

        if (!raiseEvents)
        {
            return;
        }

        if (cell is not null)
        {
            if (cell.OwningRow != previous?.OwningRow)
            {
                OnRowEnter(new DataGridViewCellEventArgs(cell.ColumnIndex, cell.RowIndex));
            }

            OnCellEnter(new DataGridViewCellEventArgs(cell.ColumnIndex, cell.RowIndex));
        }

        OnCurrentCellChanged(EventArgs.Empty);
        SyncManagerPosition();
        if (cell is not null && EditMode == DataGridViewEditMode.EditOnEnter && Focused)
        {
            BeginEdit(selectAll: true);
        }
    }

    /// <summary>Selects from the current cell: the cell, its row or a range from the anchor.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="extend">Whether to select from the anchor to the cell, as Shift does.</param>
    /// <param name="toggle">Whether to add to the selection or remove from it, as Ctrl does.</param>
    /// <param name="wholeRow">Whether the row header was tapped.</param>
    internal void SelectFromCurrent(DataGridViewCell cell, bool extend, bool toggle, bool wholeRow = false)
    {
        var rows = _selectionMode == DataGridViewSelectionMode.FullRowSelect || (wholeRow && _selectionMode == DataGridViewSelectionMode.RowHeaderSelect);
        var columns = _selectionMode == DataGridViewSelectionMode.FullColumnSelect;
        if (!MultiSelect)
        {
            extend = false;
            toggle = false;
        }

        if (!extend && !toggle)
        {
            ClearSelectionCore();
            _anchorCell = cell;
        }

        var anchor = extend && _anchorCell?.DataGridView == this ? _anchorCell : cell;
        if (toggle && !extend)
        {
            if (rows)
            {
                cell.OwningRow!.SetSelectedInternal(!cell.OwningRow.Selected);
            }
            else if (columns)
            {
                cell.OwningColumn!.SetSelectedInternal(!cell.OwningColumn.Selected);
            }
            else if (!_selectedCells.Remove(cell))
            {
                _selectedCells.Add(cell);
            }

            _anchorCell = cell;
            Invalidate();
            return;
        }

        if (extend)
        {
            ClearSelectionCore();
        }

        var top = Math.Min(anchor.RowIndex, cell.RowIndex);
        var bottom = Math.Max(anchor.RowIndex, cell.RowIndex);
        if (rows)
        {
            for (var i = top; i <= bottom; i++)
            {
                _rows[i].SetSelectedInternal(true);
            }
        }
        else if (columns)
        {
            var first = Math.Min(anchor.OwningColumn!.DisplayIndex, cell.OwningColumn!.DisplayIndex);
            var last = Math.Max(anchor.OwningColumn.DisplayIndex, cell.OwningColumn.DisplayIndex);
            foreach (var column in _columnOrder.Where(c => c.DisplayIndex >= first && c.DisplayIndex <= last))
            {
                column.SetSelectedInternal(true);
            }
        }
        else
        {
            var first = Math.Min(anchor.OwningColumn!.DisplayIndex, cell.OwningColumn!.DisplayIndex);
            var last = Math.Max(anchor.OwningColumn.DisplayIndex, cell.OwningColumn.DisplayIndex);
            for (var i = top; i <= bottom; i++)
            {
                foreach (var column in _columnOrder.Where(c => c.Visible && c.DisplayIndex >= first && c.DisplayIndex <= last))
                {
                    _selectedCells.Add(_rows[i].Cells[column.Index]);
                }
            }
        }

        Invalidate();
    }

    private void ClearSelectionCore()
    {
        _selectedCells.Clear();
        foreach (var row in _rows.Items)
        {
            row.SetSelectedInternal(false);
        }

        foreach (var column in _columns.Items)
        {
            column.SetSelectedInternal(false);
        }
    }

    private void SetSorted(DataGridViewColumn? column, SortOrder order)
    {
        if (_sortedColumn is not null && _sortedColumn != column)
        {
            _sortedColumn.HeaderCell.SortGlyphDirection = SortOrder.None;
        }

        _sortedColumn = column;
        _sortOrder = order;
        if (column is not null)
        {
            column.HeaderCell.SortGlyphDirection = order;
        }

        LayoutGrid();
        Invalidate();
        OnSorted(EventArgs.Empty);
    }

    private int CompareCells(DataGridViewColumn column, DataGridViewRow a, DataGridViewRow b, int columnIndex)
    {
        var first = a.Cells[columnIndex].Value;
        var second = b.Cells[columnIndex].Value;
        if (Events[EventSortCompare] is not null)
        {
            var e = new DataGridViewSortCompareEventArgs(column, first, second, a.Index, b.Index);
            OnSortCompare(e);
            if (e.Handled)
            {
                return e.SortResult;
            }
        }

        if (first is null or DBNull)
        {
            return second is null or DBNull ? 0 : -1;
        }

        if (second is null or DBNull)
        {
            return 1;
        }

        if (first is IComparable comparable && first.GetType() == second.GetType())
        {
            return comparable.CompareTo(second);
        }

        return CultureInfo.CurrentCulture.CompareInfo.Compare(
            Convert.ToString(a.Cells[columnIndex].FormattedValue, CultureInfo.CurrentCulture),
            Convert.ToString(b.Cells[columnIndex].FormattedValue, CultureInfo.CurrentCulture),
            CompareOptions.IgnoreCase);
    }
}
