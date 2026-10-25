namespace System.Drawing;

/// <summary>The unit of measure for a value, such as a font's size.</summary>
public enum GraphicsUnit
{
    /// <summary>The world coordinate system unit.</summary>
    World = 0,

    /// <summary>The unit of the display device: pixels for screens.</summary>
    Display = 1,

    /// <summary>A device pixel.</summary>
    Pixel = 2,

    /// <summary>A printer's point (1/72 inch).</summary>
    Point = 3,

    /// <summary>An inch.</summary>
    Inch = 4,

    /// <summary>The document unit (1/300 inch).</summary>
    Document = 5,

    /// <summary>A millimeter.</summary>
    Millimeter = 6,
}
