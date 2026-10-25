namespace System.Drawing.Text;

/// <summary>The quality of text rendering.</summary>
public enum TextRenderingHint
{
    /// <summary>Each character is drawn using its glyph bitmap, with the system's default rendering hint.</summary>
    SystemDefault = 0,

    /// <summary>Each character is drawn using its glyph bitmap, with hinting.</summary>
    SingleBitPerPixelGridFit = 1,

    /// <summary>Each character is drawn using its glyph bitmap, without hinting.</summary>
    SingleBitPerPixel = 2,

    /// <summary>Each character is drawn using its antialiased glyph bitmap, with hinting.</summary>
    AntiAliasGridFit = 3,

    /// <summary>Each character is drawn using its antialiased glyph bitmap, without hinting.</summary>
    AntiAlias = 4,

    /// <summary>Each character is drawn using its ClearType glyph bitmap, with hinting.</summary>
    ClearTypeGridFit = 5,
}

/// <summary>The type of display for hot-key prefixes, the ampersands that mark access keys.</summary>
public enum HotkeyPrefix
{
    /// <summary>No hot-key prefix: an ampersand is shown as is.</summary>
    None = 0,

    /// <summary>The letter after an ampersand is underlined.</summary>
    Show = 1,

    /// <summary>The ampersand is not shown and nothing is underlined.</summary>
    Hide = 2,
}

/// <summary>Generic font families.</summary>
public enum GenericFontFamilies
{
    /// <summary>A generic serif font family.</summary>
    Serif = 0,

    /// <summary>A generic sans serif font family.</summary>
    SansSerif = 1,

    /// <summary>A generic monospace font family.</summary>
    Monospace = 2,
}
