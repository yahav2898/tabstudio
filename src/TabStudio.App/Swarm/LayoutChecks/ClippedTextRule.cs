using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;

namespace TabStudio.App.Swarm.LayoutChecks;

/// <summary>
/// SWRM001, clipped text: text that does not fit the room its layout gives it. It is trimmed with an ellipsis, loses
/// lines at the bottom, runs past the edge of its box, or is cut by a container or the edge of the screen.
/// </summary>
/// <remarks>
/// Text in a scroll viewer that scrolls it into view is not clipped, and neither is text inside a control that is
/// already reported off screen. Rounding of a dip or so is forgiven.
/// </remarks>
internal static class ClippedTextRule
{
    /// <summary>How much text may overflow, in dips, before it counts, so rounding is never reported.</summary>
    private const double Tolerance = 1;

    /// <summary>Finds clipped text.</summary>
    /// <param name="scan">The app's view on one screen.</param>
    /// <param name="offScreen">Elements already reported off screen: their text is not reported again.</param>
    public static IEnumerable<(ScannedElement Element, LayoutIssueSeverity Severity, string Message)> Find(LayoutScan scan, IReadOnlySet<Visual> offScreen)
    {
        foreach (var element in scan.Elements)
        {
            if (element.Visual is not TextBlock text || string.IsNullOrWhiteSpace(text.Text) || IsInside(text, offScreen))
            {
                continue;
            }

            if (Check(scan, element, text) is { } problem)
            {
                yield return (element, problem.Severity, problem.Message);
            }
        }
    }

    private static (LayoutIssueSeverity Severity, string Message)? Check(LayoutScan scan, ScannedElement element, TextBlock text)
    {
        var content = text.Text!;
        var label = LayoutText.Label(text);
        var room = text.Bounds.Size.Deflate(text.Padding);
        if (room.Width < Tolerance || room.Height < Tolerance)
        {
            return (LayoutIssueSeverity.Error, $"{label} has no room: its box is {LayoutText.Dips(text.Bounds.Size)} dips, so none of it shows.");
        }

        var layout = text.TextLayout;
        var lines = layout.TextLines;
        if (lines.Any(line => line.HasCollapsed))
        {
            return (LayoutIssueSeverity.Warning, $"{label} is trimmed with an ellipsis on {LayoutText.Screen(scan.Profile)}: people cannot read all of it.");
        }

        var natural = NaturalSize(text, room);
        if (lines.Count > 0 && lines[^1].FirstTextSourceIndex + lines[^1].Length is var laidOut && laidOut < content.Length && !string.IsNullOrWhiteSpace(content[laidOut..]))
        {
            return (LayoutIssueSeverity.Error, $"{label} is cut off at the bottom: it needs {LayoutText.Dips(natural.Height)} dips of height and has {LayoutText.Dips(room.Height)}.");
        }

        if (layout.Width > room.Width + Tolerance)
        {
            return (LayoutIssueSeverity.Error, $"{label} runs past its box: it needs {LayoutText.Dips(layout.Width)} dips of width and has {LayoutText.Dips(room.Width)}.");
        }

        if (layout.Height > room.Height + Tolerance)
        {
            return (LayoutIssueSeverity.Error, $"{label} is cut off: it needs {LayoutText.Dips(layout.Height)} dips of height and has {LayoutText.Dips(room.Height)}.");
        }

        // Laid out in full, but a container, or the edge of the screen, may hide part of it.
        if (TextArea(text, layout, scan.Root) is { } area && Hidden(area, element.Reach) is { } hidden)
        {
            var edge = scan.Screen.Contains(area.Deflate(Tolerance)) ? "its container" : "the edge of the screen";
            return (LayoutIssueSeverity.Error, $"{label} is cut off by {edge}: {LayoutText.Dips(hidden)} of its {LayoutText.Dips(area.Width)} × {LayoutText.Dips(area.Height)} dips are hidden.");
        }

        return null;
    }

    /// <summary>Where the text itself is drawn, in the root's coordinates, rather than its whole box.</summary>
    private static Rect? TextArea(TextBlock text, TextLayout layout, Visual root)
    {
        if (text.TransformToVisual(root) is not { } toRoot)
        {
            return null;
        }

        // Right-to-left runs can come back with negative or no width; such a run takes no room of its own.
        Rect? area = null;
        // A wrapped line's trailing spaces are not drawn, so they are left out: they may run past the box.
        var drawn = layout.TextLines
            .Where(line => line.Length > line.TrailingWhitespaceLength)
            .SelectMany(line => layout.HitTestTextRange(line.FirstTextSourceIndex, line.Length - line.TrailingWhitespaceLength));
        foreach (var rect in drawn.Select(Normalized))
        {
            if (rect.Width > 0 && rect.Height > 0)
            {
                area = area is { } union ? union.Union(rect) : rect;
            }
        }

        var local = (area ?? new Rect(0, 0, layout.Width, layout.Height)).Translate(new Vector(text.Padding.Left, text.Padding.Top));
        return local.TransformToAABB(toRoot);
    }

    private static Rect Normalized(Rect rect) =>
        new(Math.Min(rect.X, rect.X + rect.Width), Math.Min(rect.Y, rect.Y + rect.Height), Math.Abs(rect.Width), Math.Abs(rect.Height));

    /// <summary>How much of the text is hidden, as a size, when more than the tolerance is.</summary>
    private static Size? Hidden(Rect area, Rect reach)
    {
        if (area.Width < Tolerance || area.Height < Tolerance)
        {
            return null;
        }

        var shown = area.Intersect(reach);
        var hiddenWidth = area.Width - shown.Width;
        var hiddenHeight = area.Height - shown.Height;
        if (shown == default)
        {
            return area.Size;
        }

        return hiddenWidth > Tolerance || hiddenHeight > Tolerance ? new Size(Math.Max(0, hiddenWidth), Math.Max(0, hiddenHeight)) : null;
    }

    /// <summary>The size the text would take with the box's width and as much height as it needs.</summary>
    private static Size NaturalSize(TextBlock text, Size room)
    {
        using var layout = new TextLayout(
            text.Text,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
            text.FontSize,
            text.Foreground,
            text.TextAlignment,
            text.TextWrapping,
            TextTrimming.None,
            flowDirection: text.FlowDirection,
            maxWidth: text.TextWrapping == TextWrapping.NoWrap ? double.PositiveInfinity : room.Width,
            lineHeight: text.LineHeight,
            letterSpacing: text.LetterSpacing);
        return new Size(layout.Width, layout.Height);
    }

    private static bool IsInside(Visual visual, IReadOnlySet<Visual> elements)
    {
        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (elements.Contains(current))
            {
                return true;
            }
        }

        return false;
    }
}
