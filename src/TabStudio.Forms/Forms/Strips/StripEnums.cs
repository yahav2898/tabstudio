namespace System.Windows.Forms;

/// <summary>Whether a tool strip item shows its text, its image or both.</summary>
public enum ToolStripItemDisplayStyle
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Text.</summary>
    Text = 1,

    /// <summary>Image.</summary>
    Image = 2,

    /// <summary>Image and text.</summary>
    ImageAndText = 3,
}

/// <summary>Whether a tool strip shows its move grip.</summary>
public enum ToolStripGripStyle
{
    /// <summary>Hidden.</summary>
    Hidden = 0,

    /// <summary>Visible.</summary>
    Visible = 1,
}

/// <summary>Which end of a tool strip an item sits at.</summary>
public enum ToolStripItemAlignment
{
    /// <summary>Left.</summary>
    Left = 0,

    /// <summary>Right.</summary>
    Right = 1,
}

/// <summary>How a tool strip is drawn.</summary>
public enum ToolStripRenderMode
{
    /// <summary>Custom.</summary>
    Custom = 0,

    /// <summary>System.</summary>
    System = 1,

    /// <summary>Professional.</summary>
    Professional = 2,

    /// <summary>Manager render mode.</summary>
    ManagerRenderMode = 3,
}

/// <summary>How a tool strip lays out its items.</summary>
public enum ToolStripLayoutStyle
{
    /// <summary>Stack with overflow.</summary>
    StackWithOverflow = 0,

    /// <summary>Horizontal stack with overflow.</summary>
    HorizontalStackWithOverflow = 1,

    /// <summary>Vertical stack with overflow.</summary>
    VerticalStackWithOverflow = 2,

    /// <summary>Flow.</summary>
    Flow = 3,

    /// <summary>Table.</summary>
    Table = 4,
}

/// <summary>The sides of a status label with a border.</summary>
[Flags]
public enum ToolStripStatusLabelBorderSides
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Left.</summary>
    Left = 1,

    /// <summary>Top.</summary>
    Top = 2,

    /// <summary>Right.</summary>
    Right = 4,

    /// <summary>Bottom.</summary>
    Bottom = 8,

    /// <summary>All.</summary>
    All = 15,
}

/// <summary>Whether a tool strip item's image is scaled.</summary>
public enum ToolStripItemImageScaling
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Size to fit.</summary>
    SizeToFit = 1,
}

/// <summary>The direction of a tool strip item's text.</summary>
public enum ToolStripTextDirection
{
    /// <summary>Inherit.</summary>
    Inherit = 0,

    /// <summary>Horizontal.</summary>
    Horizontal = 1,

    /// <summary>Vertical90.</summary>
    Vertical90 = 2,

    /// <summary>Vertical270.</summary>
    Vertical270 = 3,
}

/// <summary>Why a drop-down closed.</summary>
public enum ToolStripDropDownCloseReason
{
    /// <summary>A tap outside it.</summary>
    AppFocusChange = 0,

    /// <summary>A tap elsewhere in the app.</summary>
    AppClicked = 1,

    /// <summary>One of its items was clicked.</summary>
    ItemClicked = 2,

    /// <summary>A key, such as Escape.</summary>
    Keyboard = 3,

    /// <summary>The app called Close.</summary>
    CloseCalled = 4,
}

/// <summary>Which way a drop-down opens.</summary>
public enum ToolStripDropDownDirection
{
    /// <summary>Above and to the left.</summary>
    AboveLeft = 0,

    /// <summary>Above and to the right.</summary>
    AboveRight = 1,

    /// <summary>Below and to the left.</summary>
    BelowLeft = 2,

    /// <summary>Below and to the right.</summary>
    BelowRight = 3,

    /// <summary>To the left.</summary>
    Left = 4,

    /// <summary>To the right.</summary>
    Right = 5,

    /// <summary>The default for the item.</summary>
    Default = 7,
}

/// <summary>Whether an item may go to a strip's overflow menu.</summary>
public enum ToolStripItemOverflow
{
    /// <summary>Never.</summary>
    Never = 0,

    /// <summary>Always.</summary>
    Always = 1,

    /// <summary>As needed.</summary>
    AsNeeded = 2,
}

/// <summary>Where an item is placed.</summary>
public enum ToolStripItemPlacement
{
    /// <summary>On the main strip.</summary>
    Main = 0,

    /// <summary>On the overflow menu.</summary>
    Overflow = 1,

    /// <summary>Not shown.</summary>
    None = 2,
}

/// <summary>How the items of merged menus combine. Kept for compatibility.</summary>
public enum MergeAction
{
    /// <summary>Append.</summary>
    Append = 0,

    /// <summary>Insert.</summary>
    Insert = 1,

    /// <summary>Replace.</summary>
    Replace = 2,

    /// <summary>Remove.</summary>
    Remove = 3,

    /// <summary>Match only.</summary>
    MatchOnly = 4,
}

/// <summary>The look of a three-dimensional border.</summary>
[Flags]
public enum Border3DStyle
{
    /// <summary>Raised outer edge.</summary>
    RaisedOuter = 1,

    /// <summary>Sunken outer edge.</summary>
    SunkenOuter = 2,

    /// <summary>Raised inner edge.</summary>
    RaisedInner = 4,

    /// <summary>Sunken inner edge.</summary>
    SunkenInner = 8,

    /// <summary>Raised.</summary>
    Raised = RaisedOuter | RaisedInner,

    /// <summary>Etched.</summary>
    Etched = SunkenOuter | RaisedInner,

    /// <summary>Bump.</summary>
    Bump = RaisedOuter | SunkenInner,

    /// <summary>Sunken.</summary>
    Sunken = SunkenOuter | SunkenInner,

    /// <summary>Adjust.</summary>
    Adjust = 8192,

    /// <summary>Flat.</summary>
    Flat = 16394,
}
