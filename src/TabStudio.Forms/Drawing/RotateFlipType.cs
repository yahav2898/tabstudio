namespace System.Drawing;

/// <summary>How <see cref="Image.RotateFlip"/> turns an image: a clockwise rotation, then an optional flip.</summary>
#pragma warning disable CA1069 // GDI+ names each combination twice.
public enum RotateFlipType
{
    /// <summary>Neither rotated nor flipped.</summary>
    RotateNoneFlipNone = 0,

    /// <summary>Rotated 90 degrees clockwise.</summary>
    Rotate90FlipNone = 1,

    /// <summary>Rotated 180 degrees.</summary>
    Rotate180FlipNone = 2,

    /// <summary>Rotated 270 degrees clockwise.</summary>
    Rotate270FlipNone = 3,

    /// <summary>Flipped horizontally.</summary>
    RotateNoneFlipX = 4,

    /// <summary>Rotated 90 degrees clockwise and flipped horizontally.</summary>
    Rotate90FlipX = 5,

    /// <summary>Rotated 180 degrees and flipped horizontally.</summary>
    Rotate180FlipX = 6,

    /// <summary>Rotated 270 degrees clockwise and flipped horizontally.</summary>
    Rotate270FlipX = 7,

    /// <summary>Flipped vertically.</summary>
    RotateNoneFlipY = Rotate180FlipX,

    /// <summary>Rotated 90 degrees clockwise and flipped vertically.</summary>
    Rotate90FlipY = Rotate270FlipX,

    /// <summary>Rotated 180 degrees and flipped vertically.</summary>
    Rotate180FlipY = RotateNoneFlipX,

    /// <summary>Rotated 270 degrees clockwise and flipped vertically.</summary>
    Rotate270FlipY = Rotate90FlipX,

    /// <summary>Flipped both ways.</summary>
    RotateNoneFlipXY = Rotate180FlipNone,

    /// <summary>Rotated 90 degrees clockwise and flipped both ways.</summary>
    Rotate90FlipXY = Rotate270FlipNone,

    /// <summary>Rotated 180 degrees and flipped both ways.</summary>
    Rotate180FlipXY = RotateNoneFlipNone,

    /// <summary>Rotated 270 degrees clockwise and flipped both ways.</summary>
    Rotate270FlipXY = Rotate90FlipNone,
}
#pragma warning restore CA1069
