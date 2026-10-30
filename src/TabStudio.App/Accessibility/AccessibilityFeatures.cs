using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TabStudio.App.Theming;

namespace TabStudio.App.Accessibility;

/// <summary>
/// Starts what makes the IDE accessible, for every view at once (docs/design/README.md, "Accessibility"): the type
/// scale (<see cref="TypeScale"/>), the touch targets (<see cref="TouchTargets"/>), the accessible names
/// (<see cref="AccessibleNames"/>), and the device's settings: no animations when it asks (<see cref="HiddenAnimations.ReduceMotion"/>)
/// and a mirrored layout when its language is written right to left.
/// </summary>
public static class AccessibilityFeatures
{
    private static readonly List<WeakReference<TopLevel>> Windows = [];
    private static SystemAccessibility? _system;

    /// <summary>Starts them. Call once, on the UI thread, as the app starts and before its first window shows.</summary>
    /// <param name="system">The device's accessibility settings.</param>
    public static void Start(SystemAccessibility system)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (_system is not null)
        {
            return;
        }

        _system = system;
        if (Application.Current is { } application && !application.Resources.ContainsKey("Type.Body"))
        {
            TypeScale.Apply(application.Resources, TypeScale.Factor);
        }

        TouchTargets.Start();
        AccessibleNames.Start();
        TrimmedText.Start();
        TypeScale.Changed += (_, _) => RemeasureWindows();
        HiddenAnimations.ReduceMotion = system.ReduceMotion;
        system.Changed += (_, _) =>
        {
            HiddenAnimations.ReduceMotion = system.ReduceMotion;
            foreach (var reference in Windows.ToArray())
            {
                if (reference.TryGetTarget(out var window))
                {
                    window.FlowDirection = DirectionOf(system);
                }
                else
                {
                    Windows.Remove(reference);
                }
            }
        };
        Control.LoadedEvent.AddClassHandler<TopLevel>((window, _) =>
        {
            if (window is PopupRoot)
            {
                return;
            }

            Windows.RemoveAll(r => !r.TryGetTarget(out var known) || ReferenceEquals(known, window));
            Windows.Add(new WeakReference<TopLevel>(window));
            if (system.IsRightToLeft)
            {
                window.FlowDirection = FlowDirection.RightToLeft;
            }
        });
    }

    /// <summary>
    /// Lays every window out again after the text size changed. A font size change re-measures the text, but a panel
    /// that sized a column or a row to it before may keep the old size until something else changes; measuring
    /// everything once makes every view take its new size at once.
    /// </summary>
    public static void RemeasureWindows()
    {
        foreach (var reference in Windows.ToArray())
        {
            if (!reference.TryGetTarget(out var window))
            {
                Windows.Remove(reference);
                continue;
            }

            window.InvalidateMeasure();
            foreach (var element in window.GetVisualDescendants().OfType<Layoutable>())
            {
                element.InvalidateMeasure();
            }
        }
    }

    /// <summary>
    /// Puts Fluent's own sizes back in an app's resources, for the app preview and the designer: the IDE's text size
    /// and touch targets change Fluent's font sizes and minimum heights for the IDE, and an app's views must look as
    /// they do on a device. A size the app's own resources set is kept.
    /// </summary>
    /// <param name="resources">The resources around an app's views.</param>
    public static void RestoreFluentDefaults(IResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        foreach (var (key, value) in FluentDefaults)
        {
            if (!resources.ContainsKey(key))
            {
                resources[key] = value;
            }
        }
    }

    /// <summary>The Fluent theme's values (Avalonia 11.3) of the resources the IDE changes.</summary>
    private static readonly (string Key, double Value)[] FluentDefaults =
    [
        ("ControlContentThemeFontSize", 14),
        ("ToolTipContentThemeFontSize", 12),
        ("TextControlThemeMinHeight", 32),
        ("ComboBoxMinHeight", 32),
        ("CheckBoxMinHeight", 32),
        ("RadioButtonMinHeight", 32),
        ("SplitButtonMinHeight", 32),
        ("TreeViewItemMinHeight", 32),
        ("MenuFlyoutThemeMinHeight", 32),
        ("CalendarDatePickerMinHeight", 32),
    ];

    private static FlowDirection DirectionOf(SystemAccessibility system) =>
        system.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
