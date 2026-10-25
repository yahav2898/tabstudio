using System.Collections.Concurrent;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// Measures text with Avalonia's text layout, the same layout <see cref="DisplayListRenderer"/> draws it with. Safe on
/// any thread: the Forms thread measures while the UI thread draws.
/// </summary>
/// <remarks>
/// Without Avalonia (no font manager), text is estimated from its length, so pure layout code still works.
/// </remarks>
internal static class TextMetrics
{
    /// <summary>The share of the em size an average character takes when there is no font manager to measure with.</summary>
    private const double EstimatedCharacterWidth = 0.55;

    /// <summary>The share of the em size a line takes when there is no font manager to measure with.</summary>
    private const double EstimatedLineHeight = 1.25;

    private static readonly ConcurrentDictionary<FontSpec, double> LineHeights = new();
    private static int _available = -1;

    /// <summary>Whether Avalonia's text layout is available, which it is once Avalonia is initialized.</summary>
    public static bool IsAvailable
    {
        get
        {
            if (_available < 0)
            {
                try
                {
                    _ = FontManager.Current;
                    _available = 1;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            return _available == 1;
        }
    }

    /// <summary>The height of one line of text in a font, in device-independent pixels.</summary>
    /// <param name="font">The font.</param>
    public static double LineHeight(FontSpec font) => LineHeights.GetOrAdd(font, static spec =>
    {
        if (!IsAvailable)
        {
            return Math.Ceiling(spec.Size * EstimatedLineHeight);
        }

        using var layout = CreateLayout("Ag", spec, null, double.PositiveInfinity, double.PositiveInfinity, TextAlignment.Left, TextWrapping.NoWrap, trimming: false);
        return layout.Height;
    });

    /// <summary>Measures text on one line, or wrapped at a width.</summary>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="font">The font.</param>
    /// <param name="maxWidth">The width to wrap at, or infinity for no wrapping.</param>
    /// <returns>The width and height of the text, at least one line high.</returns>
    public static Avalonia.Size Measure(string text, FontSpec font, double maxWidth = double.PositiveInfinity)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return new Avalonia.Size(0, LineHeight(font));
        }

        if (!IsAvailable)
        {
            return Estimate(text, font, maxWidth);
        }

        var wrapping = double.IsPositiveInfinity(maxWidth) ? TextWrapping.NoWrap : TextWrapping.Wrap;
        using var layout = CreateLayout(text, font, null, maxWidth, double.PositiveInfinity, TextAlignment.Left, wrapping, trimming: false);
        return new Avalonia.Size(Math.Max(layout.WidthIncludingTrailingWhitespace, layout.Width), Math.Max(layout.Height, LineHeight(font)));
    }

    /// <summary>Creates a text layout of a spec, as both measuring and drawing do.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="foreground">The text's brush, or <see langword="null"/> to only measure.</param>
    /// <param name="maxWidth">The width to align, wrap or trim at.</param>
    /// <param name="maxHeight">The height to stop laying out lines at.</param>
    /// <param name="alignment">The horizontal alignment within <paramref name="maxWidth"/>.</param>
    /// <param name="wrapping">Whether lines wrap at <paramref name="maxWidth"/>.</param>
    /// <param name="trimming">Whether text that does not fit ends with an ellipsis.</param>
    public static TextLayout CreateLayout(
        string text,
        FontSpec font,
        IBrush? foreground,
        double maxWidth,
        double maxHeight,
        TextAlignment alignment,
        TextWrapping wrapping,
        bool trimming) =>
        new(
            text,
            font.Typeface,
            font.Size,
            foreground,
            alignment,
            wrapping,
            trimming ? TextTrimming.CharacterEllipsis : null,
            font.Decorations,
            FlowDirection.LeftToRight,
            maxWidth,
            maxHeight);

    /// <summary>The character index nearest to a point in laid-out text, for placing a caret where a finger taps.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="x">The horizontal position from the start of the text.</param>
    /// <param name="y">The vertical position from the top of the text.</param>
    /// <param name="maxWidth">The width the text wraps at, or infinity.</param>
    public static int CharacterIndexAt(string text, FontSpec font, double x, double y, double maxWidth = double.PositiveInfinity)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        if (!IsAvailable)
        {
            return Math.Clamp((int)Math.Round(x / (font.Size * EstimatedCharacterWidth)), 0, text.Length);
        }

        var wrapping = double.IsPositiveInfinity(maxWidth) ? TextWrapping.NoWrap : TextWrapping.Wrap;
        using var layout = CreateLayout(text, font, null, maxWidth, double.PositiveInfinity, TextAlignment.Left, wrapping, trimming: false);
        var hit = layout.HitTestPoint(new Avalonia.Point(x, y));
        return Math.Clamp(hit.TextPosition + (hit.IsTrailing ? 1 : 0), 0, text.Length);
    }

    private static Avalonia.Size Estimate(string text, FontSpec font, double maxWidth)
    {
        var lines = text.Split('\n');
        var characterWidth = font.Size * EstimatedCharacterWidth;
        var lineHeight = LineHeight(font);
        double width = 0;
        var count = 0;
        foreach (var line in lines)
        {
            var lineWidth = line.TrimEnd('\r').Length * characterWidth;
            if (!double.IsPositiveInfinity(maxWidth) && lineWidth > maxWidth && maxWidth > 0)
            {
                count += (int)Math.Ceiling(lineWidth / maxWidth);
                width = maxWidth;
            }
            else
            {
                count++;
                width = Math.Max(width, lineWidth);
            }
        }

        return new Avalonia.Size(width, Math.Max(1, count) * lineHeight);
    }
}
