using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>Location, size, client area, padding, margins, docking, anchoring and automatic sizing.</summary>
public partial class Control
{
    private int _x;
    private int _y;
    private int _width;
    private int _height;
    private int _clientWidth;
    private int _clientHeight;
    private Padding _padding;
    private Padding _margin;
    private Size _minimumSize;
    private Size _maximumSize;
    private DockStyle _dock;
    private AnchorStyles _anchor = AnchorStyles.Top | AnchorStyles.Left;
    private bool _autoSize;

    /// <summary>The size of a new control.</summary>
    protected virtual Size DefaultSize => Size.Empty;

    /// <summary>The padding of a new control.</summary>
    protected virtual Padding DefaultPadding => Padding.Empty;

    /// <summary>The margin of a new control: 3 pixels on every side.</summary>
    protected virtual Padding DefaultMargin => new(3);

    /// <summary>The minimum size of a new control.</summary>
    protected virtual Size DefaultMinimumSize => Size.Empty;

    /// <summary>The maximum size of a new control.</summary>
    protected virtual Size DefaultMaximumSize => Size.Empty;

    /// <summary>The distance from the parent's left edge to the control's.</summary>
    public int Left
    {
        get => _x;
        set => SetBounds(value, _y, _width, _height, BoundsSpecified.X);
    }

    /// <summary>The distance from the parent's top edge to the control's.</summary>
    public int Top
    {
        get => _y;
        set => SetBounds(_x, value, _width, _height, BoundsSpecified.Y);
    }

    /// <summary>The width.</summary>
    public int Width
    {
        get => _width;
        set => SetBounds(_x, _y, value, _height, BoundsSpecified.Width);
    }

    /// <summary>The height.</summary>
    public int Height
    {
        get => _height;
        set => SetBounds(_x, _y, _width, value, BoundsSpecified.Height);
    }

    /// <summary>The distance from the parent's left edge to the control's right edge.</summary>
    public int Right => _x + _width;

    /// <summary>The distance from the parent's top edge to the control's bottom edge.</summary>
    public int Bottom => _y + _height;

    /// <summary>The top-left corner, in the parent's client coordinates.</summary>
    public Point Location
    {
        get => new(_x, _y);
        set => SetBounds(value.X, value.Y, _width, _height, BoundsSpecified.Location);
    }

    /// <summary>The size.</summary>
    public Size Size
    {
        get => new(_width, _height);
        set => SetBounds(_x, _y, value.Width, value.Height, BoundsSpecified.Size);
    }

    /// <summary>The location and size, in the parent's client coordinates.</summary>
    public Rectangle Bounds
    {
        get => new(_x, _y, _width, _height);
        set => SetBounds(value.X, value.Y, value.Width, value.Height, BoundsSpecified.All);
    }

    /// <summary>The size of the client area: the control without its border and scroll bars.</summary>
    public Size ClientSize
    {
        get => new(_clientWidth, _clientHeight);
        set => SetClientSizeCore(value.Width, value.Height);
    }

    /// <summary>The client area, at the origin.</summary>
    public Rectangle ClientRectangle => new(0, 0, _clientWidth, _clientHeight);

    /// <summary>The area child controls are laid out in: the client area, moved by scrolling.</summary>
    public virtual Rectangle DisplayRectangle => ClientRectangle;

    /// <summary>The space inside the control around its content and docked children.</summary>
    public Padding Padding
    {
        get => _padding;
        set
        {
            if (_padding == value)
            {
                return;
            }

            _padding = value;
            OnPaddingChanged(EventArgs.Empty);
        }
    }

    /// <summary>The space around the control that flow and table layouts keep.</summary>
    public Padding Margin
    {
        get => _margin;
        set
        {
            if (_margin == value)
            {
                return;
            }

            _margin = value;
            OnMarginChanged(EventArgs.Empty);
        }
    }

    /// <summary>The smallest size the control can have; zero for no limit.</summary>
    public virtual Size MinimumSize
    {
        get => _minimumSize;
        set
        {
            if (_minimumSize == value)
            {
                return;
            }

            _minimumSize = value;
            if (_maximumSize.Width > 0 && value.Width > _maximumSize.Width)
            {
                _maximumSize.Width = value.Width;
            }

            if (_maximumSize.Height > 0 && value.Height > _maximumSize.Height)
            {
                _maximumSize.Height = value.Height;
            }

            if (_width < value.Width || _height < value.Height)
            {
                Size = new Size(Math.Max(_width, value.Width), Math.Max(_height, value.Height));
            }
        }
    }

    /// <summary>The largest size the control can have; zero for no limit.</summary>
    public virtual Size MaximumSize
    {
        get => _maximumSize;
        set
        {
            if (_maximumSize == value)
            {
                return;
            }

            _maximumSize = value;
            var (width, height) = Constrain(_width, _height);
            if (width != _width || height != _height)
            {
                Size = new Size(width, height);
            }
        }
    }

    /// <summary>The edge of the parent the control is docked to.</summary>
    [DefaultValue(DockStyle.None)]
    public virtual DockStyle Dock
    {
        get => _dock;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(DockStyle));
            }

            if (_dock == value)
            {
                return;
            }

            _dock = value;
            if (value != DockStyle.None)
            {
                // Docking and anchoring exclude each other, as in Windows Forms.
                _anchor = AnchorStyles.Top | AnchorStyles.Left;
            }

            OnDockChanged(EventArgs.Empty);
        }
    }

    /// <summary>The edges of the parent the control keeps its distance to when the parent is resized.</summary>
    [DefaultValue(AnchorStyles.Top | AnchorStyles.Left)]
    public virtual AnchorStyles Anchor
    {
        get => _anchor;
        set
        {
            if (_anchor == value)
            {
                return;
            }

            _anchor = value;
            if (_dock != DockStyle.None)
            {
                _dock = DockStyle.None;
                OnDockChanged(EventArgs.Empty);
            }

            _parent?.InitChildLayout(this);
            _parent?.PerformLayout(this, nameof(Anchor));
        }
    }

    /// <summary>Whether the control sizes itself to its content.</summary>
    public virtual bool AutoSize
    {
        get => _autoSize;
        set
        {
            if (_autoSize == value)
            {
                return;
            }

            _autoSize = value;
            OnAutoSizeChanged(EventArgs.Empty);
        }
    }

    /// <summary>The size the control would have to fit its content.</summary>
    public Size PreferredSize => GetPreferredSize(Size.Empty);

    /// <summary>The non-client edges: the border and scroll bars, which the client area excludes.</summary>
    internal virtual Padding NonClientInsets => Padding.Empty;

    /// <summary>The size the control would have to fit its content, within a proposed size.</summary>
    /// <param name="proposedSize">The size available; zero for no limit.</param>
    public virtual Size GetPreferredSize(Size proposedSize)
    {
        var size = GetPreferredSizeCore(proposedSize);
        var (width, height) = Constrain(size.Width, size.Height);
        return new Size(width, height);
    }

    /// <summary>The size of the content, without the minimum and maximum size applied.</summary>
    /// <param name="proposedSize">The size available; zero for no limit.</param>
    internal virtual Size GetPreferredSizeCore(Size proposedSize) =>
        _controls.Count > 0 ? LayoutEngine.GetPreferredSize(this, proposedSize) : Size;

    /// <summary>Sets the location and size.</summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void SetBounds(int x, int y, int width, int height) => SetBounds(x, y, width, height, BoundsSpecified.All);

    /// <summary>Sets some of the location and size.</summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="specified">Which of the values to use.</param>
    public void SetBounds(int x, int y, int width, int height, BoundsSpecified specified)
    {
        if ((specified & BoundsSpecified.X) == 0)
        {
            x = _x;
        }

        if ((specified & BoundsSpecified.Y) == 0)
        {
            y = _y;
        }

        if ((specified & BoundsSpecified.Width) == 0)
        {
            width = _width;
        }

        if ((specified & BoundsSpecified.Height) == 0)
        {
            height = _height;
        }

        if (x == _x && y == _y && width == _width && height == _height)
        {
            return;
        }

        VerifyThread();
        SetBoundsCore(x, y, width, height, specified);

        // The user moved or sized the control: its anchors are measured again, and the parent lays out its docked
        // and automatically sized children again.
        if (_parent is { } parent && !parent.IsLayingOut)
        {
            parent.InitChildLayout(this);
            if (_dock != DockStyle.None || parent.AutoSize)
            {
                parent.PerformLayout(this, nameof(Bounds));
            }
        }
    }

    /// <summary>Sets the bounds, within the minimum and maximum size, and raises the move and size events.</summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="specified">Which values were asked for.</param>
    protected virtual void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        (width, height) = Constrain(width, height);
        UpdateBounds(x, y, width, height);
    }

    /// <summary>Sets the size of the client area, which sets the size to it plus the border and scroll bars.</summary>
    /// <param name="x">The client width.</param>
    /// <param name="y">The client height.</param>
    protected virtual void SetClientSizeCore(int x, int y)
    {
        var insets = NonClientInsets;
        Size = new Size(x + insets.Horizontal, y + insets.Vertical);
    }

    /// <summary>Records new bounds and raises LocationChanged, SizeChanged and ClientSizeChanged as needed.</summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    protected void UpdateBounds(int x, int y, int width, int height)
    {
        var moved = x != _x || y != _y;
        var resized = width != _width || height != _height;
        _x = x;
        _y = y;
        _width = Math.Max(0, width);
        _height = Math.Max(0, height);
        var oldClient = (_clientWidth, _clientHeight);
        UpdateClientSize();
        var clientResized = oldClient != (_clientWidth, _clientHeight);

        if (moved)
        {
            OnLocationChanged(EventArgs.Empty);
        }

        if (resized)
        {
            OnSizeChanged(EventArgs.Empty);
        }

        if (clientResized)
        {
            OnClientSizeChanged(EventArgs.Empty);
        }

        if (moved || resized)
        {
            InvalidateWindow();
        }
    }

    /// <summary>Recomputes the client size from the size and the non-client edges.</summary>
    internal void UpdateClientSize()
    {
        var insets = NonClientInsets;
        _clientWidth = Math.Max(0, _width - insets.Horizontal);
        _clientHeight = Math.Max(0, _height - insets.Vertical);
    }

    /// <summary>Sets the bounds a layout engine computed, without measuring the anchors again.</summary>
    /// <param name="bounds">The bounds.</param>
    internal void SetBoundsFromLayout(Rectangle bounds)
    {
        if (bounds != Bounds)
        {
            SetBoundsCore(bounds.X, bounds.Y, bounds.Width, bounds.Height, BoundsSpecified.None);
        }
    }

    /// <summary>A size within the minimum and maximum size.</summary>
    internal (int Width, int Height) Constrain(int width, int height)
    {
        if (_maximumSize.Width > 0)
        {
            width = Math.Min(width, _maximumSize.Width);
        }

        if (_maximumSize.Height > 0)
        {
            height = Math.Min(height, _maximumSize.Height);
        }

        return (Math.Max(width, _minimumSize.Width), Math.Max(height, _minimumSize.Height));
    }

    /// <summary>Converts a point from screen coordinates to the client area's.</summary>
    /// <param name="p">The point on the screen.</param>
    public Point PointToClient(Point p)
    {
        var origin = ClientOriginOnScreen();
        return new Point(p.X - origin.X, p.Y - origin.Y);
    }

    /// <summary>Converts a point from the client area's coordinates to the screen's.</summary>
    /// <param name="p">The point in the client area.</param>
    public Point PointToScreen(Point p)
    {
        var origin = ClientOriginOnScreen();
        return new Point(p.X + origin.X, p.Y + origin.Y);
    }

    /// <summary>Converts a rectangle from screen coordinates to the client area's.</summary>
    /// <param name="r">The rectangle on the screen.</param>
    public Rectangle RectangleToClient(Rectangle r) => new(PointToClient(r.Location), r.Size);

    /// <summary>Converts a rectangle from the client area's coordinates to the screen's.</summary>
    /// <param name="r">The rectangle in the client area.</param>
    public Rectangle RectangleToScreen(Rectangle r) => new(PointToScreen(r.Location), r.Size);

    /// <summary>Scales the control and its children by a factor, as automatic scaling does.</summary>
    /// <param name="factor">The horizontal and vertical factors.</param>
    public void Scale(SizeF factor)
    {
        SuspendLayout();
        try
        {
            ScaleControl(factor, BoundsSpecified.All);
            foreach (var child in _controls.ToArray())
            {
                child.Scale(factor);
            }
        }
        finally
        {
            ResumeLayout(false);
        }
    }

    /// <summary>Scales the control's own bounds, padding and margin.</summary>
    /// <param name="factor">The horizontal and vertical factors.</param>
    /// <param name="specified">Which bounds to scale.</param>
    protected virtual void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        Padding = ScalePadding(_padding, factor);
        Margin = ScalePadding(_margin, factor);
        var bounds = GetScaledBounds(Bounds, factor, specified);
        SetBoundsCore(bounds.X, bounds.Y, bounds.Width, bounds.Height, BoundsSpecified.None);
    }

    /// <summary>The bounds scaled by a factor; top-level forms keep their location.</summary>
    /// <param name="bounds">The bounds.</param>
    /// <param name="factor">The horizontal and vertical factors.</param>
    /// <param name="specified">Which bounds to scale.</param>
    protected virtual Rectangle GetScaledBounds(Rectangle bounds, SizeF factor, BoundsSpecified specified)
    {
        var insets = NonClientInsets;
        var x = this is Form { TopLevel: true } ? bounds.X : (int)Math.Round(bounds.X * factor.Width);
        var y = this is Form { TopLevel: true } ? bounds.Y : (int)Math.Round(bounds.Y * factor.Height);
        var width = GetStyle(ControlStyles.FixedWidth) ? bounds.Width : (int)Math.Round((bounds.Width - insets.Horizontal) * factor.Width) + insets.Horizontal;
        var height = GetStyle(ControlStyles.FixedHeight) ? bounds.Height : (int)Math.Round((bounds.Height - insets.Vertical) * factor.Height) + insets.Vertical;
        return new Rectangle(x, y, width, height);
    }

    /// <summary>Where the client area's origin is in the window's client area.</summary>
    internal Point ClientOriginInWindow()
    {
        if (_parent is null)
        {
            return Point.Empty;
        }

        var parentOrigin = _parent.ClientOriginInWindow();
        var insets = NonClientInsets;
        return new Point(parentOrigin.X + _x + insets.Left, parentOrigin.Y + _y + insets.Top);
    }

    /// <summary>The control's bounds in the window's client area.</summary>
    internal Rectangle BoundsInWindow()
    {
        if (_parent is null)
        {
            return new Rectangle(0, 0, _width, _height);
        }

        var parentOrigin = _parent.ClientOriginInWindow();
        return new Rectangle(parentOrigin.X + _x, parentOrigin.Y + _y, _width, _height);
    }

    /// <summary>Where the client area's origin is on the screen: the app preview's desktop.</summary>
    private Point ClientOriginOnScreen()
    {
        var inWindow = ClientOriginInWindow();
        var form = TopLevelControl as Form;
        var window = form?.ClientOriginOnScreen ?? Point.Empty;
        return new Point(window.X + inWindow.X, window.Y + inWindow.Y);
    }

    private static Padding ScalePadding(Padding padding, SizeF factor) => new(
        (int)Math.Round(padding.Left * factor.Width),
        (int)Math.Round(padding.Top * factor.Height),
        (int)Math.Round(padding.Right * factor.Width),
        (int)Math.Round(padding.Bottom * factor.Height));
}
