using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TabStudio.App.Accessibility;

/// <summary>
/// The touch target of every control that takes a tap (docs/design/README.md, "Touch targets and density"): 48 dips
/// on a phone, 44 on a tablet and 32 in desktop mode, with a mouse. It is one set of resources per window, chosen by
/// the window's width, that every control theme and style reads:
/// <list type="bullet">
/// <item><c>Target.Size</c>: the least height (and width) of a control that takes a tap, which is its hit area;</item>
/// <item><c>Target.Visual</c>: the size a flat or outlined button draws, inside its hit area, so a toolbar does not grow
/// heavier as its targets grow;</item>
/// <item><c>Target.Inset</c>: the transparent margin between the two, which still takes the tap;</item>
/// <item>Fluent's own minimum heights (text boxes, combo boxes, check boxes, menu items, tree items), set to the target.</item>
/// </list>
/// </summary>
public static class TouchTargets
{
    /// <summary>A phone's touch target.</summary>
    public const double Phone = 48;

    /// <summary>A tablet's touch target.</summary>
    public const double Tablet = 44;

    /// <summary>The pointer target in desktop mode, with a mouse and keyboard on an external display.</summary>
    public const double Desktop = 32;

    /// <summary>The narrowest window that is not a phone.</summary>
    public const double PhoneWidth = 600;

    /// <summary>The transparent margin that takes the tap around what a flat button draws, on each side.</summary>
    public const double Inset = 2;

    private static readonly string[] FluentMinimums =
    [
        "TextControlThemeMinHeight",
        "ComboBoxMinHeight",
        "CheckBoxMinHeight",
        "RadioButtonMinHeight",
        "SplitButtonMinHeight",
        "TreeViewItemMinHeight",
        "MenuFlyoutThemeMinHeight",
        "CalendarDatePickerMinHeight",
    ];

    private static bool _started;

    /// <summary>Starts sizing the targets of every window by its width. Call once, on the UI thread, as the app starts.</summary>
    public static void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        if (Application.Current is { } application)
        {
            Apply(application.Resources, Tablet);
        }

        Visual.BoundsProperty.Changed.AddClassHandler<TopLevel>((topLevel, _) => Update(topLevel));
    }

    /// <summary>The touch target for a window of a width.</summary>
    /// <param name="width">The window's width in dips.</param>
    public static double For(double width) => width > 0 && width < PhoneWidth ? Phone : Tablet;

    /// <summary>Sizes a control's targets for desktop mode, with a mouse: the IDE on an external display.</summary>
    /// <param name="control">The control that holds the IDE there.</param>
    public static void UseDesktop(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        Apply(control.Resources, Desktop);
    }

    /// <summary>Sets the target resources for a target size.</summary>
    /// <param name="resources">The window's, the app's or a control's resources.</param>
    /// <param name="target">The target, in dips.</param>
    public static void Apply(IResourceDictionary resources, double target)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (resources.TryGetValue("Target.Size", out var current) && current is double size && Math.Abs(size - target) < 0.01)
        {
            return;
        }

        resources["Target.Size"] = target;
        resources["Target.Visual"] = target - (2 * Inset);
        resources["Target.Inset"] = new Thickness(Inset);
        foreach (var key in FluentMinimums)
        {
            resources[key] = target;
        }
    }

    private static void Update(TopLevel topLevel)
    {
        // A popup takes its window's targets: it is part of that window.
        if (topLevel is PopupRoot || topLevel.Bounds.Width <= 0)
        {
            return;
        }

        Apply(topLevel.Resources, For(topLevel.Bounds.Width));
    }
}
