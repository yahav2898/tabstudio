using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>An item that opens a drop-down of more items: a menu item or a drop-down button.</summary>
public abstract class ToolStripDropDownItem : ToolStripItem
{
    private static readonly object EventDropDownOpening = new();
    private static readonly object EventDropDownOpened = new();
    private static readonly object EventDropDownClosed = new();
    private static readonly object EventDropDownItemClicked = new();

    private ToolStripDropDown? _dropDown;

    /// <summary>Creates an item.</summary>
    protected ToolStripDropDownItem()
    {
    }

    /// <summary>Creates an item with text, an image and a click handler.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    protected ToolStripDropDownItem(string? text, Image? image, EventHandler? onClick)
        : base(text, image, onClick)
    {
    }

    /// <summary>Creates an item with text, an image, a click handler and a name.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    /// <param name="name">The name.</param>
    protected ToolStripDropDownItem(string? text, Image? image, EventHandler? onClick, string? name)
        : base(text, image, onClick, name)
    {
    }

    /// <summary>Creates an item with text, an image and the items of its drop-down.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="dropDownItems">The drop-down's items.</param>
    protected ToolStripDropDownItem(string? text, Image? image, params ToolStripItem[] dropDownItems)
        : base(text, image, null)
    {
        DropDownItems.AddRange(dropDownItems);
    }

    /// <summary>Occurs before the drop-down opens; handlers often fill it here.</summary>
    public event EventHandler? DropDownOpening
    {
        add => Events.AddHandler(EventDropDownOpening, value);
        remove => Events.RemoveHandler(EventDropDownOpening, value);
    }

    /// <summary>Occurs when the drop-down has opened.</summary>
    public event EventHandler? DropDownOpened
    {
        add => Events.AddHandler(EventDropDownOpened, value);
        remove => Events.RemoveHandler(EventDropDownOpened, value);
    }

    /// <summary>Occurs when the drop-down has closed.</summary>
    public event EventHandler? DropDownClosed
    {
        add => Events.AddHandler(EventDropDownClosed, value);
        remove => Events.RemoveHandler(EventDropDownClosed, value);
    }

    /// <summary>Occurs when an item of the drop-down is clicked.</summary>
    public event ToolStripItemClickedEventHandler? DropDownItemClicked
    {
        add => Events.AddHandler(EventDropDownItemClicked, value);
        remove => Events.RemoveHandler(EventDropDownItemClicked, value);
    }

    /// <summary>The drop-down.</summary>
    public ToolStripDropDown DropDown
    {
        get => _dropDown ??= CreateDefaultDropDown();
        set
        {
            _dropDown = value;
            if (value is not null)
            {
                value.OwnerItem = this;
            }
        }
    }

    /// <summary>The items of the drop-down.</summary>
    public ToolStripItemCollection DropDownItems => DropDown.Items;

    /// <summary>Whether the drop-down has items.</summary>
    public virtual bool HasDropDownItems => _dropDown is { Items.Count: > 0 };

    /// <summary>Whether the drop-down has items; the same as <see cref="HasDropDownItems"/>.</summary>
    public bool HasDropDown => HasDropDownItems;

    /// <summary>Whether the drop-down is open.</summary>
    public override bool Pressed => _dropDown is { IsOpen: true } || base.Pressed;

    /// <summary>Which way the drop-down opens.</summary>
    public ToolStripDropDownDirection DropDownDirection { get; set; } = ToolStripDropDownDirection.Default;

    /// <summary>Whether the item is on a drop-down rather than a bar.</summary>
    public bool IsOnDropDown => Owner is ToolStripDropDown;

    /// <summary>Opens the drop-down.</summary>
    public void ShowDropDown()
    {
        if (Owner is { } owner)
        {
            owner.OpenDropDown(this);
        }
    }

    /// <summary>Closes the drop-down.</summary>
    public void HideDropDown() => _dropDown?.Close(ToolStripDropDownCloseReason.CloseCalled);

    /// <summary>Creates the drop-down the item opens.</summary>
    protected virtual ToolStripDropDown CreateDefaultDropDown() => new ToolStripDropDownMenu { OwnerItem = this };

    /// <summary>Raises DropDownOpening.</summary>
    /// <param name="e">The event data.</param>
    protected internal virtual void OnDropDownShow(EventArgs e) => ((EventHandler?)Events[EventDropDownOpening])?.Invoke(this, e);

    /// <summary>Raises DropDownOpened.</summary>
    /// <param name="e">The event data.</param>
    protected internal virtual void OnDropDownOpened(EventArgs e) => ((EventHandler?)Events[EventDropDownOpened])?.Invoke(this, e);

    /// <summary>Raises DropDownClosed.</summary>
    /// <param name="e">The event data.</param>
    protected internal virtual void OnDropDownClosed(EventArgs e)
    {
        Invalidate();
        ((EventHandler?)Events[EventDropDownClosed])?.Invoke(this, e);
    }

    /// <summary>Raises DropDownItemClicked.</summary>
    /// <param name="e">The event data.</param>
    protected internal virtual void OnDropDownItemClicked(ToolStripItemClickedEventArgs e) =>
        ((ToolStripItemClickedEventHandler?)Events[EventDropDownItemClicked])?.Invoke(this, e);

    /// <summary>Whether a tap on the item opens its drop-down rather than clicking it.</summary>
    internal virtual bool OpensOnPress(Point pointInItem) => true;

    /// <summary>Clicks the item without opening its drop-down, for keyboard activation.</summary>
    internal void ClickInternal() => OnClick(EventArgs.Empty);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dropDown?.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>An item of a menu, which can be checked, have a shortcut key and open a submenu.</summary>
public class ToolStripMenuItem : ToolStripDropDownItem
{
    private static readonly object EventCheckedChanged = new();
    private static readonly object EventCheckStateChanged = new();
    private CheckState _checkState;
    private Keys _shortcutKeys;

    /// <summary>Creates a menu item.</summary>
    public ToolStripMenuItem()
    {
    }

    /// <summary>Creates a menu item with text.</summary>
    /// <param name="text">The text.</param>
    public ToolStripMenuItem(string? text)
        : base(text, null, (EventHandler?)null)
    {
    }

    /// <summary>Creates a menu item with an image.</summary>
    /// <param name="image">The image.</param>
    public ToolStripMenuItem(Image? image)
        : base(null, image, (EventHandler?)null)
    {
    }

    /// <summary>Creates a menu item with text and an image.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    public ToolStripMenuItem(string? text, Image? image)
        : base(text, image, (EventHandler?)null)
    {
    }

    /// <summary>Creates a menu item with text, an image and a click handler.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    public ToolStripMenuItem(string? text, Image? image, EventHandler? onClick)
        : base(text, image, onClick)
    {
    }

    /// <summary>Creates a menu item with text, an image, a click handler and a name.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    /// <param name="name">The name.</param>
    public ToolStripMenuItem(string? text, Image? image, EventHandler? onClick, string? name)
        : base(text, image, onClick, name)
    {
    }

    /// <summary>Creates a menu item with text, an image, a click handler and a shortcut key.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    /// <param name="shortcutKeys">The shortcut key.</param>
    public ToolStripMenuItem(string? text, Image? image, EventHandler? onClick, Keys shortcutKeys)
        : base(text, image, onClick)
    {
        _shortcutKeys = shortcutKeys;
    }

    /// <summary>Creates a menu item with a submenu.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="dropDownItems">The submenu's items.</param>
    public ToolStripMenuItem(string? text, Image? image, params ToolStripItem[] dropDownItems)
        : base(text, image, dropDownItems)
    {
    }

    /// <summary>Occurs when <see cref="Checked"/> changes.</summary>
    public event EventHandler? CheckedChanged
    {
        add => Events.AddHandler(EventCheckedChanged, value);
        remove => Events.RemoveHandler(EventCheckedChanged, value);
    }

    /// <summary>Occurs when <see cref="CheckState"/> changes.</summary>
    public event EventHandler? CheckStateChanged
    {
        add => Events.AddHandler(EventCheckStateChanged, value);
        remove => Events.RemoveHandler(EventCheckStateChanged, value);
    }

    /// <summary>Whether the item shows a check mark.</summary>
    public bool Checked
    {
        get => _checkState != CheckState.Unchecked;
        set => CheckState = value ? CheckState.Checked : CheckState.Unchecked;
    }

    /// <summary>The check state.</summary>
    public CheckState CheckState
    {
        get => _checkState;
        set
        {
            if (_checkState == value)
            {
                return;
            }

            var wasChecked = Checked;
            _checkState = value;
            Invalidate();
            if (wasChecked != Checked)
            {
                OnCheckedChanged(EventArgs.Empty);
            }

            OnCheckStateChanged(EventArgs.Empty);
        }
    }

    /// <summary>Whether a click toggles <see cref="Checked"/>.</summary>
    public bool CheckOnClick { get; set; }

    /// <summary>The shortcut key that clicks the item, such as Ctrl+S.</summary>
    public Keys ShortcutKeys
    {
        get => _shortcutKeys;
        set
        {
            _shortcutKeys = value;
            Relayout();
        }
    }

    /// <summary>Whether the shortcut key is shown next to the text.</summary>
    public bool ShowShortcutKeys { get; set; } = true;

    /// <summary>The text shown for the shortcut key instead of its name.</summary>
    public string? ShortcutKeyDisplayString { get; set; }

    /// <summary>The text shown for the shortcut, or an empty string.</summary>
    internal string ShortcutText =>
        !ShowShortcutKeys ? string.Empty : ShortcutKeyDisplayString ?? (_shortcutKeys == Keys.None ? string.Empty : KeysConverter.ToText(_shortcutKeys));

    /// <inheritdoc />
    protected override Padding DefaultPadding => new(4, 0, 4, 0);

    /// <inheritdoc />
    protected override Padding DefaultMargin => Padding.Empty;

    /// <inheritdoc />
    protected override Size DefaultSize => new(32, 19);

    /// <summary>Raises CheckedChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCheckedChanged(EventArgs e) => ((EventHandler?)Events[EventCheckedChanged])?.Invoke(this, e);

    /// <summary>Raises CheckStateChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCheckStateChanged(EventArgs e) => ((EventHandler?)Events[EventCheckStateChanged])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void OnClick(EventArgs e)
    {
        if (CheckOnClick)
        {
            Checked = !Checked;
        }

        base.OnClick(e);
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size constrainingSize)
    {
        if (!IsOnDropDown)
        {
            var text = TextMetrics.Measure(DisplayText, Font.Spec);
            var image = ShowsImage ? ImageSize.Width + 4 : 0;
            return new Size((int)Math.Ceiling(text.Width) + image + Padding.Horizontal + 4, Math.Max(20, (int)Math.Ceiling(text.Height) + 5));
        }

        return ToolStripDropDownMenu.MeasureItem(this);
    }

    /// <inheritdoc />
    internal override void PaintOnStrip(Graphics g, Rectangle bounds)
    {
        if (Pressed)
        {
            // An open menu's title on the menu bar looks joined to its drop-down.
            using var fill = new SolidBrush(Theme.MenuBackground);
            using var border = new Pen(Theme.MenuBorder);
            g.FillRectangle(fill, bounds);
            g.DrawLine(border, bounds.Left, bounds.Bottom, bounds.Left, bounds.Top);
            g.DrawLine(border, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
            g.DrawLine(border, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom);
            PaintContent(g, Rectangle.FromLTRB(bounds.Left + Padding.Left, bounds.Top, bounds.Right - Padding.Right, bounds.Bottom));
            return;
        }

        if (Selected && Enabled)
        {
            using var fill = new SolidBrush(Theme.ItemHot);
            using var border = new Pen(Theme.ItemHotBorder);
            g.FillRectangle(fill, bounds);
            g.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }

        PaintContent(g, Rectangle.FromLTRB(bounds.Left + Padding.Left, bounds.Top, bounds.Right - Padding.Right, bounds.Bottom));
    }
}

/// <summary>A tool bar button that opens a drop-down.</summary>
public class ToolStripDropDownButton : ToolStripDropDownItem
{
    /// <summary>Creates a drop-down button.</summary>
    public ToolStripDropDownButton()
    {
        AutoToolTip = true;
    }

    /// <summary>Creates a drop-down button with text.</summary>
    /// <param name="text">The text.</param>
    public ToolStripDropDownButton(string? text)
        : base(text, null, (EventHandler?)null)
    {
        AutoToolTip = true;
    }

    /// <summary>Creates a drop-down button with an image.</summary>
    /// <param name="image">The image.</param>
    public ToolStripDropDownButton(Image? image)
        : base(null, image, (EventHandler?)null)
    {
        AutoToolTip = true;
    }

    /// <summary>Creates a drop-down button with text and an image.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    public ToolStripDropDownButton(string? text, Image? image)
        : base(text, image, (EventHandler?)null)
    {
        AutoToolTip = true;
    }

    /// <summary>Creates a drop-down button with text, an image and a click handler.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    public ToolStripDropDownButton(string? text, Image? image, EventHandler? onClick)
        : base(text, image, onClick)
    {
        AutoToolTip = true;
    }

    /// <summary>Creates a drop-down button with text, an image and the items of its drop-down.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="dropDownItems">The drop-down's items.</param>
    public ToolStripDropDownButton(string? text, Image? image, params ToolStripItem[] dropDownItems)
        : base(text, image, dropDownItems)
    {
        AutoToolTip = true;
    }

    /// <summary>Whether the button shows a down arrow.</summary>
    public bool ShowDropDownArrow { get; set; } = true;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size constrainingSize)
    {
        var size = base.GetPreferredSize(constrainingSize);
        return ShowDropDownArrow ? new Size(size.Width + 9, size.Height) : size;
    }

    /// <inheritdoc />
    internal override void PaintOnStrip(Graphics g, Rectangle bounds)
    {
        if (!ShowDropDownArrow)
        {
            base.PaintOnStrip(g, bounds);
            return;
        }

        PaintHighlight(g, bounds);
        PaintContent(g, Rectangle.FromLTRB(bounds.Left + 4 + Padding.Left, bounds.Top + Padding.Top, bounds.Right - 12 - Padding.Right, bounds.Bottom - Padding.Bottom));
        var arrow = new Rectangle(bounds.Right - 12, bounds.Top + ((bounds.Height - 6) / 2), 9, 6);
        ThemePainter.DrawArrow(g, arrow, ArrowDirection.Down, Enabled ? Theme.Glyph : Theme.DisabledText);
    }
}

/// <summary>A tool bar button with an arrow beside it: the button clicks, the arrow opens a drop-down.</summary>
public class ToolStripSplitButton : ToolStripDropDownItem
{
    private static readonly object EventButtonClick = new();
    private static readonly object EventButtonDoubleClick = new();

    /// <summary>Creates a split button.</summary>
    public ToolStripSplitButton()
    {
        AutoToolTip = true;
    }

    /// <summary>Creates a split button with text.</summary>
    /// <param name="text">The text.</param>
    public ToolStripSplitButton(string? text)
        : base(text, null, (EventHandler?)null)
    {
        AutoToolTip = true;
    }

    /// <summary>Creates a split button with an image.</summary>
    /// <param name="image">The image.</param>
    public ToolStripSplitButton(Image? image)
        : base(null, image, (EventHandler?)null)
    {
        AutoToolTip = true;
    }

    /// <summary>Creates a split button with text and an image.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    public ToolStripSplitButton(string? text, Image? image)
        : base(text, image, (EventHandler?)null)
    {
        AutoToolTip = true;
    }

    /// <summary>Creates a split button with text, an image and a click handler.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    public ToolStripSplitButton(string? text, Image? image, EventHandler? onClick)
        : base(text, image, onClick)
    {
        AutoToolTip = true;
    }

    /// <summary>Creates a split button with text, an image and the items of its drop-down.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="dropDownItems">The drop-down's items.</param>
    public ToolStripSplitButton(string? text, Image? image, params ToolStripItem[] dropDownItems)
        : base(text, image, dropDownItems)
    {
        AutoToolTip = true;
    }

    /// <summary>Occurs when the button part is clicked.</summary>
    public event EventHandler? ButtonClick
    {
        add => Events.AddHandler(EventButtonClick, value);
        remove => Events.RemoveHandler(EventButtonClick, value);
    }

    /// <summary>Occurs when the button part is double-clicked.</summary>
    public event EventHandler? ButtonDoubleClick
    {
        add => Events.AddHandler(EventButtonDoubleClick, value);
        remove => Events.RemoveHandler(EventButtonDoubleClick, value);
    }

    /// <summary>The width of the arrow part.</summary>
    public int DropDownButtonWidth { get; set; } = 11;

    /// <summary>The item of the drop-down the button part clicks, if any.</summary>
    public ToolStripItem? DefaultItem { get; set; }

    /// <summary>Whether the button part is pressed.</summary>
    public bool ButtonPressed { get; private set; }

    /// <summary>Clicks the button part.</summary>
    public void PerformButtonClick()
    {
        if (Enabled && Available)
        {
            OnButtonClick(EventArgs.Empty);
        }
    }

    /// <summary>Raises ButtonClick, and clicks the default item if there is one.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnButtonClick(EventArgs e)
    {
        DefaultItem?.PerformClick();
        ((EventHandler?)Events[EventButtonClick])?.Invoke(this, e);
    }

    /// <summary>Raises ButtonDoubleClick.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnButtonDoubleClick(EventArgs e) => ((EventHandler?)Events[EventButtonDoubleClick])?.Invoke(this, e);

    /// <inheritdoc />
    public override Size GetPreferredSize(Size constrainingSize)
    {
        var size = base.GetPreferredSize(constrainingSize);
        return new Size(size.Width + DropDownButtonWidth, size.Height);
    }

    /// <inheritdoc />
    internal override bool OpensOnPress(Point pointInItem) => pointInItem.X >= Bounds.Width - DropDownButtonWidth;

    /// <summary>A tap on the button part.</summary>
    internal void TapButton()
    {
        ButtonPressed = false;
        OnButtonClick(EventArgs.Empty);
        OnClick(EventArgs.Empty);
    }

    /// <inheritdoc />
    internal override void PaintOnStrip(Graphics g, Rectangle bounds)
    {
        var button = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right - DropDownButtonWidth, bounds.Bottom);
        var arrowPart = Rectangle.FromLTRB(button.Right, bounds.Top, bounds.Right, bounds.Bottom);
        PaintHighlight(g, bounds);
        if (Selected || Pressed)
        {
            using var border = new Pen(Theme.ItemHotBorder);
            g.DrawLine(border, arrowPart.Left, bounds.Top, arrowPart.Left, bounds.Bottom - 1);
        }

        PaintContent(g, Rectangle.FromLTRB(button.Left + 4 + Padding.Left, button.Top, button.Right - 4, button.Bottom));
        var arrow = new Rectangle(arrowPart.Left + ((arrowPart.Width - 7) / 2), bounds.Top + ((bounds.Height - 4) / 2), 7, 4);
        ThemePainter.DrawArrow(g, arrow, ArrowDirection.Down, Enabled ? Theme.Glyph : Theme.DisabledText);
    }
}
