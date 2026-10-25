namespace System.Windows.Forms;

/// <summary>Which scroll bars a text box shows.</summary>
public enum ScrollBars
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Horizontal.</summary>
    Horizontal = 1,

    /// <summary>Vertical.</summary>
    Vertical = 2,

    /// <summary>Both.</summary>
    Both = 3,
}

/// <summary>The case of the characters typed in a text box.</summary>
public enum CharacterCasing
{
    /// <summary>Normal.</summary>
    Normal = 0,

    /// <summary>Upper.</summary>
    Upper = 1,

    /// <summary>Lower.</summary>
    Lower = 2,
}

/// <summary>The style of a combo box.</summary>
public enum ComboBoxStyle
{
    /// <summary>An editable box with its list always shown.</summary>
    Simple = 0,

    /// <summary>An editable box with a drop-down list.</summary>
    DropDown = 1,

    /// <summary>A drop-down list the user picks from without typing.</summary>
    DropDownList = 2,
}

/// <summary>How many items of a list box can be selected.</summary>
public enum SelectionMode
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>One.</summary>
    One = 1,

    /// <summary>Multi simple.</summary>
    MultiSimple = 2,

    /// <summary>Multi extended.</summary>
    MultiExtended = 3,
}

/// <summary>Whether a list's items are drawn by the control or by the app.</summary>
public enum DrawMode
{
    /// <summary>Normal.</summary>
    Normal = 0,

    /// <summary>Owner draw fixed.</summary>
    OwnerDrawFixed = 1,

    /// <summary>Owner draw variable.</summary>
    OwnerDrawVariable = 2,
}

/// <summary>How a date and time picker shows its value.</summary>
public enum DateTimePickerFormat
{
    /// <summary>Long.</summary>
    Long = 1,

    /// <summary>Short.</summary>
    Short = 2,

    /// <summary>Time.</summary>
    Time = 4,

    /// <summary>Custom.</summary>
    Custom = 8,
}

/// <summary>How a progress bar shows progress.</summary>
public enum ProgressBarStyle
{
    /// <summary>Blocks.</summary>
    Blocks = 0,

    /// <summary>Continuous.</summary>
    Continuous = 1,

    /// <summary>Marquee.</summary>
    Marquee = 2,
}

/// <summary>Where a track bar shows its tick marks.</summary>
public enum TickStyle
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Top left.</summary>
    TopLeft = 1,

    /// <summary>Bottom right.</summary>
    BottomRight = 2,

    /// <summary>Both.</summary>
    Both = 3,
}

/// <summary>How a picture box shows its image.</summary>
public enum PictureBoxSizeMode
{
    /// <summary>Normal.</summary>
    Normal = 0,

    /// <summary>Stretch image.</summary>
    StretchImage = 1,

    /// <summary>Auto size.</summary>
    AutoSize = 2,

    /// <summary>Center image.</summary>
    CenterImage = 3,

    /// <summary>Zoom.</summary>
    Zoom = 4,
}

/// <summary>Where the tabs of a tab control are.</summary>
public enum TabAlignment
{
    /// <summary>Top.</summary>
    Top = 0,

    /// <summary>Bottom.</summary>
    Bottom = 1,

    /// <summary>Left.</summary>
    Left = 2,

    /// <summary>Right.</summary>
    Right = 3,
}

/// <summary>The look of the tabs of a tab control.</summary>
public enum TabAppearance
{
    /// <summary>Normal.</summary>
    Normal = 0,

    /// <summary>Buttons.</summary>
    Buttons = 1,

    /// <summary>Flat buttons.</summary>
    FlatButtons = 2,
}

/// <summary>How the tabs of a tab control are sized.</summary>
public enum TabSizeMode
{
    /// <summary>Normal.</summary>
    Normal = 0,

    /// <summary>Fill to right.</summary>
    FillToRight = 1,

    /// <summary>Fixed.</summary>
    Fixed = 2,
}

/// <summary>When a link label underlines its links.</summary>
public enum LinkBehavior
{
    /// <summary>System default.</summary>
    SystemDefault = 0,

    /// <summary>Always underline.</summary>
    AlwaysUnderline = 1,

    /// <summary>Hover underline.</summary>
    HoverUnderline = 2,

    /// <summary>Never underline.</summary>
    NeverUnderline = 3,
}

/// <summary>How a text box completes what is typed. Kept for compatibility.</summary>
public enum AutoCompleteMode
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Suggest.</summary>
    Suggest = 1,

    /// <summary>Append.</summary>
    Append = 2,

    /// <summary>Suggest append.</summary>
    SuggestAppend = 3,
}

/// <summary>Where a text box finds completions. Kept for compatibility.</summary>
public enum AutoCompleteSource
{
    /// <summary>File system.</summary>
    FileSystem = 1,

    /// <summary>History list.</summary>
    HistoryList = 2,

    /// <summary>Recently used list.</summary>
    RecentlyUsedList = 4,

    /// <summary>All url.</summary>
    AllUrl = 6,

    /// <summary>All system sources.</summary>
    AllSystemSources = 7,

    /// <summary>File system directories.</summary>
    FileSystemDirectories = 32,

    /// <summary>Custom source.</summary>
    CustomSource = 64,

    /// <summary>None.</summary>
    None = 128,

    /// <summary>List items.</summary>
    ListItems = 256,
}

/// <summary>What a scroll bar did.</summary>
public enum ScrollEventType
{
    /// <summary>Small decrement.</summary>
    SmallDecrement = 0,

    /// <summary>Small increment.</summary>
    SmallIncrement = 1,

    /// <summary>Large decrement.</summary>
    LargeDecrement = 2,

    /// <summary>Large increment.</summary>
    LargeIncrement = 3,

    /// <summary>Thumb position.</summary>
    ThumbPosition = 4,

    /// <summary>Thumb track.</summary>
    ThumbTrack = 5,

    /// <summary>First.</summary>
    First = 6,

    /// <summary>Last.</summary>
    Last = 7,

    /// <summary>End scroll.</summary>
    EndScroll = 8,
}

/// <summary>The orientation of a scroll bar.</summary>
public enum ScrollOrientation
{
    /// <summary>Horizontal scroll.</summary>
    HorizontalScroll = 0,

    /// <summary>Vertical scroll.</summary>
    VerticalScroll = 1,
}

/// <summary>The order items are sorted in.</summary>
public enum SortOrder
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Ascending.</summary>
    Ascending = 1,

    /// <summary>Descending.</summary>
    Descending = 2,
}

/// <summary>How a list view shows its items.</summary>
public enum View
{
    /// <summary>Large icon.</summary>
    LargeIcon = 0,

    /// <summary>Details.</summary>
    Details = 1,

    /// <summary>Small icon.</summary>
    SmallIcon = 2,

    /// <summary>List.</summary>
    List = 3,

    /// <summary>Tile.</summary>
    Tile = 4,
}

/// <summary>Whether a list view's column headers are shown and clickable.</summary>
public enum ColumnHeaderStyle
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Nonclickable.</summary>
    Nonclickable = 1,

    /// <summary>Clickable.</summary>
    Clickable = 2,
}

/// <summary>When a data binding writes the control's value to its data source.</summary>
public enum DataSourceUpdateMode
{
    /// <summary>On validation.</summary>
    OnValidation = 0,

    /// <summary>On property changed.</summary>
    OnPropertyChanged = 1,

    /// <summary>Never.</summary>
    Never = 2,
}
