using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace System.Windows.Forms;

/// <summary>A row of a grid: its cells, its height and, in a bound grid, the item of the data source it shows.</summary>
public class DataGridViewRow : DataGridViewBand
{
    private readonly DataGridViewCellCollection _cells;
    private DataGridViewRowHeaderCell _headerCell = new();
    private int _height = -1;
    private int _minimumHeight = 3;

    /// <summary>Creates a row with no cells.</summary>
    public DataGridViewRow()
    {
        _cells = new DataGridViewCellCollection(this);
        _headerCell.OwningRow = this;
    }

    /// <summary>The cells, one for each column.</summary>
    public DataGridViewCellCollection Cells => _cells;

    /// <summary>The item of the data source the row shows, in a bound grid.</summary>
    public object? DataBoundItem { get; internal set; }

    /// <summary>Whether the row is the row for new records, at the bottom of the grid.</summary>
    public bool IsNewRow { get; internal set; }

    /// <summary>The header cell.</summary>
    [AllowNull]
    public DataGridViewRowHeaderCell HeaderCell
    {
        get => _headerCell;
        set
        {
            _headerCell = value ?? new DataGridViewRowHeaderCell();
            _headerCell.OwningRow = this;
        }
    }

    /// <summary>The height.</summary>
    public int Height
    {
        get => _height >= 0 ? _height : DataGridView?.RowTemplate.Height ?? DefaultHeight;
        set
        {
            var height = Math.Max(_minimumHeight, value);
            if (height == _height)
            {
                return;
            }

            _height = height;
            DataGridView?.OnRowHeightChanged(this);
        }
    }

    /// <summary>The least height.</summary>
    public int MinimumHeight
    {
        get => _minimumHeight;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 2);
            _minimumHeight = value;
        }
    }

    /// <summary>The height of the line below the row.</summary>
    public int DividerHeight { get; set; }

    /// <summary>An error shown with an icon in the row's header.</summary>
    [AllowNull]
    public string ErrorText
    {
        get => field ?? string.Empty;
        set
        {
            field = value;
            DataGridView?.Invalidate();
        }
    }

    /// <summary>The row's state.</summary>
    public override DataGridViewElementStates State =>
        (Visible ? DataGridViewElementStates.Visible : DataGridViewElementStates.None)
        | (ReadOnly ? DataGridViewElementStates.ReadOnly : DataGridViewElementStates.None)
        | (Selected ? DataGridViewElementStates.Selected : DataGridViewElementStates.None);

    /// <summary>The height of a row in the default font.</summary>
    internal static int DefaultHeight => Control.DefaultFont.Height + 9;

    /// <summary>Creates the row's cells from a grid's columns.</summary>
    /// <param name="dataGridView">The grid.</param>
    public void CreateCells(DataGridView dataGridView)
    {
        ArgumentNullException.ThrowIfNull(dataGridView);
        _cells.ClearInternal();
        foreach (var column in dataGridView.Columns.Items)
        {
            var cell = (DataGridViewCell)(column.CellTemplate ?? new DataGridViewTextBoxCell()).Clone();
            _cells.AddInternal(cell);
        }
    }

    /// <summary>Creates the row's cells from a grid's columns, with values.</summary>
    /// <param name="dataGridView">The grid.</param>
    /// <param name="values">The values.</param>
    public void CreateCells(DataGridView dataGridView, params object?[] values)
    {
        CreateCells(dataGridView);
        SetValues(values);
    }

    /// <summary>Sets the values of the first cells.</summary>
    /// <param name="values">The values.</param>
    /// <returns>Whether every value had a cell.</returns>
    public bool SetValues(params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        for (var i = 0; i < values.Length && i < _cells.Count; i++)
        {
            _cells[i].Value = values[i];
        }

        return values.Length <= _cells.Count;
    }

    /// <summary>The height the row needs. Rows have the height they are given.</summary>
    /// <param name="rowIndex">The row.</param>
    /// <param name="autoSizeRowMode">How to measure.</param>
    /// <param name="fixedWidth">Whether the widths are fixed.</param>
    public virtual int GetPreferredHeight(int rowIndex, DataGridViewAutoSizeRowMode autoSizeRowMode, bool fixedWidth) => Height;

    /// <inheritdoc />
    public override object Clone()
    {
        var row = (DataGridViewRow)base.Clone();
        var clone = new DataGridViewRow
        {
            _height = _height,
            _minimumHeight = _minimumHeight,
            Tag = Tag,
            DividerHeight = DividerHeight,
        };

        if (HasDefaultCellStyle)
        {
            clone.DefaultCellStyle = DefaultCellStyle.Clone();
        }

        foreach (DataGridViewCell cell in _cells)
        {
            clone._cells.AddInternal((DataGridViewCell)cell.Clone());
        }

        _ = row;
        return clone;
    }

    /// <inheritdoc />
    public override string ToString() => $"DataGridViewRow {{ Index={Index} }}";
}

/// <summary>How a row sizes itself. Rows keep their height.</summary>
public enum DataGridViewAutoSizeRowMode
{
    /// <summary>Its header.</summary>
    RowHeader = 1,

    /// <summary>Every cell except its header.</summary>
    AllCellsExceptHeader = 2,

    /// <summary>Every cell.</summary>
    AllCells = 3,
}

/// <summary>The cells of a row.</summary>
[ListBindable(false)]
public class DataGridViewCellCollection : BaseCollection, IList
{
    private readonly List<DataGridViewCell> _cells = [];
    private readonly DataGridViewRow _owner;

    /// <summary>Creates the cells of a row.</summary>
    /// <param name="dataGridViewRow">The row.</param>
    public DataGridViewCellCollection(DataGridViewRow dataGridViewRow) => _owner = dataGridViewRow;

    /// <inheritdoc />
    public override int Count => _cells.Count;

    /// <inheritdoc />
    protected override ArrayList List => [.. _cells];

    /// <inheritdoc />
    bool IList.IsFixedSize => false;

    /// <inheritdoc />
    bool IList.IsReadOnly => false;

    /// <summary>The cell at an index.</summary>
    /// <param name="index">The index.</param>
    public DataGridViewCell this[int index]
    {
        get => _cells[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.OwningRow = _owner;
            value.OwningColumn = _cells[index].OwningColumn;
            value.DataGridView = _owner.DataGridView;
            _cells[index] = value;
        }
    }

    /// <summary>The cell of a column, by the column's name.</summary>
    /// <param name="columnName">The name.</param>
    public DataGridViewCell this[string columnName]
    {
        get
        {
            var index = _owner.DataGridView?.Columns[columnName]?.Index ?? -1;
            if (index < 0 || index >= _cells.Count)
            {
                throw new ArgumentException($"Column named {columnName} cannot be found.", nameof(columnName));
            }

            return _cells[index];
        }

        set
        {
            var index = _owner.DataGridView?.Columns[columnName]?.Index ?? -1;
            if (index < 0 || index >= _cells.Count)
            {
                throw new ArgumentException($"Column named {columnName} cannot be found.", nameof(columnName));
            }

            this[index] = value;
        }
    }

    /// <inheritdoc />
    object? IList.this[int index]
    {
        get => this[index];
        set => this[index] = (DataGridViewCell)value!;
    }

    /// <summary>Adds a cell, to a row not yet in a grid.</summary>
    /// <param name="dataGridViewCell">The cell.</param>
    /// <returns>Its index.</returns>
    public virtual int Add(DataGridViewCell dataGridViewCell)
    {
        if (_owner.DataGridView is not null)
        {
            throw new InvalidOperationException("Cells cannot be added to a row that belongs to a DataGridView.");
        }

        AddInternal(dataGridViewCell);
        return _cells.Count - 1;
    }

    /// <summary>Adds cells, to a row not yet in a grid.</summary>
    /// <param name="dataGridViewCells">The cells.</param>
    public virtual void AddRange(params DataGridViewCell[] dataGridViewCells)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCells);
        foreach (var cell in dataGridViewCells)
        {
            Add(cell);
        }
    }

    /// <summary>Whether a cell is in the row.</summary>
    /// <param name="dataGridViewCell">The cell.</param>
    public virtual bool Contains(DataGridViewCell dataGridViewCell) => _cells.Contains(dataGridViewCell);

    /// <summary>The index of a cell, or -1.</summary>
    /// <param name="dataGridViewCell">The cell.</param>
    public int IndexOf(DataGridViewCell dataGridViewCell) => _cells.IndexOf(dataGridViewCell);

    /// <summary>Copies the cells into an array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="index">Where to start.</param>
    public void CopyTo(DataGridViewCell[] array, int index) => _cells.CopyTo(array, index);

    /// <summary>Adds a cell, from the grid.</summary>
    internal void AddInternal(DataGridViewCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        cell.OwningRow = _owner;
        cell.DataGridView = _owner.DataGridView;
        _cells.Add(cell);
    }

    /// <summary>Inserts a cell, from the grid.</summary>
    internal void InsertInternal(int index, DataGridViewCell cell)
    {
        cell.OwningRow = _owner;
        cell.DataGridView = _owner.DataGridView;
        _cells.Insert(Math.Clamp(index, 0, _cells.Count), cell);
    }

    /// <summary>Removes a cell, from the grid.</summary>
    internal void RemoveAtInternal(int index)
    {
        if (index >= 0 && index < _cells.Count)
        {
            _cells.RemoveAt(index);
        }
    }

    /// <summary>Removes every cell, from the grid.</summary>
    internal void ClearInternal() => _cells.Clear();

    /// <summary>The cells.</summary>
    internal List<DataGridViewCell> Items => _cells;

    /// <inheritdoc />
    int IList.Add(object? value) => Add((DataGridViewCell)value!);

    /// <inheritdoc />
    void IList.Clear() => throw new NotSupportedException("The cells of a row are its columns' cells.");

    /// <inheritdoc />
    bool IList.Contains(object? value) => value is DataGridViewCell cell && Contains(cell);

    /// <inheritdoc />
    int IList.IndexOf(object? value) => value is DataGridViewCell cell ? IndexOf(cell) : -1;

    /// <inheritdoc />
    void IList.Insert(int index, object? value) => throw new NotSupportedException("The cells of a row are its columns' cells.");

    /// <inheritdoc />
    void IList.Remove(object? value) => throw new NotSupportedException("The cells of a row are its columns' cells.");

    /// <inheritdoc />
    void IList.RemoveAt(int index) => throw new NotSupportedException("The cells of a row are its columns' cells.");
}

/// <summary>The rows of a grid.</summary>
[ListBindable(false)]
public class DataGridViewRowCollection : IList
{
    private readonly List<DataGridViewRow> _rows = [];

    /// <summary>Creates the rows of a grid.</summary>
    /// <param name="dataGridView">The grid.</param>
    public DataGridViewRowCollection(DataGridView dataGridView) => DataGridView = dataGridView;

    /// <summary>The number of rows, with the row for new records.</summary>
    public int Count => _rows.Count;

    /// <inheritdoc />
    bool IList.IsFixedSize => false;

    /// <inheritdoc />
    bool IList.IsReadOnly => false;

    /// <inheritdoc />
    bool ICollection.IsSynchronized => false;

    /// <inheritdoc />
    object ICollection.SyncRoot => this;

    /// <summary>The grid.</summary>
    protected DataGridView DataGridView { get; }

    /// <summary>The rows, in order.</summary>
    internal List<DataGridViewRow> Items => _rows;

    /// <summary>The row at an index.</summary>
    /// <param name="index">The index.</param>
    public DataGridViewRow this[int index] => _rows[index];

    /// <inheritdoc />
    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException("Rows are added with Add or Insert.");
    }

    /// <summary>Adds a row of empty cells.</summary>
    /// <returns>Its index.</returns>
    public virtual int Add() => Add([]);

    /// <summary>Adds rows of empty cells.</summary>
    /// <param name="count">The number of rows.</param>
    /// <returns>The index of the last.</returns>
    public virtual int Add(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var index = -1;
        for (var i = 0; i < count; i++)
        {
            index = Add();
        }

        return index;
    }

    /// <summary>Adds a row with values.</summary>
    /// <param name="values">The values, one for each column.</param>
    /// <returns>Its index.</returns>
    public virtual int Add(params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var row = (DataGridViewRow)DataGridView.RowTemplate.Clone();
        Insert(DataGridView.NewRowIndex >= 0 ? DataGridView.NewRowIndex : _rows.Count, row);
        row.SetValues(values);
        return row.Index;
    }

    /// <summary>Adds a row.</summary>
    /// <param name="dataGridViewRow">The row.</param>
    /// <returns>Its index.</returns>
    public virtual int Add(DataGridViewRow dataGridViewRow)
    {
        Insert(DataGridView.NewRowIndex >= 0 ? DataGridView.NewRowIndex : _rows.Count, dataGridViewRow);
        return dataGridViewRow.Index;
    }

    /// <summary>Adds rows.</summary>
    /// <param name="dataGridViewRows">The rows.</param>
    public virtual void AddRange(params DataGridViewRow[] dataGridViewRows)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewRows);
        foreach (var row in dataGridViewRows)
        {
            Add(row);
        }
    }

    /// <summary>Adds a copy of a row.</summary>
    /// <param name="indexSource">The row to copy.</param>
    /// <returns>The new row's index.</returns>
    public virtual int AddCopy(int indexSource) => Add((DataGridViewRow)_rows[indexSource].Clone());

    /// <summary>Inserts a row with values.</summary>
    /// <param name="rowIndex">Where.</param>
    /// <param name="values">The values.</param>
    public virtual void Insert(int rowIndex, params object?[] values)
    {
        var row = (DataGridViewRow)DataGridView.RowTemplate.Clone();
        Insert(rowIndex, row);
        row.SetValues(values);
    }

    /// <summary>Inserts a row.</summary>
    /// <param name="rowIndex">Where.</param>
    /// <param name="dataGridViewRow">The row.</param>
    public virtual void Insert(int rowIndex, DataGridViewRow dataGridViewRow)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewRow);
        DataGridView.ThrowIfBoundForRowChange();
        if (dataGridViewRow.DataGridView is not null)
        {
            throw new InvalidOperationException("Row provided already belongs to a DataGridView control.");
        }

        if (DataGridView.Columns.Count == 0)
        {
            throw new InvalidOperationException("No row can be added to a DataGridView control that does not have columns. Columns must be added first.");
        }

        InsertInternal(rowIndex, dataGridViewRow);
        DataGridView.OnRowsAddedInternal(dataGridViewRow.Index, 1);
    }

    /// <summary>Removes a row.</summary>
    /// <param name="dataGridViewRow">The row.</param>
    public virtual void Remove(DataGridViewRow dataGridViewRow)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewRow);
        var index = _rows.IndexOf(dataGridViewRow);
        if (index >= 0)
        {
            RemoveAt(index);
        }
    }

    /// <summary>Removes the row at an index.</summary>
    /// <param name="index">The index.</param>
    public virtual void RemoveAt(int index)
    {
        var row = _rows[index];
        if (row.IsNewRow)
        {
            throw new InvalidOperationException("Uncommitted new row cannot be deleted.");
        }

        if (DataGridView.RemoveBoundRow(row))
        {
            return;
        }

        RemoveInternal(index);
        DataGridView.OnRowsRemovedInternal(index, 1);
    }

    /// <summary>Removes every row.</summary>
    public virtual void Clear()
    {
        DataGridView.ThrowIfBoundForRowChange();
        var count = _rows.Count(row => !row.IsNewRow);
        _rows.RemoveAll(row => !row.IsNewRow);
        Reindex();
        if (count > 0)
        {
            DataGridView.OnRowsRemovedInternal(0, count);
        }
    }

    /// <summary>Whether a row is in the grid.</summary>
    /// <param name="dataGridViewRow">The row.</param>
    public virtual bool Contains(DataGridViewRow dataGridViewRow) => _rows.Contains(dataGridViewRow);

    /// <summary>The index of a row, or -1.</summary>
    /// <param name="dataGridViewRow">The row.</param>
    public int IndexOf(DataGridViewRow dataGridViewRow) => _rows.IndexOf(dataGridViewRow);

    /// <summary>The number of rows in a state.</summary>
    /// <param name="includeFilter">The state.</param>
    public int GetRowCount(DataGridViewElementStates includeFilter) => _rows.Count(row => Matches(row, includeFilter));

    /// <summary>The total height of the rows in a state.</summary>
    /// <param name="includeFilter">The state.</param>
    public int GetRowsHeight(DataGridViewElementStates includeFilter) => _rows.Where(row => Matches(row, includeFilter)).Sum(row => row.Height);

    /// <summary>The index of the first row in a state, or -1.</summary>
    /// <param name="includeFilter">The state.</param>
    public int GetFirstRow(DataGridViewElementStates includeFilter) => _rows.FindIndex(row => Matches(row, includeFilter));

    /// <summary>The index of the last row in a state, or -1.</summary>
    /// <param name="includeFilter">The state.</param>
    public int GetLastRow(DataGridViewElementStates includeFilter) => _rows.FindLastIndex(row => Matches(row, includeFilter));

    /// <summary>The index of the next row in a state after a row, or -1.</summary>
    /// <param name="indexStart">The row.</param>
    /// <param name="includeFilter">The state.</param>
    public int GetNextRow(int indexStart, DataGridViewElementStates includeFilter)
    {
        for (var i = indexStart + 1; i < _rows.Count; i++)
        {
            if (Matches(_rows[i], includeFilter))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The index of the previous row in a state before a row, or -1.</summary>
    /// <param name="indexStart">The row.</param>
    /// <param name="includeFilter">The state.</param>
    public int GetPreviousRow(int indexStart, DataGridViewElementStates includeFilter)
    {
        for (var i = indexStart - 1; i >= 0; i--)
        {
            if (Matches(_rows[i], includeFilter))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The state of a row.</summary>
    /// <param name="rowIndex">The row.</param>
    public virtual DataGridViewElementStates GetRowState(int rowIndex) => _rows[rowIndex].State;

    /// <summary>A row, which grids of Windows Forms share between rows; here every row is its own.</summary>
    /// <param name="rowIndex">The row.</param>
    public DataGridViewRow SharedRow(int rowIndex) => _rows[rowIndex];

    /// <summary>Copies the rows into an array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="index">Where to start.</param>
    public void CopyTo(DataGridViewRow[] array, int index) => _rows.CopyTo(array, index);

    /// <inheritdoc />
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_rows).CopyTo(array, index);

    /// <inheritdoc />
    public IEnumerator GetEnumerator() => _rows.ToArray().GetEnumerator();

    /// <summary>Inserts a row without the bound check or events.</summary>
    internal void InsertInternal(int rowIndex, DataGridViewRow row)
    {
        if (row.Cells.Count == 0)
        {
            row.CreateCells(DataGridView);
        }

        row.DataGridView = DataGridView;
        _rows.Insert(Math.Clamp(rowIndex, 0, _rows.Count), row);
        DataGridView.AttachRow(row);
        Reindex();
    }

    /// <summary>Removes a row without events.</summary>
    internal void RemoveInternal(int index)
    {
        var row = _rows[index];
        _rows.RemoveAt(index);
        row.DataGridView = null;
        row.Index = -1;
        Reindex();
    }

    /// <summary>Removes every row, the row for new records too, without events.</summary>
    internal void ClearInternal()
    {
        foreach (var row in _rows)
        {
            row.DataGridView = null;
            row.Index = -1;
        }

        _rows.Clear();
    }

    /// <summary>Sorts the rows other than the row for new records.</summary>
    internal void SortInternal(Comparison<DataGridViewRow> comparison)
    {
        var newRow = _rows.Find(row => row.IsNewRow);
        _rows.RemoveAll(row => row.IsNewRow);
        var sorted = _rows.Select((row, index) => (row, index)).ToList();

        // A stable sort: rows that compare equal keep their order.
        sorted.Sort((a, b) =>
        {
            var result = comparison(a.row, b.row);
            return result != 0 ? result : a.index.CompareTo(b.index);
        });
        _rows.Clear();
        _rows.AddRange(sorted.Select(entry => entry.row));
        if (newRow is not null)
        {
            _rows.Add(newRow);
        }

        Reindex();
    }

    /// <inheritdoc />
    int IList.Add(object? value) => Add((DataGridViewRow)value!);

    /// <inheritdoc />
    void IList.Clear() => Clear();

    /// <inheritdoc />
    bool IList.Contains(object? value) => value is DataGridViewRow row && Contains(row);

    /// <inheritdoc />
    int IList.IndexOf(object? value) => value is DataGridViewRow row ? IndexOf(row) : -1;

    /// <inheritdoc />
    void IList.Insert(int index, object? value) => Insert(index, (DataGridViewRow)value!);

    /// <inheritdoc />
    void IList.Remove(object? value)
    {
        if (value is DataGridViewRow row)
        {
            Remove(row);
        }
    }

    /// <inheritdoc />
    void IList.RemoveAt(int index) => RemoveAt(index);

    private static bool Matches(DataGridViewRow row, DataGridViewElementStates filter) =>
        ((filter & DataGridViewElementStates.Visible) == 0 || row.Visible)
        && ((filter & DataGridViewElementStates.Selected) == 0 || row.Selected)
        && ((filter & DataGridViewElementStates.ReadOnly) == 0 || row.ReadOnly);

    private void Reindex()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Index = i;
        }
    }
}

/// <summary>Selected rows, cells or columns of a grid, as a read-only list.</summary>
/// <typeparam name="T">The kind of element.</typeparam>
public abstract class DataGridViewSelectedCollection<T> : BaseCollection, IList
{
    private readonly List<T> _items;

    private protected DataGridViewSelectedCollection(IEnumerable<T> items) => _items = [.. items];

    /// <inheritdoc />
    public override int Count => _items.Count;

    /// <inheritdoc />
    protected override ArrayList List => [.. _items];

    /// <inheritdoc />
    bool IList.IsFixedSize => true;

    /// <inheritdoc />
    bool IList.IsReadOnly => true;

    /// <summary>The element at an index.</summary>
    /// <param name="index">The index.</param>
    public T this[int index] => _items[index];

    /// <inheritdoc />
    object? IList.this[int index]
    {
        get => _items[index];
        set => throw new NotSupportedException("The collection is read-only.");
    }

    /// <summary>Whether an element is selected.</summary>
    /// <param name="item">The element.</param>
    public bool Contains(T item) => _items.Contains(item);

    /// <summary>Copies the elements into an array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="index">Where to start.</param>
    public void CopyTo(T[] array, int index) => _items.CopyTo(array, index);

    /// <inheritdoc />
    int IList.Add(object? value) => throw new NotSupportedException("The collection is read-only.");

    /// <inheritdoc />
    void IList.Clear() => throw new NotSupportedException("The collection is read-only.");

    /// <inheritdoc />
    bool IList.Contains(object? value) => value is T item && Contains(item);

    /// <inheritdoc />
    int IList.IndexOf(object? value) => value is T item ? _items.IndexOf(item) : -1;

    /// <inheritdoc />
    void IList.Insert(int index, object? value) => throw new NotSupportedException("The collection is read-only.");

    /// <inheritdoc />
    void IList.Remove(object? value) => throw new NotSupportedException("The collection is read-only.");

    /// <inheritdoc />
    void IList.RemoveAt(int index) => throw new NotSupportedException("The collection is read-only.");
}

/// <summary>The selected rows of a grid.</summary>
public class DataGridViewSelectedRowCollection : DataGridViewSelectedCollection<DataGridViewRow>
{
    internal DataGridViewSelectedRowCollection(IEnumerable<DataGridViewRow> rows)
        : base(rows)
    {
    }
}

/// <summary>The selected cells of a grid.</summary>
public class DataGridViewSelectedCellCollection : DataGridViewSelectedCollection<DataGridViewCell>
{
    internal DataGridViewSelectedCellCollection(IEnumerable<DataGridViewCell> cells)
        : base(cells)
    {
    }
}

/// <summary>The selected columns of a grid.</summary>
public class DataGridViewSelectedColumnCollection : DataGridViewSelectedCollection<DataGridViewColumn>
{
    internal DataGridViewSelectedColumnCollection(IEnumerable<DataGridViewColumn> columns)
        : base(columns)
    {
    }
}
