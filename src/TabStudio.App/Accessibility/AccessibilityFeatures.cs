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

    /// <summary>TextBlock's own way to drop its laid-out text, when the platform keeps it.</summary>
    private static readonly System.Reflection.MethodInfo? InvalidateTextLayout =
        typeof(TextBlock).GetMethod("InvalidateTextLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public, Type.EmptyTypes);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TopLevel, object> Known = new();
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
        TypeScale.Changed += (_, _) =>
        {
            RemeasureWindows();

            // Once more after the first layout at the new size: a scroll viewer can keep its content's old arrangement
            // when only that content's size changed (the bottom panel's tabs kept their old widths).
            Avalonia.Threading.Dispatcher.UIThread.Post(RemeasureWindows, Avalonia.Threading.DispatcherPriority.Background);
        };
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
        // A window is never loaded itself; it is found through the first control loaded in it.
        Control.LoadedEvent.AddClassHandler<Control>((control, _) => Track(TopLevel.GetTopLevel(control), system));
    }

    /// <summary>The windows the IDE shows now (not popups); for tests.</summary>
    internal static IReadOnlyList<TopLevel> LiveWindows() =>
        [.. Windows.Select(r => r.TryGetTarget(out var window) ? window : null).OfType<TopLevel>().Where(w => w.IsVisible)];

    /// <summary>
    /// Lays every window out again after the text size changed. A font size change re-measures the text, but a panel
    /// that sized a column or a row to it before, or a scroll viewer around it, may keep the old arrangement until
    /// something else changes; measuring and arranging everything again makes every view take its new size at once.
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

            if (!window.IsVisible)
            {
                continue;
            }

            window.InvalidateMeasure();
            foreach (var element in window.GetVisualDescendants().OfType<Layoutable>())
            {
                if (element is TextBlock text)
                {
                    // Text whose size it takes from a control around it keeps its old layout otherwise.
                    InvalidateTextLayout?.Invoke(text, null);
                }

                element.InvalidateMeasure();
                element.InvalidateArrange();
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

    private static void Track(TopLevel? window, SystemAccessibility system)
    {
        if (window is null or PopupRoot || Known.TryGetValue(window, out _))
        {
            return;
        }

        Known.AddOrUpdate(window, Known);
        Windows.RemoveAll(r => !r.TryGetTarget(out _));
        Windows.Add(new WeakReference<TopLevel>(window));
        if (system.IsRightToLeft)
        {
            window.FlowDirection = FlowDirection.RightToLeft;
        }
    }

    private static FlowDirection DirectionOf(SystemAccessibility system) =>
        system.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
