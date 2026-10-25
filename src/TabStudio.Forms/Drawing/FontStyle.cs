namespace System.Drawing;

/// <summary>Style information applied to text.</summary>
[Flags]
public enum FontStyle
{
    /// <summary>Normal text.</summary>
    Regular = 0,

    /// <summary>Bold text.</summary>
    Bold = 1,

    /// <summary>Italic text.</summary>
    Italic = 2,

    /// <summary>Underlined text.</summary>
    Underline = 4,

    /// <summary>Text with a line through the middle.</summary>
    Strikeout = 8,
}
