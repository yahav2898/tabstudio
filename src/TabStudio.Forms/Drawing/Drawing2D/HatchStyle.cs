namespace System.Drawing.Drawing2D;

/// <summary>The patterns of a <see cref="HatchBrush"/>.</summary>
#pragma warning disable CA1069 // GDI+ gives some patterns two names.
public enum HatchStyle
{
    /// <summary>The horizontal pattern.</summary>
    Horizontal = 0,

    /// <summary>The vertical pattern.</summary>
    Vertical = 1,

    /// <summary>The forward diagonal pattern.</summary>
    ForwardDiagonal = 2,

    /// <summary>The backward diagonal pattern.</summary>
    BackwardDiagonal = 3,

    /// <summary>The cross pattern.</summary>
    Cross = 4,

    /// <summary>The diagonal cross pattern.</summary>
    DiagonalCross = 5,

    /// <summary>The percent 05 pattern.</summary>
    Percent05 = 6,

    /// <summary>The percent 10 pattern.</summary>
    Percent10 = 7,

    /// <summary>The percent 20 pattern.</summary>
    Percent20 = 8,

    /// <summary>The percent 25 pattern.</summary>
    Percent25 = 9,

    /// <summary>The percent 30 pattern.</summary>
    Percent30 = 10,

    /// <summary>The percent 40 pattern.</summary>
    Percent40 = 11,

    /// <summary>The percent 50 pattern.</summary>
    Percent50 = 12,

    /// <summary>The percent 60 pattern.</summary>
    Percent60 = 13,

    /// <summary>The percent 70 pattern.</summary>
    Percent70 = 14,

    /// <summary>The percent 75 pattern.</summary>
    Percent75 = 15,

    /// <summary>The percent 80 pattern.</summary>
    Percent80 = 16,

    /// <summary>The percent 90 pattern.</summary>
    Percent90 = 17,

    /// <summary>The light downward diagonal pattern.</summary>
    LightDownwardDiagonal = 18,

    /// <summary>The light upward diagonal pattern.</summary>
    LightUpwardDiagonal = 19,

    /// <summary>The dark downward diagonal pattern.</summary>
    DarkDownwardDiagonal = 20,

    /// <summary>The dark upward diagonal pattern.</summary>
    DarkUpwardDiagonal = 21,

    /// <summary>The wide downward diagonal pattern.</summary>
    WideDownwardDiagonal = 22,

    /// <summary>The wide upward diagonal pattern.</summary>
    WideUpwardDiagonal = 23,

    /// <summary>The light vertical pattern.</summary>
    LightVertical = 24,

    /// <summary>The light horizontal pattern.</summary>
    LightHorizontal = 25,

    /// <summary>The narrow vertical pattern.</summary>
    NarrowVertical = 26,

    /// <summary>The narrow horizontal pattern.</summary>
    NarrowHorizontal = 27,

    /// <summary>The dark vertical pattern.</summary>
    DarkVertical = 28,

    /// <summary>The dark horizontal pattern.</summary>
    DarkHorizontal = 29,

    /// <summary>The dashed downward diagonal pattern.</summary>
    DashedDownwardDiagonal = 30,

    /// <summary>The dashed upward diagonal pattern.</summary>
    DashedUpwardDiagonal = 31,

    /// <summary>The dashed horizontal pattern.</summary>
    DashedHorizontal = 32,

    /// <summary>The dashed vertical pattern.</summary>
    DashedVertical = 33,

    /// <summary>The small confetti pattern.</summary>
    SmallConfetti = 34,

    /// <summary>The large confetti pattern.</summary>
    LargeConfetti = 35,

    /// <summary>The zig zag pattern.</summary>
    ZigZag = 36,

    /// <summary>The wave pattern.</summary>
    Wave = 37,

    /// <summary>The diagonal brick pattern.</summary>
    DiagonalBrick = 38,

    /// <summary>The horizontal brick pattern.</summary>
    HorizontalBrick = 39,

    /// <summary>The weave pattern.</summary>
    Weave = 40,

    /// <summary>The plaid pattern.</summary>
    Plaid = 41,

    /// <summary>The divot pattern.</summary>
    Divot = 42,

    /// <summary>The dotted grid pattern.</summary>
    DottedGrid = 43,

    /// <summary>The dotted diamond pattern.</summary>
    DottedDiamond = 44,

    /// <summary>The shingle pattern.</summary>
    Shingle = 45,

    /// <summary>The trellis pattern.</summary>
    Trellis = 46,

    /// <summary>The sphere pattern.</summary>
    Sphere = 47,

    /// <summary>The small grid pattern.</summary>
    SmallGrid = 48,

    /// <summary>The small checker board pattern.</summary>
    SmallCheckerBoard = 49,

    /// <summary>The large checker board pattern.</summary>
    LargeCheckerBoard = 50,

    /// <summary>The outlined diamond pattern.</summary>
    OutlinedDiamond = 51,

    /// <summary>The solid diamond pattern.</summary>
    SolidDiamond = 52,

    /// <summary>The large grid pattern, the same as <see cref="Cross"/>.</summary>
    LargeGrid = 4,

    /// <summary>The lowest value.</summary>
    Min = 0,

    /// <summary>The highest value, as GDI+ defines it.</summary>
    Max = 4,
}
#pragma warning restore CA1069
