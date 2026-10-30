using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
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

    private static FlowDirection DirectionOf(SystemAccessibility system) =>
        system.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
