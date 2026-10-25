namespace System.Drawing;

/// <summary>Alignment of content on a drawing surface, such as a label's text.</summary>
public enum ContentAlignment
{
    /// <summary>Vertically at the top, horizontally on the left.</summary>
    TopLeft = 1,

    /// <summary>Vertically at the top, horizontally in the center.</summary>
    TopCenter = 2,

    /// <summary>Vertically at the top, horizontally on the right.</summary>
    TopRight = 4,

    /// <summary>Vertically in the middle, horizontally on the left.</summary>
    MiddleLeft = 16,

    /// <summary>Vertically in the middle, horizontally in the center.</summary>
    MiddleCenter = 32,

    /// <summary>Vertically in the middle, horizontally on the right.</summary>
    MiddleRight = 64,

    /// <summary>Vertically at the bottom, horizontally on the left.</summary>
    BottomLeft = 256,

    /// <summary>Vertically at the bottom, horizontally in the center.</summary>
    BottomCenter = 512,

    /// <summary>Vertically at the bottom, horizontally on the right.</summary>
    BottomRight = 1024,
}
