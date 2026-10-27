namespace System.Drawing.Imaging;

/// <summary>Which colors a color matrix changes.</summary>
public enum ColorMatrixFlag
{
    /// <summary>Every color.</summary>
    Default = 0,

    /// <summary>Every color but grays.</summary>
    SkipGrays = 1,

    /// <summary>Only grays.</summary>
    AltGrays = 2,
}

/// <summary>What color adjustments apply to.</summary>
public enum ColorAdjustType
{
    /// <summary>Everything without its own adjustments.</summary>
    Default = 0,

    /// <summary>Bitmaps.</summary>
    Bitmap = 1,

    /// <summary>Brushes.</summary>
    Brush = 2,

    /// <summary>Pens.</summary>
    Pen = 3,

    /// <summary>Text.</summary>
    Text = 4,

    /// <summary>The number of types.</summary>
    Count = 5,

    /// <summary>Not used.</summary>
    Any = 6,
}
