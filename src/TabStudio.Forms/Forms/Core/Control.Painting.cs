using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>Painting: the background, the control's own look, the Paint event, then the children on top.</summary>
public partial class Control
{
    private DisplayListBuilder? _immediate;

    /// <summary>Whether the control is drawn into an off-screen buffer first. Drawing is always buffered here.</summary>
    protected virtual bool DoubleBuffered
    {
        get => GetStyle(ControlStyles.OptimizedDoubleBuffer);
        set => SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, value);
    }

    /// <summary>Whether the control is drawn again whenever it is resized.</summary>
    protected bool ResizeRedraw
    {
        get => GetStyle(ControlStyles.ResizeRedraw);
        set => SetStyle(ControlStyles.ResizeRedraw, value);
    }

    /// <summary>Asks for the control to be drawn again.</summary>
    public void Invalidate() => Invalidate(false);

    /// <summary>Asks for the control, and optionally its children, to be drawn again.</summary>
    /// <param name="invalidateChildren">Whether the children are drawn again too; the whole window is.</param>
    public void Invalidate(bool invalidateChildren) => Invalidate(ClientRectangle, invalidateChildren);

    /// <summary>Asks for part of the control to be drawn again.</summary>
    /// <param name="rc">The part, in client coordinates.</param>
    public void Invalidate(Rectangle rc) => Invalidate(rc, false);

    /// <summary>Asks for part of the control to be drawn again.</summary>
    /// <param name="rc">The part, in client coordinates.</param>
    /// <param name="invalidateChildren">Whether the children are drawn again too; the whole window is.</param>
    public void Invalidate(Rectangle rc, bool invalidateChildren)
    {
        _immediate?.Clear();
        InvalidateWindow();
        OnInvalidated(new InvalidateEventArgs(rc));
    }

    /// <summary>Asks for a region of the control to be drawn again.</summary>
    /// <param name="region">The region.</param>
    public void Invalidate(Region? region) => Invalidate(region, false);

    /// <summary>Asks for a region of the control to be drawn again.</summary>
    /// <param name="region">The region.</param>
    /// <param name="invalidateChildren">Whether the children are drawn again too; the whole window is.</param>
    public void Invalidate(Region? region, bool invalidateChildren) =>
        Invalidate(region is null ? ClientRectangle : Rectangle.Round(region.Bounds), invalidateChildren);

    /// <summary>Draws the parts asked for now, rather than when the app is next idle.</summary>
    public void Update() => Window?.PaintNow();

    /// <summary>Draws the control again now.</summary>
    public virtual void Refresh()
    {
        Invalidate(true);
        Update();
    }

    /// <summary>
    /// Creates a surface to draw on the control right away. As in Windows Forms, what is drawn stays until the control
    /// is drawn again, for example when it is invalidated; draw in the Paint event to keep drawing.
    /// </summary>
    public Graphics CreateGraphics()
    {
        _immediate ??= new DisplayListBuilder();
        var origin = ClientOriginInWindow();
        var client = new Rectangle(origin, ClientSize);
        return new Graphics(_immediate, origin, client, onDispose: _ => InvalidateWindow(), onRecord: InvalidateWindow, replaceable: true);
    }

    /// <summary>Raises the Invalidated event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnInvalidated(InvalidateEventArgs e) => ((InvalidateEventHandler?)Events[EventInvalidated])?.Invoke(this, e);

    /// <summary>Fills the background with <see cref="BackColor"/> and draws <see cref="BackgroundImage"/>.</summary>
    /// <param name="pevent">The surface and the area to draw.</param>
    protected virtual void OnPaintBackground(PaintEventArgs pevent)
    {
        var color = BackColor;
        if (color.A > 0)
        {
            using var brush = new SolidBrush(color);
            pevent.Graphics.FillRectangle(brush, ClientRectangle);
        }

        if (_backgroundImage is { } image)
        {
            DrawBackgroundImage(pevent.Graphics, image, _backgroundImageLayout, ClientRectangle);
        }
    }

    /// <summary>Raises the Paint event, after the control's own look is drawn.</summary>
    /// <param name="e">The surface and the area to draw.</param>
    protected virtual void OnPaint(PaintEventArgs e) => ((PaintEventHandler?)Events[EventPaint])?.Invoke(this, e);

    /// <summary>Draws the control's own look, such as a button's face, before the Paint event, in client coordinates.</summary>
    /// <param name="g">The surface.</param>
    internal virtual void PaintContent(Graphics g)
    {
    }

    /// <summary>Draws the border and scroll bars, after the children, in the control's bounds coordinates.</summary>
    /// <param name="g">The surface.</param>
    internal virtual void PaintNonClient(Graphics g)
    {
    }

    /// <summary>Asks the window, or the designer, to draw again.</summary>
    internal void InvalidateWindow()
    {
        var root = TopLevelControl!;
        if (root is Form { WindowInternal: { } window })
        {
            window.Invalidate();
        }
        else
        {
            root.RepaintRequested?.Invoke();
        }
    }

    /// <summary>Paints the control and its children, with its bounds' top left at a point of the window.</summary>
    /// <param name="builder">Where drawing is recorded.</param>
    /// <param name="clip">The area that may be drawn, in the window's client area.</param>
    /// <param name="origin">Where the control's bounds start in the window's client area.</param>
    internal void PaintTree(DisplayListBuilder builder, Rectangle clip, Point origin)
    {
        var bounds = new Rectangle(origin, Size);
        var visible = Rectangle.Intersect(clip, bounds);
        if (visible.Width <= 0 || visible.Height <= 0)
        {
            return;
        }

        var insets = NonClientInsets;
        var clientOrigin = new Point(origin.X + insets.Left, origin.Y + insets.Top);
        PaintClient(builder, Rectangle.Intersect(visible, new Rectangle(clientOrigin, ClientSize)), clientOrigin);
        PaintNonClient(new Graphics(builder, origin, visible));
    }

    /// <summary>Paints the client area and the children, with the client area's origin at a point of the window.</summary>
    /// <param name="builder">Where drawing is recorded.</param>
    /// <param name="clip">The area that may be drawn, in the window's client area.</param>
    /// <param name="clientOrigin">Where the client area starts in the window's client area.</param>
    internal void PaintClient(DisplayListBuilder builder, Rectangle clip, Point clientOrigin)
    {
        if (clip.Width <= 0 || clip.Height <= 0)
        {
            return;
        }

        var area = new Rectangle(clip.X - clientOrigin.X, clip.Y - clientOrigin.Y, clip.Width, clip.Height);
        using (var background = new PaintEventArgs(new Graphics(builder, clientOrigin, clip), area))
        {
            OnPaintBackground(background);
        }

        PaintContent(new Graphics(builder, clientOrigin, clip));
        using (var paint = new PaintEventArgs(new Graphics(builder, clientOrigin, clip), area))
        {
            OnPaint(paint);
        }

        var children = _controls.ToArray();
        for (var i = children.Length - 1; i >= 0; i--)
        {
            var child = children[i];
            if (child.VisibleSelf || IsInDesigner)
            {
                child.PaintTree(builder, clip, new Point(clientOrigin.X + child._x, clientOrigin.Y + child._y));
            }
        }

        if (_immediate is { Count: > 0 } immediate)
        {
            builder.AddRange(immediate.Snapshot());
        }
    }

    /// <summary>Draws a background image with a layout.</summary>
    internal static void DrawBackgroundImage(Graphics g, Image image, ImageLayout layout, Rectangle bounds)
    {
        switch (layout)
        {
            case ImageLayout.None:
                g.DrawImage(image, bounds.X, bounds.Y, image.Width, image.Height);
                break;
            case ImageLayout.Center:
                g.DrawImage(image, bounds.X + ((bounds.Width - image.Width) / 2), bounds.Y + ((bounds.Height - image.Height) / 2), image.Width, image.Height);
                break;
            case ImageLayout.Stretch:
                g.DrawImage(image, bounds);
                break;
            case ImageLayout.Zoom:
                g.DrawImage(image, Zoom(bounds, image.Size));
                break;
            default:
                for (var y = bounds.Y; y < bounds.Bottom; y += Math.Max(1, image.Height))
                {
                    for (var x = bounds.X; x < bounds.Right; x += Math.Max(1, image.Width))
                    {
                        g.DrawImage(image, x, y, image.Width, image.Height);
                    }
                }

                break;
        }
    }

    /// <summary>The largest rectangle of an image's proportions that fits a rectangle, centered in it.</summary>
    internal static Rectangle Zoom(Rectangle bounds, Size image)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return bounds;
        }

        var scale = Math.Min(bounds.Width / (double)image.Width, bounds.Height / (double)image.Height);
        var width = (int)Math.Round(image.Width * scale);
        var height = (int)Math.Round(image.Height * scale);
        return new Rectangle(bounds.X + ((bounds.Width - width) / 2), bounds.Y + ((bounds.Height - height) / 2), width, height);
    }
}
