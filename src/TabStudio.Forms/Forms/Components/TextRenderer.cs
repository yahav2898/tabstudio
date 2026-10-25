using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>Measures and draws text as Windows Forms controls do, with a little padding at each side.</summary>
public static class TextRenderer
{
    /// <summary>The size of text in a font.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    public static Size MeasureText(string? text, Font? font) => MeasureText(text, font, Size.Empty, TextFormatFlags.Default);

    /// <summary>The size of text in a font, within a proposed size.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="proposedSize">The size available; a width wraps when the flags wrap.</param>
    public static Size MeasureText(string? text, Font? font, Size proposedSize) => MeasureText(text, font, proposedSize, TextFormatFlags.Default);

    /// <summary>The size of text in a font, within a proposed size, with flags.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="proposedSize">The size available.</param>
    /// <param name="flags">How the text is laid out.</param>
    public static Size MeasureText(string? text, Font? font, Size proposedSize, TextFormatFlags flags)
    {
        font ??= Control.DefaultFont;
        if (string.IsNullOrEmpty(text))
        {
            return Size.Empty;
        }

        var padding = Padding(font, flags);
        var wrap = (flags & TextFormatFlags.WordBreak) != 0 && proposedSize.Width > 0;
        var shown = (flags & TextFormatFlags.NoPrefix) != 0 ? text : Graphics.Prefixed(text, System.Drawing.Text.HotkeyPrefix.Hide);
        var size = TextMetrics.Measure(shown, font.Spec, wrap ? Math.Max(1, proposedSize.Width - padding) : double.PositiveInfinity);
        return new Size((int)Math.Ceiling(size.Width) + padding, (int)Math.Ceiling(size.Height));
    }

    /// <summary>The size of text on a surface.</summary>
    /// <param name="dc">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    public static Size MeasureText(IDeviceContext dc, string? text, Font? font) => MeasureText(text, font);

    /// <summary>The size of text on a surface, within a proposed size.</summary>
    /// <param name="dc">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="proposedSize">The size available.</param>
    public static Size MeasureText(IDeviceContext dc, string? text, Font? font, Size proposedSize) => MeasureText(text, font, proposedSize);

    /// <summary>The size of text on a surface, within a proposed size, with flags.</summary>
    /// <param name="dc">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="proposedSize">The size available.</param>
    /// <param name="flags">How the text is laid out.</param>
    public static Size MeasureText(IDeviceContext dc, string? text, Font? font, Size proposedSize, TextFormatFlags flags) => MeasureText(text, font, proposedSize, flags);

    /// <summary>Draws text at a point.</summary>
    /// <param name="dc">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="pt">Where the text starts.</param>
    /// <param name="foreColor">The color.</param>
    public static void DrawText(IDeviceContext dc, string? text, Font? font, Point pt, Color foreColor) =>
        DrawText(dc, text, font, pt, foreColor, Color.Empty, TextFormatFlags.Default);

    /// <summary>Draws text at a point, with flags.</summary>
    /// <param name="dc">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="pt">Where the text starts.</param>
    /// <param name="foreColor">The color.</param>
    /// <param name="flags">How the text is laid out.</param>
    public static void DrawText(IDeviceContext dc, string? text, Font? font, Point pt, Color foreColor, TextFormatFlags flags) =>
        DrawText(dc, text, font, pt, foreColor, Color.Empty, flags);

    /// <summary>Draws text at a point, on a background.</summary>
    /// <param name="dc">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="pt">Where the text starts.</param>
    /// <param name="foreColor">The color.</param>
    /// <param name="backColor">The background.</param>
    public static void DrawText(IDeviceContext dc, string? text, Font? font, Point pt, Color foreColor, Color backColor) =>
        DrawText(dc, text, font, pt, foreColor, backColor, TextFormatFlags.Default);

    /// <summary>Draws text at a point, on a background, with flags.</summary>
    /// <param name="dc">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="pt">Where the text starts.</param>
    /// <param name="foreColor">The color.</param>
    /// <param name="backColor">The background.</param>
    /// <param name="flags">How the text is laid out.</param>
    public static void DrawText(IDeviceContext dc, string? text, Font? font, Point pt, Color foreColor, Color backColor, TextFormatFlags flags)
    {
        var size = MeasureText(text, font, Size.Empty, flags);
        DrawText(dc, text, font, new Rectangle(pt, size), foreColor, backColor, flags);
    }

    /// <summary>Draws text in a rectangle.</summary>
    /// <param name="dc">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="bounds">The rectangle.</param>
    /// <param name="foreColor">The color.</param>
    public static void DrawText(IDeviceContext dc, string? text, Font? font, Rectangle bounds, Color foreColor) =>
        DrawText(dc, text, font, bounds, foreColor, Color.Empty, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

    /// <summary>Draws text in a rectangle, with flags.</summary>
    /// <param name="dc">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="bounds">The rectangle.</param>
    /// <param name="foreColor">The color.</param>
    /// <param name="flags">How the text is laid out.</param>
    public static void DrawText(IDeviceContext dc, string? text, Font? font, Rectangle bounds, Color foreColor, TextFormatFlags flags) =>
        DrawText(dc, text, font, bounds, foreColor, Color.Empty, flags);

    /// <summary>Draws text in a rectangle, on a background.</summary>
    /// <param name="dc">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="bounds">The rectangle.</param>
    /// <param name="foreColor">The color.</param>
    /// <param name="backColor">The background.</param>
    public static void DrawText(IDeviceContext dc, string? text, Font? font, Rectangle bounds, Color foreColor, Color backColor) =>
        DrawText(dc, text, font, bounds, foreColor, backColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

    /// <summary>Draws text in a rectangle, on a background, with flags.</summary>
    /// <param name="dc">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="bounds">The rectangle.</param>
    /// <param name="foreColor">The color.</param>
    /// <param name="backColor">The background.</param>
    /// <param name="flags">How the text is laid out.</param>
    public static void DrawText(IDeviceContext dc, string? text, Font? font, Rectangle bounds, Color foreColor, Color backColor, TextFormatFlags flags)
    {
        if (dc is not Graphics g)
        {
            throw new NotSupportedException("TextRenderer draws only on a Graphics in TabStudio's Forms layer.");
        }

        font ??= Control.DefaultFont;
        if (!backColor.IsEmpty && backColor.A > 0)
        {
            using var brush = new SolidBrush(backColor);
            g.FillRectangle(brush, bounds);
        }

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var padding = Padding(font, flags) / 2;
        var area = Rectangle.FromLTRB(bounds.Left + padding, bounds.Top, bounds.Right - padding, bounds.Bottom);
        var horizontal = (flags & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center : (flags & TextFormatFlags.Right) != 0 ? StringAlignment.Far : StringAlignment.Near;
        var vertical = (flags & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center : (flags & TextFormatFlags.Bottom) != 0 ? StringAlignment.Far : StringAlignment.Near;
        var ellipsis = (flags & (TextFormatFlags.EndEllipsis | TextFormatFlags.WordEllipsis | TextFormatFlags.PathEllipsis)) != 0;
        var shown = (flags & TextFormatFlags.NoPrefix) != 0 ? text : Graphics.Prefixed(text, System.Drawing.Text.HotkeyPrefix.Hide);
        g.DrawText(shown, font, foreColor, area, new TextOptions(horizontal, vertical, Wrap: (flags & TextFormatFlags.WordBreak) != 0, ellipsis, Clip: (flags & TextFormatFlags.NoClipping) == 0));
    }

    /// <summary>The padding Windows adds around text: a sixth of the font's height at each side, unless asked not to.</summary>
    private static int Padding(Font font, TextFormatFlags flags) =>
        (flags & TextFormatFlags.NoPadding) != 0 ? 0 : (flags & TextFormatFlags.LeftAndRightPadding) != 0 ? (int)Math.Ceiling(font.Height / 3f) + 4 : (int)Math.Ceiling(font.Height / 3f);
}

/// <summary>How TextRenderer lays out text.</summary>
[Flags]
public enum TextFormatFlags
{
    /// <summary>The defaults: top left, on one line.</summary>
    Default = 0,

    /// <summary>Left.</summary>
    Left = 0,

    /// <summary>Top.</summary>
    Top = 0,

    /// <summary>Centered horizontally.</summary>
    HorizontalCenter = 1,

    /// <summary>Right.</summary>
    Right = 2,

    /// <summary>Centered vertically.</summary>
    VerticalCenter = 4,

    /// <summary>Bottom.</summary>
    Bottom = 8,

    /// <summary>Wraps at word breaks.</summary>
    WordBreak = 16,

    /// <summary>One line.</summary>
    SingleLine = 32,

    /// <summary>Tab characters expand.</summary>
    ExpandTabs = 64,

    /// <summary>Not clipped.</summary>
    NoClipping = 256,

    /// <summary>External leading.</summary>
    ExternalLeading = 512,

    /// <summary>Ampersands are shown.</summary>
    NoPrefix = 2048,

    /// <summary>Internal.</summary>
    Internal = 4096,

    /// <summary>Text box control behavior.</summary>
    TextBoxControl = 8192,

    /// <summary>Path ellipsis.</summary>
    PathEllipsis = 16384,

    /// <summary>End ellipsis.</summary>
    EndEllipsis = 32768,

    /// <summary>Modify string.</summary>
    ModifyString = 65536,

    /// <summary>Right to left.</summary>
    RightToLeft = 131072,

    /// <summary>Word ellipsis.</summary>
    WordEllipsis = 262144,

    /// <summary>No full width character break.</summary>
    NoFullWidthCharacterBreak = 524288,

    /// <summary>Hide prefix.</summary>
    HidePrefix = 1048576,

    /// <summary>Prefix only.</summary>
    PrefixOnly = 2097152,

    /// <summary>Preserve graphics clipping.</summary>
    PreserveGraphicsClipping = 16777216,

    /// <summary>Preserve graphics translate transform.</summary>
    PreserveGraphicsTranslateTransform = 33554432,

    /// <summary>No padding.</summary>
    NoPadding = 268435456,

    /// <summary>Left and right padding.</summary>
    LeftAndRightPadding = 536870912,

    /// <summary>Glyph overhang padding.</summary>
    GlyphOverhangPadding = 0,
}
