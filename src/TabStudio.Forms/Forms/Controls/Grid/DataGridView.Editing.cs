using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// Editing: a text or combo box over the current cell, which the device's keyboard types into; committing parses
/// the typed value into the cell's value type, through CellValidating and CellParsing, and reports errors through
/// DataError or the Windows Forms default error dialog.
/// </summary>
public partial class DataGridView
{
    private DataGridViewTextBoxEditingControl? _textEditor;
    private DataGridViewComboBoxEditingControl? _comboEditor;
    private Control? _editingControl;
    private DataGridViewCell? _editingCell;
    private bool _currentCellDirty;
    private int _addedRowForEdit = -1;

    /// <summary>The control editing the current cell, if any.</summary>
    public Control? EditingControl => _editingControl;

    /// <summary>Whether the current cell is being edited.</summary>
    public bool IsCurrentCellInEditMode => _editingControl is not null;

    /// <summary>Whether the current cell's typed value differs from its value.</summary>
    public bool IsCurrentCellDirty => _currentCellDirty;

    /// <summary>Whether the current row has uncommitted changes.</summary>
    public bool IsCurrentRowDirty => _currentCellDirty;

    /// <summary>Starts editing the current cell.</summary>
    /// <param name="selectAll">Whether the editor selects all the text.</param>
    /// <returns>Whether editing started.</returns>
    public virtual bool BeginEdit(bool selectAll)
    {
        if (_editingControl is not null)
        {
            return true;
        }

        if (_currentCell is not { } cell || cell.ReadOnly || cell.EditType is not { } editType)
        {
            return false;
        }

        var cancel = new DataGridViewCellCancelEventArgs(cell.ColumnIndex, cell.RowIndex);
        OnCellBeginEdit(cancel);
        if (cancel.Cancel)
        {
            return false;
        }

        if (cell.OwningRow is { IsNewRow: true } newRow)
        {
            cell = StartNewRow(newRow, cell);
            if (cell is null)
            {
                return false;
            }
        }

        var style = cell.InheritedStyle;
        Control control = typeof(ComboBox).IsAssignableFrom(editType) ? _comboEditor ??= new DataGridViewComboBoxEditingControl() : _textEditor ??= new DataGridViewTextBoxEditingControl();
        var editor = (IDataGridViewEditingControl)control;
        editor.EditingControlDataGridView = this;
        editor.EditingControlRowIndex = cell.RowIndex;
        _editingCell = cell;
        if (control is DataGridViewComboBoxEditingControl combo && cell is DataGridViewComboBoxCell comboCell)
        {
            combo.Fill(comboCell);
        }

        // The cell's value goes into the control before the grid is editing, so it does not count as a change.
        editor.ApplyCellStyleToEditingControl(style);
        if (control is DataGridViewTextBoxEditingControl text && cell is DataGridViewTextBoxCell textCell)
        {
            text.MaxLength = textCell.MaxInputLength;
        }

        editor.EditingControlFormattedValue = cell.FormattedValueFor(cell.Value, style) ?? string.Empty;
        editor.EditingControlValueChanged = false;
        _editingControl = control;

        if (control.Parent != this)
        {
            Controls.Add(control);
        }

        UpdateEditingControlBounds();
        control.Visible = true;
        OnEditingControlShowing(new DataGridViewEditingControlShowingEventArgs(control, style));
        editor.PrepareEditingControlForEdit(selectAll);
        control.Focus();
        if (control is DataGridViewComboBoxEditingControl dropDown)
        {
            // On a tablet, a combo box cell opens its list right away rather than waiting for a second tap.
            dropDown.DroppedDown = true;
        }

        Invalidate();
        return true;
    }

    /// <summary>Commits the current cell's typed value and stops editing.</summary>
    /// <returns>Whether the value was valid and editing stopped.</returns>
    public bool EndEdit() => EndEdit(DataGridViewDataErrorContexts.Commit);

    /// <summary>Commits the current cell's typed value and stops editing.</summary>
    /// <param name="context">Why editing ends.</param>
    /// <returns>Whether the value was valid and editing stopped.</returns>
    public bool EndEdit(DataGridViewDataErrorContexts context)
    {
        if (_editingControl is not { } control || _editingCell is not { } cell)
        {
            return true;
        }

        var editor = (IDataGridViewEditingControl)control;
        var formatted = editor.GetEditingControlFormattedValue(context);
        if (!CommitTyped(cell, formatted, editor.EditingControlValueChanged))
        {
            return false;
        }

        FinishEdit(cell, keepAddedRow: true);
        return true;
    }

    /// <summary>Stops editing the current cell without committing its typed value.</summary>
    /// <returns>Whether editing stopped.</returns>
    public bool CancelEdit()
    {
        if (_editingCell is not { } cell)
        {
            return true;
        }

        FinishEdit(cell, keepAddedRow: false);
        return true;
    }

    /// <summary>Commits the current cell's typed value and keeps editing.</summary>
    /// <param name="context">Why the value is committed.</param>
    /// <returns>Whether the value was valid.</returns>
    public bool CommitEdit(DataGridViewDataErrorContexts context)
    {
        if (_editingControl is not { } control || _editingCell is not { } cell)
        {
            return true;
        }

        var editor = (IDataGridViewEditingControl)control;
        if (!CommitTyped(cell, editor.GetEditingControlFormattedValue(context), editor.EditingControlValueChanged))
        {
            return false;
        }

        editor.EditingControlValueChanged = false;
        SetDirty(false);
        return true;
    }

    /// <summary>Tells the grid the current cell's typed value changed or was committed, as editing controls do.</summary>
    /// <param name="dirty">Whether it differs from the cell's value.</param>
    public virtual void NotifyCurrentCellDirty(bool dirty) => SetDirty(dirty);

    /// <summary>Shows the cell's value again in its editing control.</summary>
    /// <returns>Whether the control was updated.</returns>
    public bool RefreshEdit()
    {
        if (_editingControl is IDataGridViewEditingControl editor && _editingCell is { } cell)
        {
            editor.EditingControlFormattedValue = cell.FormattedValueFor(cell.Value, cell.InheritedStyle) ?? string.Empty;
            editor.EditingControlValueChanged = false;
            SetDirty(false);
            return true;
        }

        return false;
    }

    /// <summary>Draws a cell again with its value, after it changed in the data source.</summary>
    /// <param name="columnIndex">The column.</param>
    /// <param name="rowIndex">The row.</param>
    public void UpdateCellValue(int columnIndex, int rowIndex) => Invalidate();

    /// <summary>Draws a cell again.</summary>
    /// <param name="columnIndex">The column.</param>
    /// <param name="rowIndex">The row.</param>
    public void InvalidateCell(int columnIndex, int rowIndex) => Invalidate();

    /// <summary>Draws a cell again.</summary>
    /// <param name="dataGridViewCell">The cell.</param>
    public void InvalidateCell(DataGridViewCell dataGridViewCell) => Invalidate();

    /// <summary>Draws a row again.</summary>
    /// <param name="rowIndex">The row.</param>
    public void InvalidateRow(int rowIndex) => Invalidate();

    /// <summary>Draws a column again.</summary>
    /// <param name="columnIndex">The column.</param>
    public void InvalidateColumn(int columnIndex) => Invalidate();

    /// <summary>Whether a cell is being edited.</summary>
    internal bool IsEditing(DataGridViewCell cell) => _editingCell == cell && _editingControl is not null;

    /// <summary>The typed value of a cell being edited, or <see langword="null"/>.</summary>
    internal object? EditedFormattedValueOf(DataGridViewCell cell) =>
        _editingCell == cell && _editingControl is IDataGridViewEditingControl editor ? editor.EditingControlFormattedValue : null;

    /// <summary>Raises CellClick, for cells.</summary>
    internal void RaiseCellClick(DataGridViewCellEventArgs e) => OnCellClick(e);

    /// <summary>Raises CellContentClick, for cells.</summary>
    internal void RaiseCellContentClick(DataGridViewCellEventArgs e) => OnCellContentClick(e);

    /// <summary>Raises CellValueChanged, for cells.</summary>
    internal void RaiseCellValueChanged(DataGridViewCellEventArgs e)
    {
        Invalidate();
        OnCellValueChanged(e);
    }

    /// <summary>Raises CellFormatting, for cells.</summary>
    internal void RaiseCellFormatting(DataGridViewCellFormattingEventArgs e) => OnCellFormatting(e);

    /// <summary>Raises DataError; without handlers, shows the Windows Forms default error dialog when asked.</summary>
    internal void RaiseDataError(DataGridViewDataErrorEventArgs e, bool showDialog)
    {
        if (Events[EventDataError] is not null)
        {
            OnDataError(e);
            if (e.ThrowException && e.Exception is { } exception)
            {
                throw exception;
            }

            return;
        }

        if (showDialog && e.Exception is { } error)
        {
            MessageBox.Show(
                $"The following exception occurred in the DataGridView:\r\n\r\n{error}\r\n\r\nTo replace this default dialog please handle the DataError event.",
                "DataGridView Default Error Dialog",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>Sets a cell's value from a tap, as a check box cell does, with the events of an edit.</summary>
    internal void CommitCellValue(DataGridViewCell cell, object? value)
    {
        if (cell.ReadOnly)
        {
            return;
        }

        if (cell.OwningRow is { IsNewRow: true } newRow && StartNewRow(newRow, cell) is { } added)
        {
            cell = added;
            _addedRowForEdit = -1;
        }

        var cancel = new DataGridViewCellCancelEventArgs(cell.ColumnIndex, cell.RowIndex);
        OnCellBeginEdit(cancel);
        if (cancel.Cancel)
        {
            return;
        }

        SetDirty(true);
        try
        {
            cell.Value = value;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or InvalidCastException)
        {
            RaiseDataError(new DataGridViewDataErrorEventArgs(exception, cell.ColumnIndex, cell.RowIndex, DataGridViewDataErrorContexts.Commit), showDialog: true);
        }

        SetDirty(false);
        OnCellEndEdit(new DataGridViewCellEventArgs(cell.ColumnIndex, cell.RowIndex));
    }

    /// <summary>Puts the editing control over the current cell again, after a scroll or a layout change.</summary>
    private void UpdateEditingControlBounds()
    {
        if (_editingControl is not { } control || _editingCell is not { } cell || cell.RowIndex < 0 || cell.ColumnIndex < 0)
        {
            return;
        }

        var bounds = CellBounds(cell.ColumnIndex, cell.RowIndex);
        var area = CellsArea;
        var visible = Rectangle.Intersect(bounds, area);
        if (visible.Width <= 0 || visible.Height <= 0)
        {
            control.SetBoundsFromLayout(new Rectangle(-10000, -10000, bounds.Width, bounds.Height));
            return;
        }

        control.SetBoundsFromLayout(Rectangle.FromLTRB(bounds.Left + 1, bounds.Top + 1, bounds.Right - 1, bounds.Bottom - 1));
    }

    private void HideEditingControl()
    {
        if (_editingControl is { } control)
        {
            var hadFocus = control.ContainsFocus;
            _editingControl = null;
            control.Visible = false;
            if (hadFocus)
            {
                Focus();
            }
        }

        _editingCell = null;
        SetDirty(false);
    }

    private bool CommitTyped(DataGridViewCell cell, object? formatted, bool changed)
    {
        var validating = new DataGridViewCellValidatingEventArgs(cell.ColumnIndex, cell.RowIndex, formatted);
        OnCellValidating(validating);
        if (validating.Cancel)
        {
            return false;
        }

        if (changed)
        {
            var style = cell.InheritedStyle;
            object? value;
            try
            {
                var parsing = new DataGridViewCellParsingEventArgs(cell.RowIndex, cell.ColumnIndex, formatted, cell.ValueType, style);
                OnCellParsing(parsing);
                value = parsing.ParsingApplied ? parsing.Value : cell.ParseFormattedValue(parsing.Value, parsing.InheritedCellStyle, null, null);
                cell.Value = value;
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or ArgumentException or InvalidOperationException or OverflowException or NotSupportedException)
            {
                var error = new DataGridViewDataErrorEventArgs(exception, cell.ColumnIndex, cell.RowIndex, DataGridViewDataErrorContexts.Parsing | DataGridViewDataErrorContexts.Commit);
                RaiseDataError(error, showDialog: true);
                if (Events[EventDataError] is null || error.Cancel)
                {
                    return false;
                }
            }
        }

        OnCellValidated(new DataGridViewCellEventArgs(cell.ColumnIndex, cell.RowIndex));
        return true;
    }

    private void FinishEdit(DataGridViewCell cell, bool keepAddedRow)
    {
        var columnIndex = cell.ColumnIndex;
        var rowIndex = cell.RowIndex;
        var added = _addedRowForEdit;
        _addedRowForEdit = -1;
        HideEditingControl();
        if (added >= 0)
        {
            if (IsBound)
            {
                FinishBoundItem(added, keepAddedRow);
            }
            else if (!keepAddedRow && added < _rows.Count && !_rows[added].IsNewRow && _rows[added].Cells.Items.TrueForAll(c => c.StoredValue is null))
            {
                // Escape on a row the edit just started removes it again, as in Windows.
                _rows.RemoveInternal(added);
                OnRowsRemovedInternal(added, 1);
                if (_rows.Count > 0)
                {
                    var row = Math.Min(added, _rows.Count - 1);
                    SetCurrentCellCore(_rows[row].Cells[Math.Min(columnIndex, _rows[row].Cells.Count - 1)], raiseEvents: true);
                    SelectFromCurrent(_currentCell!, extend: false, toggle: false);
                }
            }
        }

        if (rowIndex >= 0 && columnIndex >= 0)
        {
            OnCellEndEdit(new DataGridViewCellEventArgs(columnIndex, rowIndex));
        }

        Invalidate();
    }

    /// <summary>Turns the row for new records into a row, as the user starts typing in it, and adds a new one below.</summary>
    /// <returns>The cell to edit in the row, or <see langword="null"/> when no row could be added.</returns>
    private DataGridViewCell? StartNewRow(DataGridViewRow newRow, DataGridViewCell cell)
    {
        var columnIndex = cell.ColumnIndex;
        if (IsBound)
        {
            var index = AddBoundItem();
            if (index < 0 || index >= _rows.Count)
            {
                return null;
            }

            _addedRowForEdit = index;
            var bound = _rows[index].Cells[columnIndex];
            SetCurrentCellCore(bound, raiseEvents: false);
            SelectFromCurrent(bound, extend: false, toggle: false);
            OnUserAddedRow(new DataGridViewRowEventArgs(_rows[NewRowIndex >= 0 ? NewRowIndex : index]));
            return bound;
        }

        newRow.IsNewRow = false;
        _addedRowForEdit = newRow.Index;
        OnDefaultValuesNeeded(new DataGridViewRowEventArgs(newRow));
        var next = (DataGridViewRow)_rowTemplate.Clone();
        next.IsNewRow = true;
        _rows.InsertInternal(_rows.Count, next);
        OnRowsAddedInternal(next.Index, 1);
        OnUserAddedRow(new DataGridViewRowEventArgs(next));
        return cell;
    }

    private void SetDirty(bool dirty)
    {
        if (_currentCellDirty != dirty)
        {
            _currentCellDirty = dirty;
            OnCurrentCellDirtyStateChanged(EventArgs.Empty);
        }
    }
}

/// <summary>A control that edits a grid's cell.</summary>
public interface IDataGridViewEditingControl
{
    /// <summary>The grid.</summary>
    DataGridView? EditingControlDataGridView { get; set; }

    /// <summary>The typed value.</summary>
    object EditingControlFormattedValue { get; set; }

    /// <summary>The row of the cell edited.</summary>
    int EditingControlRowIndex { get; set; }

    /// <summary>Whether the value was changed.</summary>
    bool EditingControlValueChanged { get; set; }

    /// <summary>The cursor over the editing panel.</summary>
    Cursor EditingPanelCursor { get; }

    /// <summary>Whether the control moves when its value changes.</summary>
    bool RepositionEditingControlOnValueChange { get; }

    /// <summary>Takes a cell's style.</summary>
    /// <param name="dataGridViewCellStyle">The style.</param>
    void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle);

    /// <summary>Whether the control takes a key rather than the grid.</summary>
    /// <param name="keyData">The key.</param>
    /// <param name="dataGridViewWantsInputKey">Whether the grid would take it.</param>
    bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey);

    /// <summary>The typed value.</summary>
    /// <param name="context">Why it is asked for.</param>
    object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context);

    /// <summary>Prepares the control for editing.</summary>
    /// <param name="selectAll">Whether to select all the text.</param>
    void PrepareEditingControlForEdit(bool selectAll);
}

/// <summary>The text box that edits a grid's text cells.</summary>
[ToolboxItem(false)]
public class DataGridViewTextBoxEditingControl : TextBox, IDataGridViewEditingControl
{
    /// <summary>Creates the editing control.</summary>
    public DataGridViewTextBoxEditingControl()
    {
        BorderStyle = BorderStyle.None;
        TabStop = false;
        AutoSize = false;
    }

    /// <inheritdoc />
    public DataGridView? EditingControlDataGridView { get; set; }

    /// <inheritdoc />
    public object EditingControlFormattedValue
    {
        get => Text;
        set => Text = value as string ?? Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture);
    }

    /// <inheritdoc />
    public int EditingControlRowIndex { get; set; }

    /// <inheritdoc />
    public bool EditingControlValueChanged { get; set; }

    /// <inheritdoc />
    public Cursor EditingPanelCursor => Cursors.Default;

    /// <inheritdoc />
    public virtual bool RepositionEditingControlOnValueChange => false;

    /// <inheritdoc />
    public virtual void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCellStyle);
        if (dataGridViewCellStyle.Font is { } font)
        {
            Font = font;
        }

        if (!dataGridViewCellStyle.BackColor.IsEmpty && dataGridViewCellStyle.BackColor.A == 255)
        {
            BackColor = dataGridViewCellStyle.BackColor;
        }

        ForeColor = dataGridViewCellStyle.ForeColor;
        TextAlign = dataGridViewCellStyle.Alignment switch
        {
            DataGridViewContentAlignment.TopCenter or DataGridViewContentAlignment.MiddleCenter or DataGridViewContentAlignment.BottomCenter => HorizontalAlignment.Center,
            DataGridViewContentAlignment.TopRight or DataGridViewContentAlignment.MiddleRight or DataGridViewContentAlignment.BottomRight => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left,
        };
    }

    /// <inheritdoc />
    public virtual bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Delete or Keys.Back || !dataGridViewWantsInputKey;

    /// <inheritdoc />
    public virtual object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context) => Text;

    /// <inheritdoc />
    public virtual void PrepareEditingControlForEdit(bool selectAll)
    {
        if (selectAll)
        {
            SelectAll();
        }
        else
        {
            Select(TextLength, 0);
        }
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        if (EditingControlDataGridView is { IsCurrentCellInEditMode: true } grid && Visible)
        {
            EditingControlValueChanged = true;
            grid.NotifyCurrentCellDirty(true);
        }
    }
}

/// <summary>The combo box that edits a grid's combo box cells.</summary>
[ToolboxItem(false)]
public class DataGridViewComboBoxEditingControl : ComboBox, IDataGridViewEditingControl
{
    private DataGridViewComboBoxCell? _cell;

    /// <summary>Creates the editing control.</summary>
    public DataGridViewComboBoxEditingControl()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        TabStop = false;
    }

    /// <inheritdoc />
    public DataGridView? EditingControlDataGridView { get; set; }

    /// <inheritdoc />
    public object EditingControlFormattedValue
    {
        get => Text;
        set
        {
            var text = value as string ?? Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
            SelectedIndex = FindStringExact(text);
        }
    }

    /// <inheritdoc />
    public int EditingControlRowIndex { get; set; }

    /// <inheritdoc />
    public bool EditingControlValueChanged { get; set; }

    /// <inheritdoc />
    public Cursor EditingPanelCursor => Cursors.Default;

    /// <inheritdoc />
    public virtual bool RepositionEditingControlOnValueChange => false;

    /// <inheritdoc />
    public virtual void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCellStyle);
        if (dataGridViewCellStyle.Font is { } font)
        {
            Font = font;
        }

        ForeColor = dataGridViewCellStyle.ForeColor;
    }

    /// <inheritdoc />
    public virtual bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown || !dataGridViewWantsInputKey;

    /// <inheritdoc />
    public virtual object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context) => Text;

    /// <inheritdoc />
    public virtual void PrepareEditingControlForEdit(bool selectAll)
    {
    }

    /// <summary>Fills the list with a cell's items.</summary>
    internal void Fill(DataGridViewComboBoxCell cell)
    {
        _cell = cell;
        Items.Clear();
        foreach (var item in cell.ListItems)
        {
            Items.Add(cell.ItemText(item));
        }
    }

    /// <inheritdoc />
    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        if (EditingControlDataGridView is { IsCurrentCellInEditMode: true } grid && _cell is not null && Visible)
        {
            EditingControlValueChanged = true;
            grid.NotifyCurrentCellDirty(true);
        }
    }

    /// <inheritdoc />
    protected override void OnDropDownClosed(EventArgs e)
    {
        base.OnDropDownClosed(e);
        if (EditingControlDataGridView is { IsCurrentCellInEditMode: true } grid && Visible)
        {
            // Picking an item commits it, so a tap is enough on a tablet.
            grid.EndEdit();
        }
    }
}
