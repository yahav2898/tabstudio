using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>
/// A control that scrolls its children when they do not fit and <see cref="AutoScroll"/> is on: forms, panels and
/// user controls. Scroll bars appear as in Windows; a finger drags the content.
/// </summary>
public class ScrollableControl : Control
{
    private static readonly object EventScroll = new();

    private readonly ScrollBarModel _vertical = new(vertical: true);
    private readonly ScrollBarModel _horizontal = new(vertical: false);
    private bool _autoScroll;
    private Size _autoScrollMargin;
    private Size _autoScrollMinSize;
    private Size _extent;
    private int _scrollX;
    private int _scrollY;
    private bool _updatingScroll;

    /// <summary>Creates a scrollable control.</summary>
    public ScrollableControl()
    {
        SetStyle(ControlStyles.ContainerControl, true);
        HorizontalScroll = new HScrollProperties(this);
        VerticalScroll = new VScrollProperties(this);
    }

    /// <summary>Occurs when the content scrolls.</summary>
    public event ScrollEventHandler? Scroll
    {
        add => Events.AddHandler(EventScroll, value);
        remove => Events.RemoveHandler(EventScroll, value);
    }

    /// <summary>Whether scroll bars appear when the children do not fit.</summary>
    public virtual bool AutoScroll
    {
        get => _autoScroll;
        set
        {
            if (_autoScroll == value)
            {
                return;
            }

            _autoScroll = value;
            PerformLayout(this, nameof(AutoScroll));
        }
    }

    /// <summary>The space kept beyond the furthest child when scrolling.</summary>
    public Size AutoScrollMargin
    {
        get => _autoScrollMargin;
        set
        {
            _autoScrollMargin = value;
            PerformLayout(this, nameof(AutoScrollMargin));
        }
    }

    /// <summary>The smallest scrollable area.</summary>
    public Size AutoScrollMinSize
    {
        get => _autoScrollMinSize;
        set
        {
            _autoScrollMinSize = value;
            PerformLayout(this, nameof(AutoScrollMinSize));
        }
    }

    /// <summary>The scroll position, as negative offsets, as Windows Forms reports it; set it with positive offsets.</summary>
    public Point AutoScrollPosition
    {
        get => new(-_scrollX, -_scrollY);
        set => SetDisplayRectLocation(-Math.Abs(value.X), -Math.Abs(value.Y));
    }

    /// <summary>The horizontal scroll bar's properties.</summary>
    public HScrollProperties HorizontalScroll { get; }

    /// <summary>The vertical scroll bar's properties.</summary>
    public VScrollProperties VerticalScroll { get; }

    /// <summary>The area children are laid out in: the client area, or the scrolled content area, inside the padding.</summary>
    public override Rectangle DisplayRectangle
    {
        get
        {
            var client = ClientRectangle;
            var area = !_autoScroll
                ? client
                : new Rectangle(-_scrollX, -_scrollY, Math.Max(client.Width, _extent.Width), Math.Max(client.Height, _extent.Height));
            var padding = Padding;
            return new Rectangle(area.X + padding.Left, area.Y + padding.Top, Math.Max(0, area.Width - padding.Horizontal), Math.Max(0, area.Height - padding.Vertical));
        }
    }

    /// <summary>The width of the control's own border, which the client area excludes.</summary>
    internal virtual int BorderSize => 0;

    /// <inheritdoc />
    internal override Padding NonClientInsets
    {
        get
        {
            var border = BorderSize;
            return new Padding(border, border, border + (_vertical.Visible ? Theme.ScrollBarSize : 0), border + (_horizontal.Visible ? Theme.ScrollBarSize : 0));
        }
    }

    /// <inheritdoc />
    internal override bool ScrollsByTouch => _autoScroll && (_vertical.Visible || _horizontal.Visible);

    internal ScrollBarModel VerticalBar => _vertical;

    internal ScrollBarModel HorizontalBar => _horizontal;

    /// <summary>Scrolls so a child is in view.</summary>
    /// <param name="activeControl">The child, or one of its descendants.</param>
    public void ScrollControlIntoView(Control? activeControl)
    {
        if (!_autoScroll || activeControl is null || !Contains(activeControl))
        {
            return;
        }

        var child = activeControl;
        while (child.Parent != this)
        {
            child = child.Parent!;
        }

        var client = ClientRectangle;
        var x = _scrollX;
        var y = _scrollY;
        if (child.Left < 0)
        {
            x += child.Left;
        }
        else if (child.Right > client.Width)
        {
            x += Math.Min(child.Right - client.Width, child.Left);
        }

        if (child.Top < 0)
        {
            y += child.Top;
        }
        else if (child.Bottom > client.Height)
        {
            y += Math.Min(child.Bottom - client.Height, child.Top);
        }

        SetDisplayRectLocation(-x, -y);
    }

    /// <summary>Scrolls the content so the display area starts at a point, moving the children.</summary>
    /// <param name="x">The display area's new left: zero or negative.</param>
    /// <param name="y">The display area's new top: zero or negative.</param>
    protected void SetDisplayRectLocation(int x, int y)
    {
        var newX = Math.Clamp(-x, 0, _horizontal.MaximumValue);
        var newY = Math.Clamp(-y, 0, _vertical.MaximumValue);
        _horizontal.Value = newX;
        _vertical.Value = newY;
        var dx = _scrollX - newX;
        var dy = _scrollY - newY;
        if (dx == 0 && dy == 0)
        {
            return;
        }

        var oldX = _scrollX;
        var oldY = _scrollY;
        _scrollX = newX;
        _scrollY = newY;
        foreach (var child in Controls.ToArray())
        {
            child.SetBoundsFromLayout(new Rectangle(child.Left + dx, child.Top + dy, child.Width, child.Height));
        }

        Invalidate();
        if (dy != 0)
        {
            OnScroll(new ScrollEventArgs(ScrollEventType.ThumbPosition, oldY, newY, ScrollOrientation.VerticalScroll));
        }

        if (dx != 0)
        {
            OnScroll(new ScrollEventArgs(ScrollEventType.ThumbPosition, oldX, newX, ScrollOrientation.HorizontalScroll));
        }
    }

    /// <summary>Raises the Scroll event.</summary>
    /// <param name="se">The event data.</param>
    protected virtual void OnScroll(ScrollEventArgs se) => ((ScrollEventHandler?)Events[EventScroll])?.Invoke(this, se);

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_autoScroll && !_updatingScroll)
        {
            UpdateScrollBars();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_autoScroll && _vertical.Visible && e is HandledMouseEventArgs { Handled: false } handled)
        {
            SetDisplayRectLocation(-_scrollX, -(_scrollY - (e.Delta / 120 * 48)));
            handled.Handled = true;
        }
    }

    /// <inheritdoc />
    internal override void ScrollByTouch(int dx, int dy) => SetDisplayRectLocation(-(_scrollX + dx), -(_scrollY + dy));

    /// <inheritdoc />
    internal override void PaintNonClient(Graphics g)
    {
        _vertical.Paint(g, Enabled);
        _horizontal.Paint(g, Enabled);
        if (_vertical.Visible && _horizontal.Visible)
        {
            using var corner = new SolidBrush(Theme.ScrollTrack);
            g.FillRectangle(corner, _vertical.Bounds.X, _horizontal.Bounds.Y, Theme.ScrollBarSize, Theme.ScrollBarSize);
        }
    }

    /// <inheritdoc />
    internal override bool NonClientMouseDown(Point point)
    {
        var bar = _vertical.Visible && _vertical.Bounds.Contains(point) ? _vertical : _horizontal.Visible && _horizontal.Bounds.Contains(point) ? _horizontal : null;
        if (bar is null || !bar.PointerDown(point))
        {
            return false;
        }

        ApplyBars();
        return true;
    }

    /// <inheritdoc />
    internal override void NonClientMouseMove(Point point)
    {
        _vertical.PointerMove(point);
        _horizontal.PointerMove(point);
        ApplyBars();
    }

    /// <inheritdoc />
    internal override void NonClientMouseUp(Point point)
    {
        _vertical.PointerUp();
        _horizontal.PointerUp();
        Invalidate();
    }

    /// <summary>Scrolls to the values the scroll bars were moved to.</summary>
    private void ApplyBars()
    {
        SetDisplayRectLocation(-_horizontal.Value, -_vertical.Value);
        Invalidate();
    }

    /// <summary>Measures the children and shows the scroll bars they need.</summary>
    private void UpdateScrollBars()
    {
        _updatingScroll = true;
        try
        {
            var right = _autoScrollMinSize.Width;
            var bottom = _autoScrollMinSize.Height;
            foreach (var child in Controls.ToArray())
            {
                if (!child.VisibleSelf || child.Dock == DockStyle.Fill)
                {
                    continue;
                }

                right = Math.Max(right, child.Right + _scrollX + _autoScrollMargin.Width);
                bottom = Math.Max(bottom, child.Bottom + _scrollY + _autoScrollMargin.Height);
            }

            _extent = new Size(right, bottom);
            var border = BorderSize;
            var width = Width - (2 * border);
            var height = Height - (2 * border);
            var vertical = bottom > height;
            var horizontal = right > width - (vertical ? Theme.ScrollBarSize : 0);
            vertical = vertical || (horizontal && bottom > height - Theme.ScrollBarSize);
            var changed = vertical != _vertical.Visible || horizontal != _horizontal.Visible;
            _vertical.Visible = vertical;
            _horizontal.Visible = horizontal;
            UpdateClientSize();
            var client = ClientSize;
            _vertical.Bounds = new Rectangle(Width - border - Theme.ScrollBarSize, border, Theme.ScrollBarSize, client.Height);
            _horizontal.Bounds = new Rectangle(border, Height - border - Theme.ScrollBarSize, client.Width, Theme.ScrollBarSize);
            _vertical.ContentLength = bottom;
            _vertical.ViewLength = client.Height;
            _horizontal.ContentLength = right;
            _horizontal.ViewLength = client.Width;
            if (changed)
            {
                // The client area changed size: docked and anchored children follow it.
                LayoutEngine.Layout(this, new LayoutEventArgs(this, "AutoScroll"));
            }

            SetDisplayRectLocation(-Math.Min(_scrollX, _horizontal.MaximumValue), -Math.Min(_scrollY, _vertical.MaximumValue));
        }
        finally
        {
            _updatingScroll = false;
        }
    }
}

/// <summary>The properties of a scroll bar of a <see cref="ScrollableControl"/>.</summary>
public abstract class ScrollProperties
{
    private readonly ScrollableControl _owner;

    private protected ScrollProperties(ScrollableControl owner) => _owner = owner;

    /// <summary>Whether the scroll bar can be used.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How far the track scrolls.</summary>
    public int LargeChange
    {
        get => Bar.ViewLength;
        set
        {
            // The view's size decides the page here.
        }
    }

    /// <summary>The largest value.</summary>
    public int Maximum
    {
        get => Bar.ContentLength;
        set
        {
            // The children's extent decides the maximum here.
        }
    }

    /// <summary>The smallest value: 0.</summary>
    public int Minimum
    {
        get => 0;
        set
        {
            // Scrolling starts at 0 here.
        }
    }

    /// <summary>How far an arrow scrolls.</summary>
    public int SmallChange
    {
        get => Bar.SmallChange;
        set => Bar.SmallChange = Math.Max(1, value);
    }

    /// <summary>The scroll position.</summary>
    public int Value
    {
        get => Bar.Value;
        set => Scroll(value);
    }

    /// <summary>Whether the scroll bar is shown.</summary>
    public bool Visible
    {
        get => Bar.Visible;
        set
        {
            // AutoScroll shows the scroll bars the content needs.
        }
    }

    private protected ScrollableControl Owner => _owner;

    private protected abstract ScrollBarModel Bar { get; }

    private protected abstract void Scroll(int value);
}

/// <summary>The horizontal scroll bar of a <see cref="ScrollableControl"/>.</summary>
public sealed class HScrollProperties : ScrollProperties
{
    /// <summary>Creates the properties of a control's horizontal scroll bar.</summary>
    /// <param name="container">The control.</param>
    public HScrollProperties(ScrollableControl container)
        : base(container)
    {
    }

    private protected override ScrollBarModel Bar => Owner.HorizontalBar;

    private protected override void Scroll(int value) => Owner.AutoScrollPosition = new Point(value, -Owner.AutoScrollPosition.Y);
}

/// <summary>The vertical scroll bar of a <see cref="ScrollableControl"/>.</summary>
public sealed class VScrollProperties : ScrollProperties
{
    /// <summary>Creates the properties of a control's vertical scroll bar.</summary>
    /// <param name="container">The control.</param>
    public VScrollProperties(ScrollableControl container)
        : base(container)
    {
    }

    private protected override ScrollBarModel Bar => Owner.VerticalBar;

    private protected override void Scroll(int value) => Owner.AutoScrollPosition = new Point(-Owner.AutoScrollPosition.X, value);
}
