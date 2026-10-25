using System.Drawing;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// A scroll bar drawn by a control in its non-client area: its geometry, its value in pixels of content, and the
/// pointer's dragging of its thumb and pressing of its arrows and track.
/// </summary>
internal sealed class ScrollBarModel
{
    private int _dragOffset = -1;

    /// <summary>Creates a scroll bar.</summary>
    /// <param name="vertical">Whether it is vertical.</param>
    public ScrollBarModel(bool vertical) => Vertical = vertical;

    /// <summary>Whether it is vertical.</summary>
    public bool Vertical { get; }

    /// <summary>Whether it is shown.</summary>
    public bool Visible { get; set; }

    /// <summary>Where it is, in its control's bounds coordinates.</summary>
    public Rectangle Bounds { get; set; }

    /// <summary>The length of the content.</summary>
    public int ContentLength { get; set; }

    /// <summary>The length of the part of the content in view.</summary>
    public int ViewLength { get; set; }

    /// <summary>How far an arrow scrolls.</summary>
    public int SmallChange { get; set; } = 16;

    /// <summary>The scroll position: the content's offset, from 0 to <see cref="MaximumValue"/>.</summary>
    public int Value { get; set; }

    /// <summary>The largest scroll position.</summary>
    public int MaximumValue => Math.Max(0, ContentLength - ViewLength);

    /// <summary>Whether the thumb is being dragged.</summary>
    public bool IsDragging => _dragOffset >= 0;

    /// <summary>The thumb's bounds, in the control's bounds coordinates.</summary>
    public Rectangle Thumb
    {
        get
        {
            var arrow = Theme.ScrollBarSize;
            var track = (Vertical ? Bounds.Height : Bounds.Width) - (2 * arrow);
            if (track <= 0 || ContentLength <= ViewLength || ContentLength <= 0)
            {
                return Rectangle.Empty;
            }

            var length = Math.Max(arrow, (int)((long)track * ViewLength / ContentLength));
            length = Math.Min(length, track);
            var position = MaximumValue == 0 ? 0 : (int)((long)(track - length) * Value / MaximumValue);
            return Vertical
                ? new Rectangle(Bounds.X, Bounds.Y + arrow + position, Bounds.Width, length)
                : new Rectangle(Bounds.X + arrow + position, Bounds.Y, length, Bounds.Height);
        }
    }

    /// <summary>Draws the scroll bar.</summary>
    /// <param name="g">The surface, in the control's bounds coordinates.</param>
    /// <param name="enabled">Whether it can scroll.</param>
    public void Paint(Graphics g, bool enabled)
    {
        if (Visible)
        {
            ThemePainter.DrawScrollBar(g, Bounds, Vertical, Thumb, IsDragging, enabled && MaximumValue > 0);
        }
    }

    /// <summary>A press on the scroll bar: an arrow scrolls a step, the track a page, the thumb starts a drag.</summary>
    /// <param name="point">The point, in the control's bounds coordinates.</param>
    /// <returns>Whether the point is on the scroll bar.</returns>
    public bool PointerDown(Point point)
    {
        if (!Visible || !Bounds.Contains(point))
        {
            return false;
        }

        var arrow = Theme.ScrollBarSize;
        var along = Vertical ? point.Y - Bounds.Y : point.X - Bounds.X;
        var length = Vertical ? Bounds.Height : Bounds.Width;
        var thumb = Thumb;
        var thumbStart = Vertical ? thumb.Y - Bounds.Y : thumb.X - Bounds.X;
        var thumbLength = Vertical ? thumb.Height : thumb.Width;
        if (along < arrow)
        {
            Value = Math.Max(0, Value - SmallChange);
        }
        else if (along >= length - arrow)
        {
            Value = Math.Min(MaximumValue, Value + SmallChange);
        }
        else if (thumbLength > 0 && along >= thumbStart && along < thumbStart + thumbLength)
        {
            _dragOffset = along - thumbStart;
        }
        else if (thumbLength > 0)
        {
            Value = along < thumbStart ? Math.Max(0, Value - ViewLength) : Math.Min(MaximumValue, Value + ViewLength);
        }

        return true;
    }

    /// <summary>A move while the thumb is dragged.</summary>
    /// <param name="point">The point, in the control's bounds coordinates.</param>
    public void PointerMove(Point point)
    {
        if (!IsDragging)
        {
            return;
        }

        var arrow = Theme.ScrollBarSize;
        var thumb = Thumb;
        var track = (Vertical ? Bounds.Height : Bounds.Width) - (2 * arrow) - (Vertical ? thumb.Height : thumb.Width);
        if (track <= 0)
        {
            return;
        }

        var along = (Vertical ? point.Y - Bounds.Y : point.X - Bounds.X) - arrow - _dragOffset;
        Value = Math.Clamp((int)((long)along * MaximumValue / track), 0, MaximumValue);
    }

    /// <summary>The end of a press.</summary>
    public void PointerUp() => _dragOffset = -1;
}
