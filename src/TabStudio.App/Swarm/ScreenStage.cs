using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TabStudio.App.Swarm;

/// <summary>
/// Where the screens TabStudio draws stay while nobody looks at them, such as virtual devices while the Device
/// Matrix is in a tab behind another: still laid out, still running, still composed, but never painted and never in
/// the way of a touch.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia's controls check what was last composed where, such as a button checking that a lifted finger is still
/// over it, so a screen that takes mirrored taps must be composed even when hidden. A hidden control is not composed;
/// one of opacity 0 is composed and hit-tested but never painted. The stage is that: an invisible layer of the
/// window's overlay, scaled down so that every screen on it lies inside the window, where composition hit-tests.
/// </para>
/// <para>Taps from the person go through it to the IDE below; it only holds screens.</para>
/// </remarks>
internal sealed class ScreenStage : Canvas
{
    /// <summary>How small screens are on the stage, so a tablet's screen fits in a phone's window.</summary>
    private const double StageScale = 0.2;

    private ScreenStage()
    {
        Opacity = 0;
        IsHitTestVisible = false;
        RenderTransformOrigin = RelativePoint.TopLeft;
        RenderTransform = new ScaleTransform(StageScale, StageScale);
    }

    /// <summary>The stage of the window a control is in, created the first time.</summary>
    /// <param name="visual">A control in the window.</param>
    /// <returns>The stage, or <see langword="null"/> when the control is not in a window yet.</returns>
    public static ScreenStage? Of(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (OverlayLayer.GetOverlayLayer(visual) is not { } layer)
        {
            return null;
        }

        foreach (var child in layer.Children)
        {
            if (child is ScreenStage existing)
            {
                return existing;
            }
        }

        var stage = new ScreenStage();
        layer.Children.Add(stage);
        return stage;
    }

    /// <summary>Holds a screen's control, taking it from wherever it was shown.</summary>
    /// <param name="surface">The screen's control.</param>
    public void Hold(Control surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (ReferenceEquals(surface.Parent, this))
        {
            return;
        }

        ScreenPlacement.Detach(surface);
        Children.Add(surface);
    }
}

/// <summary>Moving a screen's control between the places that show it: a tile, the full-size view or the stage.</summary>
internal static class ScreenPlacement
{
    /// <summary>Takes a control out of the panel or decorator that holds it, so another can.</summary>
    /// <param name="control">The control.</param>
    public static void Detach(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                break;
            case ContentControl content when ReferenceEquals(content.Content, control):
                content.Content = null;
                break;
        }
    }

    /// <summary>Shows a control in a decorator, taking it from wherever it was.</summary>
    /// <param name="control">The control.</param>
    /// <param name="host">Where to show it.</param>
    public static void Show(Control control, Decorator host)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(host);
        if (ReferenceEquals(host.Child, control))
        {
            return;
        }

        Detach(control);
        host.Child = control;
    }
}
