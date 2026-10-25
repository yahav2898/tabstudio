using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Layout;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>
/// A tool bar: a row of buttons, labels, separators, drop-down buttons and hosted controls. Menu bars, status bars
/// and drop-down menus are tool strips too.
/// </summary>
/// <remarks>
/// Items that do not fit are hidden rather than moved to an overflow menu. The strip draws in the Windows 10 look of
/// the professional renderer; a custom <c>Renderer</c> is not supported.
/// </remarks>
[DefaultEvent(nameof(ItemClicked))]
[DefaultProperty(nameof(Items))]
public class ToolStrip : ScrollableControl
{
    private static readonly object EventItemClicked = new();
    private static readonly object EventItemAdded = new();
    private static readonly object EventItemRemoved = new();

    /// <summary>The font of strips whose font is not set: the system's menu font.</summary>
    private static readonly Font DefaultStripFont = SystemFonts.MenuFont;

    /// <summary>The width of the move grip, with its margins.</summary>
    private const int GripWidth = 7;

    private readonly ToolStripItemCollection _items;
    private ToolStripGripStyle _gripStyle = ToolStripGripStyle.Visible;
    private ToolStripLayoutStyle _layoutStyle;
    private Size _imageScalingSize = new(16, 16);
    private ToolStripItem? _hot;
    private ToolStripItem? _pressed;

    /// <summary>Creates a tool strip.</summary>
    public ToolStrip()
    {
        _items = new ToolStripItemCollection(this);
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        TabStop = false;
        AutoSize = true;
        Dock = DefaultDock;
    }

    /// <summary>Creates a tool strip with items.</summary>
    /// <param name="items">The items.</param>
    public ToolStrip(params ToolStripItem[] items)
        : this()
    {
        Items.AddRange(items);
    }

    /// <summary>Occurs when an item is clicked.</summary>
    public event ToolStripItemClickedEventHandler? ItemClicked
    {
        add => Events.AddHandler(EventItemClicked, value);
        remove => Events.RemoveHandler(EventItemClicked, value);
    }

    /// <summary>Occurs when an item is added.</summary>
    public event ToolStripItemEventHandler? ItemAdded
    {
        add => Events.AddHandler(EventItemAdded, value);
        remove => Events.RemoveHandler(EventItemAdded, value);
    }

    /// <summary>Occurs when an item is removed.</summary>
    public event ToolStripItemEventHandler? ItemRemoved
    {
        add => Events.AddHandler(EventItemRemoved, value);
        remove => Events.RemoveHandler(EventItemRemoved, value);
    }

    /// <summary>The items.</summary>
    public virtual ToolStripItemCollection Items => _items;

    /// <summary>The font: the system's menu font unless set, rather than the parent's, as in Windows Forms.</summary>
    [AllowNull]
    public override Font Font
    {
        get => IsFontSet ? base.Font : DefaultStripFont;
        set => base.Font = value;
    }

    /// <summary>Whether the move grip is shown.</summary>
    public ToolStripGripStyle GripStyle
    {
        get => _gripStyle;
        set
        {
            _gripStyle = value;
            PerformLayout();
            Invalidate();
        }
    }

    /// <summary>Where the move grip is.</summary>
    public Rectangle GripRectangle => GripShown ? new Rectangle(0, 0, GripWidth, Height) : Rectangle.Empty;

    /// <summary>The space around the move grip.</summary>
    public Padding GripMargin { get; set; } = new(2);

    /// <summary>How the items are laid out.</summary>
    public ToolStripLayoutStyle LayoutStyle
    {
        get => _layoutStyle;
        set
        {
            _layoutStyle = value;
            PerformLayout();
            Invalidate();
        }
    }

    /// <summary>Whether the items run across or down.</summary>
    public Orientation Orientation =>
        this is ToolStripDropDown || _layoutStyle == ToolStripLayoutStyle.VerticalStackWithOverflow
        || (_layoutStyle == ToolStripLayoutStyle.StackWithOverflow && Dock is DockStyle.Left or DockStyle.Right)
            ? Orientation.Vertical
            : Orientation.Horizontal;

    /// <summary>How the strip is drawn. Every mode draws the professional look.</summary>
    public ToolStripRenderMode RenderMode { get; set; } = ToolStripRenderMode.ManagerRenderMode;

    /// <summary>The size images are scaled to.</summary>
    public Size ImageScalingSize
    {
        get => _imageScalingSize;
        set
        {
            _imageScalingSize = value;
            PerformLayout();
            Invalidate();
        }
    }

    /// <summary>Whether items show their tooltips.</summary>
    public bool ShowItemToolTips { get; set; } = true;

    /// <summary>Whether the strip stretches across its container. Kept for compatibility.</summary>
    public bool Stretch { get; set; }

    /// <summary>Whether items that do not fit go to an overflow menu. Items that do not fit are hidden.</summary>
    public bool CanOverflow { get; set; } = true;

    /// <summary>Whether items can be reordered by dragging. Kept for compatibility.</summary>
    public bool AllowItemReorder { get; set; }

    /// <summary>Whether menus of MDI children merge. Kept for compatibility.</summary>
    public bool AllowMerge { get; set; } = true;

    /// <summary>The direction of the items' text. Kept for compatibility.</summary>
    public virtual ToolStripTextDirection TextDirection { get; set; } = ToolStripTextDirection.Horizontal;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => ToolStripLayout.Instance;

    /// <summary>The edge the strip docks to unless set.</summary>
    protected virtual DockStyle DefaultDock => DockStyle.Top;

    /// <inheritdoc />
    protected override Size DefaultSize => new(100, 25);

    /// <inheritdoc />
    protected override Padding DefaultPadding => new(0, 0, 1, 0);

    /// <inheritdoc />
    protected override Padding DefaultMargin => Padding.Empty;

    /// <summary>Whether the strip draws the move grip.</summary>
    private protected bool GripShown => _gripStyle == ToolStripGripStyle.Visible && Orientation == Orientation.Horizontal && this is not ToolStripDropDown;

    /// <summary>The least height of the strip across, whatever its items.</summary>
    private protected virtual int MinimumThickness => 25;

    /// <summary>The item under the pointer.</summary>
    internal ToolStripItem? HotItem => _hot;

    /// <summary>The item at a point.</summary>
    /// <param name="point">The point, in client coordinates.</param>
    public ToolStripItem? GetItemAt(Point point)
    {
        foreach (var item in _items.ToList())
        {
            if (item.Available && !item.Bounds.IsEmpty && item.Bounds.Contains(point))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>The item at a point.</summary>
    /// <param name="x">The x coordinate, in client coordinates.</param>
    /// <param name="y">The y coordinate, in client coordinates.</param>
    public ToolStripItem? GetItemAt(int x, int y) => GetItemAt(new Point(x, y));

    /// <inheritdoc />
    public override string ToString() => $"{base.ToString()}, Name: {Name}, Items: {_items.Count}";

    /// <summary>Raises ItemClicked.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnItemClicked(ToolStripItemClickedEventArgs e) =>
        ((ToolStripItemClickedEventHandler?)Events[EventItemClicked])?.Invoke(this, e);

    /// <summary>Raises ItemAdded.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnItemAdded(ToolStripItemEventArgs e) =>
        ((ToolStripItemEventHandler?)Events[EventItemAdded])?.Invoke(this, e);

    /// <summary>Raises ItemRemoved.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnItemRemoved(ToolStripItemEventArgs e) =>
        ((ToolStripItemEventHandler?)Events[EventItemRemoved])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs mea)
    {
        base.OnMouseMove(mea);
        var item = GetItemAt(mea.Location);
        if (_pressed is null)
        {
            SetHot(item?.CanSelect == true ? item : null);
        }
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_pressed is null)
        {
            SetHot(null);
        }
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs mea)
    {
        base.OnMouseDown(mea);
        if (GetItemAt(mea.Location) is not { } item || !item.Enabled)
        {
            return;
        }

        item.DispatchMouseDown(new MouseEventArgs(mea.Button, mea.Clicks, mea.X - item.Bounds.X, mea.Y - item.Bounds.Y, mea.Delta));
        if (mea.Button != MouseButtons.Left || !item.CanSelect)
        {
            return;
        }

        SetHot(item);
        if (item is ToolStripDropDownItem dropDownItem && dropDownItem.OpensOnPress(new Point(mea.X - item.Bounds.X, mea.Y - item.Bounds.Y)))
        {
            // Menus open on the press, as in Windows; the release then does nothing.
            OpenDropDown(dropDownItem);
            return;
        }

        _pressed = item;
        item.Pressed = true;
        item.Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs mea)
    {
        base.OnMouseUp(mea);
        var item = _pressed;
        _pressed = null;
        if (item is null)
        {
            return;
        }

        item.Pressed = false;
        item.Invalidate();
        if (!item.Bounds.Contains(mea.Location))
        {
            SetHot(null);
            return;
        }

        item.DispatchMouseUp(new MouseEventArgs(mea.Button, mea.Clicks, mea.X - item.Bounds.X, mea.Y - item.Bounds.Y, mea.Delta));
        ClickItem(item);
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        PaintBackground(g);
        if (GripShown)
        {
            PaintGrip(g);
        }

        foreach (var item in _items.ToList())
        {
            if (item.Available && !item.Bounds.IsEmpty && item is not ToolStripControlHost)
            {
                item.PaintOnStrip(g, item.Bounds);
            }
        }
    }

    /// <summary>Draws the strip's background, behind its items.</summary>
    private protected virtual void PaintBackground(Graphics g)
    {
        if (HasLocalBackColor)
        {
            return;
        }

        var bounds = ClientRectangle;
        using (var gradient = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height)), Color.FromArgb(250, 250, 250), Color.FromArgb(238, 238, 238), LinearGradientMode.Vertical))
        {
            g.FillRectangle(gradient, bounds);
        }

        using var line = new Pen(Color.FromArgb(218, 218, 218));
        g.DrawLine(line, 0, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
    }

    /// <summary>Clicks an item: raises ItemClicked, then the item's Click.</summary>
    internal void ClickItem(ToolStripItem item)
    {
        if (!item.Enabled)
        {
            return;
        }

        OnItemClicked(new ToolStripItemClickedEventArgs(item));
        if (item is ToolStripSplitButton split)
        {
            split.TapButton();
        }
        else
        {
            item.PerformClick();
        }
    }

    /// <summary>Opens an item's drop-down below it, or beside it on a vertical strip.</summary>
    internal virtual void OpenDropDown(ToolStripDropDownItem item)
    {
        if (Window is not { } window)
        {
            return;
        }

        var origin = ClientOriginInWindow();
        var anchor = new Rectangle(origin.X + item.Bounds.X, origin.Y + item.Bounds.Y, item.Bounds.Width, item.Bounds.Height);
        item.DropDown.OpenFrom(item, this, window, anchor, Orientation == Orientation.Vertical, parent: null);
        SetHot(item);
        Invalidate();
    }

    /// <summary>Clicks the menu item whose shortcut key is pressed, searching every drop-down.</summary>
    /// <param name="keyData">The key with modifiers.</param>
    /// <returns>Whether an item took the key.</returns>
    internal bool ProcessShortcut(Keys keyData)
    {
        if (keyData == Keys.None || !Enabled)
        {
            return false;
        }

        if (FindShortcut(_items, keyData) is { } item)
        {
            Window?.CloseAllPopups();
            item.Owner?.ClickItem(item);
            return true;
        }

        return ProcessMnemonicKey(keyData);
    }

    /// <summary>Opens the menu whose access key is pressed with Alt, on a menu bar.</summary>
    private protected virtual bool ProcessMnemonicKey(Keys keyData) => false;

    /// <summary>Called when an item is added.</summary>
    internal void OnItemAddedInternal(ToolStripItem item)
    {
        if (item is ToolStripControlHost host && this is not ToolStripDropDown)
        {
            Controls.Add(host.Control);
        }

        PerformLayout();
        Invalidate();
        OnItemAdded(new ToolStripItemEventArgs(item));
    }

    /// <summary>Called when an item is removed.</summary>
    internal void OnItemRemovedInternal(ToolStripItem item)
    {
        if (item is ToolStripControlHost host && host.Control.Parent == this)
        {
            Controls.Remove(host.Control);
        }

        if (_hot == item)
        {
            _hot = null;
        }

        if (_pressed == item)
        {
            _pressed = null;
        }

        PerformLayout();
        Invalidate();
        OnItemRemoved(new ToolStripItemEventArgs(item));
    }

    /// <summary>The tooltip of the item under the pointer, for the window's tooltips.</summary>
    internal string? HotItemToolTip() =>
        ShowItemToolTips && _hot is { } item && item.ToolTipText is { Length: > 0 } text ? text : null;

    /// <summary>Highlights an item, or none.</summary>
    internal void SetHot(ToolStripItem? item)
    {
        if (_hot == item)
        {
            return;
        }

        if (_hot is { } previous)
        {
            previous.Selected = false;
            previous.DispatchMouseLeave();
        }

        _hot = item;
        if (item is not null)
        {
            item.Selected = true;
            item.DispatchMouseEnter();
        }

        Window?.RestartToolTip(this);
        Invalidate();
    }

    /// <summary>Lays out the items: across or down, the right-aligned ones from the far end, springs taking the space left.</summary>
    internal void LayoutItems()
    {
        var display = DisplayRectangle;
        var items = _items.ToList().Where(item => item.Available).ToList();
        foreach (var item in _items.ToList().Where(item => !item.Available))
        {
            item.Bounds = Rectangle.Empty;
        }

        if (Orientation == Orientation.Vertical)
        {
            LayoutDown(display, items);
        }
        else
        {
            LayoutAcross(display, items);
        }

        foreach (var host in items.OfType<ToolStripControlHost>())
        {
            if (host.Control.Parent == this)
            {
                host.Control.Visible = !host.Bounds.IsEmpty;
                if (!host.Bounds.IsEmpty)
                {
                    host.Control.SetBoundsFromLayout(host.ControlBounds());
                }
            }
        }
    }

    /// <summary>The size the strip needs for its items.</summary>
    internal Size MeasureItems()
    {
        var items = _items.ToList().Where(item => item.Available).ToList();
        var padding = Padding;
        if (Orientation == Orientation.Vertical)
        {
            var width = 0;
            var height = 0;
            foreach (var item in items)
            {
                var size = item.LayoutSize;
                width = Math.Max(width, size.Width + item.Margin.Horizontal);
                height += size.Height + item.Margin.Vertical;
            }

            return new Size(width + padding.Horizontal, height + padding.Vertical);
        }
        else
        {
            var width = GripShown ? GripWidth : 0;
            var height = 0;
            foreach (var item in items)
            {
                var size = item.LayoutSize;
                width += size.Width + item.Margin.Horizontal;
                height = Math.Max(height, size.Height + item.Margin.Vertical);
            }

            return new Size(width + padding.Horizontal, Math.Max(MinimumThickness, height + padding.Vertical));
        }
    }

    /// <inheritdoc />
    internal override Size GetPreferredSizeCore(Size proposedSize)
    {
        var size = MeasureItems();
        return Orientation == Orientation.Horizontal ? new Size(Math.Max(size.Width, 1), size.Height) : size;
    }

    /// <summary>Whether the app set the strip's background color.</summary>
    private protected bool HasLocalBackColor => this is not ToolStripDropDown && IsBackColorSet;

    private void LayoutAcross(Rectangle display, List<ToolStripItem> items)
    {
        var left = display.Left + (GripShown ? GripWidth : 0);
        var right = display.Right;
        var springs = items.OfType<ToolStripStatusLabel>().Where(label => label.Spring).ToList();
        var fixedWidth = items.Where(item => !springs.Contains(item)).Sum(item => item.LayoutSize.Width + item.Margin.Horizontal);
        var springWidth = springs.Count == 0 ? 0 : Math.Max(0, (right - left - fixedWidth - springs.Sum(s => s.Margin.Horizontal)) / springs.Count);

        foreach (var item in items.Where(item => item.Alignment == ToolStripItemAlignment.Left || springs.Count > 0))
        {
            var width = springs.Contains(item) ? springWidth : item.LayoutSize.Width;
            item.Bounds = Fit(new Rectangle(left + item.Margin.Left, display.Top + item.Margin.Top, width, display.Height - item.Margin.Vertical), right);
            left += width + item.Margin.Horizontal;
        }

        if (springs.Count > 0)
        {
            return;
        }

        foreach (var item in items.Where(item => item.Alignment == ToolStripItemAlignment.Right))
        {
            var width = item.LayoutSize.Width;
            var x = right - item.Margin.Right - width;
            item.Bounds = x < left ? Rectangle.Empty : new Rectangle(x, display.Top + item.Margin.Top, width, display.Height - item.Margin.Vertical);
            right = x - item.Margin.Left;
        }
    }

    private static Rectangle Fit(Rectangle bounds, int right) => bounds.Right > right + 1 ? Rectangle.Empty : bounds;

    private void LayoutDown(Rectangle display, List<ToolStripItem> items)
    {
        var top = display.Top;
        foreach (var item in items)
        {
            var size = item.LayoutSize;
            item.Bounds = new Rectangle(display.Left + item.Margin.Left, top + item.Margin.Top, Math.Max(0, display.Width - item.Margin.Horizontal), size.Height);
            top += size.Height + item.Margin.Vertical;
        }
    }

    private void PaintGrip(Graphics g)
    {
        using var dark = new SolidBrush(Color.FromArgb(153, 153, 153));
        using var light = new SolidBrush(Color.White);
        for (var y = 4; y + 2 < Height - 3; y += 4)
        {
            g.FillRectangle(light, 4, y + 1, 2, 2);
            g.FillRectangle(dark, 3, y, 2, 2);
        }
    }

    private static ToolStripItem? FindShortcut(ToolStripItemCollection items, Keys keyData)
    {
        foreach (var item in items.ToList())
        {
            if (item is ToolStripMenuItem menuItem && menuItem.ShortcutKeys == keyData && menuItem.Enabled && menuItem.Available)
            {
                return menuItem;
            }

            if (item is ToolStripDropDownItem { HasDropDownItems: true, Enabled: true } dropDown && FindShortcut(dropDown.DropDownItems, keyData) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Lays out a strip's items, and the controls they host.</summary>
    private sealed class ToolStripLayout : LayoutEngine
    {
        public static ToolStripLayout Instance { get; } = new();

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            ((ToolStrip)container).LayoutItems();
            return ((ToolStrip)container).AutoSize;
        }

        internal override Size GetPreferredSize(Control container, Size proposedSize) => ((ToolStrip)container).MeasureItems();
    }
}

/// <summary>The menu bar of a form.</summary>
public class MenuStrip : ToolStrip
{
    private static readonly object EventMenuActivate = new();
    private static readonly object EventMenuDeactivate = new();

    /// <summary>Creates a menu bar.</summary>
    public MenuStrip()
    {
        GripStyle = ToolStripGripStyle.Hidden;
        CanOverflow = false;
        ShowItemToolTips = false;
    }

    /// <summary>Occurs when a menu of the bar opens.</summary>
    public event EventHandler? MenuActivate
    {
        add => Events.AddHandler(EventMenuActivate, value);
        remove => Events.RemoveHandler(EventMenuActivate, value);
    }

    /// <summary>Occurs when the bar's menus close.</summary>
    public event EventHandler? MenuDeactivate
    {
        add => Events.AddHandler(EventMenuDeactivate, value);
        remove => Events.RemoveHandler(EventMenuDeactivate, value);
    }

    /// <summary>The menu item that lists the MDI children. Kept for compatibility: TabStudio's forms have no MDI.</summary>
    public ToolStripMenuItem? MdiWindowListItem { get; set; }

    /// <inheritdoc />
    protected override Padding DefaultPadding => new(6, 2, 0, 2);

    /// <inheritdoc />
    protected override Size DefaultSize => new(200, 24);

    /// <inheritdoc />
    private protected override int MinimumThickness => 24;

    /// <summary>Raises MenuActivate.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMenuActivate(EventArgs e) => ((EventHandler?)Events[EventMenuActivate])?.Invoke(this, e);

    /// <summary>Raises MenuDeactivate.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMenuDeactivate(EventArgs e) => ((EventHandler?)Events[EventMenuDeactivate])?.Invoke(this, e);

    /// <inheritdoc />
    internal override void OpenDropDown(ToolStripDropDownItem item)
    {
        OnMenuActivate(EventArgs.Empty);
        base.OpenDropDown(item);
    }

    /// <summary>Called when the bar's open menu closes.</summary>
    internal void OnMenuClosed() => OnMenuDeactivate(EventArgs.Empty);

    /// <inheritdoc />
    private protected override void PaintBackground(Graphics g)
    {
        if (!HasLocalBackColor)
        {
            using var brush = new SolidBrush(Theme.Window);
            g.FillRectangle(brush, ClientRectangle);
        }
    }

    /// <inheritdoc />
    private protected override bool ProcessMnemonicKey(Keys keyData)
    {
        if ((keyData & Keys.Modifiers) != Keys.Alt)
        {
            return false;
        }

        var code = keyData & Keys.KeyCode;
        char letter;
        if (code is >= Keys.A and <= Keys.Z)
        {
            letter = (char)('A' + (code - Keys.A));
        }
        else if (code is >= Keys.D0 and <= Keys.D9)
        {
            letter = (char)('0' + (code - Keys.D0));
        }
        else
        {
            return false;
        }

        foreach (var item in Items.ToList())
        {
            if (item.Available && item.Enabled && ToolStripDropDown.Mnemonic(item.Text) == letter)
            {
                if (item is ToolStripDropDownItem { HasDropDownItems: true } dropDown)
                {
                    OpenDropDown(dropDown);
                }
                else
                {
                    ClickItem(item);
                }

                return true;
            }
        }

        return false;
    }
}

/// <summary>The status bar of a form.</summary>
public class StatusStrip : ToolStrip
{
    /// <summary>Creates a status bar.</summary>
    public StatusStrip()
    {
        GripStyle = ToolStripGripStyle.Hidden;
        CanOverflow = false;
        LayoutStyle = ToolStripLayoutStyle.Table;
    }

    /// <summary>Whether the bar shows a sizing grip in its corner.</summary>
    public bool SizingGrip { get; set; } = true;

    /// <summary>Where the sizing grip is.</summary>
    public Rectangle SizeGripBounds => SizingGrip ? new Rectangle(Width - 12, Height - 12, 12, 12) : Rectangle.Empty;

    /// <inheritdoc />
    protected override DockStyle DefaultDock => DockStyle.Bottom;

    /// <inheritdoc />
    protected override Padding DefaultPadding => new(1, 0, 14, 0);

    /// <inheritdoc />
    protected override Size DefaultSize => new(200, 22);

    /// <inheritdoc />
    private protected override int MinimumThickness => 22;

    /// <inheritdoc />
    private protected override void PaintBackground(Graphics g)
    {
        if (!HasLocalBackColor)
        {
            using var brush = new SolidBrush(Theme.StripBackground);
            g.FillRectangle(brush, ClientRectangle);
        }

        if (SizingGrip && FindForm() is { FormBorderStyle: FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow, WindowState: not FormWindowState.Maximized })
        {
            using var dots = new SolidBrush(Color.FromArgb(160, 160, 160));
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column <= row; column++)
                {
                    g.FillRectangle(dots, Width - 5 - (column * 4), Height - 5 - ((2 - row) * 4), 2, 2);
                }
            }
        }
    }
}
