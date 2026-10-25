using System.Drawing.Text;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>Drawing and measuring text.</summary>
public sealed partial class Graphics
{
    /// <summary>Draws text at a point.</summary>
    /// <param name="s">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="brush">The brush; the color of a gradient's start for gradients.</param>
    /// <param name="x">The left of the text.</param>
    /// <param name="y">The top of the text.</param>
    public void DrawString(string? s, Font font, Brush brush, float x, float y) => DrawString(s, font, brush, new PointF(x, y), null);

    /// <summary>Draws text at a point.</summary>
    /// <param name="s">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="brush">The brush.</param>
    /// <param name="point">The top left of the text.</param>
    public void DrawString(string? s, Font font, Brush brush, PointF point) => DrawString(s, font, brush, point, null);

    /// <summary>Draws text at a point, aligned to it as the format says.</summary>
    /// <param name="s">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="brush">The brush.</param>
    /// <param name="x">The x the text is aligned to.</param>
    /// <param name="y">The y the text is aligned to.</param>
    /// <param name="format">The alignment.</param>
    public void DrawString(string? s, Font font, Brush brush, float x, float y, StringFormat? format) => DrawString(s, font, brush, new PointF(x, y), format);

    /// <summary>Draws text at a point, aligned to it as the format says.</summary>
    /// <param name="s">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="brush">The brush.</param>
    /// <param name="point">The point the text is aligned to.</param>
    /// <param name="format">The alignment.</param>
    public void DrawString(string? s, Font font, Brush brush, PointF point, StringFormat? format)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(brush);
        if (string.IsNullOrEmpty(s))
        {
            return;
        }

        var text = Prefixed(s, format?.HotkeyPrefix ?? HotkeyPrefix.None);
        var size = TextMetrics.Measure(text, font.Spec);
        var x = point.X - Align(format?.Alignment, (float)size.Width);
        var y = point.Y - Align(format?.LineAlignment, (float)size.Height);
        Record(new TextCommand(text, font.Spec, brush.ToSpec().MainColor, new RectangleF(x, y, float.PositiveInfinity, float.PositiveInfinity), TextOptions.AtPoint));
    }

    /// <summary>Draws text in a rectangle, wrapping at its width and clipped to it.</summary>
    /// <param name="s">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="brush">The brush.</param>
    /// <param name="layoutRectangle">The rectangle.</param>
    public void DrawString(string? s, Font font, Brush brush, RectangleF layoutRectangle) => DrawString(s, font, brush, layoutRectangle, null);

    /// <summary>Draws text in a rectangle, laid out as the format says.</summary>
    /// <param name="s">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="brush">The brush.</param>
    /// <param name="layoutRectangle">The rectangle; a zero width or height does not limit the text.</param>
    /// <param name="format">Alignment, wrapping, trimming and clipping.</param>
    public void DrawString(string? s, Font font, Brush brush, RectangleF layoutRectangle, StringFormat? format)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(brush);
        if (string.IsNullOrEmpty(s))
        {
            return;
        }

        var flags = format?.FormatFlags ?? 0;
        var bounds = new RectangleF(
            layoutRectangle.X,
            layoutRectangle.Y,
            layoutRectangle.Width > 0 ? layoutRectangle.Width : float.PositiveInfinity,
            layoutRectangle.Height > 0 ? layoutRectangle.Height : float.PositiveInfinity);
        var options = new TextOptions(
            format?.Alignment ?? StringAlignment.Near,
            format?.LineAlignment ?? StringAlignment.Near,
            Wrap: (flags & StringFormatFlags.NoWrap) == 0 && !float.IsPositiveInfinity(bounds.Width),
            Ellipsis: format?.Trimming is StringTrimming.EllipsisCharacter or StringTrimming.EllipsisWord or StringTrimming.EllipsisPath,
            Clip: (flags & StringFormatFlags.NoClip) == 0);
        Record(new TextCommand(Prefixed(s, format?.HotkeyPrefix ?? HotkeyPrefix.None), font.Spec, brush.ToSpec().MainColor, bounds, options));
    }

    /// <summary>Measures text on one line per line break.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    public SizeF MeasureString(string? text, Font font) => MeasureString(text, font, SizeF.Empty, null);

    /// <summary>Measures text wrapped at a width.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="width">The width to wrap at.</param>
    public SizeF MeasureString(string? text, Font font, int width) => MeasureString(text, font, new SizeF(width, 0), null);

    /// <summary>Measures text wrapped at a width.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="width">The width to wrap at.</param>
    /// <param name="format">The layout.</param>
    public SizeF MeasureString(string? text, Font font, int width, StringFormat? format) => MeasureString(text, font, new SizeF(width, 0), format);

    /// <summary>Measures text in an area.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="layoutArea">The area; a zero width does not wrap.</param>
    public SizeF MeasureString(string? text, Font font, SizeF layoutArea) => MeasureString(text, font, layoutArea, null);

    /// <summary>Measures text drawn at a point.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="origin">The point; it does not change the size.</param>
    /// <param name="stringFormat">The layout.</param>
    public SizeF MeasureString(string? text, Font font, PointF origin, StringFormat? stringFormat) => MeasureString(text, font, SizeF.Empty, stringFormat);

    /// <summary>Measures text in an area.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="layoutArea">The area; a zero width does not wrap.</param>
    /// <param name="stringFormat">The layout.</param>
    public SizeF MeasureString(string? text, Font font, SizeF layoutArea, StringFormat? stringFormat) =>
        MeasureString(text, font, layoutArea, stringFormat, out _, out _);

    /// <summary>Measures text in an area, with how much of it fits.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="layoutArea">The area; a zero width does not wrap.</param>
    /// <param name="stringFormat">The layout.</param>
    /// <param name="charactersFitted">The number of characters measured.</param>
    /// <param name="linesFilled">The number of lines.</param>
    public SizeF MeasureString(string? text, Font font, SizeF layoutArea, StringFormat? stringFormat, out int charactersFitted, out int linesFilled)
    {
        ArgumentNullException.ThrowIfNull(font);
        var value = Prefixed(text ?? string.Empty, stringFormat?.HotkeyPrefix ?? HotkeyPrefix.None);
        var noWrap = ((stringFormat?.FormatFlags ?? 0) & StringFormatFlags.NoWrap) != 0;
        var width = layoutArea.Width > 0 && !noWrap ? layoutArea.Width : double.PositiveInfinity;
        var size = TextMetrics.Measure(value, font.Spec, width);
        charactersFitted = value.Length;
        linesFilled = Math.Max(1, (int)Math.Round(size.Height / TextMetrics.LineHeight(font.Spec)));
        var height = layoutArea.Height > 0 ? Math.Min(size.Height, layoutArea.Height) : size.Height;
        return new SizeF((float)Math.Ceiling(size.Width), (float)Math.Ceiling(height));
    }

    /// <summary>Draws text on behalf of a control: in a rectangle, with options of the Forms layer.</summary>
    /// <param name="text">The text, without access-key ampersands.</param>
    /// <param name="font">The font.</param>
    /// <param name="color">The color.</param>
    /// <param name="bounds">The layout rectangle.</param>
    /// <param name="options">The layout options.</param>
    internal void DrawText(string text, Font font, Color color, RectangleF bounds, TextOptions options)
    {
        if (text.Length > 0)
        {
            Record(new TextCommand(text, font.Spec, SystemColorValues.Argb(color), bounds, options));
        }
    }

    /// <summary>The text with access-key ampersands removed, an escaped ampersand (<c>&amp;&amp;</c>) kept as one.</summary>
    /// <param name="text">The text.</param>
    /// <param name="prefix">How ampersands are shown.</param>
    internal static string Prefixed(string text, HotkeyPrefix prefix)
    {
        if (prefix == HotkeyPrefix.None || !text.Contains('&', StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '&' && i + 1 < text.Length)
            {
                i++;
            }

            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    private static float Align(StringAlignment? alignment, float size) => alignment switch
    {
        StringAlignment.Center => size / 2,
        StringAlignment.Far => size,
        _ => 0,
    };
}
