using System.Drawing;

namespace System.Windows.Forms.Layout;

/// <summary>
/// The layout of every container that has no layout of its own: docking, then anchoring and automatic sizing, as
/// Windows Forms' DefaultLayout does it.
/// </summary>
/// <remarks>
/// <para>
/// Docking goes through the children from the last to the first: the bottom of the z-order is docked first, so the
/// control the designer adds first (the top of the z-order) gets the space that is left, as a Fill control usually
/// does.
/// </para>
/// <para>
/// Anchoring keeps each anchored edge at the distance it had from the same edge of the parent when the child was
/// added or last moved, or when the designer's code resumed layout without laying out. A child anchored to neither
/// of two opposite edges keeps its relative position between them.
/// </para>
/// </remarks>
internal sealed class DefaultLayout : LayoutEngine
{
    /// <summary>The shared instance.</summary>
    public static DefaultLayout Instance { get; } = new();

    /// <inheritdoc />
    public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
    {
        var parent = (Control)container;
        var children = Participants(parent);
        LayoutDocked(parent, children);
        LayoutAnchored(parent, children);
        return parent.AutoSize;
    }

    /// <inheritdoc />
    public override void InitLayout(object child, BoundsSpecified specified)
    {
        var control = (Control)child;
        if (specified != BoundsSpecified.None && control.Parent is { } parent)
        {
            control.AnchorInfo = Measure(control.Bounds, parent.DisplayRectangle, control.Anchor);
        }
    }

    /// <inheritdoc />
    internal override Size GetPreferredSize(Control container, Size proposedSize)
    {
        var padding = container.Padding;
        int positionedRight = 0, positionedBottom = 0;
        int dockedWidth = 0, dockedHeight = 0, dockedCross = 0, dockedCrossHeight = 0;
        foreach (var child in Participants(container))
        {
            var size = child.AutoSize ? child.GetPreferredSize(Size.Empty) : child.Size;
            switch (child.Dock)
            {
                case DockStyle.Top or DockStyle.Bottom:
                    dockedHeight += size.Height;
                    dockedCross = Math.Max(dockedCross, size.Width);
                    break;
                case DockStyle.Left or DockStyle.Right:
                    dockedWidth += size.Width;
                    dockedCrossHeight = Math.Max(dockedCrossHeight, size.Height);
                    break;
                case DockStyle.Fill:
                    dockedCross = Math.Max(dockedCross, size.Width);
                    dockedCrossHeight = Math.Max(dockedCrossHeight, size.Height);
                    break;
                default:
                    positionedRight = Math.Max(positionedRight, child.Left + size.Width + child.Margin.Right);
                    positionedBottom = Math.Max(positionedBottom, child.Top + size.Height + child.Margin.Bottom);
                    break;
            }
        }

        var insets = container.NonClientInsets;
        var width = Math.Max(positionedRight + padding.Right, dockedWidth + dockedCross + padding.Horizontal);
        var height = Math.Max(positionedBottom + padding.Bottom, dockedHeight + dockedCrossHeight + padding.Vertical);
        return new Size(width + insets.Horizontal, height + insets.Vertical);
    }

    /// <summary>The distances of a child's edges, as anchoring keeps them.</summary>
    internal static AnchorInfo Measure(Rectangle bounds, Rectangle display, AnchorStyles anchor)
    {
        var left = bounds.Left - display.X;
        var top = bounds.Top - display.Y;
        var right = bounds.Right - display.X;
        var bottom = bounds.Bottom - display.Y;

        if ((anchor & AnchorStyles.Right) != 0)
        {
            right -= display.Width;
            if ((anchor & AnchorStyles.Left) == 0)
            {
                left -= display.Width;
            }
        }
        else if ((anchor & AnchorStyles.Left) == 0)
        {
            right -= display.Width / 2;
            left -= display.Width / 2;
        }

        if ((anchor & AnchorStyles.Bottom) != 0)
        {
            bottom -= display.Height;
            if ((anchor & AnchorStyles.Top) == 0)
            {
                top -= display.Height;
            }
        }
        else if ((anchor & AnchorStyles.Top) == 0)
        {
            bottom -= display.Height / 2;
            top -= display.Height / 2;
        }

        return new AnchorInfo(left, top, right, bottom);
    }

    /// <summary>A child's bounds in a client area from its anchor distances.</summary>
    internal static Rectangle Place(AnchorInfo info, Rectangle display, AnchorStyles anchor)
    {
        var left = info.Left + display.X;
        var top = info.Top + display.Y;
        var right = info.Right + display.X;
        var bottom = info.Bottom + display.Y;

        if ((anchor & AnchorStyles.Right) != 0)
        {
            right += display.Width;
            if ((anchor & AnchorStyles.Left) == 0)
            {
                left += display.Width;
            }
        }
        else if ((anchor & AnchorStyles.Left) == 0)
        {
            right += display.Width / 2;
            left += display.Width / 2;
        }

        if ((anchor & AnchorStyles.Bottom) != 0)
        {
            bottom += display.Height;
            if ((anchor & AnchorStyles.Top) == 0)
            {
                top += display.Height;
            }
        }
        else if ((anchor & AnchorStyles.Top) == 0)
        {
            bottom += display.Height / 2;
            top += display.Height / 2;
        }

        return Rectangle.FromLTRB(left, top, Math.Max(left, right), Math.Max(top, bottom));
    }

    private static void LayoutDocked(Control parent, List<Control> children)
    {
        var remaining = parent.DisplayRectangle;

        // An MDI parent's workspace takes what the other docked controls leave, wherever it is in the z-order.
        MdiClient? workspace = null;
        for (var i = children.Count - 1; i >= 0; i--)
        {
            var child = children[i];
            if (child.Dock == DockStyle.None)
            {
                continue;
            }

            if (child is MdiClient { Dock: DockStyle.Fill } client)
            {
                workspace = client;
                continue;
            }

            var preferred = child.AutoSize ? child.GetPreferredSize(Size.Empty) : child.Size;
            switch (child.Dock)
            {
                case DockStyle.Top:
                {
                    var height = Math.Min(preferred.Height, remaining.Height);
                    child.SetBoundsFromLayout(new Rectangle(remaining.X, remaining.Y, remaining.Width, height));
                    height = child.Height;
                    remaining = new Rectangle(remaining.X, remaining.Y + height, remaining.Width, Math.Max(0, remaining.Height - height));
                    break;
                }

                case DockStyle.Bottom:
                {
                    var height = Math.Min(preferred.Height, remaining.Height);
                    child.SetBoundsFromLayout(new Rectangle(remaining.X, remaining.Bottom - height, remaining.Width, height));
                    height = child.Height;
                    remaining = new Rectangle(remaining.X, remaining.Y, remaining.Width, Math.Max(0, remaining.Height - height));
                    break;
                }

                case DockStyle.Left:
                {
                    var width = Math.Min(preferred.Width, remaining.Width);
                    child.SetBoundsFromLayout(new Rectangle(remaining.X, remaining.Y, width, remaining.Height));
                    width = child.Width;
                    remaining = new Rectangle(remaining.X + width, remaining.Y, Math.Max(0, remaining.Width - width), remaining.Height);
                    break;
                }

                case DockStyle.Right:
                {
                    var width = Math.Min(preferred.Width, remaining.Width);
                    child.SetBoundsFromLayout(new Rectangle(remaining.Right - width, remaining.Y, width, remaining.Height));
                    width = child.Width;
                    remaining = new Rectangle(remaining.X, remaining.Y, Math.Max(0, remaining.Width - width), remaining.Height);
                    break;
                }

                case DockStyle.Fill:
                    child.SetBoundsFromLayout(remaining);
                    break;
            }
        }

        workspace?.SetBoundsFromLayout(remaining);
    }

    private static void LayoutAnchored(Control parent, List<Control> children)
    {
        var display = parent.DisplayRectangle;
        foreach (var child in children)
        {
            if (child.Dock != DockStyle.None)
            {
                continue;
            }

            var anchor = child.Anchor;
            var info = child.AnchorInfo ??= Measure(child.Bounds, display, anchor);
            var bounds = Place(info, display, anchor);

            if (child.AutoSize)
            {
                // An automatically sized child grows away from the edges it is anchored to.
                var preferred = child.GetPreferredSize(Size.Empty);
                var anchoredRight = (anchor & (AnchorStyles.Left | AnchorStyles.Right)) == AnchorStyles.Right;
                var anchoredBottom = (anchor & (AnchorStyles.Top | AnchorStyles.Bottom)) == AnchorStyles.Bottom;
                var stretchesHorizontally = (anchor & (AnchorStyles.Left | AnchorStyles.Right)) == (AnchorStyles.Left | AnchorStyles.Right);
                var stretchesVertically = (anchor & (AnchorStyles.Top | AnchorStyles.Bottom)) == (AnchorStyles.Top | AnchorStyles.Bottom);
                var width = stretchesHorizontally ? bounds.Width : preferred.Width;
                var height = stretchesVertically ? bounds.Height : preferred.Height;
                var x = anchoredRight ? bounds.Right - width : bounds.X;
                var y = anchoredBottom ? bounds.Bottom - height : bounds.Y;
                bounds = new Rectangle(x, y, width, height);
            }

            child.SetBoundsFromLayout(bounds);
        }
    }
}

/// <summary>The distances anchoring keeps between a child's edges and its parent's.</summary>
/// <param name="Left">The left distance.</param>
/// <param name="Top">The top distance.</param>
/// <param name="Right">The right distance.</param>
/// <param name="Bottom">The bottom distance.</param>
internal sealed record AnchorInfo(int Left, int Top, int Right, int Bottom);
