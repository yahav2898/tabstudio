using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>A drop-down of items, shown above the form: a menu, a submenu or a context menu.</summary>
public class ToolStripDropDown : ToolStrip
{
    private static readonly object EventOpening = new();
    private static readonly object EventOpened = new();
    private static readonly object EventClosing = new();
    private static readonly object EventClosed = new();

    private MenuPopup? _popup;

    /// <summary>Creates a drop-down.</summary>
    public ToolStripDropDown()
    {
        GripStyle = ToolStripGripStyle.Hidden;
    }

    /// <summary>Occurs before the drop-down opens; set Cancel to keep it closed.</summary>
    public event CancelEventHandler? Opening
    {
        add => Events.AddHandler(EventOpening, value);
        remove => Events.RemoveHandler(EventOpening, value);
    }

    /// <summary>Occurs when the drop-down has opened.</summary>
    public event EventHandler? Opened
    {
        add => Events.AddHandler(EventOpened, value);
        remove => Events.RemoveHandler(EventOpened, value);
    }

    /// <summary>Occurs before the drop-down closes.</summary>
    public event ToolStripDropDownClosingEventHandler? Closing
    {
        add => Events.AddHandler(EventClosing, value);
        remove => Events.RemoveHandler(EventClosing, value);
    }

    /// <summary>Occurs when the drop-down has closed.</summary>
    public event ToolStripDropDownClosedEventHandler? Closed
    {
        add => Events.AddHandler(EventClosed, value);
        remove => Events.RemoveHandler(EventClosed, value);
    }

    /// <summary>The item that opens the drop-down, if any.</summary>
    public ToolStripItem? OwnerItem { get; set; }

    /// <summary>Whether the drop-down closes when the user taps elsewhere. It always does.</summary>
    public bool AutoClose { get; set; } = true;

    /// <summary>Whether the drop-down casts a shadow.</summary>
    public bool DropShadowEnabled { get; set; } = true;

    /// <summary>The opacity. Kept for compatibility: drop-downs are opaque.</summary>
    public double Opacity { get; set; } = 1;

    /// <summary>Whether the drop-down is a window of its own. Kept for compatibility.</summary>
    public bool TopLevel { get; set; } = true;

    /// <summary>Which way the drop-down opens by default.</summary>
    public ToolStripDropDownDirection DefaultDropDownDirection { get; set; } = ToolStripDropDownDirection.Right;

    /// <summary>Whether the drop-down is open.</summary>
    internal bool IsOpen => _popup is not null;

    /// <summary>The popup the drop-down is open in.</summary>
    internal MenuPopup? Popup => _popup;

    /// <inheritdoc />
    protected override DockStyle DefaultDock => DockStyle.None;

    /// <inheritdoc />
    protected override Padding DefaultPadding => new(1, 2, 1, 2);

    /// <summary>Opens the drop-down at the pointer.</summary>
    public void Show() => Show(Cursor.Position);

    /// <summary>Opens the drop-down at a point of the screen.</summary>
    /// <param name="screenLocation">The point.</param>
    public void Show(Point screenLocation)
    {
        if (FormsRuntime.Current?.ActiveWindow is not { } window)
        {
            return;
        }

        var origin = window.Form.ClientOriginOnScreen;
        OpenAt(window.Form, window, new Point(screenLocation.X - origin.X, screenLocation.Y - origin.Y));
    }

    /// <summary>Opens the drop-down at a point of the screen.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    public void Show(int x, int y) => Show(new Point(x, y));

    /// <summary>Opens the drop-down at a point of the screen, in a direction.</summary>
    /// <param name="position">The point.</param>
    /// <param name="direction">The direction; drop-downs open below and to the right, moving to fit the screen.</param>
    public void Show(Point position, ToolStripDropDownDirection direction) => Show(position);

    /// <summary>Opens the drop-down at a point of a control.</summary>
    /// <param name="control">The control.</param>
    /// <param name="position">The point, in the control's client coordinates.</param>
    public void Show(Control control, Point position)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control.Window is not { } window)
        {
            return;
        }

        var origin = control.ClientOriginInWindow();
        OpenAt(control, window, new Point(origin.X + position.X, origin.Y + position.Y));
    }

    /// <summary>Opens the drop-down at a point of a control.</summary>
    /// <param name="control">The control.</param>
    /// <param name="x">The x coordinate, in the control's client coordinates.</param>
    /// <param name="y">The y coordinate, in the control's client coordinates.</param>
    public void Show(Control control, int x, int y) => Show(control, new Point(x, y));

    /// <summary>Opens the drop-down at a point of a control, in a direction.</summary>
    /// <param name="control">The control.</param>
    /// <param name="position">The point, in the control's client coordinates.</param>
    /// <param name="direction">The direction; drop-downs open below and to the right, moving to fit the screen.</param>
    public void Show(Control control, Point position, ToolStripDropDownDirection direction) => Show(control, position);

    /// <summary>Closes the drop-down.</summary>
    public void Close() => Close(ToolStripDropDownCloseReason.CloseCalled);

    /// <summary>Closes the drop-down for a reason.</summary>
    /// <param name="reason">Why.</param>
    public void Close(ToolStripDropDownCloseReason reason)
    {
        if (_popup is not { } popup)
        {
            return;
        }

        var closing = new ToolStripDropDownClosingEventArgs(reason);
        OnClosing(closing);
        if (closing.Cancel)
        {
            return;
        }

        popup.CloseReason = reason;
        popup.Close();
    }

    /// <summary>Raises Opening.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnOpening(CancelEventArgs e) => ((CancelEventHandler?)Events[EventOpening])?.Invoke(this, e);

    /// <summary>Raises Opened.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnOpened(EventArgs e) => ((EventHandler?)Events[EventOpened])?.Invoke(this, e);

    /// <summary>Raises Closing.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnClosing(ToolStripDropDownClosingEventArgs e) => ((ToolStripDropDownClosingEventHandler?)Events[EventClosing])?.Invoke(this, e);

    /// <summary>Raises Closed.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnClosed(ToolStripDropDownClosedEventArgs e) => ((ToolStripDropDownClosedEventHandler?)Events[EventClosed])?.Invoke(this, e);

    /// <summary>The access key of a text: the character after its single ampersand, in upper case, or '\0'.</summary>
    internal static char Mnemonic(string text)
    {
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] == '&')
            {
                if (text[i + 1] != '&')
                {
                    return char.ToUpperInvariant(text[i + 1]);
                }

                i++;
            }
        }

        return '\0';
    }

    /// <summary>Opens the drop-down from an item, below it or beside it.</summary>
    /// <param name="ownerItem">The item.</param>
    /// <param name="owner">The control the item is on.</param>
    /// <param name="window">The window.</param>
    /// <param name="anchor">The item's bounds, in the window's client coordinates.</param>
    /// <param name="beside">Whether the drop-down opens beside the item rather than below it.</param>
    /// <param name="parent">The menu the item is on, for a submenu.</param>
    internal void OpenFrom(ToolStripDropDownItem ownerItem, Control owner, FormWindow window, Rectangle anchor, bool beside, MenuPopup? parent)
    {
        if (IsOpen)
        {
            return;
        }

        OwnerItem = ownerItem;
        ownerItem.OnDropDownShow(EventArgs.Empty);
        Open(owner, window, anchor, beside, parent);
        if (IsOpen)
        {
            ownerItem.OnDropDownOpened(EventArgs.Empty);
            ownerItem.Invalidate();
        }
    }

    /// <summary>Opens the drop-down with its top left at a point of a window.</summary>
    internal void OpenAt(Control owner, FormWindow window, Point location)
    {
        if (this is ContextMenuStrip menu)
        {
            menu.SourceControl = owner;
        }

        Close(ToolStripDropDownCloseReason.CloseCalled);
        window.CloseAllPopups();
        Open(owner, window, new Rectangle(location, Size.Empty), beside: false, parent: null);
    }

    /// <summary>Called when the popup the drop-down is open in closes.</summary>
    internal void OnPopupClosed(ToolStripDropDownCloseReason reason)
    {
        _popup = null;
        RepaintRequested = null;
        SetHot(null);
        if (reason != ToolStripDropDownCloseReason.CloseCalled)
        {
            OnClosing(new ToolStripDropDownClosingEventArgs(reason));
        }

        OnClosed(new ToolStripDropDownClosedEventArgs(reason));
        if (OwnerItem is ToolStripDropDownItem item)
        {
            item.OnDropDownClosed(EventArgs.Empty);
            if (item.Owner is { } strip and not ToolStripDropDown)
            {
                strip.SetHot(null);
                (strip as MenuStrip)?.OnMenuClosed();
            }
        }
    }

    /// <summary>Opens the submenu of an item on the drop-down.</summary>
    internal override void OpenDropDown(ToolStripDropDownItem item) => _popup?.OpenSubmenu(item, select: false);

    /// <summary>Raises the events of an item tapped on the drop-down, before the drop-down closes.</summary>
    internal void RaiseItemClicked(ToolStripItem item)
    {
        var e = new ToolStripItemClickedEventArgs(item);
        OnItemClicked(e);
        (OwnerItem as ToolStripDropDownItem)?.OnDropDownItemClicked(e);
    }

    /// <summary>Draws the drop-down, in its own coordinates.</summary>
    internal void PaintPopup(Graphics g)
    {
        using (var background = new SolidBrush(IsBackColorSet ? BackColor : Theme.MenuBackground))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        PaintContent(g);
    }

    /// <inheritdoc />
    private protected override void PaintBackground(Graphics g)
    {
        using var border = new Pen(Theme.MenuBorder);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    private void Open(Control owner, FormWindow window, Rectangle anchor, bool beside, MenuPopup? parent)
    {
        var opening = new CancelEventArgs();
        OnOpening(opening);
        if (opening.Cancel || !Items.ToList().Any(item => item.Available))
        {
            return;
        }

        var size = MeasureItems();
        SetBounds(0, 0, size.Width, size.Height);
        LayoutItems();

        var origin = window.Form.ClientOriginOnScreen;
        var desktopSize = window.Runtime.DesktopSize;
        var desktop = new Rectangle(-origin.X, -origin.Y, desktopSize.Width, desktopSize.Height);
        var x = beside ? anchor.Right - 2 : anchor.Left;
        var y = beside ? anchor.Top - Padding.Top - 1 : anchor.Bottom;
        if (beside && x + size.Width > desktop.Right)
        {
            x = anchor.Left - size.Width + 2;
        }

        if (y + size.Height > desktop.Bottom)
        {
            y = beside ? desktop.Bottom - size.Height : anchor.Top - size.Height;
        }

        var height = Math.Min(size.Height, desktop.Height);
        x = Math.Clamp(x, desktop.Left, Math.Max(desktop.Left, desktop.Right - size.Width));
        y = Math.Clamp(y, desktop.Top, Math.Max(desktop.Top, desktop.Bottom - height));

        var popup = new MenuPopup(this, owner, parent) { Bounds = new Rectangle(x, y, size.Width, height) };
        _popup = popup;
        RepaintRequested = popup.Invalidate;
        window.OpenPopup(popup);
        OnOpened(EventArgs.Empty);
    }
}

/// <summary>A drop-down menu: items with a margin for check marks and images, shortcut keys and submenu arrows.</summary>
public class ToolStripDropDownMenu : ToolStripDropDown
{
    /// <summary>The width of the margin for check marks and images.</summary>
    internal const int ImageMarginWidth = 28;

    /// <summary>Where item text starts.</summary>
    internal const int TextLeft = ImageMarginWidth + 6;

    /// <summary>The width kept for a submenu's arrow.</summary>
    internal const int ArrowWidth = 20;

    /// <summary>Creates a drop-down menu.</summary>
    public ToolStripDropDownMenu()
    {
    }

    /// <summary>Whether the margin for images is shown.</summary>
    public bool ShowImageMargin { get; set; } = true;

    /// <summary>Whether a separate margin for check marks is shown. Check marks share the image margin.</summary>
    public bool ShowCheckMargin { get; set; }

    /// <summary>The size a menu item needs on a drop-down.</summary>
    internal static Size MeasureItem(ToolStripMenuItem item)
    {
        var text = TextMetrics.Measure(item.DisplayText, item.Font.Spec);
        var shortcut = item.ShortcutText;
        var shortcutWidth = shortcut.Length == 0 ? 0 : (int)Math.Ceiling(TextMetrics.Measure(shortcut, item.Font.Spec).Width) + 24;
        var width = TextLeft + (int)Math.Ceiling(text.Width) + shortcutWidth + ArrowWidth + 4;
        return new Size(Math.Max(width, 120), Math.Max(22, (int)Math.Ceiling(text.Height) + 7));
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        if (ShowImageMargin || ShowCheckMargin)
        {
            using var margin = new SolidBrush(Theme.StripBackground);
            g.FillRectangle(margin, 1, 1, ImageMarginWidth, Height - 2);
        }

        PaintBackground(g);
        foreach (var item in Items.ToList())
        {
            if (!item.Available || item.Bounds.IsEmpty)
            {
                continue;
            }

            switch (item)
            {
                case ToolStripSeparator:
                {
                    using var pen = new Pen(Theme.Separator);
                    var y = item.Bounds.Top + (item.Bounds.Height / 2);
                    g.DrawLine(pen, item.Bounds.Left + TextLeft - 2, y, item.Bounds.Right - 1, y);
                    break;
                }

                case ToolStripMenuItem menuItem:
                    PaintMenuItem(g, menuItem);
                    break;

                default:
                    item.PaintHighlight(g, item.Bounds);
                    item.PaintContent(g, Rectangle.FromLTRB(item.Bounds.Left + TextLeft, item.Bounds.Top, item.Bounds.Right - 4, item.Bounds.Bottom));
                    break;
            }
        }
    }

    private static void PaintMenuItem(Graphics g, ToolStripMenuItem item)
    {
        var bounds = item.Bounds;
        var enabled = item.Enabled;
        if ((item.Selected || item.Pressed) && enabled)
        {
            using var fill = new SolidBrush(Theme.ItemHot);
            using var border = new Pen(Theme.ItemHotBorder);
            g.FillRectangle(fill, bounds.X + 1, bounds.Y, bounds.Width - 2, bounds.Height);
            g.DrawRectangle(border, bounds.X + 1, bounds.Y, bounds.Width - 3, bounds.Height - 1);
        }
        else if (item.Selected || item.Pressed)
        {
            using var border = new Pen(Theme.Separator);
            g.DrawRectangle(border, bounds.X + 1, bounds.Y, bounds.Width - 3, bounds.Height - 1);
        }

        var glyphArea = new Rectangle(bounds.X + 3, bounds.Y + ((bounds.Height - 20) / 2), 20, 20);
        if (item.Checked)
        {
            using var box = new SolidBrush(enabled ? Color.FromArgb(144, 200, 246) : Theme.ButtonDisabledFace);
            g.FillRectangle(box, glyphArea);
            if (item.Image is null)
            {
                using var check = new Pen(enabled ? Theme.Glyph : Theme.DisabledText, 1.6f);
                var cx = glyphArea.X + 10;
                var cy = glyphArea.Y + 10;
                if (item.CheckState == CheckState.Indeterminate)
                {
                    using var dot = new SolidBrush(enabled ? Theme.Glyph : Theme.DisabledText);
                    g.FillRectangle(dot, cx - 3, cy - 3, 6, 6);
                }
                else
                {
                    g.DrawLines(check, [new PointF(cx - 4.5f, cy), new PointF(cx - 1.5f, cy + 3), new PointF(cx + 4.5f, cy - 3.5f)]);
                }
            }
        }

        if (item.Image is { } image)
        {
            var size = item.ImageSize;
            g.DrawImage(image, new Rectangle(glyphArea.X + ((glyphArea.Width - size.Width) / 2), glyphArea.Y + ((glyphArea.Height - size.Height) / 2), size.Width, size.Height));
        }

        var color = enabled ? item.ForeColor : Theme.DisabledText;
        var textBounds = Rectangle.FromLTRB(bounds.Left + TextLeft, bounds.Top, bounds.Right - ArrowWidth, bounds.Bottom);
        ThemePainter.DrawText(g, item.Text, item.Font, color, textBounds, ContentAlignment.MiddleLeft);
        if (item.ShortcutText is { Length: > 0 } shortcut)
        {
            ThemePainter.DrawText(g, shortcut, item.Font, color, textBounds, ContentAlignment.MiddleRight, mnemonics: false);
        }

        if (item.HasDropDownItems)
        {
            var arrow = new Rectangle(bounds.Right - ArrowWidth + 6, bounds.Top + ((bounds.Height - 8) / 2), 5, 8);
            ThemePainter.DrawArrow(g, arrow, ArrowDirection.Right, enabled ? Theme.Glyph : Theme.DisabledText);
        }
    }
}

/// <summary>A context menu: the menu a long press or a right click opens on a control.</summary>
public class ContextMenuStrip : ToolStripDropDownMenu
{
    /// <summary>Creates a context menu.</summary>
    public ContextMenuStrip()
    {
    }

    /// <summary>Creates a context menu that is disposed with a container.</summary>
    /// <param name="container">The container.</param>
    public ContextMenuStrip(IContainer container)
        : this()
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    /// <summary>The control the menu was opened on.</summary>
    public Control? SourceControl { get; internal set; }

    /// <summary>Opens the menu for a control at a point of the window, for a long press or a right click.</summary>
    internal void ShowAt(Control owner, FormWindow window, Point point) => OpenAt(owner, window, point);
}
