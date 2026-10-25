using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Rendering;
using Avalonia.VisualTree;

namespace TabStudio.App.Swarm;

/// <summary>
/// Finds what a finger at a point of a screen touches: the topmost element there that takes input, as Avalonia's own
/// hit testing finds it.
/// </summary>
/// <remarks>
/// Avalonia hit-tests what was last drawn, which a screen in a tile the user is not looking at, or one laid out a
/// moment ago, may not have yet. This walks the laid-out visual tree of the screen instead, by the same rules: the
/// last drawn child first, hidden and disabled elements and those that do not take input skipped, clipping kept, and
/// an element hit only where it draws something, so a panel without a background lets taps through to what is under it.
/// </remarks>
internal static class ScreenHitTest
{
    /// <summary>The topmost element at a point that takes input.</summary>
    /// <param name="root">The screen's root.</param>
    /// <param name="point">The point, in the root's coordinates.</param>
    /// <returns>The element, or <see langword="null"/> when nothing there takes input.</returns>
    public static Interactive? ElementAt(Visual root, Point point)
    {
        ArgumentNullException.ThrowIfNull(root);
        for (var visual = HitTest(root, root, point); visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Interactive interactive)
            {
                return interactive;
            }
        }

        return null;
    }

    private static Visual? HitTest(Visual visual, Visual root, Point point)
    {
        if (!visual.IsVisible || visual is InputElement { IsHitTestVisible: false } or InputElement { IsEffectivelyEnabled: false })
        {
            return null;
        }

        if (root.TransformToVisual(visual) is not { } toLocal)
        {
            return null;
        }

        var local = point.Transform(toLocal);
        var inside = new Rect(visual.Bounds.Size).Contains(local);
        if (visual.ClipToBounds && !inside)
        {
            return null;
        }

        // Children are drawn by z-index, then in order (OrderBy keeps the order of equal ones): the last drawn is on top.
        var children = visual.GetVisualChildren().OrderBy(c => c.ZIndex).ToList();
        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (HitTest(children[i], root, point) is { } hit)
            {
                return hit;
            }
        }

        return inside && DrawsAt(visual, local) ? visual : null;
    }

    /// <summary>Whether an element draws at a point of its own, so a touch there lands on it.</summary>
    private static bool DrawsAt(Visual visual, Point local) => visual switch
    {
        ICustomHitTest custom => custom.HitTest(local),
        Panel panel => panel.Background is not null,
        Border border => border.Background is not null || (border.BorderBrush is not null && InBorder(border, local)),
        ContentPresenter presenter => presenter.Background is not null || (presenter.BorderBrush is not null && InBorder(presenter, local)),
        TextBlock or TextPresenter or Image or Shape => true,
        Decorator or ContentControl => false,
        _ => !IsAvaloniaType(visual.GetType()),
    };

    private static bool InBorder(Control control, Point local)
    {
        var thickness = control switch
        {
            Border border => border.BorderThickness,
            ContentPresenter presenter => presenter.BorderThickness,
            _ => default,
        };
        var inner = new Rect(control.Bounds.Size).Deflate(thickness);
        return !inner.Contains(local);
    }

    /// <summary>
    /// Whether a type is one of Avalonia's own, whose drawing the rules above know. An app's own control that draws
    /// itself is hit anywhere in its bounds.
    /// </summary>
    private static bool IsAvaloniaType(Type type) =>
        type.Assembly.GetName().Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true;
}
