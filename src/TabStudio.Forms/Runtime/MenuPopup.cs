using System.Drawing;
using System.Windows.Forms;

namespace TabStudio.Forms.Runtime;

/// <summary>
/// An open drop-down menu: a menu of the menu bar, a submenu, a drop-down button's menu or a context menu. Moving
/// over an item highlights it and opens its submenu; a tap clicks it; the arrow keys, Enter, Escape and access keys
/// work as in Windows.
/// </summary>
internal sealed class MenuPopup : Popup
{
    /// <summary>How long the pointer rests on an item before its submenu opens, in milliseconds.</summary>
    private const int SubmenuDelay = 300;

    private readonly ToolStripDropDown _dropDown;
    private readonly Control _owner;
    private MenuPopup? _child;
    private IDisposable? _submenuTimer;
    private ToolStripItem? _pressedItem;
    private int _scroll;
    private Point _dragStart;

    /// <summary>Creates the popup of a drop-down.</summary>
    /// <param name="dropDown">The drop-down.</param>
    /// <param name="owner">The control the drop-down was opened from.</param>
    /// <param name="parent">The menu this one is a submenu of, if any.</param>
    public MenuPopup(ToolStripDropDown dropDown, Control owner, MenuPopup? parent)
    {
        _dropDown = dropDown;
        _owner = owner;
        Parent = parent;
        if (parent is not null)
        {
            parent._child = this;
        }
    }

    /// <inheritdoc />
    public override Control Owner => _owner;

    /// <summary>The drop-down shown.</summary>
    public ToolStripDropDown DropDown => _dropDown;

    /// <summary>The menu this one is a submenu of, if any.</summary>
    public MenuPopup? Parent { get; }

    /// <summary>Why the popup closes, as its drop-down's Closed event reports.</summary>
    public ToolStripDropDownCloseReason CloseReason { get; set; } = ToolStripDropDownCloseReason.AppClicked;

    /// <summary>The first menu of the chain.</summary>
    private MenuPopup Root => Parent?.Root ?? this;

    /// <summary>The bar the chain opened from, when it is a menu bar or a tool bar.</summary>
    private ToolStrip? Bar => Root._dropDown.OwnerItem?.Owner is { } strip and not ToolStripDropDown ? strip : null;

    /// <summary>How far the content can scroll, when the menu is taller than the screen.</summary>
    private int MaxScroll => Math.Max(0, _dropDown.Height - Bounds.Height);

    /// <inheritdoc />
    public override void Paint(Graphics g)
    {
        if (_scroll != 0)
        {
            g.TranslateTransform(0, -_scroll);
        }

        _dropDown.PaintPopup(g);
    }

    /// <inheritdoc />
    public override void PointerDown(Point point)
    {
        _dragStart = point;
        var item = ItemAt(point);
        _pressedItem = item;
        if (item is ToolStripDropDownItem { Enabled: true } opener && HasSubmenu(opener))
        {
            OpenSubmenu(opener, select: false);
        }
    }

    /// <inheritdoc />
    public override void PointerMove(Point point, bool pressed)
    {
        if (pressed && MaxScroll > 0 && Math.Abs(point.Y - _dragStart.Y) > 8)
        {
            // A finger dragging a menu taller than the screen scrolls it.
            ScrollBy(_dragStart.Y - point.Y);
            _dragStart = point;
            _pressedItem = null;
            return;
        }

        var item = ItemAt(point);
        Hover(item?.CanSelect == true ? item : null, delayed: !pressed);
    }

    /// <inheritdoc />
    public override void PointerUp(Point point)
    {
        var item = ItemAt(point);
        var pressed = _pressedItem;
        _pressedItem = null;
        if (item is null || pressed is null || item != pressed)
        {
            return;
        }

        Activate(item);
    }

    /// <inheritdoc />
    public override void Wheel(int delta) => ScrollBy(-delta / 120 * 3 * 22);

    /// <inheritdoc />
    public override void PointerMoveOutside(Point windowPoint)
    {
        if (BarItemAt(windowPoint) is ToolStripDropDownItem { Enabled: true } item && item != Root._dropDown.OwnerItem && Bar is MenuStrip bar)
        {
            // Moving along the menu bar with a menu open opens the menu under the pointer.
            Root.CloseReason = ToolStripDropDownCloseReason.CloseCalled;
            Root.Close();
            bar.OpenDropDown(item);
        }
    }

    /// <inheritdoc />
    public override bool PointerDownOutside(Point windowPoint)
    {
        if (BarItemAt(windowPoint) is not ToolStripDropDownItem item)
        {
            return false;
        }

        var bar = Bar!;
        var same = item == Root._dropDown.OwnerItem;
        Root.CloseReason = ToolStripDropDownCloseReason.AppClicked;
        Root.Close();
        if (!same && item.Enabled && bar is MenuStrip)
        {
            bar.OpenDropDown(item);
        }

        return true;
    }

    /// <inheritdoc />
    public override bool KeyDown(Keys keyData)
    {
        var code = keyData & Keys.KeyCode;
        var hot = _dropDown.HotItem;
        switch (code)
        {
            case Keys.Down:
                MoveSelection(+1);
                return true;
            case Keys.Up:
                MoveSelection(-1);
                return true;
            case Keys.Home:
                Hover(Selectable().FirstOrDefault(), delayed: false, open: false);
                return true;
            case Keys.End:
                Hover(Selectable().LastOrDefault(), delayed: false, open: false);
                return true;
            case Keys.Right:
                if (hot is ToolStripDropDownItem opener && HasSubmenu(opener))
                {
                    OpenSubmenu(opener, select: true);
                }
                else
                {
                    MoveAlongBar(+1);
                }

                return true;
            case Keys.Left:
                if (Parent is not null)
                {
                    CloseReason = ToolStripDropDownCloseReason.Keyboard;
                    Close();
                }
                else
                {
                    MoveAlongBar(-1);
                }

                return true;
            case Keys.Enter or Keys.Space:
                if (hot is not null)
                {
                    Activate(hot, fromKeyboard: true);
                }

                return true;
            case Keys.Escape:
                CloseReason = ToolStripDropDownCloseReason.Keyboard;
                Close();
                return true;
            case Keys.Tab:
                return true;
        }

        if ((keyData & (Keys.Control | Keys.Alt)) != 0)
        {
            return false;
        }

        var letter = code is >= Keys.A and <= Keys.Z ? (char)('A' + (code - Keys.A)) : code is >= Keys.D0 and <= Keys.D9 ? (char)('0' + (code - Keys.D0)) : '\0';
        if (letter != '\0' && Selectable().FirstOrDefault(item => ToolStripDropDown.Mnemonic(item.Text) == letter) is { } match)
        {
            Activate(match, fromKeyboard: true);
            return true;
        }

        // An open menu takes every other key too, except shortcuts, as in Windows.
        return true;
    }

    /// <inheritdoc />
    public override void OnClosed()
    {
        _submenuTimer?.Dispose();
        _submenuTimer = null;
        if (_child is { } child)
        {
            child.CloseReason = CloseReason;
            child.Close();
            _child = null;
        }

        if (Parent is { } parent && parent._child == this)
        {
            parent._child = null;
        }

        _dropDown.OnPopupClosed(CloseReason);
    }

    /// <summary>Opens an item's submenu beside it.</summary>
    /// <param name="item">The item.</param>
    /// <param name="select">Whether the submenu's first item is selected, as the keyboard does.</param>
    public void OpenSubmenu(ToolStripDropDownItem item, bool select)
    {
        _submenuTimer?.Dispose();
        _submenuTimer = null;
        if (Window is not { } window)
        {
            return;
        }

        if (_child is { } child)
        {
            if (child._dropDown == item.DropDown)
            {
                if (select)
                {
                    child.MoveSelection(+1);
                }

                return;
            }

            child.CloseReason = ToolStripDropDownCloseReason.CloseCalled;
            child.Close();
        }

        _dropDown.SetHot(item);
        var anchor = new Rectangle(Bounds.X + item.Bounds.X, Bounds.Y + item.Bounds.Y - _scroll, item.Bounds.Width, item.Bounds.Height);
        item.DropDown.OpenFrom(item, _owner, window, anchor, beside: true, parent: this);
        if (select && _child is { } opened)
        {
            opened.MoveSelection(+1);
        }
    }

    private static bool HasSubmenu(ToolStripDropDownItem item) => item.HasDropDownItems;

    private ToolStripItem? ItemAt(Point point) => _dropDown.GetItemAt(new Point(point.X, point.Y + _scroll));

    private ToolStripItem? BarItemAt(Point windowPoint)
    {
        if (Bar is not { } bar || bar.Window != Window)
        {
            return null;
        }

        var origin = bar.ClientOriginInWindow();
        return bar.GetItemAt(new Point(windowPoint.X - origin.X, windowPoint.Y - origin.Y));
    }

    private IEnumerable<ToolStripItem> Selectable() => _dropDown.Items.ToList().Where(item => item.Available && item.CanSelect && !item.Bounds.IsEmpty);

    private void Hover(ToolStripItem? item, bool delayed, bool open = true)
    {
        if (item == _dropDown.HotItem)
        {
            return;
        }

        _dropDown.SetHot(item);
        _submenuTimer?.Dispose();
        _submenuTimer = null;
        if (!open)
        {
            return;
        }

        if (item is ToolStripDropDownItem opener && HasSubmenu(opener))
        {
            if (delayed && Window is { } window)
            {
                _submenuTimer = window.Runtime.RunAfter(SubmenuDelay, () => OpenSubmenu(opener, select: false));
            }
            else
            {
                OpenSubmenu(opener, select: false);
            }
        }
        else if (_child is { } child && item is not null && delayed && Window is { } window)
        {
            _submenuTimer = window.Runtime.RunAfter(SubmenuDelay, () =>
            {
                child.CloseReason = ToolStripDropDownCloseReason.CloseCalled;
                child.Close();
            });
        }
    }

    private void MoveSelection(int step)
    {
        var items = Selectable().ToList();
        if (items.Count == 0)
        {
            return;
        }

        var index = _dropDown.HotItem is { } hot ? items.IndexOf(hot) : -1;
        index = index < 0 ? (step > 0 ? 0 : items.Count - 1) : (index + step + items.Count) % items.Count;
        Hover(items[index], delayed: false, open: false);
        EnsureVisible(items[index]);
    }

    private void MoveAlongBar(int step)
    {
        if (Bar is not MenuStrip bar || Root._dropDown.OwnerItem is not { } current)
        {
            return;
        }

        var items = bar.Items.ToList().Where(item => item.Available && item.Enabled && item is ToolStripDropDownItem).ToList();
        var index = items.IndexOf(current);
        if (index < 0 || items.Count < 2)
        {
            return;
        }

        var next = (ToolStripDropDownItem)items[(index + step + items.Count) % items.Count];
        Root.CloseReason = ToolStripDropDownCloseReason.Keyboard;
        Root.Close();
        bar.OpenDropDown(next);
        if (next.DropDown.Popup is { } popup)
        {
            popup.MoveSelection(+1);
        }
    }

    private void Activate(ToolStripItem item, bool fromKeyboard = false)
    {
        if (!item.Enabled || !item.CanSelect)
        {
            return;
        }

        if (item is ToolStripDropDownItem opener && HasSubmenu(opener))
        {
            OpenSubmenu(opener, select: fromKeyboard);
            return;
        }

        _dropDown.RaiseItemClicked(item);
        Root.CloseReason = ToolStripDropDownCloseReason.ItemClicked;
        Root.Close();
        item.PerformClick();
    }

    private void EnsureVisible(ToolStripItem item)
    {
        if (item.Bounds.Top < _scroll)
        {
            ScrollTo(item.Bounds.Top - _dropDown.Padding.Top);
        }
        else if (item.Bounds.Bottom > _scroll + Bounds.Height)
        {
            ScrollTo(item.Bounds.Bottom + _dropDown.Padding.Bottom - Bounds.Height);
        }
    }

    private void ScrollBy(int delta) => ScrollTo(_scroll + delta);

    private void ScrollTo(int scroll)
    {
        var clamped = Math.Clamp(scroll, 0, MaxScroll);
        if (clamped != _scroll)
        {
            _scroll = clamped;
            Invalidate();
        }
    }
}
