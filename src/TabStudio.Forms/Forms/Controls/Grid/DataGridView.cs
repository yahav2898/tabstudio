using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>
/// A grid of rows and columns of cells, filled by code or bound to a list, a binding list or a data table, with
/// headers, sorting, selection and editing in place. A finger drags the grid to scroll it, a tap selects a cell,
/// and a tap on the current cell edits it.
/// </summary>
[DefaultEvent(nameof(CellContentClick))]
public partial class DataGridView : Control, ISupportInitialize
{
    private readonly DataGridViewColumnCollection _columns;
    private readonly DataGridViewRowCollection _rows;
    private DataGridViewCellStyle _defaultCellStyle;
    private DataGridViewCellStyle _columnHeadersDefaultCellStyle;
    private DataGridViewCellStyle _rowHeadersDefaultCellStyle;
    private DataGridViewCellStyle _alternatingRowsDefaultCellStyle = new();
    private DataGridViewCellStyle _rowsDefaultCellStyle = new();
    private DataGridViewRow _rowTemplate = new();
    private DataGridViewHeaderCell _topLeftHeaderCell = new DataGridViewTopLeftHeaderCell();
    private BorderStyle _borderStyle = BorderStyle.FixedSingle;
    private Color _backgroundColor = SystemColors.AppWorkspace;
    private Color _gridColor = SystemColors.ControlDark;
    private bool _readOnly;
    private bool _allowUserToAddRows = true;
    private bool _columnHeadersVisible = true;
    private bool _rowHeadersVisible = true;
    private int _columnHeadersHeight = -1;
    private int _rowHeadersWidth = 41;
    private DataGridViewAutoSizeColumnsMode _autoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
    private DataGridViewSelectionMode _selectionMode = DataGridViewSelectionMode.RowHeaderSelect;
    private int _initializing;

    /// <summary>Creates a grid.</summary>
    public DataGridView()
    {
        _columns = CreateColumnsInstance();
        _rows = CreateRowsInstance();
        _defaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            BackColor = SystemColors.Window,
            ForeColor = SystemColors.ControlText,
            SelectionBackColor = SystemColors.Highlight,
            SelectionForeColor = SystemColors.HighlightText,
            WrapMode = DataGridViewTriState.False,
        };
        _columnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            BackColor = SystemColors.Control,
            ForeColor = SystemColors.WindowText,
            SelectionBackColor = SystemColors.Highlight,
            SelectionForeColor = SystemColors.HighlightText,
            WrapMode = DataGridViewTriState.True,
        };
        _rowHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            BackColor = SystemColors.Control,
            ForeColor = SystemColors.WindowText,
            SelectionBackColor = SystemColors.Highlight,
            SelectionForeColor = SystemColors.HighlightText,
            WrapMode = DataGridViewTriState.True,
        };
        _rowTemplate.DataGridView = null;
        _topLeftHeaderCell.DataGridView = this;
        SetStyle(ControlStyles.StandardDoubleClick, true);
        UpdateNewRow();
    }

    /// <summary>The columns.</summary>
    public DataGridViewColumnCollection Columns => _columns;

    /// <summary>The rows, with the row for new records at the bottom when users can add rows.</summary>
    public DataGridViewRowCollection Rows => _rows;

    /// <summary>The number of columns; setting it adds or removes text columns.</summary>
    public int ColumnCount
    {
        get => _columns.Count;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            while (_columns.Count > value)
            {
                _columns.RemoveAt(_columns.Count - 1);
            }

            while (_columns.Count < value)
            {
                _columns.Add(new DataGridViewTextBoxColumn());
            }
        }
    }

    /// <summary>The number of rows, with the row for new records; setting it adds or removes rows.</summary>
    public int RowCount
    {
        get => _rows.Count;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (_columns.Count == 0 && value > 0)
            {
                ColumnCount = 1;
            }

            var target = value;
            while (_rows.Count > target && _rows.Count > (NewRowIndex >= 0 ? 1 : 0))
            {
                var last = NewRowIndex >= 0 ? _rows.Count - 2 : _rows.Count - 1;
                _rows.RemoveAt(last);
            }

            while (_rows.Count < target)
            {
                _rows.Add();
            }
        }
    }

    /// <summary>The style of the cells.</summary>
    [AllowNull]
    public DataGridViewCellStyle DefaultCellStyle
    {
        get => _defaultCellStyle;
        set
        {
            _defaultCellStyle = value ?? new DataGridViewCellStyle();
            Invalidate();
        }
    }

    /// <summary>The style of the column headers.</summary>
    [AllowNull]
    public DataGridViewCellStyle ColumnHeadersDefaultCellStyle
    {
        get => _columnHeadersDefaultCellStyle;
        set
        {
            _columnHeadersDefaultCellStyle = value ?? new DataGridViewCellStyle();
            Invalidate();
        }
    }

    /// <summary>The style of the row headers.</summary>
    [AllowNull]
    public DataGridViewCellStyle RowHeadersDefaultCellStyle
    {
        get => _rowHeadersDefaultCellStyle;
        set
        {
            _rowHeadersDefaultCellStyle = value ?? new DataGridViewCellStyle();
            Invalidate();
        }
    }

    /// <summary>The style of every other row.</summary>
    [AllowNull]
    public DataGridViewCellStyle AlternatingRowsDefaultCellStyle
    {
        get => _alternatingRowsDefaultCellStyle;
        set
        {
            _alternatingRowsDefaultCellStyle = value ?? new DataGridViewCellStyle();
            Invalidate();
        }
    }

    /// <summary>The style of the rows.</summary>
    [AllowNull]
    public DataGridViewCellStyle RowsDefaultCellStyle
    {
        get => _rowsDefaultCellStyle;
        set
        {
            _rowsDefaultCellStyle = value ?? new DataGridViewCellStyle();
            Invalidate();
        }
    }

    /// <summary>The row new rows copy, with its height.</summary>
    [AllowNull]
    public DataGridViewRow RowTemplate
    {
        get => _rowTemplate;
        set => _rowTemplate = value ?? new DataGridViewRow();
    }

    /// <summary>The header cell at the top left.</summary>
    [AllowNull]
    public DataGridViewHeaderCell TopLeftHeaderCell
    {
        get => _topLeftHeaderCell;
        set
        {
            _topLeftHeaderCell = value ?? new DataGridViewTopLeftHeaderCell();
            _topLeftHeaderCell.DataGridView = this;
            Invalidate();
        }
    }

    /// <summary>The border.</summary>
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            UpdateClientSize();
            PerformLayout();
            Invalidate();
        }
    }

    /// <summary>The color of the area with no cells.</summary>
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            Invalidate();
        }
    }

    /// <summary>The color of the lines between cells.</summary>
    public Color GridColor
    {
        get => _gridColor;
        set
        {
            _gridColor = value;
            Invalidate();
        }
    }

    /// <summary>The lines between cells.</summary>
    public DataGridViewCellBorderStyle CellBorderStyle
    {
        get => field;
        set
        {
            field = value;
            Invalidate();
        }
    } = DataGridViewCellBorderStyle.Single;

    /// <summary>The border of the column headers.</summary>
    public DataGridViewHeaderBorderStyle ColumnHeadersBorderStyle { get; set; } = DataGridViewHeaderBorderStyle.Raised;

    /// <summary>The border of the row headers.</summary>
    public DataGridViewHeaderBorderStyle RowHeadersBorderStyle { get; set; } = DataGridViewHeaderBorderStyle.Raised;

    /// <summary>Whether the headers have the visual style's look.</summary>
    public bool EnableHeadersVisualStyles { get; set; } = true;

    /// <summary>Whether no cell can be edited.</summary>
    public bool ReadOnly
    {
        get => _readOnly;
        set
        {
            _readOnly = value;
            if (value)
            {
                EndEdit();
            }

            Invalidate();
        }
    }

    /// <summary>Whether users can add rows, in the row for new records.</summary>
    public bool AllowUserToAddRows
    {
        get => _allowUserToAddRows;
        set
        {
            _allowUserToAddRows = value;
            UpdateNewRow();
        }
    }

    /// <summary>Whether users can delete selected rows with the Delete key.</summary>
    public bool AllowUserToDeleteRows { get; set; } = true;

    /// <summary>Whether users can reorder columns. Kept for compatibility.</summary>
    public bool AllowUserToOrderColumns { get; set; }

    /// <summary>Whether users can resize columns by dragging their headers' edges.</summary>
    public bool AllowUserToResizeColumns { get; set; } = true;

    /// <summary>Whether users can resize rows. Kept for compatibility.</summary>
    public bool AllowUserToResizeRows { get; set; } = true;

    /// <summary>Whether binding to a data source creates columns for its properties.</summary>
    public bool AutoGenerateColumns { get; set; } = true;

    /// <summary>How the columns are sized.</summary>
    public DataGridViewAutoSizeColumnsMode AutoSizeColumnsMode
    {
        get => _autoSizeColumnsMode;
        set
        {
            _autoSizeColumnsMode = value;
            OnColumnLayoutChanged();
        }
    }

    /// <summary>How the rows are sized. Rows keep their height.</summary>
    public DataGridViewAutoSizeRowsMode AutoSizeRowsMode { get; set; }

    /// <summary>Whether the column headers are shown.</summary>
    public bool ColumnHeadersVisible
    {
        get => _columnHeadersVisible;
        set
        {
            _columnHeadersVisible = value;
            OnColumnLayoutChanged();
        }
    }

    /// <summary>The height of the column headers.</summary>
    public int ColumnHeadersHeight
    {
        get => _columnHeadersHeight >= 0 ? _columnHeadersHeight : Font.Height + 13;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 4);
            _columnHeadersHeight = value;
            OnColumnLayoutChanged();
        }
    }

    /// <summary>How the column headers' height is set.</summary>
    public DataGridViewColumnHeadersHeightSizeMode ColumnHeadersHeightSizeMode { get; set; } = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;

    /// <summary>Whether the row headers are shown.</summary>
    public bool RowHeadersVisible
    {
        get => _rowHeadersVisible;
        set
        {
            _rowHeadersVisible = value;
            OnColumnLayoutChanged();
        }
    }

    /// <summary>The width of the row headers.</summary>
    public int RowHeadersWidth
    {
        get => _rowHeadersWidth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 4);
            _rowHeadersWidth = value;
            OnColumnLayoutChanged();
        }
    }

    /// <summary>How the row headers' width is set.</summary>
    public DataGridViewRowHeadersWidthSizeMode RowHeadersWidthSizeMode { get; set; } = DataGridViewRowHeadersWidthSizeMode.EnableResizing;

    /// <summary>How the grid selects.</summary>
    public DataGridViewSelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            _selectionMode = value;
            ClearSelection();
            if (CurrentCell is { } current)
            {
                SelectFromCurrent(current, extend: false, toggle: false);
            }
        }
    }

    /// <summary>Whether several cells or rows can be selected.</summary>
    public bool MultiSelect { get; set; } = true;

    /// <summary>When editing a cell starts.</summary>
    public DataGridViewEditMode EditMode { get; set; } = DataGridViewEditMode.EditOnKeystrokeOrF2;

    /// <summary>Which scroll bars the grid shows when its content does not fit.</summary>
    public ScrollBars ScrollBars
    {
        get => field;
        set
        {
            field = value;
            PerformLayout();
        }
    } = ScrollBars.Both;

    /// <summary>What Ctrl+C copies.</summary>
    public DataGridViewClipboardCopyMode ClipboardCopyMode { get; set; } = DataGridViewClipboardCopyMode.EnableWithAutoHeaderText;

    /// <summary>Whether Tab moves focus out of the grid rather than between its cells.</summary>
    public bool StandardTab { get; set; }

    /// <summary>Whether cells show their error icons.</summary>
    public bool ShowCellErrors { get; set; } = true;

    /// <summary>Whether rows show their error icons.</summary>
    public bool ShowRowErrors { get; set; } = true;

    /// <summary>Whether cells show their tooltips.</summary>
    public bool ShowCellToolTips { get; set; } = true;

    /// <summary>Whether the header of the row being edited shows a pencil.</summary>
    public bool ShowEditingIcon { get; set; } = true;

    /// <summary>Whether the app supplies the values, in CellValueNeeded and CellValuePushed.</summary>
    public bool VirtualMode { get; set; }

    /// <summary>The index of the row for new records, or -1.</summary>
    public int NewRowIndex => _rows.Items.FindIndex(row => row.IsNewRow);

    /// <inheritdoc />
    protected override Size DefaultSize => new(240, 150);

    /// <inheritdoc />
    internal override Padding NonClientInsets => new(ThemePainter.BorderWidth(_borderStyle));

    /// <inheritdoc />
    public void BeginInit() => _initializing++;

    /// <inheritdoc />
    public void EndInit()
    {
        if (_initializing > 0 && --_initializing == 0)
        {
            UpdateNewRow();
            OnColumnLayoutChanged();
        }
    }

    /// <summary>Creates the columns collection.</summary>
    protected virtual DataGridViewColumnCollection CreateColumnsInstance() => new(this);

    /// <summary>Creates the rows collection.</summary>
    protected virtual DataGridViewRowCollection CreateRowsInstance() => new(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DetachDataSource();
            _textEditor?.Dispose();
            _comboEditor?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>The style a cell is drawn with: the grid's, the column's, the rows', the alternating rows', the row's and the cell's own.</summary>
    internal DataGridViewCellStyle InheritedStyleOf(DataGridViewCell cell)
    {
        var style = new DataGridViewCellStyle(_defaultCellStyle);
        if (style.Font is null)
        {
            style.Font = Font;
        }

        if (cell is DataGridViewHeaderCell)
        {
            style.ApplyStyle(cell is DataGridViewRowHeaderCell ? _rowHeadersDefaultCellStyle : _columnHeadersDefaultCellStyle);
        }
        else
        {
            if (cell.OwningColumn is { HasDefaultCellStyle: true } column)
            {
                style.ApplyStyle(column.DefaultCellStyle);
            }

            style.ApplyStyle(_rowsDefaultCellStyle);
            if (cell.RowIndex % 2 == 1)
            {
                style.ApplyStyle(_alternatingRowsDefaultCellStyle);
            }

            if (cell.OwningRow is { HasDefaultCellStyle: true } row)
            {
                style.ApplyStyle(row.DefaultCellStyle);
            }
        }

        if (cell.HasStyle)
        {
            style.ApplyStyle(cell.Style);
        }

        return style;
    }

    /// <summary>Called when a row or column is shown or hidden.</summary>
    internal void OnBandVisibilityChanged() => OnColumnLayoutChanged();

    /// <summary>Called when the columns change size or order, or the headers change.</summary>
    internal void OnColumnLayoutChanged()
    {
        if (_initializing > 0)
        {
            return;
        }

        LayoutGrid();
        Invalidate();
    }

    /// <summary>Called when a column's width changes.</summary>
    internal void OnColumnWidthChanged(DataGridViewColumn column)
    {
        OnColumnLayoutChanged();
        OnColumnWidthChanged(new DataGridViewColumnEventArgs(column));
    }

    /// <summary>Called when a row's height changes.</summary>
    internal void OnRowHeightChanged(DataGridViewRow row)
    {
        LayoutGrid();
        Invalidate();
    }

    /// <summary>Called when a column is added.</summary>
    internal void OnColumnAdded(DataGridViewColumn column)
    {
        foreach (var row in _rows.Items)
        {
            var cell = (DataGridViewCell)(column.CellTemplate ?? new DataGridViewTextBoxCell()).Clone();
            row.Cells.InsertInternal(column.Index, cell);
            AttachRow(row);
        }

        UpdateNewRow();
        OnColumnLayoutChanged();
        OnColumnAdded(new DataGridViewColumnEventArgs(column));
    }

    /// <summary>Called when a column is removed.</summary>
    internal void OnColumnRemoved(DataGridViewColumn column, int index)
    {
        if (_currentCell?.OwningColumn == column)
        {
            CancelEdit();
            _currentCell = null;
        }

        foreach (var row in _rows.Items)
        {
            _selectedCells.Remove(row.Cells.Items.ElementAtOrDefault(index)!);
            row.Cells.RemoveAtInternal(index);
            AttachRow(row);
        }

        if (_columns.Count == 0)
        {
            _rows.ClearInternal();
        }

        OnColumnLayoutChanged();
        OnColumnRemoved(new DataGridViewColumnEventArgs(column));
    }

    /// <summary>Links a row's cells to their columns and to the grid.</summary>
    internal void AttachRow(DataGridViewRow row)
    {
        row.DataGridView = this;
        row.HeaderCell.DataGridView = this;
        for (var i = 0; i < row.Cells.Count; i++)
        {
            var cell = row.Cells.Items[i];
            cell.DataGridView = this;
            cell.OwningRow = row;
            cell.OwningColumn = i < _columns.Count ? _columns[i] : null;
        }
    }

    /// <summary>Called when rows were added.</summary>
    internal void OnRowsAddedInternal(int rowIndex, int count)
    {
        LayoutGrid();
        Invalidate();
        OnRowsAdded(new DataGridViewRowsAddedEventArgs(rowIndex, count));
        EnsureCurrentCell();
    }

    /// <summary>Called when rows were removed.</summary>
    internal void OnRowsRemovedInternal(int rowIndex, int count)
    {
        _selectedCells.RemoveWhere(cell => cell.OwningRow?.DataGridView != this);
        if (_currentCell?.OwningRow?.DataGridView != this)
        {
            CancelEdit();
            _currentCell = null;
            EnsureCurrentCell();
        }

        LayoutGrid();
        Invalidate();
        OnRowsRemoved(new DataGridViewRowsRemovedEventArgs(rowIndex, count));
    }

    /// <summary>Called when a row or column was selected or deselected.</summary>
    internal void OnBandSelectionChanged(DataGridViewBand band)
    {
        Invalidate();
        OnSelectionChanged(EventArgs.Empty);
    }

    /// <summary>The columns in display order.</summary>
    internal IEnumerable<DataGridViewColumn> DisplayedColumns() =>
        _columns.Items.OrderBy(column => column.DisplayIndex).ThenBy(column => column.Index);

    /// <summary>Adds or removes the row for new records, as the settings and the data source allow.</summary>
    private void UpdateNewRow()
    {
        if (_initializing > 0)
        {
            return;
        }

        var wanted = _allowUserToAddRows && _columns.Count > 0 && !_readOnly && CanAddToSource;
        var index = NewRowIndex;
        if (wanted && index < 0)
        {
            var row = (DataGridViewRow)_rowTemplate.Clone();
            row.IsNewRow = true;
            _rows.InsertInternal(_rows.Count, row);
            LayoutGrid();
            Invalidate();
            EnsureCurrentCell();
        }
        else if (!wanted && index >= 0)
        {
            if (_currentCell?.RowIndex == index)
            {
                CancelEdit();
                _currentCell = null;
            }

            _rows.RemoveInternal(index);
            LayoutGrid();
            Invalidate();
            EnsureCurrentCell();
        }
    }

    /// <summary>The information about a point of a grid: the cell, header or scroll bar there.</summary>
    public sealed class HitTestInfo
    {
        /// <summary>No part of the grid.</summary>
        public static readonly HitTestInfo Nowhere = new(-1, -1, DataGridViewHitTestType.None, -1, -1);

        internal HitTestInfo(int columnIndex, int rowIndex, DataGridViewHitTestType type, int columnX, int rowY)
        {
            ColumnIndex = columnIndex;
            RowIndex = rowIndex;
            Type = type;
            ColumnX = columnX;
            RowY = rowY;
        }

        /// <summary>The column, or -1.</summary>
        public int ColumnIndex { get; }

        /// <summary>The row, or -1.</summary>
        public int RowIndex { get; }

        /// <summary>The part of the grid.</summary>
        public DataGridViewHitTestType Type { get; }

        /// <summary>The left of the column, or -1.</summary>
        public int ColumnX { get; }

        /// <summary>The top of the row, or -1.</summary>
        public int RowY { get; }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is HitTestInfo other && other.ColumnIndex == ColumnIndex && other.RowIndex == RowIndex && other.Type == Type;

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(ColumnIndex, RowIndex, Type);

        /// <inheritdoc />
        public override string ToString() => $"{{ ColumnIndex = {ColumnIndex}, RowIndex = {RowIndex}, Type = {Type} }}";
    }
}

/// <summary>Data about a grid's binding having completed.</summary>
/// <param name="listChangedType">What changed.</param>
public class DataGridViewBindingCompleteEventArgs(ListChangedType listChangedType) : EventArgs
{
    /// <summary>What changed.</summary>
    public ListChangedType ListChangedType { get; } = listChangedType;
}

/// <summary>Handles a grid's binding having completed.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewBindingCompleteEventHandler(object? sender, DataGridViewBindingCompleteEventArgs e);
