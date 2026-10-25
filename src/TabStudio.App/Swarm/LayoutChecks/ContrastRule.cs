using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TabStudio.App.Swarm.LayoutChecks;

/// <summary>
/// SWRM005, low contrast: text whose color is too close to what is behind it to read comfortably, below WCAG AA's
/// 4.5:1, or 3:1 for large text. The colors are the ones a person sees: the text's brush and opacity, over the
/// backgrounds of the elements around it, each with its own opacity.
/// </summary>
/// <remarks>
/// Text on a gradient or a picture is left alone: its contrast changes from one letter to the next, and guessing
/// would raise false alarms. Text of a disabled control is exempt, as WCAG exempts inactive controls.
/// </remarks>
internal static class ContrastRule
{
    /// <summary>Finds text with too little contrast.</summary>
    /// <param name="scan">The app's view on one screen.</param>
    public static IEnumerable<(ScannedElement Element, LayoutIssueSeverity Severity, string Message)> Find(LayoutScan scan)
    {
        foreach (var element in scan.Elements)
        {
            if (element.Visual is not Control { IsEffectivelyEnabled: true } text || TextOf(text) is not { Length: > 0 } content || string.IsNullOrWhiteSpace(content)
                || TextElement.GetForeground(text) is not ISolidColorBrush foreground || Background(text, scan.Profile) is not { } background)
            {
                continue;
            }

            var color = ContrastMath.WithOpacity(foreground.Color, foreground.Opacity * element.Opacity);
            var ratio = ContrastMath.Ratio(ContrastMath.Over(color, background), background);
            var large = ContrastMath.IsLargeText(TextElement.GetFontSize(text), TextElement.GetFontWeight(text));
            var minimum = ContrastMath.Minimum(large);
            if (ratio >= minimum - 0.005)
            {
                continue;
            }

            var severity = ratio < (large ? 2 : ContrastMath.LargeTextMinimum) ? LayoutIssueSeverity.Error : LayoutIssueSeverity.Warning;
            var kind = large ? "large text" : "text this size";
            yield return (element, severity, string.Create(
                CultureInfo.InvariantCulture,
                $"{LayoutText.Label(text)} has a contrast of {ratio:0.0}:1 with its background; {kind} needs at least {minimum:0.#}:1 (WCAG AA)."));
        }
    }

    /// <summary>The text an element draws: a text block's, or a text box's typed text.</summary>
    private static string? TextOf(Control control) => control switch
    {
        TextBlock block => block.Text,
        TextPresenter presenter => presenter.Text,
        _ => null,
    };

    /// <summary>
    /// The opaque color behind an element: the backgrounds of the element and those around it, from the nearest
    /// opaque one up, blended as they are drawn. <see langword="null"/> when one of them is not a plain color.
    /// </summary>
    internal static Color? Background(Visual visual, ScreenProfile profile)
    {
        var layers = new List<Color>();
        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (BackgroundOf(current) is not { } brush)
            {
                continue;
            }

            if (brush is not ISolidColorBrush solid)
            {
                return null;
            }

            var color = ContrastMath.WithOpacity(solid.Color, solid.Opacity * current.Opacity);
            layers.Add(color);
            if (color.A == 255)
            {
                break;
            }
        }

        // Under every layer, a screen in its theme's color.
        var seen = profile.IsDark ? Colors.Black : Colors.White;
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            seen = ContrastMath.Over(layers[i], seen);
        }

        return seen;
    }

    /// <summary>The background an element draws itself, if any.</summary>
    private static IBrush? BackgroundOf(Visual visual) => visual switch
    {
        Panel panel => panel.Background,
        Border border => border.Background,
        ContentPresenter presenter => presenter.Background,
        TextBlock block => block.Background,
        TopLevel topLevel => topLevel.Background,
        _ => null,
    };
}
