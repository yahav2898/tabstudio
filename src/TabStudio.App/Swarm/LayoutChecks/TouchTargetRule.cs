using Avalonia;

namespace TabStudio.App.Swarm.LayoutChecks;

/// <summary>
/// SWRM004, small touch targets: controls a finger cannot hit reliably. On phones and tablets a target needs 44 × 44
/// dips, as Apple's and Google's guidelines ask; dense desktop UI, used with a mouse, needs 24 × 24, WCAG 2.2's
/// minimum target size.
/// </summary>
internal static class TouchTargetRule
{
    /// <summary>The smallest touch target on a phone, a large phone or a tablet, in dips.</summary>
    public const double TouchMinimum = 44;

    /// <summary>The smallest pointer target in a desktop window, in dips.</summary>
    public const double DesktopMinimum = 24;

    /// <summary>Finds controls that are too small to tap.</summary>
    /// <param name="scan">The app's view on one screen.</param>
    /// <param name="offScreen">Elements reported off screen, which are left out.</param>
    public static IEnumerable<(ScannedElement Element, LayoutIssueSeverity Severity, string Message)> Find(LayoutScan scan, IReadOnlySet<Visual> offScreen)
    {
        var desktop = scan.Profile.FormFactor == FormFactor.Desktop;
        var minimum = desktop ? DesktopMinimum : TouchMinimum;
        foreach (var element in scan.Elements)
        {
            var size = element.Bounds.Size;
            if (!element.IsInteractive || offScreen.Contains(element.Visual) || size.Width < 1 || size.Height < 1
                || (size.Width >= minimum - 0.5 && size.Height >= minimum - 0.5))
            {
                continue;
            }

            var severity = size.Width < minimum / 2 || size.Height < minimum / 2 ? LayoutIssueSeverity.Error : LayoutIssueSeverity.Warning;
            var need = desktop
                ? $"in a desktop window, pointer targets need at least {LayoutText.Dips(new Size(DesktopMinimum, DesktopMinimum))}"
                : $"on {LayoutText.Screen(scan.Profile)}, touch targets need at least {LayoutText.Dips(new Size(TouchMinimum, TouchMinimum))} to be tapped reliably";
            yield return (element, severity, $"{LayoutText.Label(element.Visual)} is {LayoutText.Dips(size)} dips; {need}.");
        }
    }
}
