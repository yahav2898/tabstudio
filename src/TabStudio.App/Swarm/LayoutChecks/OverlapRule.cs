using Avalonia;
using Avalonia.VisualTree;

namespace TabStudio.App.Swarm.LayoutChecks;

/// <summary>
/// SWRM003, overlapping controls: two controls a person can tap that lie over each other, so taps meant for the one
/// below go to the one on top. The one below is reported, with the one that covers it.
/// </summary>
/// <remarks>
/// A control inside another, such as a button in a list item, is not an overlap, and neither is content that scrolls
/// under a control that stays, such as a list under a floating button: it scrolls out from under it.
/// </remarks>
internal static class OverlapRule
{
    /// <summary>How much two controls may share, in dips each way, before it counts: borders that touch never do.</summary>
    private const double Tolerance = 1;

    /// <summary>The share of the smaller control that, once covered, makes an overlap an error rather than a warning.</summary>
    private const double SeriousShare = 0.25;

    /// <summary>Finds overlapping controls.</summary>
    /// <param name="scan">The app's view on one screen.</param>
    /// <param name="offScreen">Elements reported off screen, which are left out.</param>
    public static IEnumerable<(ScannedElement Element, LayoutIssueSeverity Severity, string Message)> Find(LayoutScan scan, IReadOnlySet<Visual> offScreen)
    {
        var targets = scan.Elements.Where(e => e.IsInteractive && !offScreen.Contains(e.Visual) && e.Reachable.Width > Tolerance && e.Reachable.Height > Tolerance).ToList();
        var covered = new HashSet<Visual>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < targets.Count; i++)
        {
            for (var j = i + 1; j < targets.Count; j++)
            {
                var (a, b) = (targets[i], targets[j]);
                // Content of one scroll viewer scrolls out from under a control in another, as a document's
                // content does from under the bottom panel's tab strip, which scrolls on its own.
                if (a.ScrollDepth != b.ScrollDepth || !ReferenceEquals(ScrollOwner(a.Visual), ScrollOwner(b.Visual))
                    || a.Visual.IsVisualAncestorOf(b.Visual) || b.Visual.IsVisualAncestorOf(a.Visual))
                {
                    continue;
                }

                var shared = a.Reachable.Intersect(b.Reachable);
                if (shared.Width <= Tolerance || shared.Height <= Tolerance)
                {
                    continue;
                }

                var (top, below) = OnTop(scan, shared, a, b);
                if (!covered.Add(below.Visual))
                {
                    continue;
                }

                var smaller = Math.Min(Area(a.Reachable), Area(b.Reachable));
                var severity = Area(shared) >= SeriousShare * smaller ? LayoutIssueSeverity.Error : LayoutIssueSeverity.Warning;
                var topLabel = LayoutText.Label(top.Visual);
                yield return (below, severity, $"{LayoutText.Label(below.Visual)} lies under {topLabel}: {LayoutText.Dips(shared.Size)} dips of them overlap, and a tap there goes to {topLabel}.");
            }
        }
    }

    /// <summary>Which of two controls a tap in their shared area reaches, as a finger finds it.</summary>
    private static (ScannedElement Top, ScannedElement Below) OnTop(LayoutScan scan, Rect shared, ScannedElement a, ScannedElement b)
    {
        var hit = ScreenHitTest.ElementAt(scan.Root, shared.Center);
        return hit is not null && (ReferenceEquals(hit, a.Visual) || a.Visual.IsVisualAncestorOf(hit)) ? (a, b) : (b, a);
    }

    private static double Area(Rect rect) => rect.Width * rect.Height;

    /// <summary>The scroll viewer a control scrolls in, if any.</summary>
    private static Avalonia.Controls.ScrollViewer? ScrollOwner(Visual visual) =>
        visual.GetVisualAncestors().OfType<Avalonia.Controls.ScrollViewer>().FirstOrDefault();
}
