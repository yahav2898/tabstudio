using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>Data about a cell of a grid.</summary>
/// <param name="columnIndex">The cell's column.</param>
/// <param name="rowIndex">The cell's row.</param>
public class DataGridViewCellEventArgs(int columnIndex, int rowIndex) : EventArgs
{
    /// <summary>The cell's column.</summary>
    public int ColumnIndex { get; } = columnIndex;

    /// <summary>The cell's row.</summary>
    public int RowIndex { get; } = rowIndex;
}

/// <summary>Data about a cell of a grid, which handlers can cancel.</summary>
/// <param name="columnIndex">The cell's column.</param>
/// <param name="rowIndex">The cell's row.</param>
public class DataGridViewCellCancelEventArgs(int columnIndex, int rowIndex) : CancelEventArgs
{
    /// <summary>The cell's column.</summary>
    public int ColumnIndex { get; } = columnIndex;

    /// <summary>The cell's row.</summary>
    public int RowIndex { get; } = rowIndex;
}

/// <summary>Data about a cell being validated.</summary>
/// <param name="columnIndex">The cell's column.</param>
/// <param name="rowIndex">The cell's row.</param>
/// <param name="formattedValue">The value as typed.</param>
public class DataGridViewCellValidatingEventArgs(int columnIndex, int rowIndex, object? formattedValue) : CancelEventArgs
{
    /// <summary>The cell's column.</summary>
    public int ColumnIndex { get; } = columnIndex;

    /// <summary>The cell's row.</summary>
    public int RowIndex { get; } = rowIndex;

    /// <summary>The value as typed.</summary>
    public object? FormattedValue { get; } = formattedValue;
}

/// <summary>Data about a value being formatted for a cell.</summary>
/// <param name="columnIndex">The cell's column.</param>
/// <param name="rowIndex">The cell's row.</param>
/// <param name="value">The value.</param>
/// <param name="desiredType">The type wanted.</param>
/// <param name="cellStyle">The cell's style, which handlers may change.</param>
public class DataGridViewCellFormattingEventArgs(int columnIndex, int rowIndex, object? value, Type? desiredType, DataGridViewCellStyle cellStyle) : ConvertEventArgs(value, desiredType)
{
    /// <summary>The cell's column.</summary>
    public int ColumnIndex { get; } = columnIndex;

    /// <summary>The cell's row.</summary>
    public int RowIndex { get; } = rowIndex;

    /// <summary>The cell's style, which handlers may change.</summary>
    public DataGridViewCellStyle CellStyle { get; set; } = cellStyle;

    /// <summary>Whether the handler formatted the value, so the grid does not.</summary>
    public bool FormattingApplied { get; set; }
}

/// <summary>Data about a typed value being parsed for a cell.</summary>
/// <param name="rowIndex">The cell's row.</param>
/// <param name="columnIndex">The cell's column.</param>
/// <param name="value">The typed value.</param>
/// <param name="desiredType">The type wanted.</param>
/// <param name="inheritedCellStyle">The cell's style.</param>
public class DataGridViewCellParsingEventArgs(int rowIndex, int columnIndex, object? value, Type? desiredType, DataGridViewCellStyle inheritedCellStyle) : ConvertEventArgs(value, desiredType)
{
    /// <summary>The cell's row.</summary>
    public int RowIndex { get; } = rowIndex;

    /// <summary>The cell's column.</summary>
    public int ColumnIndex { get; } = columnIndex;

    /// <summary>The cell's style.</summary>
    public DataGridViewCellStyle InheritedCellStyle { get; set; } = inheritedCellStyle;

    /// <summary>Whether the handler parsed the value, so the grid does not.</summary>
    public bool ParsingApplied { get; set; }
}

/// <summary>Data about a value a grid could not format, parse or commit.</summary>
/// <param name="exception">The error.</param>
/// <param name="columnIndex">The cell's column.</param>
/// <param name="rowIndex">The cell's row.</param>
/// <param name="context">What the grid was doing.</param>
public class DataGridViewDataErrorEventArgs(Exception? exception, int columnIndex, int rowIndex, DataGridViewDataErrorContexts context) : DataGridViewCellCancelEventArgs(columnIndex, rowIndex)
{
    /// <summary>The error.</summary>
    public Exception? Exception { get; } = exception;

    /// <summary>What the grid was doing.</summary>
    public DataGridViewDataErrorContexts Context { get; } = context;

    /// <summary>Whether the grid throws the error after the handlers.</summary>
    public bool ThrowException { get; set; }
}

/// <summary>Data about a row of a grid.</summary>
/// <param name="dataGridViewRow">The row.</param>
public class DataGridViewRowEventArgs(DataGridViewRow dataGridViewRow) : EventArgs
{
    /// <summary>The row.</summary>
    public DataGridViewRow Row { get; } = dataGridViewRow;
}

/// <summary>Data about a row of a grid, which handlers can cancel.</summary>
/// <param name="dataGridViewRow">The row.</param>
public class DataGridViewRowCancelEventArgs(DataGridViewRow? dataGridViewRow) : CancelEventArgs
{
    /// <summary>The row.</summary>
    public DataGridViewRow? Row { get; } = dataGridViewRow;
}

/// <summary>Data about rows added to a grid.</summary>
/// <param name="rowIndex">The first row.</param>
/// <param name="rowCount">The number of rows.</param>
public class DataGridViewRowsAddedEventArgs(int rowIndex, int rowCount) : EventArgs
{
    /// <summary>The first row.</summary>
    public int RowIndex { get; } = rowIndex;

    /// <summary>The number of rows.</summary>
    public int RowCount { get; } = rowCount;
}

/// <summary>Data about rows removed from a grid.</summary>
/// <param name="rowIndex">The first row.</param>
/// <param name="rowCount">The number of rows.</param>
public class DataGridViewRowsRemovedEventArgs(int rowIndex, int rowCount) : EventArgs
{
    /// <summary>The first row.</summary>
    public int RowIndex { get; } = rowIndex;

    /// <summary>The number of rows.</summary>
    public int RowCount { get; } = rowCount;
}

/// <summary>Data about a column of a grid.</summary>
/// <param name="dataGridViewColumn">The column.</param>
public class DataGridViewColumnEventArgs(DataGridViewColumn dataGridViewColumn) : EventArgs
{
    /// <summary>The column.</summary>
    public DataGridViewColumn Column { get; } = dataGridViewColumn;
}

/// <summary>Data about a press or tap on a cell of a grid.</summary>
/// <param name="columnIndex">The cell's column, or -1 for a row header.</param>
/// <param name="rowIndex">The cell's row, or -1 for a column header.</param>
/// <param name="localX">The x coordinate in the cell.</param>
/// <param name="localY">The y coordinate in the cell.</param>
/// <param name="e">The pointer's data.</param>
public class DataGridViewCellMouseEventArgs(int columnIndex, int rowIndex, int localX, int localY, MouseEventArgs e)
    : MouseEventArgs(e.Button, e.Clicks, localX, localY, e.Delta)
{
    /// <summary>The cell's column, or -1 for a row header.</summary>
    public int ColumnIndex { get; } = columnIndex;

    /// <summary>The cell's row, or -1 for a column header.</summary>
    public int RowIndex { get; } = rowIndex;
}

/// <summary>Data for comparing two cells when a grid sorts.</summary>
/// <param name="dataGridViewColumn">The column.</param>
/// <param name="cellValue1">The first value.</param>
/// <param name="cellValue2">The second value.</param>
/// <param name="rowIndex1">The first row.</param>
/// <param name="rowIndex2">The second row.</param>
public class DataGridViewSortCompareEventArgs(DataGridViewColumn dataGridViewColumn, object? cellValue1, object? cellValue2, int rowIndex1, int rowIndex2) : HandledEventArgs
{
    /// <summary>The column.</summary>
    public DataGridViewColumn Column { get; } = dataGridViewColumn;

    /// <summary>The first value.</summary>
    public object? CellValue1 { get; } = cellValue1;

    /// <summary>The second value.</summary>
    public object? CellValue2 { get; } = cellValue2;

    /// <summary>The first row.</summary>
    public int RowIndex1 { get; } = rowIndex1;

    /// <summary>The second row.</summary>
    public int RowIndex2 { get; } = rowIndex2;

    /// <summary>The comparison: less than, equal to or greater than zero.</summary>
    public int SortResult { get; set; }
}

/// <summary>Data about the control a grid shows to edit a cell.</summary>
/// <param name="control">The control.</param>
/// <param name="cellStyle">The cell's style.</param>
public class DataGridViewEditingControlShowingEventArgs(Control control, DataGridViewCellStyle cellStyle) : EventArgs
{
    /// <summary>The control, such as a text box, whose events handlers can subscribe to.</summary>
    public Control Control { get; } = control;

    /// <summary>The cell's style.</summary>
    public DataGridViewCellStyle CellStyle { get; set; } = cellStyle;
}

/// <summary>Data about a cell's state change.</summary>
/// <param name="dataGridViewCell">The cell.</param>
/// <param name="stateChanged">The state that changed.</param>
public class DataGridViewCellStateChangedEventArgs(DataGridViewCell dataGridViewCell, DataGridViewElementStates stateChanged) : EventArgs
{
    /// <summary>The cell.</summary>
    public DataGridViewCell Cell { get; } = dataGridViewCell;

    /// <summary>The state that changed.</summary>
    public DataGridViewElementStates StateChanged { get; } = stateChanged;
}

/// <summary>Data for drawing a cell, which handlers can draw themselves.</summary>
public class DataGridViewCellPaintingEventArgs : HandledEventArgs
{
    internal DataGridViewCellPaintingEventArgs(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, int columnIndex, DataGridViewElementStates cellState, object? value, object? formattedValue, DataGridViewCellStyle cellStyle, Action<DataGridViewPaintParts> paint)
    {
        Graphics = graphics;
        ClipBounds = clipBounds;
        CellBounds = cellBounds;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        State = cellState;
        Value = value;
        FormattedValue = formattedValue;
        CellStyle = cellStyle;
        PaintAction = paint;
    }

    /// <summary>The surface.</summary>
    public Graphics Graphics { get; }

    /// <summary>The area being drawn.</summary>
    public Rectangle ClipBounds { get; }

    /// <summary>The cell's bounds.</summary>
    public Rectangle CellBounds { get; }

    /// <summary>The cell's row, or -1 for a column header.</summary>
    public int RowIndex { get; }

    /// <summary>The cell's column, or -1 for a row header.</summary>
    public int ColumnIndex { get; }

    /// <summary>The cell's state.</summary>
    public DataGridViewElementStates State { get; }

    /// <summary>The cell's value.</summary>
    public object? Value { get; }

    /// <summary>The cell's value as shown.</summary>
    public object? FormattedValue { get; }

    /// <summary>The cell's style.</summary>
    public DataGridViewCellStyle CellStyle { get; }

    /// <summary>The parts to draw.</summary>
    public DataGridViewPaintParts PaintParts { get; } = DataGridViewPaintParts.All;

    private Action<DataGridViewPaintParts> PaintAction { get; }

    /// <summary>Draws the parts of the cell the grid would.</summary>
    /// <param name="clipBounds">The area being drawn.</param>
    /// <param name="paintParts">The parts.</param>
    public void Paint(Rectangle clipBounds, DataGridViewPaintParts paintParts) => PaintAction(paintParts);

    /// <summary>Draws the cell's background.</summary>
    /// <param name="clipBounds">The area being drawn.</param>
    /// <param name="cellsPaintSelectionBackground">Whether to draw the selection.</param>
    public void PaintBackground(Rectangle clipBounds, bool cellsPaintSelectionBackground) =>
        PaintAction(DataGridViewPaintParts.Background | DataGridViewPaintParts.Border | (cellsPaintSelectionBackground ? DataGridViewPaintParts.SelectionBackground : DataGridViewPaintParts.None));

    /// <summary>Draws the cell's content.</summary>
    /// <param name="clipBounds">The area being drawn.</param>
    public void PaintContent(Rectangle clipBounds) => PaintAction(DataGridViewPaintParts.ContentForeground);
}

/// <summary>Handles a cell event.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewCellEventHandler(object? sender, DataGridViewCellEventArgs e);

/// <summary>Handles a cell event that can be canceled.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewCellCancelEventHandler(object? sender, DataGridViewCellCancelEventArgs e);

/// <summary>Handles validating a cell.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewCellValidatingEventHandler(object? sender, DataGridViewCellValidatingEventArgs e);

/// <summary>Handles formatting a cell's value.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewCellFormattingEventHandler(object? sender, DataGridViewCellFormattingEventArgs e);

/// <summary>Handles parsing a cell's typed value.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewCellParsingEventHandler(object? sender, DataGridViewCellParsingEventArgs e);

/// <summary>Handles a data error.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewDataErrorEventHandler(object? sender, DataGridViewDataErrorEventArgs e);

/// <summary>Handles a row event.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewRowEventHandler(object? sender, DataGridViewRowEventArgs e);

/// <summary>Handles a row event that can be canceled.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewRowCancelEventHandler(object? sender, DataGridViewRowCancelEventArgs e);

/// <summary>Handles rows added.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewRowsAddedEventHandler(object? sender, DataGridViewRowsAddedEventArgs e);

/// <summary>Handles rows removed.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewRowsRemovedEventHandler(object? sender, DataGridViewRowsRemovedEventArgs e);

/// <summary>Handles a column event.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewColumnEventHandler(object? sender, DataGridViewColumnEventArgs e);

/// <summary>Handles a press or tap on a cell.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewCellMouseEventHandler(object? sender, DataGridViewCellMouseEventArgs e);

/// <summary>Handles comparing cells for sorting.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewSortCompareEventHandler(object? sender, DataGridViewSortCompareEventArgs e);

/// <summary>Handles the editing control being shown.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewEditingControlShowingEventHandler(object? sender, DataGridViewEditingControlShowingEventArgs e);

/// <summary>Handles a cell's state change.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewCellStateChangedEventHandler(object? sender, DataGridViewCellStateChangedEventArgs e);

/// <summary>Handles drawing a cell.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewCellPaintingEventHandler(object? sender, DataGridViewCellPaintingEventArgs e);

/// <summary>Data about a cell's value in a virtual grid.</summary>
/// <param name="columnIndex">The cell's column.</param>
/// <param name="rowIndex">The cell's row.</param>
public class DataGridViewCellValueEventArgs(int columnIndex, int rowIndex) : EventArgs
{
    /// <summary>The cell's column.</summary>
    public int ColumnIndex { get; } = columnIndex;

    /// <summary>The cell's row.</summary>
    public int RowIndex { get; } = rowIndex;

    /// <summary>The value; CellValueNeeded handlers set it.</summary>
    public object? Value { get; set; }
}

/// <summary>Handles a cell's value in a virtual grid.</summary>
/// <param name="sender">The grid.</param>
/// <param name="e">The event data.</param>
public delegate void DataGridViewCellValueEventHandler(object? sender, DataGridViewCellValueEventArgs e);
