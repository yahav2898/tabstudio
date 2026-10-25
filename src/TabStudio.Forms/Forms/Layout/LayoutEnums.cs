namespace System.Windows.Forms;

/// <summary>The edges of a container a control keeps its distance to when the container is resized.</summary>
[Flags]
public enum AnchorStyles
{
    /// <summary>The control is anchored to no edge: it keeps its relative position.</summary>
    None = 0,

    /// <summary>The top edge.</summary>
    Top = 1,

    /// <summary>The bottom edge.</summary>
    Bottom = 2,

    /// <summary>The left edge.</summary>
    Left = 4,

    /// <summary>The right edge.</summary>
    Right = 8,
}

/// <summary>The edge of its container a control is docked to.</summary>
public enum DockStyle
{
    /// <summary>The control is not docked.</summary>
    None = 0,

    /// <summary>Docked to the top edge, across the container's width.</summary>
    Top = 1,

    /// <summary>Docked to the bottom edge, across the container's width.</summary>
    Bottom = 2,

    /// <summary>Docked to the left edge, down the container's height.</summary>
    Left = 3,

    /// <summary>Docked to the right edge, down the container's height.</summary>
    Right = 4,

    /// <summary>Fills the space the other docked controls leave.</summary>
    Fill = 5,
}

/// <summary>How a control sizes itself to its content when AutoSize is on.</summary>
public enum AutoSizeMode
{
    /// <summary>The control grows and shrinks to fit its content.</summary>
    GrowAndShrink = 0,

    /// <summary>The control grows to fit its content but never shrinks below its size.</summary>
    GrowOnly = 1,
}

/// <summary>How a container scales its controls when the font or resolution differs from the one it was designed with.</summary>
public enum AutoScaleMode
{
    /// <summary>No scaling.</summary>
    None = 0,

    /// <summary>Scaling by the font's average character size.</summary>
    Font = 1,

    /// <summary>Scaling by the screen resolution.</summary>
    Dpi = 2,

    /// <summary>The parent's scaling.</summary>
    Inherit = 3,
}

/// <summary>Which bounds of a control are set.</summary>
[Flags]
public enum BoundsSpecified
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>X.</summary>
    X = 1,

    /// <summary>Y.</summary>
    Y = 2,

    /// <summary>Location.</summary>
    Location = 3,

    /// <summary>Width.</summary>
    Width = 4,

    /// <summary>Height.</summary>
    Height = 8,

    /// <summary>Size.</summary>
    Size = 12,

    /// <summary>All.</summary>
    All = 15,
}

/// <summary>How the rows and columns of a TableLayoutPanel are sized.</summary>
public enum SizeType
{
    /// <summary>Sized to fit its contents.</summary>
    AutoSize = 0,

    /// <summary>A fixed number of pixels.</summary>
    Absolute = 1,

    /// <summary>A percentage of the space left over.</summary>
    Percent = 2,
}

/// <summary>The direction controls are laid out in a FlowLayoutPanel.</summary>
public enum FlowDirection
{
    /// <summary>Left to right.</summary>
    LeftToRight = 0,

    /// <summary>Top down.</summary>
    TopDown = 1,

    /// <summary>Right to left.</summary>
    RightToLeft = 2,

    /// <summary>Bottom up.</summary>
    BottomUp = 3,
}

/// <summary>The borders between the cells of a TableLayoutPanel.</summary>
public enum TableLayoutPanelCellBorderStyle
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Single.</summary>
    Single = 1,

    /// <summary>Inset.</summary>
    Inset = 2,

    /// <summary>Inset double.</summary>
    InsetDouble = 3,

    /// <summary>Outset.</summary>
    Outset = 4,

    /// <summary>Outset double.</summary>
    OutsetDouble = 5,

    /// <summary>Outset partial.</summary>
    OutsetPartial = 6,
}

/// <summary>How a TableLayoutPanel grows when a control is added to a full table.</summary>
public enum TableLayoutPanelGrowStyle
{
    /// <summary>Fixed size.</summary>
    FixedSize = 0,

    /// <summary>Add rows.</summary>
    AddRows = 1,

    /// <summary>Add columns.</summary>
    AddColumns = 2,
}

/// <summary>A horizontal or vertical orientation.</summary>
public enum Orientation
{
    /// <summary>Horizontal.</summary>
    Horizontal = 0,

    /// <summary>Vertical.</summary>
    Vertical = 1,
}
