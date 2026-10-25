using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>
/// Layout: column widths, filling the grid's width when asked, the rows' tops, the scroll bars and scrolling, and
/// where each cell is.
/// </summary>
public partial class DataGridView
{
    private readonly ScrollBarModel _verticalBar = new(vertical: true);
    private readonly ScrollBarModel _horizontalBar = new(vertical: false);
    private List<DataGridViewColumn> _columnOrder = [];
    private Dictionary<DataGridViewColumn, int> _columnLefts = [];
    private int[] _rowTops = [0];
    private int _contentWidth;
    private bool _layingOutGrid;

    /// <summary>The first row in view.</summary>
    public int FirstDisplayedScrollingRowIndex
    {
        get
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Visible && _rowTops[i + 1] > _verticalBar.Value)
                {
                    return i;
                }
            }

            return -1;
        }

        set
        {
            if (value >= 0 && value < _rows.Count)
            {
                ScrollVerticallyTo(_rowTops[value]);
            }
        }
    }

    /// <summary>The first column in view.</summary>
    public int FirstDisplayedScrollingColumnIndex
    {
        get => _columnOrder.Find(column => column.Visible && _columnLefts[column] + column.Width > _horizontalBar.Value)?.Index ?? -1;
        set
        {
            if (value >= 0 && value < _columns.Count && _columnLefts.TryGetValue(_columns[value], out var left))
            {
                HorizontalScrollingOffset = left;
            }
        }
    }

    /// <summary>The first cell in view.</summary>
    public DataGridViewCell? FirstDisplayedCell
    {
        get
        {
            var row = FirstDisplayedScrollingRowIndex;
            var column = FirstDisplayedScrollingColumnIndex;
            return row >= 0 && column >= 0 ? _rows[row].Cells[column] : null;
        }

        set
        {
            if (value is not null)
            {
                ScrollIntoView(value);
            }
        }
    }

    /// <summary>How far the grid is scrolled horizontally, in pixels.</summary>
    public int HorizontalScrollingOffset
    {
        get => _horizontalBar.Value;
        set
        {
            var clamped = Math.Clamp(value, 0, _horizontalBar.MaximumValue);
            if (clamped != _horizontalBar.Value)
            {
                var old = _horizontalBar.Value;
                _horizontalBar.Value = clamped;
                AfterScroll(ScrollOrientation.HorizontalScroll, old, clamped);
            }
        }
    }

    /// <summary>How far the grid is scrolled vertically, in pixels.</summary>
    protected int VerticalScrollingOffset => _verticalBar.Value;

    /// <summary>The width of the row headers as shown.</summary>
    private int RowHeadersShownWidth => _rowHeadersVisible ? _rowHeadersWidth : 0;

    /// <summary>The height of the column headers as shown.</summary>
    private int ColumnHeadersShownHeight => _columnHeadersVisible ? ColumnHeadersHeight : 0;

    /// <summary>The area the cells scroll in, in client coordinates.</summary>
    private Rectangle CellsArea
    {
        get
        {
            var client = ClientRectangle;
            var right = client.Right - (_verticalBar.Visible ? Theme.ScrollBarSize : 0);
            var bottom = client.Bottom - (_horizontalBar.Visible ? Theme.ScrollBarSize : 0);
            return Rectangle.FromLTRB(RowHeadersShownWidth, ColumnHeadersShownHeight, Math.Max(RowHeadersShownWidth, right), Math.Max(ColumnHeadersShownHeight, bottom));
        }
    }

    /// <inheritdoc />
    internal override bool ScrollsByTouch => _verticalBar.MaximumValue > 0 || _horizontalBar.MaximumValue > 0;

    /// <summary>The number of rows in view.</summary>
    /// <param name="includePartialRow">Whether a row partly in view counts.</param>
    public int DisplayedRowCount(bool includePartialRow)
    {
        var area = CellsArea;
        var count = 0;
        for (var i = 0; i < _rows.Count; i++)
        {
            if (!_rows[i].Visible)
            {
                continue;
            }

            var top = _rowTops[i] - _verticalBar.Value;
            var bottom = _rowTops[i + 1] - _verticalBar.Value;
            if (bottom > 0 && top < area.Height && (includePartialRow || (top >= 0 && bottom <= area.Height)))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The number of columns in view.</summary>
    /// <param name="includePartialColumns">Whether a column partly in view counts.</param>
    public int DisplayedColumnCount(bool includePartialColumns)
    {
        var area = CellsArea;
        return _columnOrder.Count(column =>
        {
            var left = _columnLefts[column] - _horizontalBar.Value;
            var right = left + column.Width;
            return column.Visible && right > 0 && left < area.Width && (includePartialColumns || (left >= 0 && right <= area.Width));
        });
    }

    /// <summary>A cell's bounds on the grid, in client coordinates.</summary>
    /// <param name="columnIndex">The column, or -1 for the row header.</param>
    /// <param name="rowIndex">The row, or -1 for the column header.</param>
    /// <param name="cutOverflow">Whether to cut the bounds to the visible area.</param>
    public Rectangle GetCellDisplayRectangle(int columnIndex, int rowIndex, bool cutOverflow)
    {
        var bounds = CellBounds(columnIndex, rowIndex);
        if (!cutOverflow)
        {
            return bounds;
        }

        var visible = Rectangle.Intersect(bounds, VisibleAreaFor(columnIndex, rowIndex));
        return visible.Width > 0 && visible.Height > 0 ? visible : Rectangle.Empty;
    }

    /// <summary>A column's bounds on the grid, header and cells, in client coordinates.</summary>
    /// <param name="columnIndex">The column.</param>
    /// <param name="cutOverflow">Whether to cut the bounds to the visible area.</param>
    public Rectangle GetColumnDisplayRectangle(int columnIndex, bool cutOverflow)
    {
        var header = CellBounds(columnIndex, -1);
        var bounds = new Rectangle(header.X, 0, header.Width, ClientSize.Height);
        return cutOverflow ? Rectangle.Intersect(bounds, new Rectangle(RowHeadersShownWidth, 0, CellsArea.Width, ClientSize.Height)) : bounds;
    }

    /// <summary>A row's bounds on the grid, header and cells, in client coordinates.</summary>
    /// <param name="rowIndex">The row.</param>
    /// <param name="cutOverflow">Whether to cut the bounds to the visible area.</param>
    public Rectangle GetRowDisplayRectangle(int rowIndex, bool cutOverflow)
    {
        var header = CellBounds(-1, rowIndex);
        var bounds = new Rectangle(0, header.Y, RowHeadersShownWidth + _contentWidth, header.Height);
        return cutOverflow ? Rectangle.Intersect(bounds, new Rectangle(0, ColumnHeadersShownHeight, ClientSize.Width, CellsArea.Height)) : bounds;
    }

    /// <summary>What is at a point of the grid.</summary>
    /// <param name="x">The x coordinate, in client coordinates.</param>
    /// <param name="y">The y coordinate, in client coordinates.</param>
    public HitTestInfo HitTest(int x, int y)
    {
        var point = new Point(x, y);
        if (_verticalBar.Visible && _verticalBar.Bounds.Contains(point))
        {
            return new HitTestInfo(-1, -1, DataGridViewHitTestType.VerticalScrollBar, -1, -1);
        }

        if (_horizontalBar.Visible && _horizontalBar.Bounds.Contains(point))
        {
            return new HitTestInfo(-1, -1, DataGridViewHitTestType.HorizontalScrollBar, -1, -1);
        }

        var area = CellsArea;
        var column = -1;
        var columnX = -1;
        if (x >= RowHeadersShownWidth && x < area.Right)
        {
            var contentX = x - RowHeadersShownWidth + _horizontalBar.Value;
            foreach (var candidate in _columnOrder)
            {
                var left = _columnLefts[candidate];
                if (candidate.Visible && contentX >= left && contentX < left + candidate.Width)
                {
                    column = candidate.Index;
                    columnX = left - _horizontalBar.Value + RowHeadersShownWidth;
                    break;
                }
            }
        }

        var row = -1;
        var rowY = -1;
        if (y >= ColumnHeadersShownHeight && y < area.Bottom)
        {
            var contentY = y - ColumnHeadersShownHeight + _verticalBar.Value;
            row = RowAt(contentY);
            rowY = row >= 0 ? _rowTops[row] - _verticalBar.Value + ColumnHeadersShownHeight : -1;
        }

        var inRowHeaders = _rowHeadersVisible && x < RowHeadersShownWidth;
        var inColumnHeaders = _columnHeadersVisible && y < ColumnHeadersShownHeight;
        return (inRowHeaders, inColumnHeaders) switch
        {
            (true, true) => new HitTestInfo(-1, -1, DataGridViewHitTestType.TopLeftHeader, -1, -1),
            (true, false) when row >= 0 => new HitTestInfo(-1, row, DataGridViewHitTestType.RowHeader, -1, rowY),
            (false, true) when column >= 0 => new HitTestInfo(column, -1, DataGridViewHitTestType.ColumnHeader, columnX, -1),
            (false, false) when row >= 0 && column >= 0 => new HitTestInfo(column, row, DataGridViewHitTestType.Cell, columnX, rowY),
            _ => HitTestInfo.Nowhere,
        };
    }

    /// <summary>Sizes every column to its content, by the grid's mode.</summary>
    public void AutoResizeColumns() => AutoResizeColumns(_autoSizeColumnsMode == DataGridViewAutoSizeColumnsMode.None ? DataGridViewAutoSizeColumnsMode.AllCells : _autoSizeColumnsMode);

    /// <summary>Sizes every column to its content.</summary>
    /// <param name="autoSizeColumnsMode">What to fit.</param>
    public void AutoResizeColumns(DataGridViewAutoSizeColumnsMode autoSizeColumnsMode)
    {
        foreach (var column in _columns.Items)
        {
            column.SetWidthFromLayout(PreferredWidth(column, (DataGridViewAutoSizeColumnMode)autoSizeColumnsMode));
        }

        OnColumnLayoutChanged();
    }

    /// <summary>Sizes a column to its content.</summary>
    /// <param name="columnIndex">The column.</param>
    public void AutoResizeColumn(int columnIndex) => AutoResizeColumn(columnIndex, DataGridViewAutoSizeColumnMode.AllCells);

    /// <summary>Sizes a column to its content.</summary>
    /// <param name="columnIndex">The column.</param>
    /// <param name="autoSizeColumnMode">What to fit.</param>
    public void AutoResizeColumn(int columnIndex, DataGridViewAutoSizeColumnMode autoSizeColumnMode)
    {
        var column = _columns[columnIndex];
        column.Width = PreferredWidth(column, autoSizeColumnMode);
    }

    /// <summary>Sizes the rows to their content. Rows keep their height.</summary>
    public void AutoResizeRows()
    {
    }

    /// <summary>Sizes the rows to their content. Rows keep their height.</summary>
    /// <param name="autoSizeRowsMode">What to fit.</param>
    public void AutoResizeRows(DataGridViewAutoSizeRowsMode autoSizeRowsMode)
    {
    }

    /// <summary>Sizes the column headers' height to their text.</summary>
    public void AutoResizeColumnHeadersHeight()
    {
        var height = Font.Height + 13;
        foreach (var column in _columns.Items.Where(c => c.Visible))
        {
            var text = TextMetrics.Measure(column.HeaderText, (column.HeaderCell.InheritedStyle.Font ?? Font).Spec, Math.Max(1, column.Width - 10));
            height = Math.Max(height, (int)Math.Ceiling(text.Height) + 10);
        }

        ColumnHeadersHeight = height;
    }

    /// <summary>Sizes the row headers' width. Row headers keep their width.</summary>
    /// <param name="rowHeadersWidthSizeMode">What to fit.</param>
    public void AutoResizeRowHeadersWidth(DataGridViewRowHeadersWidthSizeMode rowHeadersWidthSizeMode)
    {
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutGrid();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        LayoutGrid();
        Invalidate();
    }

    /// <inheritdoc />
    internal override void ScrollByTouch(int dx, int dy)
    {
        if (dy != 0)
        {
            ScrollVerticallyTo(_verticalBar.Value + dy);
        }

        if (dx != 0)
        {
            HorizontalScrollingOffset = _horizontalBar.Value + dx;
        }
    }

    /// <summary>Lays out the columns and rows, and shows the scroll bars they need.</summary>
    internal void LayoutGrid()
    {
        if (_layingOutGrid || _initializing > 0)
        {
            return;
        }

        _layingOutGrid = true;
        try
        {
            _columnOrder = [.. DisplayedColumns()];
            var client = ClientRectangle;
            var rowTops = new int[_rows.Count + 1];
            for (var i = 0; i < _rows.Count; i++)
            {
                rowTops[i + 1] = rowTops[i] + (_rows[i].Visible ? _rows[i].Height : 0);
            }

            _rowTops = rowTops;
            var contentHeight = rowTops[^1];
            var viewHeight = client.Height - ColumnHeadersShownHeight;
            var vertical = (ScrollBars & ScrollBars.Vertical) != 0 && contentHeight > viewHeight;
            SizeColumns(client.Width - RowHeadersShownWidth - (vertical ? Theme.ScrollBarSize : 0));
            var horizontal = (ScrollBars & ScrollBars.Horizontal) != 0 && _contentWidth > client.Width - RowHeadersShownWidth - (vertical ? Theme.ScrollBarSize : 0);
            if (horizontal && !vertical && (ScrollBars & ScrollBars.Vertical) != 0 && contentHeight > viewHeight - Theme.ScrollBarSize)
            {
                vertical = true;
                SizeColumns(client.Width - RowHeadersShownWidth - Theme.ScrollBarSize);
            }

            _verticalBar.Visible = vertical;
            _horizontalBar.Visible = horizontal;
            var area = CellsArea;
            _verticalBar.Bounds = new Rectangle(client.Right - Theme.ScrollBarSize, 0, Theme.ScrollBarSize, client.Height - (horizontal ? Theme.ScrollBarSize : 0));
            _horizontalBar.Bounds = new Rectangle(0, client.Bottom - Theme.ScrollBarSize, client.Width - (vertical ? Theme.ScrollBarSize : 0), Theme.ScrollBarSize);
            _verticalBar.ContentLength = contentHeight;
            _verticalBar.ViewLength = Math.Max(0, area.Height);
            _verticalBar.SmallChange = RowTemplate.Height;
            _horizontalBar.ContentLength = _contentWidth;
            _horizontalBar.ViewLength = Math.Max(0, area.Width);
            _horizontalBar.SmallChange = 20;
            _verticalBar.Value = Math.Clamp(_verticalBar.Value, 0, _verticalBar.MaximumValue);
            _horizontalBar.Value = Math.Clamp(_horizontalBar.Value, 0, _horizontalBar.MaximumValue);
            UpdateEditingControlBounds();
        }
        finally
        {
            _layingOutGrid = false;
        }
    }

    /// <summary>Scrolls so a cell is in view.</summary>
    internal void ScrollIntoView(DataGridViewCell cell)
    {
        var area = CellsArea;
        if (cell.RowIndex >= 0 && cell.RowIndex < _rows.Count)
        {
            var top = _rowTops[cell.RowIndex];
            var bottom = _rowTops[cell.RowIndex + 1];
            if (top < _verticalBar.Value)
            {
                ScrollVerticallyTo(top);
            }
            else if (bottom > _verticalBar.Value + area.Height)
            {
                ScrollVerticallyTo(bottom - area.Height);
            }
        }

        if (cell.OwningColumn is { } column && _columnLefts.TryGetValue(column, out var left))
        {
            if (left < _horizontalBar.Value)
            {
                HorizontalScrollingOffset = left;
            }
            else if (left + column.Width > _horizontalBar.Value + area.Width)
            {
                HorizontalScrollingOffset = Math.Min(left, left + column.Width - area.Width);
            }
        }
    }

    /// <summary>A cell's bounds in client coordinates; -1 for a header.</summary>
    internal Rectangle CellBounds(int columnIndex, int rowIndex)
    {
        int x, width;
        if (columnIndex < 0)
        {
            x = 0;
            width = RowHeadersShownWidth;
        }
        else
        {
            var column = _columns[columnIndex];
            x = RowHeadersShownWidth + (_columnLefts.TryGetValue(column, out var left) ? left : 0) - _horizontalBar.Value;
            width = column.Visible ? column.Width : 0;
        }

        int y, height;
        if (rowIndex < 0)
        {
            y = 0;
            height = ColumnHeadersShownHeight;
        }
        else
        {
            y = ColumnHeadersShownHeight + _rowTops[Math.Min(rowIndex, _rowTops.Length - 1)] - _verticalBar.Value;
            height = rowIndex < _rows.Count && _rows[rowIndex].Visible ? _rows[rowIndex].Height : 0;
        }

        return new Rectangle(x, y, width, height);
    }

    /// <summary>The area a cell shows in: the cells' area, or the header strip it belongs to.</summary>
    private Rectangle VisibleAreaFor(int columnIndex, int rowIndex)
    {
        var area = CellsArea;
        return (columnIndex < 0, rowIndex < 0) switch
        {
            (true, true) => new Rectangle(0, 0, RowHeadersShownWidth, ColumnHeadersShownHeight),
            (true, false) => new Rectangle(0, area.Top, RowHeadersShownWidth, area.Height),
            (false, true) => new Rectangle(area.Left, 0, area.Width, ColumnHeadersShownHeight),
            _ => area,
        };
    }

    private int RowAt(int contentY)
    {
        if (contentY < 0 || contentY >= _rowTops[^1])
        {
            return -1;
        }

        // The row tops are sorted: a binary search finds the row.
        var low = 0;
        var high = _rows.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (contentY < _rowTops[middle])
            {
                high = middle - 1;
            }
            else if (contentY >= _rowTops[middle + 1])
            {
                low = middle + 1;
            }
            else
            {
                return middle;
            }
        }

        return -1;
    }

    private void ScrollVerticallyTo(int offset)
    {
        var clamped = Math.Clamp(offset, 0, _verticalBar.MaximumValue);
        if (clamped != _verticalBar.Value)
        {
            var old = _verticalBar.Value;
            _verticalBar.Value = clamped;
            AfterScroll(ScrollOrientation.VerticalScroll, old, clamped);
        }
    }

    private void AfterScroll(ScrollOrientation orientation, int oldValue, int newValue)
    {
        UpdateEditingControlBounds();
        Invalidate();
        OnScroll(new ScrollEventArgs(ScrollEventType.ThumbPosition, oldValue, newValue, orientation));
    }

    /// <summary>Sets the widths of the columns that size themselves, filling an available width.</summary>
    private void SizeColumns(int available)
    {
        var visible = _columnOrder.Where(column => column.Visible).ToList();
        foreach (var column in visible)
        {
            var mode = column.InheritedAutoSizeMode;
            if (mode is not (DataGridViewAutoSizeColumnMode.None or DataGridViewAutoSizeColumnMode.NotSet or DataGridViewAutoSizeColumnMode.Fill))
            {
                column.SetWidthFromLayout(PreferredWidth(column, mode));
            }
        }

        var fill = visible.Where(column => column.InheritedAutoSizeMode == DataGridViewAutoSizeColumnMode.Fill).ToList();
        if (fill.Count > 0)
        {
            var fixedWidth = visible.Except(fill).Sum(column => column.Width);
            var remaining = Math.Max(0, available - fixedWidth);
            var weights = fill.Sum(column => column.FillWeight);
            var given = 0;
            for (var i = 0; i < fill.Count; i++)
            {
                var width = i == fill.Count - 1 ? remaining - given : (int)(remaining * fill[i].FillWeight / weights);
                fill[i].SetWidthFromLayout(width);
                given += fill[i].Width;
            }
        }

        var lefts = new Dictionary<DataGridViewColumn, int>();
        var x = 0;
        foreach (var column in _columnOrder)
        {
            lefts[column] = x;
            if (column.Visible)
            {
                x += column.Width;
            }
        }

        _columnLefts = lefts;
        _contentWidth = x;
    }

    private int PreferredWidth(DataGridViewColumn column, DataGridViewAutoSizeColumnMode mode)
    {
        var width = column.MinimumWidth;
        if (mode is DataGridViewAutoSizeColumnMode.ColumnHeader or DataGridViewAutoSizeColumnMode.AllCells or DataGridViewAutoSizeColumnMode.DisplayedCells)
        {
            width = Math.Max(width, (int)Math.Ceiling(TextMetrics.Measure(column.HeaderText, (_columnHeadersDefaultCellStyle.Font ?? Font).Spec).Width) + 24);
        }

        if (mode is DataGridViewAutoSizeColumnMode.AllCells or DataGridViewAutoSizeColumnMode.AllCellsExceptHeader or DataGridViewAutoSizeColumnMode.DisplayedCells or DataGridViewAutoSizeColumnMode.DisplayedCellsExceptHeader)
        {
            foreach (var row in _rows.Items)
            {
                if (column.Index < row.Cells.Count && row.Cells[column.Index] is var cell && cell.FormattedValue is string text)
                {
                    width = Math.Max(width, (int)Math.Ceiling(TextMetrics.Measure(text, (cell.InheritedStyle.Font ?? Font).Spec).Width) + 8);
                }
                else if (column.Index < row.Cells.Count && row.Cells[column.Index] is DataGridViewCheckBoxCell)
                {
                    width = Math.Max(width, ThemePainter.GlyphSize + 8);
                }
            }
        }

        return width;
    }
}
