namespace System.Windows.Forms;

/// <summary>How a grid selects.</summary>
public enum DataGridViewSelectionMode
{
    /// <summary>Cells.</summary>
    CellSelect = 0,

    /// <summary>Whole rows.</summary>
    FullRowSelect = 1,

    /// <summary>Whole columns.</summary>
    FullColumnSelect = 2,

    /// <summary>Rows from their headers, cells otherwise.</summary>
    RowHeaderSelect = 3,

    /// <summary>Columns from their headers, cells otherwise.</summary>
    ColumnHeaderSelect = 4,
}

/// <summary>When editing a cell starts.</summary>
public enum DataGridViewEditMode
{
    /// <summary>When the cell becomes current.</summary>
    EditOnEnter = 0,

    /// <summary>On a key or F2.</summary>
    EditOnKeystroke = 1,

    /// <summary>On a key or F2, or a tap on the current cell.</summary>
    EditOnKeystrokeOrF2 = 2,

    /// <summary>On F2.</summary>
    EditOnF2 = 3,

    /// <summary>Only when the app calls BeginEdit.</summary>
    EditProgrammatically = 4,
}

/// <summary>How a grid sizes its columns.</summary>
public enum DataGridViewAutoSizeColumnsMode
{
    /// <summary>None.</summary>
    None = 1,

    /// <summary>To the header.</summary>
    ColumnHeader = 2,

    /// <summary>To every cell except the header.</summary>
    AllCellsExceptHeader = 4,

    /// <summary>To every cell.</summary>
    AllCells = 6,

    /// <summary>To the shown cells except the header.</summary>
    DisplayedCellsExceptHeader = 8,

    /// <summary>To the shown cells.</summary>
    DisplayedCells = 10,

    /// <summary>To fill the grid's width.</summary>
    Fill = 16,
}

/// <summary>How a column sizes itself.</summary>
public enum DataGridViewAutoSizeColumnMode
{
    /// <summary>The grid's mode.</summary>
    NotSet = 0,

    /// <summary>None.</summary>
    None = 1,

    /// <summary>To the header.</summary>
    ColumnHeader = 2,

    /// <summary>To every cell except the header.</summary>
    AllCellsExceptHeader = 4,

    /// <summary>To every cell.</summary>
    AllCells = 6,

    /// <summary>To the shown cells except the header.</summary>
    DisplayedCellsExceptHeader = 8,

    /// <summary>To the shown cells.</summary>
    DisplayedCells = 10,

    /// <summary>To fill the grid's width.</summary>
    Fill = 16,
}

/// <summary>How a grid sizes its rows. Rows keep their height.</summary>
public enum DataGridViewAutoSizeRowsMode
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Header and columns.</summary>
    AllHeaders = 5,

    /// <summary>Every cell except the header.</summary>
    AllCellsExceptHeaders = 6,

    /// <summary>Every cell.</summary>
    AllCells = 7,

    /// <summary>Shown headers.</summary>
    DisplayedHeaders = 9,

    /// <summary>Shown cells except headers.</summary>
    DisplayedCellsExceptHeaders = 10,

    /// <summary>Shown cells.</summary>
    DisplayedCells = 11,
}

/// <summary>How a column sorts.</summary>
public enum DataGridViewColumnSortMode
{
    /// <summary>It does not.</summary>
    NotSortable = 0,

    /// <summary>A tap on its header sorts.</summary>
    Automatic = 1,

    /// <summary>The app sorts.</summary>
    Programmatic = 2,
}

/// <summary>How the column headers' height is set.</summary>
public enum DataGridViewColumnHeadersHeightSizeMode
{
    /// <summary>The user can resize it.</summary>
    EnableResizing = 0,

    /// <summary>It is fixed.</summary>
    DisableResizing = 1,

    /// <summary>It fits the headers.</summary>
    AutoSize = 2,
}

/// <summary>How the row headers' width is set.</summary>
public enum DataGridViewRowHeadersWidthSizeMode
{
    /// <summary>The user can resize it.</summary>
    EnableResizing = 0,

    /// <summary>It is fixed.</summary>
    DisableResizing = 1,

    /// <summary>To every header.</summary>
    AutoSizeToAllHeaders = 2,

    /// <summary>To the shown headers.</summary>
    AutoSizeToDisplayedHeaders = 3,

    /// <summary>To the first header.</summary>
    AutoSizeToFirstHeader = 4,
}

/// <summary>The border of a grid's headers.</summary>
public enum DataGridViewHeaderBorderStyle
{
    /// <summary>Custom.</summary>
    Custom = 0,

    /// <summary>Single.</summary>
    Single = 1,

    /// <summary>Raised.</summary>
    Raised = 2,

    /// <summary>Sunken.</summary>
    Sunken = 3,

    /// <summary>None.</summary>
    None = 4,
}

/// <summary>The borders between a grid's cells.</summary>
public enum DataGridViewCellBorderStyle
{
    /// <summary>Custom.</summary>
    Custom = 0,

    /// <summary>Single.</summary>
    Single = 1,

    /// <summary>Raised.</summary>
    Raised = 2,

    /// <summary>Sunken.</summary>
    Sunken = 3,

    /// <summary>None.</summary>
    None = 4,

    /// <summary>Single horizontal.</summary>
    SingleHorizontal = 5,

    /// <summary>Raised horizontal.</summary>
    RaisedHorizontal = 6,

    /// <summary>Sunken horizontal.</summary>
    SunkenHorizontal = 7,

    /// <summary>Single vertical.</summary>
    SingleVertical = 8,

    /// <summary>Raised vertical.</summary>
    RaisedVertical = 9,

    /// <summary>Sunken vertical.</summary>
    SunkenVertical = 10,
}

/// <summary>What Ctrl+C copies from a grid.</summary>
public enum DataGridViewClipboardCopyMode
{
    /// <summary>Nothing.</summary>
    Disable = 0,

    /// <summary>The cells, with header text if headers are selected.</summary>
    EnableWithAutoHeaderText = 1,

    /// <summary>The cells, without header text.</summary>
    EnableWithoutHeaderText = 2,

    /// <summary>The cells, with header text.</summary>
    EnableAlwaysIncludeHeaderText = 3,
}

/// <summary>What a grid was doing when a data error happened.</summary>
[Flags]
public enum DataGridViewDataErrorContexts
{
    /// <summary>Formatting.</summary>
    Formatting = 1,

    /// <summary>Display.</summary>
    Display = 2,

    /// <summary>Preferred size.</summary>
    PreferredSize = 4,

    /// <summary>Row deletion.</summary>
    RowDeletion = 8,

    /// <summary>Parsing.</summary>
    Parsing = 256,

    /// <summary>Commit.</summary>
    Commit = 512,

    /// <summary>Initial value restoration.</summary>
    InitialValueRestoration = 1024,

    /// <summary>Leave control.</summary>
    LeaveControl = 2048,

    /// <summary>Current cell change.</summary>
    CurrentCellChange = 4096,

    /// <summary>Scroll.</summary>
    Scroll = 8192,

    /// <summary>Clipboard content.</summary>
    ClipboardContent = 16384,
}

/// <summary>The state of a row, column or cell.</summary>
[Flags]
public enum DataGridViewElementStates
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Displayed.</summary>
    Displayed = 1,

    /// <summary>Frozen.</summary>
    Frozen = 2,

    /// <summary>Read only.</summary>
    ReadOnly = 4,

    /// <summary>Resizable.</summary>
    Resizable = 8,

    /// <summary>Resizable set.</summary>
    ResizableSet = 16,

    /// <summary>Selected.</summary>
    Selected = 32,

    /// <summary>Visible.</summary>
    Visible = 64,
}

/// <summary>True, false, or inherited.</summary>
public enum DataGridViewTriState
{
    /// <summary>Not set.</summary>
    NotSet = 0,

    /// <summary>True.</summary>
    True = 1,

    /// <summary>False.</summary>
    False = 2,
}

/// <summary>Where a cell's content sits.</summary>
public enum DataGridViewContentAlignment
{
    /// <summary>Not set.</summary>
    NotSet = 0,

    /// <summary>Top left.</summary>
    TopLeft = 1,

    /// <summary>Top center.</summary>
    TopCenter = 2,

    /// <summary>Top right.</summary>
    TopRight = 4,

    /// <summary>Middle left.</summary>
    MiddleLeft = 16,

    /// <summary>Middle center.</summary>
    MiddleCenter = 32,

    /// <summary>Middle right.</summary>
    MiddleRight = 64,

    /// <summary>Bottom left.</summary>
    BottomLeft = 256,

    /// <summary>Bottom center.</summary>
    BottomCenter = 512,

    /// <summary>Bottom right.</summary>
    BottomRight = 1024,
}

/// <summary>How a combo box cell shows its drop-down button.</summary>
public enum DataGridViewComboBoxDisplayStyle
{
    /// <summary>A combo box.</summary>
    ComboBox = 0,

    /// <summary>A drop-down button.</summary>
    DropDownButton = 1,

    /// <summary>Nothing.</summary>
    Nothing = 2,
}

/// <summary>How an image cell shows its image.</summary>
public enum DataGridViewImageCellLayout
{
    /// <summary>Not set.</summary>
    NotSet = 0,

    /// <summary>Normal.</summary>
    Normal = 1,

    /// <summary>Stretch.</summary>
    Stretch = 2,

    /// <summary>Zoom.</summary>
    Zoom = 3,
}

/// <summary>The parts of a cell to draw.</summary>
[Flags]
public enum DataGridViewPaintParts
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Background.</summary>
    Background = 1,

    /// <summary>Border.</summary>
    Border = 2,

    /// <summary>Content background.</summary>
    ContentBackground = 4,

    /// <summary>Content foreground.</summary>
    ContentForeground = 8,

    /// <summary>Error icon.</summary>
    ErrorIcon = 16,

    /// <summary>Focus.</summary>
    Focus = 32,

    /// <summary>Selection background.</summary>
    SelectionBackground = 64,

    /// <summary>All.</summary>
    All = 127,
}

/// <summary>Where a grid's hit test lands.</summary>
public enum DataGridViewHitTestType
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>A cell.</summary>
    Cell = 1,

    /// <summary>A column header.</summary>
    ColumnHeader = 2,

    /// <summary>A row header.</summary>
    RowHeader = 3,

    /// <summary>The top left header.</summary>
    TopLeftHeader = 4,

    /// <summary>The horizontal scroll bar.</summary>
    HorizontalScrollBar = 5,

    /// <summary>The vertical scroll bar.</summary>
    VerticalScrollBar = 6,
}
