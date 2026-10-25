using System.Drawing;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// The values of Windows' system colors in the default Windows 10 and 11 theme, which forms are drawn with: .NET gives
/// <see cref="SystemColors"/> other values off Windows, such as the beige of Windows XP for <c>Control</c>.
/// </summary>
internal static class SystemColorValues
{
    private static readonly Dictionary<KnownColor, int> Windows = new()
    {
        [KnownColor.ActiveBorder] = 0xB4B4B4,
        [KnownColor.ActiveCaption] = 0x99B4D1,
        [KnownColor.ActiveCaptionText] = 0x000000,
        [KnownColor.AppWorkspace] = 0xABABAB,
        [KnownColor.ButtonFace] = 0xF0F0F0,
        [KnownColor.ButtonHighlight] = 0xFFFFFF,
        [KnownColor.ButtonShadow] = 0xA0A0A0,
        [KnownColor.Control] = 0xF0F0F0,
        [KnownColor.ControlDark] = 0xA0A0A0,
        [KnownColor.ControlDarkDark] = 0x696969,
        [KnownColor.ControlLight] = 0xE3E3E3,
        [KnownColor.ControlLightLight] = 0xFFFFFF,
        [KnownColor.ControlText] = 0x000000,
        [KnownColor.Desktop] = 0x000000,
        [KnownColor.GradientActiveCaption] = 0xB9D1EA,
        [KnownColor.GradientInactiveCaption] = 0xD7E4F2,
        [KnownColor.GrayText] = 0x6D6D6D,
        [KnownColor.Highlight] = 0x0078D7,
        [KnownColor.HighlightText] = 0xFFFFFF,
        [KnownColor.HotTrack] = 0x0066CC,
        [KnownColor.InactiveBorder] = 0xF4F7FC,
        [KnownColor.InactiveCaption] = 0xBFCDDB,
        [KnownColor.InactiveCaptionText] = 0x000000,
        [KnownColor.Info] = 0xFFFFE1,
        [KnownColor.InfoText] = 0x000000,
        [KnownColor.Menu] = 0xF0F0F0,
        [KnownColor.MenuBar] = 0xF0F0F0,
        [KnownColor.MenuHighlight] = 0x0078D7,
        [KnownColor.MenuText] = 0x000000,
        [KnownColor.ScrollBar] = 0xC8C8C8,
        [KnownColor.Window] = 0xFFFFFF,
        [KnownColor.WindowFrame] = 0x646464,
        [KnownColor.WindowText] = 0x000000,
    };

    /// <summary>A color's 32-bit ARGB value as Windows draws it.</summary>
    /// <param name="color">The color.</param>
    public static uint Argb(Color color) => (uint)Resolve(color).ToArgb();

    /// <summary>A color as Windows draws it: a system color takes its Windows 10 and 11 value; any other stays as it is.</summary>
    /// <param name="color">The color.</param>
    public static Color Resolve(Color color) =>
        color.IsSystemColor && Windows.TryGetValue(color.ToKnownColor(), out var rgb) ? Color.FromArgb(unchecked((int)0xFF000000) | rgb) : color;
}
