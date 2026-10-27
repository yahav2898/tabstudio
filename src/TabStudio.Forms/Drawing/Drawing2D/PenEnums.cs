namespace System.Drawing.Drawing2D;

/// <summary>Where a pen's line is drawn relative to an outline.</summary>
public enum PenAlignment
{
    /// <summary>Centered on the outline.</summary>
    Center = 0,

    /// <summary>Inside the outline.</summary>
    Inset = 1,

    /// <summary>Outside the outline.</summary>
    Outset = 2,

    /// <summary>To the left of the outline.</summary>
    Left = 3,

    /// <summary>To the right of the outline.</summary>
    Right = 4,
}

/// <summary>The kind of brush a pen paints with.</summary>
public enum PenType
{
    /// <summary>A solid color.</summary>
    SolidColor = 0,

    /// <summary>A hatch pattern.</summary>
    HatchFill = 1,

    /// <summary>An image.</summary>
    TextureFill = 2,

    /// <summary>A path gradient.</summary>
    PathGradient = 3,

    /// <summary>A linear gradient.</summary>
    LinearGradient = 4,
}
