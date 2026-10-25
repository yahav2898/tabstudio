namespace System.Drawing.Drawing2D;

/// <summary>Whether lines, curves and the edges of filled areas are smoothed (antialiased).</summary>
public enum SmoothingMode
{
    /// <summary>An invalid mode.</summary>
    Invalid = -1,

    /// <summary>No antialiasing.</summary>
    Default = 0,

    /// <summary>No antialiasing.</summary>
    HighSpeed = 1,

    /// <summary>Antialiased rendering.</summary>
    HighQuality = 2,

    /// <summary>No antialiasing.</summary>
    None = 3,

    /// <summary>Antialiased rendering.</summary>
    AntiAlias = 4,
}

/// <summary>The algorithm used when images are scaled or rotated.</summary>
public enum InterpolationMode
{
    /// <summary>An invalid mode.</summary>
    Invalid = -1,

    /// <summary>The default mode.</summary>
    Default = 0,

    /// <summary>Low quality interpolation.</summary>
    Low = 1,

    /// <summary>High quality interpolation.</summary>
    High = 2,

    /// <summary>Bilinear interpolation.</summary>
    Bilinear = 3,

    /// <summary>Bicubic interpolation.</summary>
    Bicubic = 4,

    /// <summary>Nearest-neighbor interpolation, which keeps hard pixel edges.</summary>
    NearestNeighbor = 5,

    /// <summary>High-quality bilinear interpolation.</summary>
    HighQualityBilinear = 6,

    /// <summary>High-quality bicubic interpolation.</summary>
    HighQualityBicubic = 7,
}

/// <summary>How pixels are offset during rendering.</summary>
public enum PixelOffsetMode
{
    /// <summary>An invalid mode.</summary>
    Invalid = -1,

    /// <summary>The default mode.</summary>
    Default = 0,

    /// <summary>High speed, low quality rendering.</summary>
    HighSpeed = 1,

    /// <summary>High quality, low speed rendering.</summary>
    HighQuality = 2,

    /// <summary>No pixel offset.</summary>
    None = 3,

    /// <summary>Pixels are offset by half a unit.</summary>
    Half = 4,
}

/// <summary>The quality level used when composing drawn content.</summary>
public enum CompositingQuality
{
    /// <summary>An invalid quality.</summary>
    Invalid = -1,

    /// <summary>The default quality.</summary>
    Default = 0,

    /// <summary>High speed, low quality.</summary>
    HighSpeed = 1,

    /// <summary>High quality, low speed.</summary>
    HighQuality = 2,

    /// <summary>Gamma correction is used.</summary>
    GammaCorrected = 3,

    /// <summary>Linear values are assumed.</summary>
    AssumeLinear = 4,
}

/// <summary>How the source colors are combined with the background colors.</summary>
public enum CompositingMode
{
    /// <summary>Colors are blended with the background.</summary>
    SourceOver = 0,

    /// <summary>Colors overwrite the background.</summary>
    SourceCopy = 1,
}

/// <summary>How the interior of a closed path is filled.</summary>
public enum FillMode
{
    /// <summary>The alternate (even-odd) fill mode.</summary>
    Alternate = 0,

    /// <summary>The winding (non-zero) fill mode.</summary>
    Winding = 1,
}

/// <summary>The style of dashed lines drawn with a pen.</summary>
public enum DashStyle
{
    /// <summary>A solid line.</summary>
    Solid = 0,

    /// <summary>A line of dashes.</summary>
    Dash = 1,

    /// <summary>A line of dots.</summary>
    Dot = 2,

    /// <summary>A line of alternating dashes and dots.</summary>
    DashDot = 3,

    /// <summary>A line of alternating dashes and two dots.</summary>
    DashDotDot = 4,

    /// <summary>A user-defined dash style, from the pen's dash pattern.</summary>
    Custom = 5,
}

/// <summary>The cap at the start or end of a line drawn with a pen.</summary>
public enum LineCap
{
    /// <summary>A flat cap.</summary>
    Flat = 0,

    /// <summary>A square cap.</summary>
    Square = 1,

    /// <summary>A round cap.</summary>
    Round = 2,

    /// <summary>A triangular cap.</summary>
    Triangle = 3,

    /// <summary>No anchor.</summary>
    NoAnchor = 16,

    /// <summary>A square anchor cap.</summary>
    SquareAnchor = 17,

    /// <summary>A round anchor cap.</summary>
    RoundAnchor = 18,

    /// <summary>A diamond anchor cap.</summary>
    DiamondAnchor = 19,

    /// <summary>An arrow-shaped anchor cap.</summary>
    ArrowAnchor = 20,

    /// <summary>A mask used to check whether a cap is an anchor cap.</summary>
    AnchorMask = 240,

    /// <summary>A custom cap.</summary>
    Custom = 255,
}

/// <summary>How consecutive line segments drawn with a pen are joined.</summary>
public enum LineJoin
{
    /// <summary>A mitered join.</summary>
    Miter = 0,

    /// <summary>A beveled join.</summary>
    Bevel = 1,

    /// <summary>A rounded join.</summary>
    Round = 2,

    /// <summary>A mitered join, clipped at the miter limit.</summary>
    MiterClipped = 3,
}

/// <summary>The direction of a linear gradient.</summary>
public enum LinearGradientMode
{
    /// <summary>From left to right.</summary>
    Horizontal = 0,

    /// <summary>From top to bottom.</summary>
    Vertical = 1,

    /// <summary>From the upper left to the lower right.</summary>
    ForwardDiagonal = 2,

    /// <summary>From the upper right to the lower left.</summary>
    BackwardDiagonal = 3,
}

/// <summary>The cap of the dashes of a dashed line.</summary>
public enum DashCap
{
    /// <summary>A square cap.</summary>
    Flat = 0,

    /// <summary>A round cap.</summary>
    Round = 2,

    /// <summary>A triangular cap.</summary>
    Triangle = 3,
}
