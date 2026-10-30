using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TabStudio.App.Controls;

namespace TabStudio.App.Theming;

/// <summary>
/// Stops the IDE's looping animations while their controls are hidden, so an idle IDE draws no frames.
/// </summary>
/// <remarks>
/// Avalonia runs a style's animation for as long as its selector matches, whether or not the control is on screen,
/// and every running animation keeps the IDE rendering, which costs battery. A control that loops (a
/// <see cref="ProgressBar"/>, and a <see cref="StrokeIcon"/> with the <c>spinning</c> or <c>spinner</c> class) has the
/// <see cref="HiddenClass"/> class while it, or a control around it, is hidden; the looping styles
/// (<c>Theme/ProgressBar.axaml</c>, <c>StrokeIcon.spinning</c> and <c>StrokeIcon.spinner</c>) do not match it.
/// Visibility is followed through <see cref="Visual.IsVisibleProperty"/> rather than measured, because Avalonia does
/// not measure a hidden control.
/// </remarks>
public static class HiddenAnimations
{
    /// <summary>The class of a looping control that is hidden, itself or by a control around it.</summary>
    public const string HiddenClass = "offscreen";

    private static readonly HashSet<Control> Looping = [];
    private static bool _started;

    /// <summary>Starts following looping controls. Call once, on the UI thread, when the app starts.</summary>
    public static void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        Control.LoadedEvent.AddClassHandler<ProgressBar>((bar, _) => Watch(bar));
        Control.UnloadedEvent.AddClassHandler<ProgressBar>((bar, _) => Looping.Remove(bar));
        Control.LoadedEvent.AddClassHandler<StrokeIcon>((icon, _) => Watch(icon));
        Control.UnloadedEvent.AddClassHandler<StrokeIcon>((icon, _) => Looping.Remove(icon));
        Visual.IsVisibleProperty.Changed.AddClassHandler<Visual>((visual, _) => Refresh(visual));
    }

    /// <summary>Whether a control and every control around it are visible.</summary>
    /// <param name="visual">The control.</param>
    public static bool IsShown(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        return visual.GetSelfAndVisualAncestors().All(v => v.IsVisible);
    }

    /// <summary>
    /// Follows a control if it loops, now that it is on screen or its classes changed; <see cref="StrokeIcon"/> calls
    /// this when its classes change, since <c>spinning</c> is often bound.
    /// </summary>
    /// <param name="control">The control.</param>
    internal static void Watch(Control control)
    {
        if (!_started || !control.IsLoaded)
        {
            return;
        }

        if (Loops(control))
        {
            Looping.Add(control);
            control.Classes.Set(HiddenClass, !IsShown(control));
        }
        else if (Looping.Remove(control))
        {
            control.Classes.Set(HiddenClass, false);
        }
    }

    private static bool Loops(Control control) => control switch
    {
        ProgressBar => true,
        StrokeIcon icon => icon.Classes.Contains("spinning") || icon.Classes.Contains("spinner"),
        _ => false,
    };

    private static void Refresh(Visual changed)
    {
        if (Looping.Count == 0)
        {
            return;
        }

        // Setting the class changes no visibility, but it does raise the icon's class change, which calls Watch: work
        // on a copy.
        foreach (var control in Looping.ToArray())
        {
            if (ReferenceEquals(control, changed) || changed.IsVisualAncestorOf(control))
            {
                control.Classes.Set(HiddenClass, !IsShown(control));
            }
        }
    }
}
