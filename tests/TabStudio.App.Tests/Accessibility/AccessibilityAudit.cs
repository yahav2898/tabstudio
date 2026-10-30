using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TabStudio.App.Controls;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.LayoutChecks;

namespace TabStudio.App.Tests.Accessibility;

/// <summary>What the accessibility audit found wrong with one control.</summary>
/// <param name="Kind">The rule: <c>target</c>, <c>name</c>, <c>contrast</c>, <c>icon-contrast</c> or <c>clipped</c>.</param>
/// <param name="Path">Where the control is, the same in every screenshot it shows in.</param>
/// <param name="Text">What is wrong, in a sentence.</param>
internal sealed record AccessibilityFinding(string Kind, string Path, string Text);

/// <summary>
/// The accessibility rules of docs/design/README.md ("Accessibility"), checked on a laid-out window the way TalkBack
/// and a finger meet it:
/// <list type="bullet">
/// <item><b>target</b>: every control that takes a tap is at least the touch target of its screen (48 on a phone, 44
/// on a tablet, 32 in desktop mode), in its hit area;</item>
/// <item><b>name</b>: every such control has an accessible name that says what it is, never a class name;</item>
/// <item><b>contrast</b>: text passes WCAG AA, 4.5:1 (3:1 when large);</item>
/// <item><b>icon-contrast</b>: an icon passes 3:1, WCAG's minimum for graphics;</item>
/// <item><b>clipped</b>: no text is cut off or runs past its box (checked at the largest text size).</item>
/// </list>
/// What a running app, a web page or a designed form draws is the user's content, not the IDE, and is left out:
/// <see cref="IsContent"/>.
/// </summary>
internal static partial class AccessibilityAudit
{
    /// <summary>The touch target on a phone, in dips.</summary>
    public const double PhoneTarget = 48;

    /// <summary>The touch target on a tablet, in dips.</summary>
    public const double TabletTarget = 44;

    /// <summary>The pointer target in desktop mode, in dips.</summary>
    public const double DesktopTarget = 32;

    /// <summary>The narrowest window that is not a phone.</summary>
    public const double PhoneWidth = 600;

    /// <summary>The smallest contrast an icon or other graphic needs.</summary>
    public const double IconMinimum = 3;

    /// <summary>The touch target in a window: by its width, or 32 when it is the IDE on a desktop-mode screen.</summary>
    public static double TargetFor(TopLevel window) =>
        IsDesktopMode(window) ? DesktopTarget : window.Bounds.Width < PhoneWidth ? PhoneTarget : TabletTarget;

    /// <summary>Checks a window.</summary>
    /// <param name="window">The window, laid out and rendered.</param>
    /// <param name="dark">Whether the dark theme shows.</param>
    /// <param name="clipping">Whether to report clipped text too, as at the largest text size.</param>
    public static IReadOnlyList<AccessibilityFinding> In(Window window, bool dark, bool clipping = false)
    {
        var profile = new ScreenProfile("audit", FormFactor.Tablet, window.Bounds.Width, window.Bounds.Height, IsDark: dark);
        var scan = LayoutScan.Run(window, profile);
        var target = TargetFor(window);
        var found = new List<AccessibilityFinding>();
        foreach (var element in scan.Elements)
        {
            if (!element.IsInteractive || IsContent(element.Visual) || IsExempt(element.Visual) || IsFolded(element.Visual)
                || element.Reachable.Width < 1 || element.Reachable.Height < 1 || element.Visual is not Control control)
            {
                continue;
            }

            var size = control.Bounds.Size;
            if (size.Width < target - 0.5 || size.Height < target - 0.5)
            {
                found.Add(new("target", PathOf(window, control), $"{Label(control)} is {LayoutText.Dips(size)}; a tap needs {target} × {target}."));
            }

            if (NameProblem(control) is { } problem)
            {
                found.Add(new("name", PathOf(window, control),
                    $"{Label(control)} {problem} (its data is {control.DataContext?.GetType().Name ?? "none"}; named \"{AutomationProperties.GetName(control)}\")."));
            }
        }

        foreach (var (element, _, message) in ContrastRule.Find(scan))
        {
            if (!IsContent(element.Visual) && !IsFading(element.Visual))
            {
                var colors = element.Visual is Control text && TextElement.GetForeground(text) is ISolidColorBrush fg && ContrastRule.Background(text, profile) is { } bg
                    ? $" ({fg.Color} on {bg})" : string.Empty;
                found.Add(new("contrast", PathOf(window, element.Visual), message + colors));
            }
        }

        foreach (var element in scan.Elements)
        {
            if (element.Visual is StrokeIcon { Foreground: ISolidColorBrush brush } icon && !IsContent(icon) && icon.IsEffectivelyEnabled
                && !IsDisabledInside(icon) && !IsFading(icon) && ContrastRule.Background(icon, profile) is { } background)
            {
                var color = ContrastMath.WithOpacity(brush.Color, brush.Opacity * element.Opacity);
                var ratio = ContrastMath.Ratio(ContrastMath.Over(color, background), background);
                if (ratio < IconMinimum - 0.005)
                {
                    found.Add(new("icon-contrast", PathOf(window, icon), string.Create(CultureInfo.InvariantCulture,
                        $"An icon has a contrast of {ratio:0.0}:1 with its background; icons need {IconMinimum}:1 ({brush.Color} at {element.Opacity:0.##} on {background}).")));
                }
            }
        }

        if (clipping)
        {
            var offScreen = new HashSet<Visual>(ReferenceEqualityComparer.Instance);
            _ = OffScreenRule.Find(scan, offScreen).ToList();
            foreach (var (element, severity, message) in ClippedTextRule.Find(scan, offScreen))
            {
                // Trimming with an ellipsis is the layout's choice when the whole text is in a tooltip.
                // A hint that trims has folded away when a layout gives it no room; its whole text is in its tooltip.
                if (element.Visual is TextBlock trimmed && (trimmed.Bounds.Width < 1 || trimmed.Bounds.Height < 1)
                    && trimmed.TextTrimming != TextTrimming.None && HasToolTip(trimmed))
                {
                    continue;
                }

                // A preview kept to a few lines (MaxLines) trims its last one; its whole text is in its tooltip.
                if (element.Visual is TextBlock { MaxLines: > 0 } preview && preview.TextTrimming != TextTrimming.None && HasToolTip(preview)
                    && message.Contains("cut off at the bottom", StringComparison.Ordinal))
                {
                    continue;
                }

                // A card between an editor's lines that is wholly out of view has scrolled away with them.
                if (WhollyHidden().IsMatch(message) && element.Visual.GetVisualAncestors().Any(a => a is AvaloniaEdit.Rendering.TextView or AvaloniaEdit.TextEditor or TabStudio.App.Views.PullRequests.ReviewAnchorView))
                {
                    continue;
                }

                if (IsContent(element.Visual) || IsFolded(element.Visual) || InDataGrid(element.Visual)
                    || (severity == LayoutIssueSeverity.Warning && HasToolTip(element.Visual)) || RoundingOnly().IsMatch(message))
                {
                    continue;
                }

                var within = WhollyHidden().IsMatch(message)
                    ? " (inside " + string.Join(" < ", element.Visual.GetVisualAncestors().Take(8).Select(a => a.GetType().Name)) + ")"
                    : string.Empty;
                found.Add(new("clipped", PathOf(window, element.Visual), message + within));
            }
        }

        return [.. found.DistinctBy(f => (f.Kind, f.Path))];
    }

    /// <summary>
    /// Why a control's accessible name does not say what it is, or <see langword="null"/> when it does: what TalkBack
    /// reads, the automation peer's name.
    /// </summary>
    public static string? NameProblem(Control control)
    {
        string? name;
        try
        {
            name = ControlAutomationPeer.CreatePeerForElement(control).GetName();
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return "has no accessible name";
        }

        // A dotted name, such as a project's ("ContactBook.Avalonia"), is a class's only when a class has that name.
        name = name.Trim();
        return TypeName().IsMatch(name) && IsTypeName(name) ? $"is read as its class, \"{name}\"" : null;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> Types = new(StringComparer.Ordinal);

    /// <summary>Whether a loaded assembly has a type of that full name.</summary>
    private static bool IsTypeName(string name) => Types.GetOrAdd(name, n => AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(n, throwOnError: false) is not null));

    /// <summary>
    /// Whether an element is fading in or out, or shown as inactive: inside a control whose opacity is changing (a
    /// transition) or set below one to show that what it holds is unavailable. WCAG leaves out inactive and
    /// disappearing content; the IDE never dims readable text by opacity (docs/design/README.md, "Contrast").
    /// </summary>
    private static bool IsFading(Visual visual) =>
        visual.GetVisualAncestors().Prepend(visual).OfType<Control>().Any(c => c.Opacity < 1
            && (c.Transitions?.Any(t => t is Avalonia.Animation.DoubleTransition { Property.Name: "Opacity" }) == true || c.Classes.Contains("disabled")));

    /// <summary>
    /// Whether an element is the user's content rather than the IDE: a running app, a web page, a designed form, a
    /// virtual device's screen, or the editor's own text.
    /// </summary>
    public static bool IsContent(Visual visual) =>
        visual.GetVisualAncestors().Prepend(visual).Any(v => v is VirtualDeviceFrame or TabStudio.App.Previews.UnoPreviewControl
            || (v is Control control && TabStudio.App.Accessibility.A11y.GetIsContent(control)));

    /// <summary>Whether the IDE in a window is on a desktop-mode screen, with a mouse and keyboard.</summary>
    private static bool IsDesktopMode(TopLevel window) =>
        window.Classes.Contains("desktopMode") || window.GetVisualDescendants().Any(v => v is Control c && c.Classes.Contains("desktopMode"));

    /// <summary>Parts that are not targets of their own: scroll bars and their thumbs, splitters, and a data grid's cells.</summary>
    private static bool IsExempt(Visual visual) =>
        visual is ScrollBar or GridSplitter or Thumb || visual.GetVisualAncestors().Any(a => a is ScrollBar or DataGrid);

    private static bool InDataGrid(Visual visual) => visual.GetVisualAncestors().Any(a => a is DataGrid);

    private static bool IsDisabledInside(Visual visual) => visual.GetVisualAncestors().OfType<InputElement>().Any(a => !a.IsEnabled);

    /// <summary>Whether an element is an adaptive bar's item, or part of one, folded away into More or to its icon.</summary>
    private static bool IsFolded(Visual visual)
    {
        if (visual.GetVisualAncestors().Prepend(visual).OfType<AdaptiveBarItem>().FirstOrDefault() is { } item
            && (item.Bounds.X <= AdaptiveLayout.Parked.X + 1 || visual is TextBlock { Bounds.Width: < 1 }))
        {
            return true;
        }

        // An adaptive bar trims what it holds to nothing, or parks it, before it would overflow.
        if (visual is TextBlock { Bounds.Width: < 1 } && visual.GetVisualAncestors().Any(a => a is AdaptiveBar))
        {
            return true;
        }

        foreach (var ancestor in visual.GetVisualAncestors().Prepend(visual))
        {
            if (ancestor.GetVisualParent() is AdaptiveBar)
            {
                return ancestor.Bounds.X <= AdaptiveLayout.Parked.X + 1 || ancestor.Bounds.Width < 1;
            }
        }

        return false;
    }

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

    /// <summary>A control's place in the window, by the names or types of the controls around it, without list positions.</summary>
    private static string PathOf(Visual root, Visual visual)
    {
        var chain = visual.GetVisualAncestors().TakeWhile(a => !ReferenceEquals(a, root)).OfType<Control>()
            .Where(c => !string.IsNullOrEmpty(c.Name) || c is UserControl || c is ItemsControl)
            .Take(3).Reverse().Select(Short);
        return string.Join(" > ", chain.Append(Short((Control)visual)));
    }

    private static string Short(Control control) =>
        string.IsNullOrEmpty(control.Name) ? control.GetType().Name : control.GetType().Name + "#" + control.Name;

    private static string Label(Control control)
    {
        var tip = ToolTip.GetTip(control) as string;
        var text = control switch
        {
            ContentControl { Content: string content } => content,
            HeaderedSelectingItemsControl { Header: string header } => header,
            _ => tip,
        };
        return text is { Length: > 0 } ? $"{control.GetType().Name} \"{Trim(text)}\"" : Short(control);
    }

    private static string Trim(string text) => text.Length <= 40 ? text : text[..39] + "…";

    /// <summary>A .NET type name, such as "TabStudio.App.Controls.StrokeIcon" or "Avalonia.Controls.TextBlock".</summary>
    [GeneratedRegex(@"^([A-Za-z_][A-Za-z0-9_]*\.){1,}[A-Z][A-Za-z0-9_`]*(\[.*\])?$")]
    private static partial Regex TypeName();

    [GeneratedRegex(@"cut off by (its container|the edge of the screen): (\S+) × (\S+) of its \2 × \3 dips")]
    private static partial Regex WhollyHidden();

    [GeneratedRegex(@"cut off by (its container|the edge of the screen): [01] × [01] of")]
    private static partial Regex RoundingOnly();
}
