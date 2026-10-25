using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace TabStudio.App.Swarm.LayoutChecks;

/// <summary>One element of an app on a screen, as the layout checks see it.</summary>
/// <param name="Visual">The element.</param>
/// <param name="Bounds">Where it is, in the coordinates of the app's root view, which fills the screen.</param>
/// <param name="Reach">
/// The part of the screen where it can be seen: the screen, cut by the containers that clip it, except that a scroll
/// viewer's content reaches as far as it can be scrolled.
/// </param>
/// <param name="Opacity">Its opacity times that of every element around it.</param>
/// <param name="ScrollDepth">How many scroll viewers it is inside, which tells content that scrolls from what stays.</param>
/// <param name="IsTemplatePart">Whether it is a part of a control's template rather than something the app's XAML wrote.</param>
internal sealed record ScannedElement(Visual Visual, Rect Bounds, Rect Reach, double Opacity, int ScrollDepth, bool IsTemplatePart)
{
    /// <summary>Whether a person can touch it: an enabled control that takes input, as listed in <see cref="LayoutScan.IsInteractive"/>.</summary>
    public bool IsInteractive { get; init; }

    /// <summary>The part of it a person can see or scroll to.</summary>
    public Rect Reachable => Bounds.Intersect(Reach);
}

/// <summary>
/// One walk over the visual tree of an app on a screen, which every layout rule then reads: where each shown element
/// is, what of it can be reached, and how opaque it is.
/// </summary>
internal sealed class LayoutScan
{
    private LayoutScan(Visual root, ScreenProfile profile, List<ScannedElement> elements)
    {
        Root = root;
        Profile = profile;
        Elements = elements;
        Screen = new Rect(0, 0, profile.Width, profile.Height);
    }

    /// <summary>The app's root view, which fills the screen.</summary>
    public Visual Root { get; }

    /// <summary>The screen.</summary>
    public ScreenProfile Profile { get; }

    /// <summary>The screen's area, in the root's coordinates.</summary>
    public Rect Screen { get; }

    /// <summary>Every shown element, the root first, parents before their children.</summary>
    public IReadOnlyList<ScannedElement> Elements { get; }

    /// <summary>Walks the app's view.</summary>
    /// <param name="root">The app's root view.</param>
    /// <param name="profile">The screen it is on.</param>
    public static LayoutScan Run(Visual root, ScreenProfile profile)
    {
        var elements = new List<ScannedElement>();
        var screen = new Rect(0, 0, profile.Width, profile.Height);
        Walk(root, root, screen, 1, 0, elements);
        return new LayoutScan(root, profile, elements);
    }

    /// <summary>
    /// Whether a person can touch an element: an enabled button, toggle, text box, slider, picker or item that takes
    /// input. A list or other items control is not a target itself; its items are.
    /// </summary>
    /// <param name="visual">The element.</param>
    public static bool IsInteractive(Visual visual) => visual is Control { IsEffectivelyEnabled: true, IsHitTestVisible: true } control && control switch
    {
        ComboBox or MenuItem or TreeViewItem => true,
        ItemsControl => false,
        Button or TextBox or Slider or ListBoxItem or AutoCompleteBox or NumericUpDown => true,
        TemplatedControl { Focusable: true } and not UserControl and not ScrollViewer => true,
        _ => false,
    };

    private static void Walk(Visual visual, Visual root, Rect reach, double opacity, int scrollDepth, List<ScannedElement> elements)
    {
        if (!visual.IsVisible || visual.Opacity <= 0 || visual.TransformToVisual(root) is not { } toRoot)
        {
            return;
        }

        opacity *= visual.Opacity;
        var bounds = new Rect(visual.Bounds.Size).TransformToAABB(toRoot);
        var isTemplatePart = visual is StyledElement { TemplatedParent: not null };
        elements.Add(new ScannedElement(visual, bounds, reach, opacity, scrollDepth, isTemplatePart) { IsInteractive = !isTemplatePart && IsInteractive(visual) });

        var childReach = reach;
        if (visual.ClipToBounds || visual.Clip is not null)
        {
            childReach = childReach.Intersect(bounds);
        }

        var childDepth = scrollDepth;
        if (visual is ScrollContentPresenter presenter)
        {
            // Content reaches along a scrolling axis as far as it extends, as long as the viewport itself shows.
            childReach = ScrolledReach(presenter, root, childReach);
            childDepth++;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            Walk(child, root, childReach, opacity, childDepth, elements);
        }
    }

    private static Rect ScrolledReach(ScrollContentPresenter presenter, Visual root, Rect viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0 || presenter.Child is not { } content || content.TransformToVisual(root) is not { } toRoot)
        {
            return viewport;
        }

        var extent = new Rect(content.Bounds.Size).TransformToAABB(toRoot);
        var left = presenter.CanHorizontallyScroll ? Math.Min(extent.Left, viewport.Left) : viewport.Left;
        var right = presenter.CanHorizontallyScroll ? Math.Max(extent.Right, viewport.Right) : viewport.Right;
        var top = presenter.CanVerticallyScroll ? Math.Min(extent.Top, viewport.Top) : viewport.Top;
        var bottom = presenter.CanVerticallyScroll ? Math.Max(extent.Bottom, viewport.Bottom) : viewport.Bottom;
        return new Rect(left, top, right - left, bottom - top);
    }
}
