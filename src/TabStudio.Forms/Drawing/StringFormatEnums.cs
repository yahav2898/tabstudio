namespace System.Drawing;

/// <summary>The alignment of a text string relative to its layout rectangle.</summary>
public enum StringAlignment
{
    /// <summary>Near the start of the layout rectangle: the left, or the top.</summary>
    Near = 0,

    /// <summary>In the center of the layout rectangle.</summary>
    Center = 1,

    /// <summary>Near the end of the layout rectangle: the right, or the bottom.</summary>
    Far = 2,
}

/// <summary>Display and layout information for text strings.</summary>
[Flags]
public enum StringFormatFlags
{
    /// <summary>Text is displayed from right to left.</summary>
    DirectionRightToLeft = 1,

    /// <summary>Text is vertically aligned.</summary>
    DirectionVertical = 2,

    /// <summary>Parts of characters may overhang the layout rectangle.</summary>
    FitBlackBox = 4,

    /// <summary>Control characters are shown with a representative glyph.</summary>
    DisplayFormatControl = 32,

    /// <summary>Fallback to other fonts for unsupported characters is disabled.</summary>
    NoFontFallback = 1024,

    /// <summary>Trailing spaces are included in measurements.</summary>
    MeasureTrailingSpaces = 2048,

    /// <summary>Text is not wrapped to the next line.</summary>
    NoWrap = 4096,

    /// <summary>Only entire lines are laid out in the layout rectangle.</summary>
    LineLimit = 8192,

    /// <summary>Text that extends past the layout rectangle is shown rather than clipped.</summary>
    NoClip = 16384,
}

/// <summary>How to trim characters from a string that does not completely fit into a layout shape.</summary>
public enum StringTrimming
{
    /// <summary>No trimming.</summary>
    None = 0,

    /// <summary>The text is trimmed to the nearest character.</summary>
    Character = 1,

    /// <summary>The text is trimmed to the nearest word.</summary>
    Word = 2,

    /// <summary>The text is trimmed to the nearest character and an ellipsis is added.</summary>
    EllipsisCharacter = 3,

    /// <summary>The text is trimmed to the nearest word and an ellipsis is added.</summary>
    EllipsisWord = 4,

    /// <summary>The center of the text is removed and replaced by an ellipsis.</summary>
    EllipsisPath = 5,
}
