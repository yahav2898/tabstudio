using Avalonia;

namespace TabStudio.App.Swarm.LayoutChecks;

/// <summary>
/// The layout checks, over the visual tree of an app on one screen: clipped text (SWRM001), controls off screen
/// (SWRM002), overlapping controls (SWRM003), small touch targets (SWRM004) and low contrast (SWRM005). Each rule
/// is one a designer would agree with, tuned to raise few false alarms; <see cref="Settings"/> turns each on or off.
/// </summary>
/// <remarks>
/// It walks the laid-out view once and lets every rule read that walk, so it is quick enough to run on every screen
/// after every start of the app, on the tablet or on a phone. It never changes the view.
/// </remarks>
public sealed class LayoutInspector : ILayoutInspector
{
    /// <summary>Creates the checks with every rule on.</summary>
    public LayoutInspector()
        : this(LayoutCheckSettings.All)
    {
    }

    /// <summary>Creates the checks with some rules on.</summary>
    /// <param name="settings">The rules that run.</param>
    public LayoutInspector(LayoutCheckSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
    }

    /// <summary>The rules that run.</summary>
    public LayoutCheckSettings Settings { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<LayoutIssue> Inspect(Visual root, ScreenProfile profile)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(profile);
        var settings = Settings;
        var scan = LayoutScan.Run(root, profile);
        var found = new List<LayoutIssue>();
        var offScreen = new HashSet<Visual>(ReferenceEqualityComparer.Instance);

        // Off screen first: what lies outside is not reported again for its text or its size.
        if (settings.OffScreen)
        {
            Add(found, scan, LayoutIssueKind.OffScreen, OffScreenRule.Find(scan, offScreen));
        }

        if (settings.ClippedText)
        {
            Add(found, scan, LayoutIssueKind.ClippedText, ClippedTextRule.Find(scan, offScreen));
        }

        if (settings.Overlap)
        {
            Add(found, scan, LayoutIssueKind.Overlap, OverlapRule.Find(scan, offScreen));
        }

        if (settings.SmallTouchTarget)
        {
            Add(found, scan, LayoutIssueKind.SmallTouchTarget, TouchTargetRule.Find(scan, offScreen));
        }

        if (settings.LowContrast)
        {
            Add(found, scan, LayoutIssueKind.LowContrast, ContrastRule.Find(scan));
        }

        // One issue of a kind per element, the most serious; the most serious issues first.
        return
        [
            .. found
                .GroupBy(issue => (issue.Kind, issue.ElementPath))
                .Select(group => group.MaxBy(issue => issue.Severity)!)
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.Kind)
                .ThenBy(issue => issue.ElementPath, StringComparer.Ordinal),
        ];
    }

    private static void Add(List<LayoutIssue> found, LayoutScan scan, LayoutIssueKind kind, IEnumerable<(ScannedElement Element, LayoutIssueSeverity Severity, string Message)> results)
    {
        foreach (var (element, severity, message) in results)
        {
            // A part of a control's template is reported as the control; its own bounds show where exactly.
            if (ElementPaths.Describe(scan.Root, element.Visual) is { } path)
            {
                found.Add(new LayoutIssue(kind, severity, message, path, element.Bounds));
            }
        }
    }
}
