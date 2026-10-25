using System.Drawing;

namespace System.Windows.Forms.Layout;

/// <summary>Lays out the child controls of a container.</summary>
public abstract class LayoutEngine
{
    /// <summary>Lays out a container's children.</summary>
    /// <param name="container">The container.</param>
    /// <param name="layoutEventArgs">What changed.</param>
    /// <returns>Whether the container's parent should lay out again, because the container's preferred size changed.</returns>
    public virtual bool Layout(object container, LayoutEventArgs layoutEventArgs) => false;

    /// <summary>Prepares a child for layout when it is added or moved, such as by measuring its anchors.</summary>
    /// <param name="child">The child.</param>
    /// <param name="specified">Which bounds were set.</param>
    public virtual void InitLayout(object child, BoundsSpecified specified)
    {
    }

    /// <summary>The size a container needs to fit its children.</summary>
    /// <param name="container">The container.</param>
    /// <param name="proposedSize">The size available; zero for no limit.</param>
    internal virtual Size GetPreferredSize(Control container, Size proposedSize) => container.Size;

    /// <summary>The children that take part in layout: the visible ones, in z-order.</summary>
    /// <param name="container">The container.</param>
    internal static List<Control> Participants(Control container) =>
        [.. container.Controls.ToArray().Where(child => child.VisibleSelf)];
}
