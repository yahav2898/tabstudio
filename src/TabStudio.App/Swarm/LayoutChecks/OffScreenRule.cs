using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace TabStudio.App.Swarm.LayoutChecks;

/// <summary>
/// SWRM002, off screen: a control or an image that a person cannot reach, because it lies outside the screen, outside
/// what its scroll viewer can scroll to, or under the edge of a container that clips it.
/// </summary>
/// <remarks>
/// A list's items below the fold are reachable: their scroll viewer brings them into view. Only the outermost element
/// that is cut is reported, not everything inside it; text is the clipped-text check's.
/// </remarks>
internal static class OffScreenRule
{
    /// <summary>How much of an element may be cut, in dips, before it counts.</summary>
    private const double Tolerance = 2;

    /// <summary>Finds elements that cannot be reached.</summary>
    /// <param name="scan">The app's view on one screen.</param>
    /// <param name="reported">Collects the elements reported, so checks after this one leave what is inside them alone.</param>
    public static IEnumerable<(ScannedElement Element, LayoutIssueSeverity Severity, string Message)> Find(LayoutScan scan, ISet<Visual> reported)
    {
        foreach (var element in scan.Elements)
        {
            if (element.IsTemplatePart || !(element.IsInteractive || element.Visual is Image) || element.Bounds.Width < 1 || element.Bounds.Height < 1
                || IsInside(element.Visual, reported))
            {
                continue;
            }

            var bounds = element.Bounds;
            var shown = bounds.Intersect(element.Reach);
            var where = Where(scan, element);
            var label = LayoutText.Label(element.Visual);
            if (shown == default)
            {
                reported.Add(element.Visual);
                yield return (element, LayoutIssueSeverity.Error, $"{label} is {where}{Distance(scan, bounds)}, so it cannot be seen or tapped.");
            }
            else if (bounds.Width - shown.Width > Tolerance || bounds.Height - shown.Height > Tolerance)
            {
                reported.Add(element.Visual);
                var hiddenWidth = bounds.Width - shown.Width;
                var hiddenHeight = bounds.Height - shown.Height;
                var cut = hiddenWidth >= hiddenHeight
                    ? $"{LayoutText.Dips(hiddenWidth)} of its {LayoutText.Dips(bounds.Width)} dips of width are cut off"
                    : $"{LayoutText.Dips(hiddenHeight)} of its {LayoutText.Dips(bounds.Height)} dips of height are cut off";
                yield return (element, LayoutIssueSeverity.Warning, $"{label} is partly {where}: {cut}.");
            }
        }
    }

    /// <summary>What hides an element: the screen's edge, its scroll viewer's end, or a container.</summary>
    private static string Where(LayoutScan scan, ScannedElement element) =>
        !scan.Screen.Contains(element.Bounds.Deflate(Tolerance)) && element.ScrollDepth == 0 ? "outside the screen"
        : element.ScrollDepth > 0 ? "beyond what its scroll viewer can scroll to"
        : "hidden by a container that clips it";

    /// <summary>How far past which edge of the screen an element starts, for one entirely outside it.</summary>
    private static string Distance(LayoutScan scan, Rect bounds)
    {
        var screen = scan.Screen;
        return bounds.Left >= screen.Right ? $" ({LayoutText.Dips(bounds.Left - screen.Right)} dips past the right edge)"
            : bounds.Right <= screen.Left ? $" ({LayoutText.Dips(screen.Left - bounds.Right)} dips past the left edge)"
            : bounds.Top >= screen.Bottom ? $" ({LayoutText.Dips(bounds.Top - screen.Bottom)} dips below the bottom edge)"
            : bounds.Bottom <= screen.Top ? $" ({LayoutText.Dips(screen.Top - bounds.Bottom)} dips above the top edge)"
            : string.Empty;
    }

    private static bool IsInside(Visual visual, ISet<Visual> elements)
    {
        for (var current = visual.GetVisualParent(); current is not null; current = current.GetVisualParent())
        {
            if (elements.Contains(current))
            {
                return true;
            }
        }

        return false;
    }
}
