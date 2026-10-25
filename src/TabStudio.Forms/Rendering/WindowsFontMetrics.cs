using System.Collections.Concurrent;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// The metrics of the common Windows fonts, so forms designed on Windows keep their layout where those fonts are
/// substituted: a font's height is the Windows font's line spacing, and the substitute is drawn at the size whose
/// average character width matches the Windows font's, so automatically sized labels and buttons keep their size.
/// </summary>
internal static class WindowsFontMetrics
{
    /// <summary>The letters Windows averages character widths over, as Windows Forms' automatic scaling does.</summary>
    private const string AverageSample = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// Line spacing (as Font.Height uses it), text line height (as labels measure it) and average character width,
    /// as shares of the em size, from the fonts' metrics and the sizes Windows Forms gives controls in them.
    /// </summary>
    private static readonly Dictionary<string, (double LineSpacing, double TextHeight, double AverageWidth)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Segoe UI"] = (1.3301, 15.0 / 12, 7.0 / 12),
        ["Segoe UI Semibold"] = (1.3301, 15.0 / 12, 7.3 / 12),
        ["Segoe UI Light"] = (1.3301, 15.0 / 12, 6.7 / 12),
        ["Microsoft Sans Serif"] = (1.1318, 13.0 / 11, 6.0 / 11),
        ["MS Sans Serif"] = (1.1318, 13.0 / 11, 6.0 / 11),
        ["Tahoma"] = (1.2070, 13.0 / 11, 6.0 / 11),
        ["Verdana"] = (1.2153, 13.0 / 11, 7.0 / 11),
        ["Arial"] = (1.1499, 14.0 / 12, 6.5 / 12),
        ["Calibri"] = (1.2207, 15.0 / 12, 6.2 / 12),
        ["Times New Roman"] = (1.1499, 14.0 / 12, 6.0 / 12),
        ["Georgia"] = (1.1362, 14.0 / 12, 6.9 / 12),
        ["Courier New"] = (1.1328, 14.0 / 12, 7.2 / 12),
        ["Consolas"] = (1.1709, 14.0 / 12, 6.6 / 12),
        ["Lucida Console"] = (1.0000, 12.0 / 12, 7.2 / 12),
        ["Trebuchet MS"] = (1.1641, 14.0 / 12, 6.6 / 12),
        ["Comic Sans MS"] = (1.3936, 17.0 / 12, 7.0 / 12),
    };

    private static readonly ConcurrentDictionary<string, double> SubstituteWidths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The line spacing of a Windows font as a share of its em size, when the font is a known one.</summary>
    /// <param name="family">The Windows family name.</param>
    public static double? LineSpacing(string family) => Known.TryGetValue(family, out var metrics) ? metrics.LineSpacing : null;

    /// <summary>The height of a line of text in a substituted Windows font, as labels measure it, or NaN.</summary>
    /// <param name="family">The Windows family name.</param>
    /// <param name="drawingFamily">The family the text is drawn with.</param>
    /// <param name="emPixels">The em size in pixels.</param>
    public static double TextLineHeight(string family, string drawingFamily, double emPixels) =>
        !string.Equals(family, drawingFamily, StringComparison.OrdinalIgnoreCase) && Known.TryGetValue(family, out var metrics)
            ? Math.Round(emPixels * metrics.TextHeight)
            : double.NaN;

    /// <summary>
    /// The factor to scale a substitute's size by so its average character width is the Windows font's: 1 when the
    /// font is not substituted or not known.
    /// </summary>
    /// <param name="family">The Windows family name.</param>
    /// <param name="drawingFamily">The family the text is drawn with.</param>
    public static double WidthScale(string family, string drawingFamily)
    {
        if (string.Equals(family, drawingFamily, StringComparison.OrdinalIgnoreCase) || !Known.TryGetValue(family, out var metrics) || !TextMetrics.IsAvailable)
        {
            return 1;
        }

        var substitute = SubstituteWidths.GetOrAdd(drawingFamily, static name =>
        {
            const double size = 100;
            var spec = new FontSpec(name, size, Bold: false, Italic: false, Underline: false, Strikeout: false);
            return TextMetrics.Measure(AverageSample, spec).Width / AverageSample.Length / size;
        });

        return substitute <= 0 ? 1 : Math.Clamp(metrics.AverageWidth / substitute, 0.8, 1.2);
    }
}
