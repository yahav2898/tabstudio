using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>An item of a menu, tool bar or status bar: text, an image, or both.</summary>
[DefaultEvent(nameof(Click))]
[DefaultProperty(nameof(Text))]
public abstract class ToolStripItem : Component
{
    private static readonly object EventClick = new();
    private static readonly object EventTextChanged = new();
    private static readonly object EventEnabledChanged = new();
    private static readonly object EventVisibleChanged = new();
    private static readonly object EventMouseDown = new();
    private static readonly object EventMouseUp = new();
    private static readonly object EventMouseEnter = new();
    private static readonly object EventMouseLeave = new();

    private string? _text;
    private Image? _image;
    private bool _enabled = true;
    private bool _available = true;
    private Font? _font;
    private Color _foreColor = Color.Empty;
    private Color _backColor = Color.Empty;
    private Size _size;
    private bool _autoSize = true;

    /// <summary>Creates an item.</summary>
    protected ToolStripItem()
    {
        _size = DefaultSize;
        Padding = DefaultPadding;
        Margin = DefaultMargin;
    }

    /// <summary>Creates an item with text, an image and a click handler.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    protected ToolStripItem(string? text, Image? image, EventHandler? onClick)
        : this(text, image, onClick, null)
    {
    }

    /// <summary>Creates an item with text, an image, a click handler and a name.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    /// <param name="name">The name.</param>
    protected ToolStripItem(string? text, Image? image, EventHandler? onClick, string? name)
        : this()
    {
        _text = text;
        _image = image;
        Name = name ?? string.Empty;
        if (onClick is not null)
        {
            Click += onClick;
        }
    }

    /// <summary>Occurs when the item is clicked or tapped.</summary>
    public event EventHandler? Click
    {
        add => Events.AddHandler(EventClick, value);
        remove => Events.RemoveHandler(EventClick, value);
    }

    /// <summary>Occurs when the text changes.</summary>
    public event EventHandler? TextChanged
    {
        add => Events.AddHandler(EventTextChanged, value);
        remove => Events.RemoveHandler(EventTextChanged, value);
    }

    /// <summary>Occurs when the item is enabled or disabled.</summary>
    public event EventHandler? EnabledChanged
    {
        add => Events.AddHandler(EventEnabledChanged, value);
        remove => Events.RemoveHandler(EventEnabledChanged, value);
    }

    /// <summary>Occurs when the item is shown or hidden.</summary>
    public event EventHandler? VisibleChanged
    {
        add => Events.AddHandler(EventVisibleChanged, value);
        remove => Events.RemoveHandler(EventVisibleChanged, value);
    }

    /// <summary>Occurs when a button or finger goes down on the item.</summary>
    public event MouseEventHandler? MouseDown
    {
        add => Events.AddHandler(EventMouseDown, value);
        remove => Events.RemoveHandler(EventMouseDown, value);
    }

    /// <summary>Occurs when a button or finger is released on the item.</summary>
    public event MouseEventHandler? MouseUp
    {
        add => Events.AddHandler(EventMouseUp, value);
        remove => Events.RemoveHandler(EventMouseUp, value);
    }

    /// <summary>Occurs when the pointer enters the item.</summary>
    public event EventHandler? MouseEnter
    {
        add => Events.AddHandler(EventMouseEnter, value);
        remove => Events.RemoveHandler(EventMouseEnter, value);
    }

    /// <summary>Occurs when the pointer leaves the item.</summary>
    public event EventHandler? MouseLeave
    {
        add => Events.AddHandler(EventMouseLeave, value);
        remove => Events.RemoveHandler(EventMouseLeave, value);
    }

    /// <summary>The name, as the designer sets it to the field's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The text.</summary>
    [AllowNull]
    public virtual string Text
    {
        get => _text ?? string.Empty;
        set
        {
            if (string.Equals(_text, value, StringComparison.Ordinal))
            {
                return;
            }

            _text = value;
            Relayout();
            OnTextChanged(EventArgs.Empty);
        }
    }

    /// <summary>The image.</summary>
    public virtual Image? Image
    {
        get => _image;
        set
        {
            _image = value;
            Relayout();
        }
    }

    /// <summary>Whether the item shows its text, its image or both.</summary>
    public virtual ToolStripItemDisplayStyle DisplayStyle { get; set; } = ToolStripItemDisplayStyle.ImageAndText;

    /// <summary>Data about the item.</summary>
    public object? Tag { get; set; }

    /// <summary>The tooltip of the item.</summary>
    [AllowNull]
    public string ToolTipText
    {
        get => field ?? (AutoToolTip ? Text : string.Empty);
        set => field = value;
    }

    /// <summary>Whether the item's text is its tooltip when it has none.</summary>
    public bool AutoToolTip { get; set; }

    /// <summary>Whether the item responds to input.</summary>
    public virtual bool Enabled
    {
        get => _enabled && (Owner?.Enabled ?? true);
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            Invalidate();
            ((EventHandler?)Events[EventEnabledChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Whether the item is shown.</summary>
    public bool Visible
    {
        get => _available && (Owner?.Visible ?? true);
        set => Available = value;
    }

    /// <summary>Whether the item is placed on its strip.</summary>
    public bool Available
    {
        get => _available;
        set
        {
            if (_available == value)
            {
                return;
            }

            _available = value;
            Relayout();
            ((EventHandler?)Events[EventVisibleChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Which end of the strip the item sits at.</summary>
    public ToolStripItemAlignment Alignment { get; set; }

    /// <summary>The font: the strip's unless set.</summary>
    [AllowNull]
    public virtual Font Font
    {
        get => _font ?? Owner?.Font ?? Control.DefaultFont;
        set
        {
            _font = value;
            Relayout();
        }
    }

    /// <summary>The text color: the strip's unless set.</summary>
    public virtual Color ForeColor
    {
        get => !_foreColor.IsEmpty ? _foreColor : Owner?.ForeColor ?? SystemColors.ControlText;
        set
        {
            _foreColor = value;
            Invalidate();
        }
    }

    /// <summary>The background color; transparent unless set.</summary>
    public virtual Color BackColor
    {
        get => _backColor;
        set
        {
            _backColor = value;
            Invalidate();
        }
    }

    /// <summary>Whether the item sizes itself to its text and image.</summary>
    public bool AutoSize
    {
        get => _autoSize;
        set
        {
            _autoSize = value;
            Relayout();
        }
    }

    /// <summary>
    /// The size: the size the strip gave the item, once laid out. An automatically sized item takes the size of its
    /// text and image whatever is set, as in Windows Forms, where the designer still records the size.
    /// </summary>
    public virtual Size Size
    {
        get => !Bounds.IsEmpty ? Bounds.Size : LayoutSize;
        set
        {
            _size = value;
            Relayout();
        }
    }

    /// <summary>The width.</summary>
    public int Width
    {
        get => Size.Width;
        set => Size = new Size(value, Size.Height);
    }

    /// <summary>The height.</summary>
    public int Height
    {
        get => Size.Height;
        set => Size = new Size(Size.Width, value);
    }

    /// <summary>The item's bounds in its strip.</summary>
    public Rectangle Bounds { get; internal set; }

    /// <summary>The space inside the item around its content.</summary>
    public virtual Padding Padding { get; set; }

    /// <summary>The space around the item.</summary>
    public Padding Margin { get; set; }

    /// <summary>Where the text sits.</summary>
    public virtual ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleCenter;

    /// <summary>Where the image sits.</summary>
    public ContentAlignment ImageAlign { get; set; } = ContentAlignment.MiddleCenter;

    /// <summary>Where the text is relative to the image.</summary>
    public TextImageRelation TextImageRelation { get; set; } = TextImageRelation.ImageBeforeText;

    /// <summary>Whether the image is scaled to the strip's image size.</summary>
    public ToolStripItemImageScaling ImageScaling { get; set; } = ToolStripItemImageScaling.SizeToFit;

    /// <summary>A color of the image drawn transparent. Kept for compatibility.</summary>
    public Color ImageTransparentColor { get; set; }

    /// <summary>Whether double clicks raise DoubleClick. Kept for compatibility.</summary>
    public bool DoubleClickEnabled { get; set; }

    /// <summary>Whether the image is mirrored in right-to-left layouts. Kept for compatibility.</summary>
    public bool RightToLeftAutoMirrorImage { get; set; }

    /// <summary>The direction of the text. Kept for compatibility.</summary>
    public virtual ToolStripTextDirection TextDirection { get; set; } = ToolStripTextDirection.Horizontal;

    /// <summary>Whether the item may go to the strip's overflow menu. Items that do not fit are hidden.</summary>
    public ToolStripItemOverflow Overflow { get; set; } = ToolStripItemOverflow.AsNeeded;

    /// <summary>Where the item is placed: on the strip, or nowhere when it does not fit or is not available.</summary>
    public ToolStripItemPlacement Placement => Bounds.IsEmpty ? ToolStripItemPlacement.None : ToolStripItemPlacement.Main;

    /// <summary>How the item merges into another menu. Kept for compatibility.</summary>
    public MergeAction MergeAction { get; set; } = MergeAction.Append;

    /// <summary>Where the item merges into another menu. Kept for compatibility.</summary>
    public int MergeIndex { get; set; } = -1;

    /// <summary>The name accessibility tools read.</summary>
    public string? AccessibleName { get; set; }

    /// <summary>The description accessibility tools read.</summary>
    public string? AccessibleDescription { get; set; }

    /// <summary>The strip the item is on.</summary>
    public ToolStrip? Owner { get; internal set; }

    /// <summary>The item whose drop-down the item is on, if any.</summary>
    public ToolStripItem? OwnerItem => (Owner as ToolStripDropDown)?.OwnerItem;

    /// <summary>The strip the item is on, as a parent.</summary>
    public ToolStrip? Parent => Owner;

    /// <summary>Whether the item is highlighted, under the pointer.</summary>
    public virtual bool Selected { get; internal set; }

    /// <summary>Whether the item is pressed.</summary>
    public virtual bool Pressed { get; internal set; }

    /// <summary>Whether the item can be selected.</summary>
    public virtual bool CanSelect => Enabled;

    /// <summary>The size of a new item.</summary>
    protected virtual Size DefaultSize => new(23, 23);

    /// <summary>The padding of a new item.</summary>
    protected virtual Padding DefaultPadding => Padding.Empty;

    /// <summary>The margin of a new item.</summary>
    protected virtual Padding DefaultMargin => new(0, 1, 0, 2);

    /// <summary>The space beside the content, across both sides.</summary>
    private protected virtual int HorizontalSpace => 8;

    /// <summary>The space above and below the text, across both.</summary>
    private protected virtual int TextVerticalSpace => 4;

    /// <summary>The size the strip lays the item out at: its preferred size when sized automatically.</summary>
    internal Size LayoutSize => _autoSize ? GetPreferredSize(Size.Empty) : _size;

    /// <summary>The text without access-key ampersands.</summary>
    internal string DisplayText => Graphics.Prefixed(Text, System.Drawing.Text.HotkeyPrefix.Hide);

    /// <summary>Clicks the item, as a tap does.</summary>
    public void PerformClick()
    {
        if (Enabled && Available)
        {
            OnClick(EventArgs.Empty);
        }
    }

    /// <summary>Highlights the item.</summary>
    public void Select()
    {
        Selected = true;
        Invalidate();
    }

    /// <summary>Asks for the item to be drawn again.</summary>
    public void Invalidate() => Owner?.Invalidate();

    /// <summary>The size the item needs for its text and image.</summary>
    /// <param name="constrainingSize">The size available.</param>
    public virtual Size GetPreferredSize(Size constrainingSize)
    {
        var text = ShowsText ? TextMetrics.Measure(DisplayText, Font.Spec) : default;
        var image = ShowsImage ? ImageSize : Size.Empty;
        var width = (int)Math.Ceiling(text.Width) + image.Width + (ShowsText && ShowsImage ? 4 : 0) + Padding.Horizontal + HorizontalSpace;
        var height = Math.Max(ShowsText ? (int)Math.Ceiling(text.Height) + TextVerticalSpace : 0, ShowsImage ? image.Height + 6 : 0) + Padding.Vertical;
        return new Size(Math.Max(width, 8), Math.Max(height, 4));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Owner?.Items.Remove(this);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override string ToString() => string.IsNullOrEmpty(_text) ? base.ToString() ?? GetType().Name : _text;

    /// <summary>Raises Click.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnClick(EventArgs e) => ((EventHandler?)Events[EventClick])?.Invoke(this, e);

    /// <summary>Raises TextChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnTextChanged(EventArgs e) => ((EventHandler?)Events[EventTextChanged])?.Invoke(this, e);

    /// <summary>Raises MouseDown.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseDown(MouseEventArgs e) => ((MouseEventHandler?)Events[EventMouseDown])?.Invoke(this, e);

    /// <summary>Raises MouseUp.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseUp(MouseEventArgs e) => ((MouseEventHandler?)Events[EventMouseUp])?.Invoke(this, e);

    /// <summary>Raises MouseEnter.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseEnter(EventArgs e) => ((EventHandler?)Events[EventMouseEnter])?.Invoke(this, e);

    /// <summary>Raises MouseLeave.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseLeave(EventArgs e) => ((EventHandler?)Events[EventMouseLeave])?.Invoke(this, e);

    /// <summary>Whether the item draws its text.</summary>
    internal bool ShowsText => DisplayStyle is ToolStripItemDisplayStyle.Text or ToolStripItemDisplayStyle.ImageAndText && Text.Length > 0;

    /// <summary>Whether the item draws its image.</summary>
    internal bool ShowsImage => DisplayStyle is ToolStripItemDisplayStyle.Image or ToolStripItemDisplayStyle.ImageAndText && _image is not null;

    /// <summary>The size the image is drawn at.</summary>
    internal Size ImageSize => _image is null ? Size.Empty : ImageScaling == ToolStripItemImageScaling.SizeToFit ? Owner?.ImageScalingSize ?? new Size(16, 16) : _image.Size;

    internal void DispatchMouseDown(MouseEventArgs e) => OnMouseDown(e);

    internal void DispatchMouseUp(MouseEventArgs e) => OnMouseUp(e);

    internal void DispatchMouseEnter() => OnMouseEnter(EventArgs.Empty);

    internal void DispatchMouseLeave() => OnMouseLeave(EventArgs.Empty);

    /// <summary>Draws the item's content: its image and text, on a strip.</summary>
    /// <param name="g">The surface, in the strip's coordinates.</param>
    /// <param name="bounds">The item's bounds.</param>
    internal virtual void PaintOnStrip(Graphics g, Rectangle bounds)
    {
        if (!_backColor.IsEmpty)
        {
            using var brush = new SolidBrush(_backColor);
            g.FillRectangle(brush, bounds);
        }

        PaintHighlight(g, bounds);
        PaintContent(g, Rectangle.FromLTRB(bounds.Left + 4 + Padding.Left, bounds.Top + Padding.Top, bounds.Right - 4 - Padding.Right, bounds.Bottom - Padding.Bottom));
    }

    /// <summary>Draws the highlight of an item under the pointer or pressed.</summary>
    internal void PaintHighlight(Graphics g, Rectangle bounds)
    {
        if ((Selected || Pressed) && Enabled)
        {
            using var fill = new SolidBrush(Pressed ? Theme.ButtonPressedFace : Theme.ItemHot);
            using var border = new Pen(Pressed ? Theme.Accent : Theme.ItemHotBorder);
            g.FillRectangle(fill, bounds);
            g.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }
    }

    /// <summary>Draws the image and text in an area.</summary>
    internal void PaintContent(Graphics g, Rectangle area)
    {
        var textArea = area;
        if (ShowsImage && _image is { } image)
        {
            var size = ImageSize;
            var imageBounds = ShowsText
                ? ThemePainter.Align(area, size, ContentAlignment.MiddleLeft)
                : ThemePainter.Align(area, size, ContentAlignment.MiddleCenter);
            g.DrawImage(image, imageBounds);
            textArea = Rectangle.FromLTRB(imageBounds.Right + 4, area.Top, area.Right, area.Bottom);
        }

        if (ShowsText)
        {
            ThemePainter.DrawText(g, Text, Font, Enabled ? ForeColor : Theme.DisabledText, textArea, ShowsImage ? ContentAlignment.MiddleLeft : TextAlign, wrap: false, ellipsis: false, mnemonics: true);
        }
    }

    /// <summary>Asks the strip to lay out its items again.</summary>
    private protected void Relayout()
    {
        if (Owner is { } owner)
        {
            owner.PerformLayout();
            owner.Invalidate();
        }
    }
}

/// <summary>The items of a strip or drop-down.</summary>
[ListBindable(false)]
public class ToolStripItemCollection : ArrangedElementCollection, IList
{
    private readonly ToolStrip _owner;

    /// <summary>Creates a collection of a strip's items, with items in it.</summary>
    /// <param name="owner">The strip.</param>
    /// <param name="value">The items.</param>
    public ToolStripItemCollection(ToolStrip owner, ToolStripItem[] value)
        : this(owner)
    {
        AddRange(value);
    }

    internal ToolStripItemCollection(ToolStrip owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>The item at an index.</summary>
    /// <param name="index">The index.</param>
    public virtual ToolStripItem this[int index] => (ToolStripItem)InnerList[index]!;

    /// <summary>The first item with a name.</summary>
    /// <param name="key">The name.</param>
    public virtual ToolStripItem? this[string key] => IndexOfKey(key) is >= 0 and var index ? this[index] : null;

    /// <inheritdoc />
    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException("Items are added with Add or Insert.");
    }

    /// <inheritdoc />
    bool IList.IsReadOnly => false;

    /// <inheritdoc />
    bool IList.IsFixedSize => false;


    /// <summary>Adds an item with text.</summary>
    /// <param name="text">The text.</param>
    public ToolStripItem Add(string? text) => Add(text, null, null);

    /// <summary>Adds an item with an image.</summary>
    /// <param name="image">The image.</param>
    public ToolStripItem Add(Image? image) => Add(null, image, null);

    /// <summary>Adds an item with text and an image.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    public ToolStripItem Add(string? text, Image? image) => Add(text, image, null);

    /// <summary>Adds an item with text, an image and a click handler.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    public ToolStripItem Add(string? text, Image? image, EventHandler? onClick)
    {
        ToolStripItem item = _owner is MenuStrip or ToolStripDropDown
            ? new ToolStripMenuItem(text, image, onClick)
            : new ToolStripButton(text, image, onClick);
        Add(item);
        return item;
    }

    /// <summary>Adds an item.</summary>
    /// <param name="value">The item.</param>
    /// <returns>Its index.</returns>
    public int Add(ToolStripItem value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Insert(Count, value);
        return Count - 1;
    }

    /// <summary>Adds items.</summary>
    /// <param name="toolStripItems">The items.</param>
    public void AddRange(params ToolStripItem[] toolStripItems)
    {
        ArgumentNullException.ThrowIfNull(toolStripItems);
        foreach (var item in toolStripItems)
        {
            Add(item);
        }
    }

    /// <summary>Adds the items of another collection.</summary>
    /// <param name="toolStripItems">The items.</param>
    public void AddRange(ToolStripItemCollection toolStripItems)
    {
        ArgumentNullException.ThrowIfNull(toolStripItems);
        foreach (var item in toolStripItems.InnerList.Cast<ToolStripItem>().ToList())
        {
            Add(item);
        }
    }

    /// <summary>Inserts an item.</summary>
    /// <param name="index">Where.</param>
    /// <param name="value">The item.</param>
    public void Insert(int index, ToolStripItem value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Owner is { } previous)
        {
            previous.Items.Remove(value);
        }

        InnerList.Insert(Math.Clamp(index, 0, Count), value);
        value.Owner = _owner;
        _owner.OnItemAddedInternal(value);
    }

    /// <summary>Removes an item.</summary>
    /// <param name="value">The item.</param>
    public void Remove(ToolStripItem value)
    {
        if (!InnerList.Contains(value))
        {
            return;
        }

        InnerList.Remove(value);
        _owner.OnItemRemovedInternal(value);
        value.Owner = null;
        value.Bounds = Rectangle.Empty;
    }

    /// <summary>Removes the item at an index.</summary>
    /// <param name="index">The index.</param>
    public void RemoveAt(int index) => Remove(this[index]);

    /// <summary>Removes the first item with a name.</summary>
    /// <param name="key">The name.</param>
    public void RemoveByKey(string key)
    {
        if (this[key] is { } item)
        {
            Remove(item);
        }
    }

    /// <summary>Removes every item.</summary>
    public void Clear()
    {
        foreach (var item in InnerList.Cast<ToolStripItem>().ToList())
        {
            Remove(item);
        }
    }

    /// <summary>Whether an item is in the collection.</summary>
    /// <param name="value">The item.</param>
    public bool Contains(ToolStripItem value) => InnerList.Contains(value);

    /// <summary>Whether an item has a name.</summary>
    /// <param name="key">The name.</param>
    public bool ContainsKey(string key) => IndexOfKey(key) >= 0;

    /// <summary>The index of an item.</summary>
    /// <param name="value">The item.</param>
    public int IndexOf(ToolStripItem value) => InnerList.IndexOf(value);

    /// <summary>The index of the first item with a name.</summary>
    /// <param name="key">The name.</param>
    public int IndexOfKey(string? key)
    {
        for (var i = 0; i < Count; i++)
        {
            if (string.Equals(this[i].Name, key, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The items with a name, optionally searching drop-downs.</summary>
    /// <param name="key">The name.</param>
    /// <param name="searchAllChildren">Whether to search the items' drop-downs too.</param>
    public ToolStripItem[] Find(string key, bool searchAllChildren)
    {
        var found = new List<ToolStripItem>();
        foreach (var item in InnerList.Cast<ToolStripItem>())
        {
            if (string.Equals(item.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(item);
            }

            if (searchAllChildren && item is ToolStripDropDownItem { HasDropDownItems: true } dropDown)
            {
                found.AddRange(dropDown.DropDownItems.Find(key, true));
            }
        }

        return [.. found];
    }

    /// <summary>Copies the items into an array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="index">Where to start.</param>
    public void CopyTo(ToolStripItem[] array, int index) => InnerList.CopyTo(array, index);

    /// <summary>The items, in order.</summary>
    internal List<ToolStripItem> ToList() => [.. InnerList.Cast<ToolStripItem>()];

    /// <inheritdoc />
    int IList.Add(object? value) => Add((ToolStripItem)value!);

    /// <inheritdoc />
    void IList.Insert(int index, object? value) => Insert(index, (ToolStripItem)value!);

    /// <inheritdoc />
    void IList.Remove(object? value) => Remove((ToolStripItem)value!);

    /// <inheritdoc />
    bool IList.Contains(object? value) => value is ToolStripItem item && Contains(item);

    /// <inheritdoc />
    int IList.IndexOf(object? value) => value is ToolStripItem item ? IndexOf(item) : -1;

    /// <inheritdoc />
    void IList.Clear() => Clear();

    /// <inheritdoc />
    void IList.RemoveAt(int index) => RemoveAt(index);
}
