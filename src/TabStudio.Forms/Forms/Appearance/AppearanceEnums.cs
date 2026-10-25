namespace System.Windows.Forms;

/// <summary>The border of a control.</summary>
public enum BorderStyle
{
    /// <summary>No border.</summary>
    None = 0,

    /// <summary>A single-line border.</summary>
    FixedSingle = 1,

    /// <summary>A three-dimensional border.</summary>
    Fixed3D = 2,
}

/// <summary>The look of a button.</summary>
public enum FlatStyle
{
    /// <summary>Flat.</summary>
    Flat = 0,

    /// <summary>Popup.</summary>
    Popup = 1,

    /// <summary>Standard.</summary>
    Standard = 2,

    /// <summary>System.</summary>
    System = 3,
}

/// <summary>Whether a check box or radio button looks like a box or like a button.</summary>
public enum Appearance
{
    /// <summary>Normal.</summary>
    Normal = 0,

    /// <summary>Button.</summary>
    Button = 1,
}

/// <summary>The state of a check box.</summary>
public enum CheckState
{
    /// <summary>Unchecked.</summary>
    Unchecked = 0,

    /// <summary>Checked.</summary>
    Checked = 1,

    /// <summary>Indeterminate.</summary>
    Indeterminate = 2,
}

/// <summary>How text is aligned horizontally.</summary>
public enum HorizontalAlignment
{
    /// <summary>Left.</summary>
    Left = 0,

    /// <summary>Right.</summary>
    Right = 1,

    /// <summary>Center.</summary>
    Center = 2,
}

/// <summary>Whether something is on the left or the right.</summary>
public enum LeftRightAlignment
{
    /// <summary>Left.</summary>
    Left = 0,

    /// <summary>Right.</summary>
    Right = 1,
}

/// <summary>Where a button's text is relative to its image.</summary>
public enum TextImageRelation
{
    /// <summary>Overlay.</summary>
    Overlay = 0,

    /// <summary>Image above text.</summary>
    ImageAboveText = 1,

    /// <summary>Text above image.</summary>
    TextAboveImage = 2,

    /// <summary>Image before text.</summary>
    ImageBeforeText = 4,

    /// <summary>Text before image.</summary>
    TextBeforeImage = 8,
}

/// <summary>How a background image is laid out.</summary>
public enum ImageLayout
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Tile.</summary>
    Tile = 1,

    /// <summary>Center.</summary>
    Center = 2,

    /// <summary>Stretch.</summary>
    Stretch = 3,

    /// <summary>Zoom.</summary>
    Zoom = 4,
}

/// <summary>Whether text is laid out right to left.</summary>
public enum RightToLeft
{
    /// <summary>No.</summary>
    No = 0,

    /// <summary>Yes.</summary>
    Yes = 1,

    /// <summary>Inherit.</summary>
    Inherit = 2,
}

/// <summary>The input method editor mode of a control. Kept for compatibility; the device's keyboard decides.</summary>
public enum ImeMode
{
    /// <summary>Inherit.</summary>
    Inherit = -1,

    /// <summary>No control.</summary>
    NoControl = 0,

    /// <summary>On.</summary>
    On = 1,

    /// <summary>Off.</summary>
    Off = 2,

    /// <summary>Disable.</summary>
    Disable = 3,

    /// <summary>Hiragana.</summary>
    Hiragana = 4,

    /// <summary>Katakana.</summary>
    Katakana = 5,

    /// <summary>Katakana half.</summary>
    KatakanaHalf = 6,

    /// <summary>Alpha full.</summary>
    AlphaFull = 7,

    /// <summary>Alpha.</summary>
    Alpha = 8,

    /// <summary>Hangul full.</summary>
    HangulFull = 9,

    /// <summary>Hangul.</summary>
    Hangul = 10,

    /// <summary>Close.</summary>
    Close = 11,

    /// <summary>On half.</summary>
    OnHalf = 12,
}

/// <summary>Styles and behaviors of a control.</summary>
[Flags]
public enum ControlStyles
{
    /// <summary>Container control.</summary>
    ContainerControl = 1,

    /// <summary>User paint.</summary>
    UserPaint = 2,

    /// <summary>Opaque.</summary>
    Opaque = 4,

    /// <summary>Resize redraw.</summary>
    ResizeRedraw = 16,

    /// <summary>Fixed width.</summary>
    FixedWidth = 32,

    /// <summary>Fixed height.</summary>
    FixedHeight = 64,

    /// <summary>Standard click.</summary>
    StandardClick = 256,

    /// <summary>Selectable.</summary>
    Selectable = 512,

    /// <summary>User mouse.</summary>
    UserMouse = 1024,

    /// <summary>Supports transparent back color.</summary>
    SupportsTransparentBackColor = 2048,

    /// <summary>Standard double click.</summary>
    StandardDoubleClick = 4096,

    /// <summary>All painting in wm paint.</summary>
    AllPaintingInWmPaint = 8192,

    /// <summary>Cache text.</summary>
    CacheText = 16384,

    /// <summary>Enable notify message.</summary>
    EnableNotifyMessage = 32768,

    /// <summary>Double buffer.</summary>
    DoubleBuffer = 65536,

    /// <summary>Optimized double buffer.</summary>
    OptimizedDoubleBuffer = 131072,

    /// <summary>Use text for accessibility.</summary>
    UseTextForAccessibility = 262144,
}
