namespace System.Drawing;

/// <summary>The fonts Windows uses for its own text, as Windows 10 and 11 set them.</summary>
public static class SystemFonts
{
    /// <summary>The default font of dialogs and forms: Segoe UI, 9 points.</summary>
    public static Font DefaultFont => Create("Segoe UI", 9f, nameof(DefaultFont));

    /// <summary>The font of dialog boxes: Segoe UI, 9 points.</summary>
    public static Font DialogFont => Create("Segoe UI", 9f, nameof(DialogFont));

    /// <summary>The font of message boxes: Segoe UI, 9 points.</summary>
    public static Font MessageBoxFont => Create("Segoe UI", 9f, nameof(MessageBoxFont));

    /// <summary>The font of menus: Segoe UI, 9 points.</summary>
    public static Font MenuFont => Create("Segoe UI", 9f, nameof(MenuFont));

    /// <summary>The font of status bars: Segoe UI, 9 points.</summary>
    public static Font StatusFont => Create("Segoe UI", 9f, nameof(StatusFont));

    /// <summary>The font of window title bars: Segoe UI, 9 points.</summary>
    public static Font CaptionFont => Create("Segoe UI", 9f, nameof(CaptionFont));

    /// <summary>The font of small title bars: Segoe UI, 9 points.</summary>
    public static Font SmallCaptionFont => Create("Segoe UI", 9f, nameof(SmallCaptionFont));

    /// <summary>The font of icon titles: Segoe UI, 9 points.</summary>
    public static Font IconTitleFont => Create("Segoe UI", 9f, nameof(IconTitleFont));

    private static Font Create(string family, float size, string name) => new(family, size) { SystemFontName = name };
}
