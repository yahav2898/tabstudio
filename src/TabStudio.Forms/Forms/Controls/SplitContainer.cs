using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>
/// Two panels side by side, or one above the other, with a splitter between them that is dragged to resize them.
/// A finger can grab the splitter a little beyond its edges.
/// </summary>
[DefaultEvent(nameof(SplitterMoved))]
public class SplitContainer : ContainerControl, ISupportInitialize
{
    private static readonly object EventSplitterMoved = new();
    private static readonly object EventSplitterMoving = new();

    /// <summary>How far beyond the splitter a finger can grab it.</summary>
    private const int TouchMargin = 6;

    private Orientation _orientation = Orientation.Vertical;
    private int _splitterDistance = 50;
    private int _splitterWidth = 4;
    private int _panel1MinSize = 25;
    private int _panel2MinSize = 25;
    private bool _panel1Collapsed;
    private bool _panel2Collapsed;
    private BorderStyle _borderStyle;
    private bool _dragging;
    private int _dragOffset;
    private bool _initializing;
    private int _fixedSize = -1;

    /// <summary>Creates a split container.</summary>
    public SplitContainer()
    {
        Panel1 = new SplitterPanel(this);
        Panel2 = new SplitterPanel(this);
        SetStyle(ControlStyles.Selectable, true);
        Controls.Add(Panel1);
        Controls.Add(Panel2);
        LayoutPanels();
    }

    /// <summary>Occurs when the splitter has moved.</summary>
    public event SplitterEventHandler? SplitterMoved
    {
        add => Events.AddHandler(EventSplitterMoved, value);
        remove => Events.RemoveHandler(EventSplitterMoved, value);
    }

    /// <summary>Occurs while the splitter is dragged; handlers can cancel the move.</summary>
    public event SplitterCancelEventHandler? SplitterMoving
    {
        add => Events.AddHandler(EventSplitterMoving, value);
        remove => Events.RemoveHandler(EventSplitterMoving, value);
    }

    /// <summary>The first panel: the left or top one.</summary>
    public SplitterPanel Panel1 { get; }

    /// <summary>The second panel: the right or bottom one.</summary>
    public SplitterPanel Panel2 { get; }

    /// <summary>Whether the panels are side by side or one above the other.</summary>
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            LayoutPanels();
        }
    }

    /// <summary>The distance of the splitter from the left or top edge.</summary>
    public int SplitterDistance
    {
        get => _splitterDistance;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _splitterDistance = _initializing ? value : Clamp(value);
            RememberFixedSize();
            LayoutPanels();
        }
    }

    /// <summary>The width of the splitter.</summary>
    public int SplitterWidth
    {
        get => _splitterWidth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _splitterWidth = value;
            LayoutPanels();
        }
    }

    /// <summary>The step the splitter moves in.</summary>
    public int SplitterIncrement { get; set; } = 1;

    /// <summary>The splitter's bounds.</summary>
    public Rectangle SplitterRectangle => _orientation == Orientation.Vertical
        ? new Rectangle(_splitterDistance, 0, _splitterWidth, ClientSize.Height)
        : new Rectangle(0, _splitterDistance, ClientSize.Width, _splitterWidth);

    /// <summary>The least size of the first panel.</summary>
    public int Panel1MinSize
    {
        get => _panel1MinSize;
        set
        {
            _panel1MinSize = Math.Max(0, value);
            LayoutPanels();
        }
    }

    /// <summary>The least size of the second panel.</summary>
    public int Panel2MinSize
    {
        get => _panel2MinSize;
        set
        {
            _panel2MinSize = Math.Max(0, value);
            LayoutPanels();
        }
    }

    /// <summary>Whether the first panel is hidden.</summary>
    public bool Panel1Collapsed
    {
        get => _panel1Collapsed;
        set
        {
            _panel1Collapsed = value;
            if (value)
            {
                _panel2Collapsed = false;
            }

            LayoutPanels();
        }
    }

    /// <summary>Whether the second panel is hidden.</summary>
    public bool Panel2Collapsed
    {
        get => _panel2Collapsed;
        set
        {
            _panel2Collapsed = value;
            if (value)
            {
                _panel1Collapsed = false;
            }

            LayoutPanels();
        }
    }

    /// <summary>The panel that keeps its size when the container resizes.</summary>
    public FixedPanel FixedPanel
    {
        get => field;
        set
        {
            field = value;
            RememberFixedSize();
        }
    }

    /// <summary>Whether the splitter cannot be moved.</summary>
    public bool IsSplitterFixed { get; set; }

    /// <summary>The border.</summary>
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            UpdateClientSize();
            LayoutPanels();
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override Size DefaultSize => new(150, 100);

    /// <inheritdoc />
    internal override Padding NonClientInsets => new(ThemePainter.BorderWidth(_borderStyle));

    /// <inheritdoc />
    public void BeginInit() => _initializing = true;

    /// <inheritdoc />
    public void EndInit()
    {
        _initializing = false;
        _splitterDistance = Clamp(_splitterDistance);
        RememberFixedSize();
        LayoutPanels();
    }

    /// <summary>Raises SplitterMoved.</summary>
    /// <param name="e">The event data.</param>
    public void OnSplitterMoved(SplitterEventArgs e) => ((SplitterEventHandler?)Events[EventSplitterMoved])?.Invoke(this, e);

    /// <summary>Raises SplitterMoving.</summary>
    /// <param name="e">The event data.</param>
    public void OnSplitterMoving(SplitterCancelEventArgs e) => ((SplitterCancelEventHandler?)Events[EventSplitterMoving])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        LayoutPanels();
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && !IsSplitterFixed && GrabArea.Contains(e.Location))
        {
            _dragging = true;
            _dragOffset = (_orientation == Orientation.Vertical ? e.X : e.Y) - _splitterDistance;
            Capture = true;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = !IsSplitterFixed && GrabArea.Contains(e.Location) || _dragging
            ? (_orientation == Orientation.Vertical ? Cursors.VSplit : Cursors.HSplit)
            : Cursors.Default;
        if (!_dragging)
        {
            return;
        }

        var along = (_orientation == Orientation.Vertical ? e.X : e.Y) - _dragOffset;
        along = Clamp(along / Math.Max(1, SplitterIncrement) * Math.Max(1, SplitterIncrement));
        if (along == _splitterDistance)
        {
            return;
        }

        var moving = new SplitterCancelEventArgs(e.X, e.Y, along, along);
        OnSplitterMoving(moving);
        if (moving.Cancel)
        {
            return;
        }

        _splitterDistance = along;
        RememberFixedSize();
        LayoutPanels();
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging)
        {
            _dragging = false;
            Capture = false;
            var splitter = SplitterRectangle;
            OnSplitterMoved(new SplitterEventArgs(e.X, e.Y, splitter.X, splitter.Y));
        }
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        Focused && (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsSplitterFixed || e.Handled)
        {
            return;
        }

        var step = e.KeyCode switch
        {
            Keys.Left or Keys.Up => -SplitterIncrement,
            Keys.Right or Keys.Down => SplitterIncrement,
            _ => 0,
        };

        if (step != 0)
        {
            SplitterDistance = _splitterDistance + step;
            var splitter = SplitterRectangle;
            OnSplitterMoved(new SplitterEventArgs(splitter.X, splitter.Y, splitter.X, splitter.Y));
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(EventArgs e)
    {
        if (!_initializing && _fixedSize >= 0)
        {
            var length = Length;
            _splitterDistance = FixedPanel switch
            {
                FixedPanel.Panel1 => _fixedSize,
                FixedPanel.Panel2 => length - _splitterWidth - _fixedSize,
                _ => _splitterDistance,
            };
        }

        base.OnSizeChanged(e);
        LayoutPanels();
    }

    /// <inheritdoc />
    internal override bool KeepsPointFromChildren(Point point) => !IsSplitterFixed && GrabArea.Contains(point);

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        if (Focused && Window?.ShowFocusCues == true)
        {
            ThemePainter.DrawFocusRectangle(g, SplitterRectangle);
        }
    }

    /// <inheritdoc />
    internal override void PaintNonClient(Graphics g) => ThemePainter.DrawBorder(g, new Rectangle(Point.Empty, Size), _borderStyle);

    private int Length => _orientation == Orientation.Vertical ? ClientSize.Width : ClientSize.Height;

    private Rectangle GrabArea => _panel1Collapsed || _panel2Collapsed ? Rectangle.Empty : Rectangle.Inflate(SplitterRectangle, _orientation == Orientation.Vertical ? TouchMargin : 0, _orientation == Orientation.Vertical ? 0 : TouchMargin);

    private int Clamp(int distance)
    {
        var length = Length;
        var max = length - _splitterWidth - _panel2MinSize;
        return Math.Max(0, Math.Min(Math.Max(distance, _panel1MinSize), Math.Max(_panel1MinSize, max)));
    }

    private void RememberFixedSize() => _fixedSize = FixedPanel switch
    {
        FixedPanel.Panel1 => _splitterDistance,
        FixedPanel.Panel2 => Length - _splitterDistance - _splitterWidth,
        _ => -1,
    };

    private void LayoutPanels()
    {
        if (Panel1 is null || Panel2 is null)
        {
            return;
        }

        var client = ClientRectangle;
        Panel1.Visible = !_panel1Collapsed;
        Panel2.Visible = !_panel2Collapsed;
        if (_panel1Collapsed)
        {
            Panel2.SetBoundsFromLayout(client);
        }
        else if (_panel2Collapsed)
        {
            Panel1.SetBoundsFromLayout(client);
        }
        else if (_orientation == Orientation.Vertical)
        {
            Panel1.SetBoundsFromLayout(new Rectangle(0, 0, _splitterDistance, client.Height));
            Panel2.SetBoundsFromLayout(Rectangle.FromLTRB(Math.Min(client.Width, _splitterDistance + _splitterWidth), 0, client.Width, client.Height));
        }
        else
        {
            Panel1.SetBoundsFromLayout(new Rectangle(0, 0, client.Width, _splitterDistance));
            Panel2.SetBoundsFromLayout(Rectangle.FromLTRB(0, Math.Min(client.Height, _splitterDistance + _splitterWidth), client.Width, client.Height));
        }

        Invalidate();
    }
}

/// <summary>A panel of a split container.</summary>
public sealed class SplitterPanel : Panel
{
    /// <summary>Creates a panel of a split container.</summary>
    /// <param name="owner">The split container.</param>
    public SplitterPanel(SplitContainer owner)
    {
        Owner = owner;
    }

    /// <summary>The split container.</summary>
    internal SplitContainer Owner { get; }
}

/// <summary>Which panel of a split container keeps its size.</summary>
public enum FixedPanel
{
    /// <summary>Neither.</summary>
    None = 0,

    /// <summary>The first.</summary>
    Panel1 = 1,

    /// <summary>The second.</summary>
    Panel2 = 2,
}

/// <summary>Data about a splitter that moved.</summary>
/// <param name="x">The pointer's x coordinate.</param>
/// <param name="y">The pointer's y coordinate.</param>
/// <param name="splitX">The splitter's x coordinate.</param>
/// <param name="splitY">The splitter's y coordinate.</param>
public class SplitterEventArgs(int x, int y, int splitX, int splitY) : EventArgs
{
    /// <summary>The pointer's x coordinate.</summary>
    public int X { get; } = x;

    /// <summary>The pointer's y coordinate.</summary>
    public int Y { get; } = y;

    /// <summary>The splitter's x coordinate.</summary>
    public int SplitX { get; set; } = splitX;

    /// <summary>The splitter's y coordinate.</summary>
    public int SplitY { get; set; } = splitY;
}

/// <summary>Data about a splitter being moved, which handlers can cancel.</summary>
/// <param name="mouseCursorX">The pointer's x coordinate.</param>
/// <param name="mouseCursorY">The pointer's y coordinate.</param>
/// <param name="splitX">The splitter's x coordinate.</param>
/// <param name="splitY">The splitter's y coordinate.</param>
public class SplitterCancelEventArgs(int mouseCursorX, int mouseCursorY, int splitX, int splitY) : CancelEventArgs
{
    /// <summary>The pointer's x coordinate.</summary>
    public int MouseCursorX { get; } = mouseCursorX;

    /// <summary>The pointer's y coordinate.</summary>
    public int MouseCursorY { get; } = mouseCursorY;

    /// <summary>The splitter's x coordinate.</summary>
    public int SplitX { get; set; } = splitX;

    /// <summary>The splitter's y coordinate.</summary>
    public int SplitY { get; set; } = splitY;
}

/// <summary>Handles a splitter that moved.</summary>
/// <param name="sender">The split container.</param>
/// <param name="e">The event data.</param>
public delegate void SplitterEventHandler(object? sender, SplitterEventArgs e);

/// <summary>Handles a splitter being moved.</summary>
/// <param name="sender">The split container.</param>
/// <param name="e">The event data.</param>
public delegate void SplitterCancelEventHandler(object? sender, SplitterCancelEventArgs e);
