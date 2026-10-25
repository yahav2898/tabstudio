using System.ComponentModel;

namespace System.Windows.Forms;

/// <summary>The events of a grid.</summary>
public partial class DataGridView
{
    private static readonly object EventCellBeginEdit = new();
    private static readonly object EventCellClick = new();
    private static readonly object EventCellContentClick = new();
    private static readonly object EventCellContentDoubleClick = new();
    private static readonly object EventCellDoubleClick = new();
    private static readonly object EventCellEndEdit = new();
    private static readonly object EventCellEnter = new();
    private static readonly object EventCellLeave = new();
    private static readonly object EventCellFormatting = new();
    private static readonly object EventCellParsing = new();
    private static readonly object EventCellValidating = new();
    private static readonly object EventCellValidated = new();
    private static readonly object EventCellValueChanged = new();
    private static readonly object EventCellValueNeeded = new();
    private static readonly object EventCellValuePushed = new();
    private static readonly object EventCellMouseClick = new();
    private static readonly object EventCellMouseDoubleClick = new();
    private static readonly object EventCellMouseDown = new();
    private static readonly object EventCellMouseUp = new();
    private static readonly object EventCellMouseEnter = new();
    private static readonly object EventCellMouseLeave = new();
    private static readonly object EventCellPainting = new();
    private static readonly object EventCellStateChanged = new();
    private static readonly object EventColumnAdded = new();
    private static readonly object EventColumnRemoved = new();
    private static readonly object EventColumnWidthChanged = new();
    private static readonly object EventColumnHeaderMouseClick = new();
    private static readonly object EventColumnHeaderMouseDoubleClick = new();
    private static readonly object EventRowHeaderMouseClick = new();
    private static readonly object EventRowHeaderMouseDoubleClick = new();
    private static readonly object EventCurrentCellChanged = new();
    private static readonly object EventCurrentCellDirtyStateChanged = new();
    private static readonly object EventDataBindingComplete = new();
    private static readonly object EventDataError = new();
    private static readonly object EventDataSourceChanged = new();
    private static readonly object EventDataMemberChanged = new();
    private static readonly object EventDefaultValuesNeeded = new();
    private static readonly object EventEditingControlShowing = new();
    private static readonly object EventNewRowNeeded = new();
    private static readonly object EventRowEnter = new();
    private static readonly object EventRowLeave = new();
    private static readonly object EventRowValidating = new();
    private static readonly object EventRowValidated = new();
    private static readonly object EventRowsAdded = new();
    private static readonly object EventRowsRemoved = new();
    private static readonly object EventScroll = new();
    private static readonly object EventSelectionChanged = new();
    private static readonly object EventSortCompare = new();
    private static readonly object EventSorted = new();
    private static readonly object EventUserAddedRow = new();
    private static readonly object EventUserDeletedRow = new();
    private static readonly object EventUserDeletingRow = new();

    /// <summary>Occurs before a cell is edited; handlers can cancel it.</summary>
    public event DataGridViewCellCancelEventHandler? CellBeginEdit
    {
        add => Events.AddHandler(EventCellBeginEdit, value);
        remove => Events.RemoveHandler(EventCellBeginEdit, value);
    }

    /// <summary>Occurs when any part of a cell is tapped.</summary>
    public event DataGridViewCellEventHandler? CellClick
    {
        add => Events.AddHandler(EventCellClick, value);
        remove => Events.RemoveHandler(EventCellClick, value);
    }

    /// <summary>Occurs when a cell's content, such as a button or a check box, is tapped.</summary>
    public event DataGridViewCellEventHandler? CellContentClick
    {
        add => Events.AddHandler(EventCellContentClick, value);
        remove => Events.RemoveHandler(EventCellContentClick, value);
    }

    /// <summary>Occurs when a cell's content is double-tapped.</summary>
    public event DataGridViewCellEventHandler? CellContentDoubleClick
    {
        add => Events.AddHandler(EventCellContentDoubleClick, value);
        remove => Events.RemoveHandler(EventCellContentDoubleClick, value);
    }

    /// <summary>Occurs when a cell is double-tapped.</summary>
    public event DataGridViewCellEventHandler? CellDoubleClick
    {
        add => Events.AddHandler(EventCellDoubleClick, value);
        remove => Events.RemoveHandler(EventCellDoubleClick, value);
    }

    /// <summary>Occurs when editing a cell ends.</summary>
    public event DataGridViewCellEventHandler? CellEndEdit
    {
        add => Events.AddHandler(EventCellEndEdit, value);
        remove => Events.RemoveHandler(EventCellEndEdit, value);
    }

    /// <summary>Occurs when a cell becomes the current cell.</summary>
    public event DataGridViewCellEventHandler? CellEnter
    {
        add => Events.AddHandler(EventCellEnter, value);
        remove => Events.RemoveHandler(EventCellEnter, value);
    }

    /// <summary>Occurs when a cell stops being the current cell.</summary>
    public event DataGridViewCellEventHandler? CellLeave
    {
        add => Events.AddHandler(EventCellLeave, value);
        remove => Events.RemoveHandler(EventCellLeave, value);
    }

    /// <summary>Occurs when a cell's value is formatted to be shown.</summary>
    public event DataGridViewCellFormattingEventHandler? CellFormatting
    {
        add => Events.AddHandler(EventCellFormatting, value);
        remove => Events.RemoveHandler(EventCellFormatting, value);
    }

    /// <summary>Occurs when a typed value is parsed into a cell's value.</summary>
    public event DataGridViewCellParsingEventHandler? CellParsing
    {
        add => Events.AddHandler(EventCellParsing, value);
        remove => Events.RemoveHandler(EventCellParsing, value);
    }

    /// <summary>Occurs when a cell loses focus with a typed value; handlers can reject it.</summary>
    public event DataGridViewCellValidatingEventHandler? CellValidating
    {
        add => Events.AddHandler(EventCellValidating, value);
        remove => Events.RemoveHandler(EventCellValidating, value);
    }

    /// <summary>Occurs when a cell has been validated.</summary>
    public event DataGridViewCellEventHandler? CellValidated
    {
        add => Events.AddHandler(EventCellValidated, value);
        remove => Events.RemoveHandler(EventCellValidated, value);
    }

    /// <summary>Occurs when a cell's value changes.</summary>
    public event DataGridViewCellEventHandler? CellValueChanged
    {
        add => Events.AddHandler(EventCellValueChanged, value);
        remove => Events.RemoveHandler(EventCellValueChanged, value);
    }

    /// <summary>Occurs in virtual mode when a cell's value is needed.</summary>
    public event DataGridViewCellValueEventHandler? CellValueNeeded
    {
        add => Events.AddHandler(EventCellValueNeeded, value);
        remove => Events.RemoveHandler(EventCellValueNeeded, value);
    }

    /// <summary>Occurs in virtual mode when a cell's value was changed.</summary>
    public event DataGridViewCellValueEventHandler? CellValuePushed
    {
        add => Events.AddHandler(EventCellValuePushed, value);
        remove => Events.RemoveHandler(EventCellValuePushed, value);
    }

    /// <summary>Occurs when a cell is tapped, with where.</summary>
    public event DataGridViewCellMouseEventHandler? CellMouseClick
    {
        add => Events.AddHandler(EventCellMouseClick, value);
        remove => Events.RemoveHandler(EventCellMouseClick, value);
    }

    /// <summary>Occurs when a cell is double-tapped, with where.</summary>
    public event DataGridViewCellMouseEventHandler? CellMouseDoubleClick
    {
        add => Events.AddHandler(EventCellMouseDoubleClick, value);
        remove => Events.RemoveHandler(EventCellMouseDoubleClick, value);
    }

    /// <summary>Occurs when a button or finger goes down on a cell.</summary>
    public event DataGridViewCellMouseEventHandler? CellMouseDown
    {
        add => Events.AddHandler(EventCellMouseDown, value);
        remove => Events.RemoveHandler(EventCellMouseDown, value);
    }

    /// <summary>Occurs when a button or finger is released on a cell.</summary>
    public event DataGridViewCellMouseEventHandler? CellMouseUp
    {
        add => Events.AddHandler(EventCellMouseUp, value);
        remove => Events.RemoveHandler(EventCellMouseUp, value);
    }

    /// <summary>Occurs when the pointer enters a cell.</summary>
    public event DataGridViewCellEventHandler? CellMouseEnter
    {
        add => Events.AddHandler(EventCellMouseEnter, value);
        remove => Events.RemoveHandler(EventCellMouseEnter, value);
    }

    /// <summary>Occurs when the pointer leaves a cell.</summary>
    public event DataGridViewCellEventHandler? CellMouseLeave
    {
        add => Events.AddHandler(EventCellMouseLeave, value);
        remove => Events.RemoveHandler(EventCellMouseLeave, value);
    }

    /// <summary>Occurs when a cell is drawn; handlers can draw it themselves.</summary>
    public event DataGridViewCellPaintingEventHandler? CellPainting
    {
        add => Events.AddHandler(EventCellPainting, value);
        remove => Events.RemoveHandler(EventCellPainting, value);
    }

    /// <summary>Occurs when a cell's state, such as its selection, changes.</summary>
    public event DataGridViewCellStateChangedEventHandler? CellStateChanged
    {
        add => Events.AddHandler(EventCellStateChanged, value);
        remove => Events.RemoveHandler(EventCellStateChanged, value);
    }

    /// <summary>Occurs when a column is added.</summary>
    public event DataGridViewColumnEventHandler? ColumnAdded
    {
        add => Events.AddHandler(EventColumnAdded, value);
        remove => Events.RemoveHandler(EventColumnAdded, value);
    }

    /// <summary>Occurs when a column is removed.</summary>
    public event DataGridViewColumnEventHandler? ColumnRemoved
    {
        add => Events.AddHandler(EventColumnRemoved, value);
        remove => Events.RemoveHandler(EventColumnRemoved, value);
    }

    /// <summary>Occurs when a column's width changes.</summary>
    public event DataGridViewColumnEventHandler? ColumnWidthChanged
    {
        add => Events.AddHandler(EventColumnWidthChanged, value);
        remove => Events.RemoveHandler(EventColumnWidthChanged, value);
    }

    /// <summary>Occurs when a column header is tapped.</summary>
    public event DataGridViewCellMouseEventHandler? ColumnHeaderMouseClick
    {
        add => Events.AddHandler(EventColumnHeaderMouseClick, value);
        remove => Events.RemoveHandler(EventColumnHeaderMouseClick, value);
    }

    /// <summary>Occurs when a column header is double-tapped.</summary>
    public event DataGridViewCellMouseEventHandler? ColumnHeaderMouseDoubleClick
    {
        add => Events.AddHandler(EventColumnHeaderMouseDoubleClick, value);
        remove => Events.RemoveHandler(EventColumnHeaderMouseDoubleClick, value);
    }

    /// <summary>Occurs when a row header is tapped.</summary>
    public event DataGridViewCellMouseEventHandler? RowHeaderMouseClick
    {
        add => Events.AddHandler(EventRowHeaderMouseClick, value);
        remove => Events.RemoveHandler(EventRowHeaderMouseClick, value);
    }

    /// <summary>Occurs when a row header is double-tapped.</summary>
    public event DataGridViewCellMouseEventHandler? RowHeaderMouseDoubleClick
    {
        add => Events.AddHandler(EventRowHeaderMouseDoubleClick, value);
        remove => Events.RemoveHandler(EventRowHeaderMouseDoubleClick, value);
    }

    /// <summary>Occurs when the current cell changes.</summary>
    public event EventHandler? CurrentCellChanged
    {
        add => Events.AddHandler(EventCurrentCellChanged, value);
        remove => Events.RemoveHandler(EventCurrentCellChanged, value);
    }

    /// <summary>Occurs when the current cell's value is edited, or the edit is committed or canceled.</summary>
    public event EventHandler? CurrentCellDirtyStateChanged
    {
        add => Events.AddHandler(EventCurrentCellDirtyStateChanged, value);
        remove => Events.RemoveHandler(EventCurrentCellDirtyStateChanged, value);
    }

    /// <summary>Occurs when the grid has bound to its data source.</summary>
    public event DataGridViewBindingCompleteEventHandler? DataBindingComplete
    {
        add => Events.AddHandler(EventDataBindingComplete, value);
        remove => Events.RemoveHandler(EventDataBindingComplete, value);
    }

    /// <summary>Occurs when a value cannot be formatted, parsed or committed.</summary>
    public event DataGridViewDataErrorEventHandler? DataError
    {
        add => Events.AddHandler(EventDataError, value);
        remove => Events.RemoveHandler(EventDataError, value);
    }

    /// <summary>Occurs when the data source changes.</summary>
    public event EventHandler? DataSourceChanged
    {
        add => Events.AddHandler(EventDataSourceChanged, value);
        remove => Events.RemoveHandler(EventDataSourceChanged, value);
    }

    /// <summary>Occurs when the data member changes.</summary>
    public event EventHandler? DataMemberChanged
    {
        add => Events.AddHandler(EventDataMemberChanged, value);
        remove => Events.RemoveHandler(EventDataMemberChanged, value);
    }

    /// <summary>Occurs when the user starts a new row, to fill its default values.</summary>
    public event DataGridViewRowEventHandler? DefaultValuesNeeded
    {
        add => Events.AddHandler(EventDefaultValuesNeeded, value);
        remove => Events.RemoveHandler(EventDefaultValuesNeeded, value);
    }

    /// <summary>Occurs when the control that edits a cell is shown; handlers can subscribe to its events.</summary>
    public event DataGridViewEditingControlShowingEventHandler? EditingControlShowing
    {
        add => Events.AddHandler(EventEditingControlShowing, value);
        remove => Events.RemoveHandler(EventEditingControlShowing, value);
    }

    /// <summary>Occurs in virtual mode when the user starts a new row.</summary>
    public event DataGridViewRowEventHandler? NewRowNeeded
    {
        add => Events.AddHandler(EventNewRowNeeded, value);
        remove => Events.RemoveHandler(EventNewRowNeeded, value);
    }

    /// <summary>Occurs when a row becomes the current row.</summary>
    public event DataGridViewCellEventHandler? RowEnter
    {
        add => Events.AddHandler(EventRowEnter, value);
        remove => Events.RemoveHandler(EventRowEnter, value);
    }

    /// <summary>Occurs when a row stops being the current row.</summary>
    public event DataGridViewCellEventHandler? RowLeave
    {
        add => Events.AddHandler(EventRowLeave, value);
        remove => Events.RemoveHandler(EventRowLeave, value);
    }

    /// <summary>Occurs when the current row changes, to validate the row left; handlers can cancel it.</summary>
    public event DataGridViewCellCancelEventHandler? RowValidating
    {
        add => Events.AddHandler(EventRowValidating, value);
        remove => Events.RemoveHandler(EventRowValidating, value);
    }

    /// <summary>Occurs when a row has been validated.</summary>
    public event DataGridViewCellEventHandler? RowValidated
    {
        add => Events.AddHandler(EventRowValidated, value);
        remove => Events.RemoveHandler(EventRowValidated, value);
    }

    /// <summary>Occurs when rows are added.</summary>
    public event DataGridViewRowsAddedEventHandler? RowsAdded
    {
        add => Events.AddHandler(EventRowsAdded, value);
        remove => Events.RemoveHandler(EventRowsAdded, value);
    }

    /// <summary>Occurs when rows are removed.</summary>
    public event DataGridViewRowsRemovedEventHandler? RowsRemoved
    {
        add => Events.AddHandler(EventRowsRemoved, value);
        remove => Events.RemoveHandler(EventRowsRemoved, value);
    }

    /// <summary>Occurs when the grid scrolls.</summary>
    public event ScrollEventHandler? Scroll
    {
        add => Events.AddHandler(EventScroll, value);
        remove => Events.RemoveHandler(EventScroll, value);
    }

    /// <summary>Occurs when the selection changes.</summary>
    public event EventHandler? SelectionChanged
    {
        add => Events.AddHandler(EventSelectionChanged, value);
        remove => Events.RemoveHandler(EventSelectionChanged, value);
    }

    /// <summary>Occurs when two cells are compared to sort the rows; handlers can compare them.</summary>
    public event DataGridViewSortCompareEventHandler? SortCompare
    {
        add => Events.AddHandler(EventSortCompare, value);
        remove => Events.RemoveHandler(EventSortCompare, value);
    }

    /// <summary>Occurs when the rows have been sorted.</summary>
    public event EventHandler? Sorted
    {
        add => Events.AddHandler(EventSorted, value);
        remove => Events.RemoveHandler(EventSorted, value);
    }

    /// <summary>Occurs when the user adds a row, in the row for new records.</summary>
    public event DataGridViewRowEventHandler? UserAddedRow
    {
        add => Events.AddHandler(EventUserAddedRow, value);
        remove => Events.RemoveHandler(EventUserAddedRow, value);
    }

    /// <summary>Occurs when the user has deleted a row.</summary>
    public event DataGridViewRowEventHandler? UserDeletedRow
    {
        add => Events.AddHandler(EventUserDeletedRow, value);
        remove => Events.RemoveHandler(EventUserDeletedRow, value);
    }

    /// <summary>Occurs before the user deletes a row; handlers can cancel it.</summary>
    public event DataGridViewRowCancelEventHandler? UserDeletingRow
    {
        add => Events.AddHandler(EventUserDeletingRow, value);
        remove => Events.RemoveHandler(EventUserDeletingRow, value);
    }

    /// <summary>Raises CellBeginEdit.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellBeginEdit(DataGridViewCellCancelEventArgs e) => ((DataGridViewCellCancelEventHandler?)Events[EventCellBeginEdit])?.Invoke(this, e);

    /// <summary>Raises CellClick.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellClick(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventCellClick])?.Invoke(this, e);

    /// <summary>Raises CellContentClick.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellContentClick(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventCellContentClick])?.Invoke(this, e);

    /// <summary>Raises CellContentDoubleClick.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellContentDoubleClick(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventCellContentDoubleClick])?.Invoke(this, e);

    /// <summary>Raises CellDoubleClick.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellDoubleClick(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventCellDoubleClick])?.Invoke(this, e);

    /// <summary>Raises CellEndEdit.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellEndEdit(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventCellEndEdit])?.Invoke(this, e);

    /// <summary>Raises CellEnter.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellEnter(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventCellEnter])?.Invoke(this, e);

    /// <summary>Raises CellLeave.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellLeave(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventCellLeave])?.Invoke(this, e);

    /// <summary>Raises CellFormatting.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellFormatting(DataGridViewCellFormattingEventArgs e) => ((DataGridViewCellFormattingEventHandler?)Events[EventCellFormatting])?.Invoke(this, e);

    /// <summary>Raises CellParsing.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellParsing(DataGridViewCellParsingEventArgs e) => ((DataGridViewCellParsingEventHandler?)Events[EventCellParsing])?.Invoke(this, e);

    /// <summary>Raises CellValidating.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellValidating(DataGridViewCellValidatingEventArgs e) => ((DataGridViewCellValidatingEventHandler?)Events[EventCellValidating])?.Invoke(this, e);

    /// <summary>Raises CellValidated.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellValidated(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventCellValidated])?.Invoke(this, e);

    /// <summary>Raises CellValueChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellValueChanged(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventCellValueChanged])?.Invoke(this, e);

    /// <summary>Raises CellValueNeeded.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellValueNeeded(DataGridViewCellValueEventArgs e) => ((DataGridViewCellValueEventHandler?)Events[EventCellValueNeeded])?.Invoke(this, e);

    /// <summary>Raises CellValuePushed.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellValuePushed(DataGridViewCellValueEventArgs e) => ((DataGridViewCellValueEventHandler?)Events[EventCellValuePushed])?.Invoke(this, e);

    /// <summary>Raises CellMouseClick.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellMouseClick(DataGridViewCellMouseEventArgs e) => ((DataGridViewCellMouseEventHandler?)Events[EventCellMouseClick])?.Invoke(this, e);

    /// <summary>Raises CellMouseDoubleClick.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellMouseDoubleClick(DataGridViewCellMouseEventArgs e) => ((DataGridViewCellMouseEventHandler?)Events[EventCellMouseDoubleClick])?.Invoke(this, e);

    /// <summary>Raises CellMouseDown.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellMouseDown(DataGridViewCellMouseEventArgs e) => ((DataGridViewCellMouseEventHandler?)Events[EventCellMouseDown])?.Invoke(this, e);

    /// <summary>Raises CellMouseUp.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellMouseUp(DataGridViewCellMouseEventArgs e) => ((DataGridViewCellMouseEventHandler?)Events[EventCellMouseUp])?.Invoke(this, e);

    /// <summary>Raises CellMouseEnter.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellMouseEnter(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventCellMouseEnter])?.Invoke(this, e);

    /// <summary>Raises CellMouseLeave.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellMouseLeave(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventCellMouseLeave])?.Invoke(this, e);

    /// <summary>Raises CellPainting.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellPainting(DataGridViewCellPaintingEventArgs e) => ((DataGridViewCellPaintingEventHandler?)Events[EventCellPainting])?.Invoke(this, e);

    /// <summary>Raises CellStateChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellStateChanged(DataGridViewCellStateChangedEventArgs e) => ((DataGridViewCellStateChangedEventHandler?)Events[EventCellStateChanged])?.Invoke(this, e);

    /// <summary>Raises ColumnAdded.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnColumnAdded(DataGridViewColumnEventArgs e) => ((DataGridViewColumnEventHandler?)Events[EventColumnAdded])?.Invoke(this, e);

    /// <summary>Raises ColumnRemoved.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnColumnRemoved(DataGridViewColumnEventArgs e) => ((DataGridViewColumnEventHandler?)Events[EventColumnRemoved])?.Invoke(this, e);

    /// <summary>Raises ColumnWidthChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnColumnWidthChanged(DataGridViewColumnEventArgs e) => ((DataGridViewColumnEventHandler?)Events[EventColumnWidthChanged])?.Invoke(this, e);

    /// <summary>Raises ColumnHeaderMouseClick.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnColumnHeaderMouseClick(DataGridViewCellMouseEventArgs e) => ((DataGridViewCellMouseEventHandler?)Events[EventColumnHeaderMouseClick])?.Invoke(this, e);

    /// <summary>Raises ColumnHeaderMouseDoubleClick.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnColumnHeaderMouseDoubleClick(DataGridViewCellMouseEventArgs e) => ((DataGridViewCellMouseEventHandler?)Events[EventColumnHeaderMouseDoubleClick])?.Invoke(this, e);

    /// <summary>Raises RowHeaderMouseClick.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnRowHeaderMouseClick(DataGridViewCellMouseEventArgs e) => ((DataGridViewCellMouseEventHandler?)Events[EventRowHeaderMouseClick])?.Invoke(this, e);

    /// <summary>Raises RowHeaderMouseDoubleClick.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnRowHeaderMouseDoubleClick(DataGridViewCellMouseEventArgs e) => ((DataGridViewCellMouseEventHandler?)Events[EventRowHeaderMouseDoubleClick])?.Invoke(this, e);

    /// <summary>Raises CurrentCellChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCurrentCellChanged(EventArgs e) => ((EventHandler?)Events[EventCurrentCellChanged])?.Invoke(this, e);

    /// <summary>Raises CurrentCellDirtyStateChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCurrentCellDirtyStateChanged(EventArgs e) => ((EventHandler?)Events[EventCurrentCellDirtyStateChanged])?.Invoke(this, e);

    /// <summary>Raises DataBindingComplete.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDataBindingComplete(DataGridViewBindingCompleteEventArgs e) => ((DataGridViewBindingCompleteEventHandler?)Events[EventDataBindingComplete])?.Invoke(this, e);

    /// <summary>Raises DataError.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDataError(DataGridViewDataErrorEventArgs e) => ((DataGridViewDataErrorEventHandler?)Events[EventDataError])?.Invoke(this, e);

    /// <summary>Raises DataSourceChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDataSourceChanged(EventArgs e) => ((EventHandler?)Events[EventDataSourceChanged])?.Invoke(this, e);

    /// <summary>Raises DataMemberChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDataMemberChanged(EventArgs e) => ((EventHandler?)Events[EventDataMemberChanged])?.Invoke(this, e);

    /// <summary>Raises DefaultValuesNeeded.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDefaultValuesNeeded(DataGridViewRowEventArgs e) => ((DataGridViewRowEventHandler?)Events[EventDefaultValuesNeeded])?.Invoke(this, e);

    /// <summary>Raises EditingControlShowing.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnEditingControlShowing(DataGridViewEditingControlShowingEventArgs e) => ((DataGridViewEditingControlShowingEventHandler?)Events[EventEditingControlShowing])?.Invoke(this, e);

    /// <summary>Raises NewRowNeeded.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnNewRowNeeded(DataGridViewRowEventArgs e) => ((DataGridViewRowEventHandler?)Events[EventNewRowNeeded])?.Invoke(this, e);

    /// <summary>Raises RowEnter.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnRowEnter(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventRowEnter])?.Invoke(this, e);

    /// <summary>Raises RowLeave.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnRowLeave(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventRowLeave])?.Invoke(this, e);

    /// <summary>Raises RowValidating.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnRowValidating(DataGridViewCellCancelEventArgs e) => ((DataGridViewCellCancelEventHandler?)Events[EventRowValidating])?.Invoke(this, e);

    /// <summary>Raises RowValidated.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnRowValidated(DataGridViewCellEventArgs e) => ((DataGridViewCellEventHandler?)Events[EventRowValidated])?.Invoke(this, e);

    /// <summary>Raises RowsAdded.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnRowsAdded(DataGridViewRowsAddedEventArgs e) => ((DataGridViewRowsAddedEventHandler?)Events[EventRowsAdded])?.Invoke(this, e);

    /// <summary>Raises RowsRemoved.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnRowsRemoved(DataGridViewRowsRemovedEventArgs e) => ((DataGridViewRowsRemovedEventHandler?)Events[EventRowsRemoved])?.Invoke(this, e);

    /// <summary>Raises Scroll.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnScroll(ScrollEventArgs e) => ((ScrollEventHandler?)Events[EventScroll])?.Invoke(this, e);

    /// <summary>Raises SelectionChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSelectionChanged(EventArgs e) => ((EventHandler?)Events[EventSelectionChanged])?.Invoke(this, e);

    /// <summary>Raises SortCompare.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSortCompare(DataGridViewSortCompareEventArgs e) => ((DataGridViewSortCompareEventHandler?)Events[EventSortCompare])?.Invoke(this, e);

    /// <summary>Raises Sorted.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSorted(EventArgs e) => ((EventHandler?)Events[EventSorted])?.Invoke(this, e);

    /// <summary>Raises UserAddedRow.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnUserAddedRow(DataGridViewRowEventArgs e) => ((DataGridViewRowEventHandler?)Events[EventUserAddedRow])?.Invoke(this, e);

    /// <summary>Raises UserDeletedRow.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnUserDeletedRow(DataGridViewRowEventArgs e) => ((DataGridViewRowEventHandler?)Events[EventUserDeletedRow])?.Invoke(this, e);

    /// <summary>Raises UserDeletingRow.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnUserDeletingRow(DataGridViewRowCancelEventArgs e) => ((DataGridViewRowCancelEventHandler?)Events[EventUserDeletingRow])?.Invoke(this, e);
}
