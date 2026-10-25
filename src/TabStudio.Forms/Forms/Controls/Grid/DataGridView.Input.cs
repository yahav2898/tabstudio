using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// Input: a tap selects a cell, a row from its header or sorts by a column from its header; a tap on the current
/// cell or a double tap edits it; the keys move the current cell, edit it and delete rows; the scroll bars, the wheel
/// and a finger scroll.
/// </summary>
public partial class DataGridView
{
    /// <summary>How close to a column header's right edge a press resizes the column.</summary>
    private const int ResizeMargin = 5;

    private DataGridViewColumn? _resizing;
    private int _resizeStart;
    private int _resizeStartWidth;
    private bool _barPressed;
    private DataGridViewCell? _pressedCell;
    private bool _pressedWasCurrent;
    private DataGridViewCell? _hotCell;

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData)
    {
        if ((keyData & Keys.Alt) != 0)
        {
            return false;
        }

        return (keyData & Keys.KeyCode) switch
        {
            Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown => true,
            Keys.Enter or Keys.F2 or Keys.Delete or Keys.Space or Keys.Escape => true,
            Keys.Tab => !StandardTab && _columns.Count > 0 && _rows.Count > 0,
            _ => base.IsInputKey(keyData),
        };
    }

    /// <summary>Handles the keys the editing control passes on: Enter, Escape, Tab and the up and down arrows.</summary>
    /// <inheritdoc />
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (_editingControl is not null && ContainsFocus)
        {
            var code = keyData & Keys.KeyCode;
            switch (code)
            {
                case Keys.Escape:
                    CancelEdit();
                    Focus();
                    return true;
                case Keys.Enter:
                    if (EndEdit())
                    {
                        Focus();
                        Move((keyData & Keys.Shift) != 0 ? -1 : 1, 0, keyData);
                    }

                    return true;
                case Keys.Tab when !StandardTab:
                    if (EndEdit())
                    {
                        Focus();
                        MoveByTab((keyData & Keys.Shift) == 0);
                    }

                    return true;
                case Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown when _editingControl is not ComboBox:
                    if (EndEdit())
                    {
                        Focus();
                        Move(code is Keys.Up or Keys.PageUp ? -1 : 1, 0, keyData);
                    }

                    return true;
            }
        }

        return base.ProcessDialogKey(keyData);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _editingControl is not null)
        {
            return;
        }

        var code = e.KeyCode;
        var page = Math.Max(1, DisplayedRowCount(includePartialRow: false));
        switch (code)
        {
            case Keys.Up:
                Move(-1, 0, e.KeyData);
                break;
            case Keys.Down:
                Move(1, 0, e.KeyData);
                break;
            case Keys.Left:
                Move(0, -1, e.KeyData);
                break;
            case Keys.Right:
                Move(0, 1, e.KeyData);
                break;
            case Keys.PageUp:
                Move(-page, 0, e.KeyData);
                break;
            case Keys.PageDown:
                Move(page, 0, e.KeyData);
                break;
            case Keys.Home:
                MoveTo(e.Control ? FirstVisibleRow() : CurrentRowIndexOr(0), FirstVisibleColumn(), e.KeyData);
                break;
            case Keys.End:
                MoveTo(e.Control ? LastVisibleRow() : CurrentRowIndexOr(0), LastVisibleColumn(), e.KeyData);
                break;
            case Keys.Tab when !StandardTab:
                MoveByTab(!e.Shift);
                break;
            case Keys.Enter:
                Move(e.Shift ? -1 : 1, 0, e.KeyData);
                break;
            case Keys.F2:
                BeginEdit(selectAll: false);
                break;
            case Keys.Space when _currentCell is { } cell && cell.OnSpace():
                if (cell is DataGridViewButtonCell or DataGridViewLinkCell)
                {
                    OnCellContentClick(new DataGridViewCellEventArgs(cell.ColumnIndex, cell.RowIndex));
                }

                break;
            case Keys.Delete:
                DeleteSelectedRows();
                break;
            case Keys.A when e.Control:
                SelectAll();
                break;
            case Keys.C when e.Control:
                if (GetClipboardContent() is { } content)
                {
                    Clipboard.SetText(content.GetText());
                }

                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (e.Handled || _editingControl is not null || char.IsControl(e.KeyChar) || e.KeyChar == ' ')
        {
            return;
        }

        // Typing on the current cell starts editing it with the character typed, as in Windows.
        if (EditMode is DataGridViewEditMode.EditOnKeystroke or DataGridViewEditMode.EditOnKeystrokeOrF2 && BeginEdit(selectAll: false) && _editingControl is TextBox box)
        {
            box.Text = new string(e.KeyChar, 1);
            box.SelectionStart = 1;
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        foreach (var bar in new[] { _verticalBar, _horizontalBar })
        {
            if (bar.Visible && bar.Bounds.Contains(e.Location))
            {
                var vertical = bar == _verticalBar;
                var old = bar.Value;
                bar.PointerDown(e.Location);
                _barPressed = true;
                Capture = true;
                AfterScroll(vertical ? ScrollOrientation.VerticalScroll : ScrollOrientation.HorizontalScroll, old, bar.Value);
                return;
            }
        }

        var hit = HitTest(e.X, e.Y);
        if (hit.Type == DataGridViewHitTestType.ColumnHeader && AllowUserToResizeColumns && e.X >= hit.ColumnX + _columns[hit.ColumnIndex].Width - ResizeMargin)
        {
            _resizing = _columns[hit.ColumnIndex];
            _resizeStart = e.X;
            _resizeStartWidth = _resizing.Width;
            Capture = true;
            return;
        }

        switch (hit.Type)
        {
            case DataGridViewHitTestType.Cell:
            {
                var cell = _rows[hit.RowIndex].Cells[hit.ColumnIndex];
                var local = new Point(e.X - hit.ColumnX, e.Y - hit.RowY);
                OnCellMouseDown(new DataGridViewCellMouseEventArgs(hit.ColumnIndex, hit.RowIndex, local.X, local.Y, e));
                _pressedWasCurrent = cell == _currentCell && _editingControl is null;
                _pressedCell = cell;
                if (cell != _currentCell)
                {
                    if (!EndEdit())
                    {
                        return;
                    }

                    SetCurrentCellCore(cell, raiseEvents: true);
                }

                var modifiers = ModifierKeys;
                SelectFromCurrent(cell, extend: (modifiers & Keys.Shift) != 0, toggle: (modifiers & Keys.Control) != 0);
                Invalidate();
                OnSelectionChanged(EventArgs.Empty);
                break;
            }

            case DataGridViewHitTestType.RowHeader:
            {
                if (!EndEdit())
                {
                    return;
                }

                var row = _rows[hit.RowIndex];
                var column = _currentCell?.ColumnIndex ?? FirstVisibleColumn();
                if (column >= 0)
                {
                    SetCurrentCellCore(row.Cells[column], raiseEvents: true);
                }

                var modifiers = ModifierKeys;
                if (_selectionMode is DataGridViewSelectionMode.RowHeaderSelect or DataGridViewSelectionMode.FullRowSelect && column >= 0)
                {
                    SelectFromCurrent(row.Cells[column], extend: (modifiers & Keys.Shift) != 0, toggle: (modifiers & Keys.Control) != 0, wholeRow: true);
                }

                Invalidate();
                OnSelectionChanged(EventArgs.Empty);
                break;
            }

            case DataGridViewHitTestType.TopLeftHeader:
                SelectAll();
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_barPressed)
        {
            var oldV = _verticalBar.Value;
            var oldH = _horizontalBar.Value;
            _verticalBar.PointerMove(e.Location);
            _horizontalBar.PointerMove(e.Location);
            if (oldV != _verticalBar.Value)
            {
                AfterScroll(ScrollOrientation.VerticalScroll, oldV, _verticalBar.Value);
            }

            if (oldH != _horizontalBar.Value)
            {
                AfterScroll(ScrollOrientation.HorizontalScroll, oldH, _horizontalBar.Value);
            }

            return;
        }

        if (_resizing is { } column)
        {
            column.Width = Math.Max(column.MinimumWidth, _resizeStartWidth + e.X - _resizeStart);
            return;
        }

        var hit = HitTest(e.X, e.Y);
        Cursor = hit.Type == DataGridViewHitTestType.ColumnHeader && AllowUserToResizeColumns && e.X >= hit.ColumnX + _columns[hit.ColumnIndex].Width - ResizeMargin
            ? Cursors.SizeWE
            : Cursors.Default;
        var hotHeader = hit.Type == DataGridViewHitTestType.ColumnHeader ? hit.ColumnIndex : -1;
        if (hotHeader != _hotColumnHeader)
        {
            _hotColumnHeader = hotHeader;
            Invalidate();
        }

        var hot = hit.Type == DataGridViewHitTestType.Cell ? _rows[hit.RowIndex].Cells[hit.ColumnIndex] : null;
        if (hot != _hotCell)
        {
            if (_hotCell is { DataGridView: not null } previous && previous.RowIndex >= 0)
            {
                OnCellMouseLeave(new DataGridViewCellEventArgs(previous.ColumnIndex, previous.RowIndex));
            }

            _hotCell = hot;
            if (hot is not null)
            {
                OnCellMouseEnter(new DataGridViewCellEventArgs(hot.ColumnIndex, hot.RowIndex));
            }
        }

        // Dragging with the mouse extends the selection, as in Windows.
        if ((e.Button & MouseButtons.Left) != 0 && _pressedCell is not null && hot is not null && hot != _currentCell && MultiSelect && _editingControl is null)
        {
            SetCurrentCellCore(hot, raiseEvents: true);
            SelectFromCurrent(hot, extend: true, toggle: false);
            OnSelectionChanged(EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_barPressed)
        {
            _barPressed = false;
            _verticalBar.PointerUp();
            _horizontalBar.PointerUp();
            Capture = false;
            Invalidate();
            return;
        }

        if (_resizing is { } resized)
        {
            _resizing = null;
            Capture = false;
            OnColumnWidthChanged(new DataGridViewColumnEventArgs(resized));
            return;
        }

        var hit = HitTest(e.X, e.Y);
        var pressed = _pressedCell;
        _pressedCell = null;
        switch (hit.Type)
        {
            case DataGridViewHitTestType.Cell when pressed is not null && pressed.RowIndex == hit.RowIndex && pressed.ColumnIndex == hit.ColumnIndex:
            {
                var local = new Point(e.X - hit.ColumnX, e.Y - hit.RowY);
                var args = new DataGridViewCellEventArgs(hit.ColumnIndex, hit.RowIndex);
                OnCellMouseUp(new DataGridViewCellMouseEventArgs(hit.ColumnIndex, hit.RowIndex, local.X, local.Y, e));
                OnCellClick(args);
                if (pressed.OnTap(local))
                {
                    OnCellContentClick(args);
                }
                else if (e.Clicks < 2 && _pressedWasCurrent && EditMode is DataGridViewEditMode.EditOnKeystrokeOrF2 or DataGridViewEditMode.EditOnEnter)
                {
                    // A tap on the current cell edits it: the touch way to type in a cell.
                    BeginEdit(selectAll: true);
                }

                OnCellMouseClick(new DataGridViewCellMouseEventArgs(hit.ColumnIndex, hit.RowIndex, local.X, local.Y, e));
                break;
            }

            case DataGridViewHitTestType.ColumnHeader:
            {
                var column = _columns[hit.ColumnIndex];
                OnColumnHeaderMouseClick(new DataGridViewCellMouseEventArgs(hit.ColumnIndex, -1, e.X - hit.ColumnX, e.Y, e));
                if (column.SortMode == DataGridViewColumnSortMode.Automatic && (!IsBound || _boundList is IBindingList { SupportsSorting: true }))
                {
                    Sort(column, _sortedColumn == column && _sortOrder == SortOrder.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending);
                }
                else if (_selectionMode is DataGridViewSelectionMode.FullColumnSelect or DataGridViewSelectionMode.ColumnHeaderSelect && _rows.Count > 0)
                {
                    ClearSelectionCore();
                    column.SetSelectedInternal(true);
                    Invalidate();
                    OnSelectionChanged(EventArgs.Empty);
                }

                break;
            }

            case DataGridViewHitTestType.RowHeader:
                OnRowHeaderMouseClick(new DataGridViewCellMouseEventArgs(-1, hit.RowIndex, e.X, e.Y - hit.RowY, e));
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hotColumnHeader >= 0)
        {
            _hotColumnHeader = -1;
            Invalidate();
        }

        if (_hotCell is { DataGridView: not null } previous && previous.RowIndex >= 0)
        {
            OnCellMouseLeave(new DataGridViewCellEventArgs(previous.ColumnIndex, previous.RowIndex));
        }

        _hotCell = null;
    }

    /// <inheritdoc />
    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        var point = PointToClient(Cursor.Position);
        var hit = HitTest(point.X, point.Y);
        switch (hit.Type)
        {
            case DataGridViewHitTestType.Cell:
                OnCellDoubleClick(new DataGridViewCellEventArgs(hit.ColumnIndex, hit.RowIndex));
                if (_editingControl is null && EditMode != DataGridViewEditMode.EditProgrammatically && EditMode != DataGridViewEditMode.EditOnF2)
                {
                    BeginEdit(selectAll: true);
                }

                break;
            case DataGridViewHitTestType.ColumnHeader:
                OnColumnHeaderMouseDoubleClick(new DataGridViewCellMouseEventArgs(hit.ColumnIndex, -1, point.X - hit.ColumnX, point.Y, new MouseEventArgs(MouseButtons.Left, 2, point.X, point.Y, 0)));
                break;
            case DataGridViewHitTestType.RowHeader:
                OnRowHeaderMouseDoubleClick(new DataGridViewCellMouseEventArgs(-1, hit.RowIndex, point.X, point.Y - hit.RowY, new MouseEventArgs(MouseButtons.Left, 2, point.X, point.Y, 0)));
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollVerticallyTo(_verticalBar.Value - (e.Delta / 120 * 3 * RowTemplate.Height));
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    private int CurrentRowIndexOr(int fallback) => _currentCell?.RowIndex ?? fallback;

    private int FirstVisibleRow() => _rows.Items.FindIndex(row => row.Visible);

    private int LastVisibleRow() => _rows.Items.FindLastIndex(row => row.Visible);

    private int FirstVisibleColumn() => _columnOrder.Find(column => column.Visible)?.Index ?? -1;

    private int LastVisibleColumn() => _columnOrder.FindLast(column => column.Visible)?.Index ?? -1;

    /// <summary>Moves the current cell by rows and columns, extending the selection with Shift.</summary>
    private void Move(int rows, int columns, Keys keyData)
    {
        if (_currentCell is not { } current)
        {
            EnsureCurrentCell();
            return;
        }

        var visibleColumns = _columnOrder.Where(column => column.Visible).ToList();
        var columnPosition = visibleColumns.IndexOf(current.OwningColumn!);
        var targetColumn = visibleColumns[Math.Clamp(columnPosition + columns, 0, visibleColumns.Count - 1)].Index;
        var targetRow = current.RowIndex;
        var step = Math.Sign(rows);
        for (var moved = 0; moved < Math.Abs(rows); moved++)
        {
            var next = targetRow + step;
            while (next >= 0 && next < _rows.Count && !_rows[next].Visible)
            {
                next += step;
            }

            if (next < 0 || next >= _rows.Count)
            {
                break;
            }

            targetRow = next;
        }

        MoveTo(targetRow, targetColumn, keyData);
    }

    private void MoveTo(int row, int column, Keys keyData)
    {
        if (row < 0 || column < 0 || row >= _rows.Count || !EndEdit())
        {
            return;
        }

        var cell = _rows[row].Cells[column];
        if (cell != _currentCell)
        {
            SetCurrentCellCore(cell, raiseEvents: true);
        }

        SelectFromCurrent(cell, extend: (keyData & Keys.Shift) != 0 && (keyData & Keys.KeyCode) is not (Keys.Tab or Keys.Enter), toggle: false);
        OnSelectionChanged(EventArgs.Empty);
    }

    private void MoveByTab(bool forward)
    {
        if (_currentCell is not { } current)
        {
            EnsureCurrentCell();
            return;
        }

        var columns = _columnOrder.Where(column => column.Visible).ToList();
        var position = columns.IndexOf(current.OwningColumn!) + (forward ? 1 : -1);
        var row = current.RowIndex;
        if (position >= columns.Count)
        {
            position = 0;
            row = _rows.GetNextRow(row, DataGridViewElementStates.Visible);
        }
        else if (position < 0)
        {
            position = columns.Count - 1;
            row = _rows.GetPreviousRow(row, DataGridViewElementStates.Visible);
        }

        if (row < 0)
        {
            // Past the last cell, Tab leaves the grid, as in Windows.
            Parent?.SelectNextControl(this, forward, tabStopOnly: true, nested: true, wrap: true);
            return;
        }

        MoveTo(row, columns[position].Index, Keys.None);
    }

    private void DeleteSelectedRows()
    {
        if (!AllowUserToDeleteRows || ReadOnly)
        {
            return;
        }

        var rows = _rows.Items.Where(row => row.Selected && !row.IsNewRow).ToList();
        foreach (var row in rows)
        {
            var deleting = new DataGridViewRowCancelEventArgs(row);
            OnUserDeletingRow(deleting);
            if (deleting.Cancel)
            {
                continue;
            }

            _rows.Remove(row);
            OnUserDeletedRow(new DataGridViewRowEventArgs(row));
        }
    }
}
