using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Controls;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.LayoutChecks;
using TabStudio.App.Tests.Theming;

namespace TabStudio.App.Tests.Design;

/// <summary>A screen a panel of the audit is shown on.</summary>
/// <param name="Name">Its name in the screenshots' names, such as "portrait".</param>
/// <param name="Width">Its width in dips.</param>
/// <param name="Height">Its height in dips.</param>
/// <param name="FormFactor">The kind of device it is, for the layout checks.</param>
internal sealed record AuditScreen(string Name, double Width, double Height, FormFactor FormFactor);

/// <summary>
/// The design audit of the panels 0.15 and 0.16 added (docs/design/audit-0.16.md). A panel is shown in the real IDE on
/// a landscape tablet (1280 × 800), a portrait tablet (800 × 1280) and a phone (412 × 915), in the dark and the light
/// theme; each is saved as a screenshot (<c>screenshots/audit/</c>) and checked against the design spec:
/// <list type="bullet">
/// <item>the layout checks of docs/layout-checks.md: no clipped text, nothing off screen, no overlapping controls, no
/// text below WCAG AA;</item>
/// <item>touch targets of at least 34 pixels each way (list rows 32), the smallest the IDE's own controls use;</item>
/// <item>text trimmed with an ellipsis has its whole text in a tooltip;</item>
/// <item>a single line of text in a fixed height is centered in it, not stretched (<see cref="StretchedText"/>).</item>
/// </list>
/// Every problem goes to <c>screenshots/audit/panel-audit.txt</c>, beside the pictures that show it, and fails the test.
/// </summary>
internal static partial class PanelAudit
{
    /// <summary>The smallest touch target, in dips: the IDE's compact tool buttons and secondary buttons.</summary>
    public const double MinimumTarget = 34;

    /// <summary>The smallest list row, in dips (docs/design/README.md, "Touch targets and density").</summary>
    public const double MinimumRow = 32;

    private const string ReportName = "panel-audit.txt";

    private static readonly Lock ReportGate = new();

    /// <summary>The three screens, in the order the audit shows them.</summary>
    public static IReadOnlyList<AuditScreen> Screens { get; } =
    [
        new("landscape", 1280, 800, FormFactor.Tablet),
        new("portrait", 800, 1280, FormFactor.Tablet),
        new("phone", 412, 915, FormFactor.Phablet),
    ];

    /// <summary>The folder the audit's screenshots go to.</summary>
    public static string Folder
    {
        get
        {
            var root = Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS") ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
            return Directory.CreateDirectory(Path.Combine(root, "audit")).FullName;
        }
    }

    /// <summary>
    /// Shows a panel on every screen in both themes, saves each, and checks each. It fails once every screenshot is saved,
    /// with every problem found.
    /// </summary>
    /// <param name="window">The window the IDE is in; it is resized to each screen.</param>
    /// <param name="name">The screenshots' name, such as "audit-services"; the screen and the theme are added.</param>
    /// <param name="panel">Finds the panel in the window, after the window has been resized.</param>
    /// <param name="show">Brings the panel into view on a screen, such as opening the side bar's drawer in portrait.</param>
    public static async Task RunAsync(Window window, string name, Func<Control> panel, Func<AuditScreen, Task>? show = null)
    {
        var problems = new List<string>();
        var notes = new List<string>();
        foreach (var screen in Screens)
        {
            Resize(window, screen.Width, screen.Height);
            if (show is not null)
            {
                await show(screen);
            }

            await SettleAsync();
            foreach (var dark in new[] { true, false })
            {
                using (dark ? null : ThemeScope.Light())
                {
                    await SettleAsync();
                    var shot = $"{name}-{screen.Name}-{(dark ? "dark" : "light")}";
                    UiTestHelpers.SaveFrame(window, Path.Combine(Folder, shot + ".png"));
                    var root = Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS") ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
                    var accessible = Accessibility.AccessibilityReport.Check(window, root, "audit/" + shot);
                    if (Accessibility.AccessibilityReport.Enforced)
                    {
                        problems.AddRange(accessible.Select(f => $"{shot}: accessibility {f.Kind}: {f.Path}: {f.Text}"));
                    }
                    foreach (var (isProblem, text) in Check(window, panel(), screen, dark))
                    {
                        (isProblem ? problems : notes).Add($"{shot}: {text}");
                    }
                }
            }
        }

        await SettleAsync();
        lock (ReportGate)
        {
            File.AppendAllLines(Path.Combine(Folder, ReportName), problems.Select(p => "PROBLEM " + p).Concat(notes.Select(n => "note    " + n)));
        }

        Assert.True(problems.Count == 0, $"{name} does not follow the design spec:\n" + string.Join("\n", problems.Take(60)));
    }

    /// <summary>The problems (and, marked as not problems, the notes) in a panel on one screen.</summary>
    /// <param name="window">The window, which fills the screen.</param>
    /// <param name="panel">The panel.</param>
    /// <param name="screen">The screen.</param>
    /// <param name="dark">Whether the dark theme shows.</param>
    internal static IEnumerable<(bool IsProblem, string Text)> Check(Window window, Control panel, AuditScreen screen, bool dark)
    {
        var profile = new ScreenProfile(screen.Name, screen.FormFactor, window.Bounds.Width, window.Bounds.Height, IsDark: dark);
        var scan = LayoutScan.Run(window, profile);
        bool InPanel(Visual visual) => ReferenceEquals(visual, panel) || panel.IsVisualAncestorOf(visual);

        var offScreen = new HashSet<Visual>(ReferenceEqualityComparer.Instance);
        var found = new List<(ScannedElement Element, LayoutIssueSeverity Severity, string Message, string Code)>();
        found.AddRange(OffScreenRule.Find(scan, offScreen).Select(r => (r.Element, r.Severity, r.Message, "SWRM002")));
        found.AddRange(ClippedTextRule.Find(scan, offScreen).Select(r => (r.Element, r.Severity, r.Message, "SWRM001")));
        found.AddRange(OverlapRule.Find(scan, offScreen).Select(r => (r.Element, r.Severity, r.Message, "SWRM003")));
        found.AddRange(ContrastRule.Find(scan).Select(r => (r.Element, r.Severity, r.Message, "SWRM005")));
        var targets = scan.Elements.Where(e => e.IsInteractive).ToList();
        foreach (var (element, severity, message, code) in found.Where(f => InPanel(f.Element.Visual) && !InDataGrid(f.Element.Visual)))
        {
            // An adaptive bar folds an item to its icon by giving its label no room, and parks what goes into More off
            // screen: that is how it never clips (docs/design/README.md, "Adaptive bars").
            if (IsFolded(element.Visual))
            {
                continue;
            }

            // A dip or so cut by rounding is not text anyone misses.
            if (code == "SWRM001" && RoundingOnly().IsMatch(message))
            {
                yield return (false, $"{code} {message}");
                continue;
            }

            // The overlap rule lets content that scrolls reach as far as it scrolls; what counts here is what shows now.
            if (code == "SWRM003" && !targets.Any(t => !ReferenceEquals(t.Visual, element.Visual) && !t.Visual.IsVisualAncestorOf(element.Visual)
                    && !element.Visual.IsVisualAncestorOf(t.Visual) && Shown(t.Visual, window).Intersect(Shown(element.Visual, window)) is { Width: > 1, Height: > 1 }))
            {
                continue;
            }

            var trimmed = code == "SWRM001" && severity == LayoutIssueSeverity.Warning;
            if (trimmed)
            {
                // Trimming is the layout's choice, as long as the whole text is a long press away.
                yield return HasToolTip(element.Visual)
                    ? (false, $"{code} {message}")
                    : (true, $"{code} {message} It has no tooltip with its whole text. ({Describe(element.Visual, panel)})");
                continue;
            }

            yield return (severity == LayoutIssueSeverity.Error, $"{code} {message} ({Describe(element.Visual, panel)})");
        }

        foreach (var element in scan.Elements)
        {
            if (!element.IsInteractive || !InPanel(element.Visual) || offScreen.Contains(element.Visual) || IsExempt(element.Visual)
                || element.Reachable.Width < 1 || element.Reachable.Height < 1)
            {
                continue;
            }

            var minimum = element.Visual is ListBoxItem or TreeViewItem or MenuItem ? MinimumRow : MinimumTarget;
            var size = element.Visual.Bounds.Size;
            if (size.Width < minimum - 0.5 || size.Height < minimum - 0.5)
            {
                yield return (true, $"SWRM004 {LayoutText.Label(element.Visual)} is {LayoutText.Dips(size)} dips; touch targets need at least {minimum} × {minimum}. ({Describe(element.Visual, panel)})");
            }
        }

        foreach (var stretched in StretchedText.In(panel))
        {
            yield return (true, "Text stretched instead of centered: " + stretched);
        }
    }

    /// <summary>Lays the window out at a new size and renders it.</summary>
    public static void Resize(Window window, double width, double height)
    {
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs the UI's pending work and lays it out, twice, so what one pass changes is laid out by the next.</summary>
    public static async Task SettleAsync()
    {
        for (var i = 0; i < 2; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(10);
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Whether an element is an adaptive bar's item, or its label, folded away: parked for More, or reduced to its icon.</summary>
    private static bool IsFolded(Visual visual) =>
        visual.GetVisualAncestors().Prepend(visual).OfType<AdaptiveBarItem>().FirstOrDefault() is { } item
        && (item.Bounds.X <= AdaptiveLayout.Parked.X + 1 || visual is TextBlock { Bounds.Width: < 1 });

    /// <summary>Whether an element is part of a data grid, which scrolls its own rows and columns into view.</summary>
    private static bool InDataGrid(Visual visual) => visual.GetVisualAncestors().Any(a => a is DataGrid);

    /// <summary>The part of an element on screen now, in the window's coordinates: its bounds, cut by every container that clips.</summary>
    private static Rect Shown(Visual visual, Visual root)
    {
        if (visual.TransformToVisual(root) is not { } toRoot)
        {
            return default;
        }

        var shown = new Rect(visual.Bounds.Size).TransformToAABB(toRoot);
        foreach (var ancestor in visual.GetVisualAncestors())
        {
            if (ancestor.ClipToBounds && ancestor.TransformToVisual(root) is { } transform)
            {
                shown = shown.Intersect(new Rect(ancestor.Bounds.Size).TransformToAABB(transform));
            }
        }

        return shown;
    }

    /// <summary>Parts that are not touch targets of their own: scroll bars, splitters and the data grid's cells, whose rows have their own height.</summary>
    private static bool IsExempt(Visual visual) =>
        visual is ScrollBar or GridSplitter or Thumb
        || visual.GetVisualAncestors().Any(a => a is ScrollBar or DataGrid);

    /// <summary>Whether a text block, or a control around it, has a tooltip.</summary>
    private static bool HasToolTip(Visual visual)
    {
        var depth = 0;
        for (Visual? current = visual; current is not null && depth < 5; current = current.GetVisualParent(), depth++)
        {
            if (current is Control control && ToolTip.GetTip(control) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Where an element is in the panel, by the names or types of the elements around it.</summary>
    private static string Describe(Visual visual, Control panel)
    {
        var chain = visual.GetVisualAncestors().TakeWhile(a => !ReferenceEquals(a, panel)).OfType<Control>().Take(3).Reverse()
            .Select(c => string.IsNullOrEmpty(c.Name) ? c.GetType().Name : c.GetType().Name + "#" + c.Name);
        return string.Join(" > ", chain.Append(LayoutText.Label(visual)));
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"cut off by (its container|the edge of the screen): [01] × [01] of")]
    private static partial System.Text.RegularExpressions.Regex RoundingOnly();
}
