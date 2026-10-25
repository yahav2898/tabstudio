using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>Pages of controls with a row of tabs to switch between them.</summary>
[DefaultEvent(nameof(SelectedIndexChanged))]
[DefaultProperty(nameof(TabPages))]
public class TabControl : Control
{
    private static readonly object EventSelectedIndexChanged = new();
    private static readonly object EventSelecting = new();
    private static readonly object EventSelected = new();
    private static readonly object EventDeselecting = new();
    private static readonly object EventDeselected = new();
    private static readonly object EventDrawItem = new();

    private readonly TabPageCollection _pages;
    private int _selectedIndex = -1;
    private int _hotIndex = -1;
    private Size _itemSize = Size.Empty;
    private Point _padding = new(6, 3);
    private TabAlignment _alignment = TabAlignment.Top;

    /// <summary>Creates a tab control.</summary>
    public TabControl()
    {
        _pages = new TabPageCollection(this);
        SetStyle(ControlStyles.StandardDoubleClick, false);
    }

    /// <summary>Occurs when the selected tab changes.</summary>
    public event EventHandler? SelectedIndexChanged
    {
        add => Events.AddHandler(EventSelectedIndexChanged, value);
        remove => Events.RemoveHandler(EventSelectedIndexChanged, value);
    }

    /// <summary>Occurs before a tab is selected; handlers can cancel it.</summary>
    public event TabControlCancelEventHandler? Selecting
    {
        add => Events.AddHandler(EventSelecting, value);
        remove => Events.RemoveHandler(EventSelecting, value);
    }

    /// <summary>Occurs when a tab has been selected.</summary>
    public event TabControlEventHandler? Selected
    {
        add => Events.AddHandler(EventSelected, value);
        remove => Events.RemoveHandler(EventSelected, value);
    }

    /// <summary>Occurs before a tab is deselected; handlers can cancel it.</summary>
    public event TabControlCancelEventHandler? Deselecting
    {
        add => Events.AddHandler(EventDeselecting, value);
        remove => Events.RemoveHandler(EventDeselecting, value);
    }

    /// <summary>Occurs when a tab has been deselected.</summary>
    public event TabControlEventHandler? Deselected
    {
        add => Events.AddHandler(EventDeselected, value);
        remove => Events.RemoveHandler(EventDeselected, value);
    }

    /// <summary>Occurs when a tab of an owner-drawn tab control is drawn.</summary>
    public event DrawItemEventHandler? DrawItem
    {
        add => Events.AddHandler(EventDrawItem, value);
        remove => Events.RemoveHandler(EventDrawItem, value);
    }

    /// <summary>The pages.</summary>
    public TabPageCollection TabPages => _pages;

    /// <summary>The number of tabs.</summary>
    public int TabCount => _pages.Count;

    /// <summary>The index of the selected tab, or -1.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
            if (value >= _pages.Count && _pages.Count > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            SelectTab(value, byUser: false);
        }
    }

    /// <summary>The selected page.</summary>
    public TabPage? SelectedTab
    {
        get => _selectedIndex >= 0 && _selectedIndex < _pages.Count ? _pages[_selectedIndex] : null;
        set => SelectedIndex = value is null ? -1 : _pages.IndexOf(value);
    }

    /// <summary>Where the tabs are. Tabs are drawn at the top or the bottom.</summary>
    public TabAlignment Alignment
    {
        get => _alignment;
        set
        {
            _alignment = value;
            LayoutPages();
            Invalidate();
        }
    }

    /// <summary>How the tabs look.</summary>
    public TabAppearance Appearance { get; set; }

    /// <summary>The size of the tabs; zero parts are sized to fit.</summary>
    public Size ItemSize
    {
        get => _itemSize.IsEmpty ? new Size(0, TabHeight) : _itemSize;
        set
        {
            _itemSize = value;
            LayoutPages();
            Invalidate();
        }
    }

    /// <summary>The space around a tab's text.</summary>
    public new Point Padding
    {
        get => _padding;
        set
        {
            _padding = value;
            LayoutPages();
            Invalidate();
        }
    }

    /// <summary>How the tabs are sized.</summary>
    public TabSizeMode SizeMode { get; set; }

    /// <summary>Whether tabs wrap to several rows. Tabs are shown in one row.</summary>
    public bool Multiline { get; set; }

    /// <summary>Whether a tab highlights under the pointer.</summary>
    public bool HotTrack { get; set; }

    /// <summary>Whether tabs show their pages' tooltips.</summary>
    public bool ShowToolTips { get; set; }

    /// <summary>Whether the app draws the tabs, in the DrawItem event.</summary>
    public TabDrawMode DrawMode { get; set; }

    /// <summary>The number of rows of tabs.</summary>
    public int RowCount => _pages.Count > 0 ? 1 : 0;

    /// <summary>The area pages fill.</summary>
    public override Rectangle DisplayRectangle
    {
        get
        {
            var header = TabHeight + 2;
            var client = ClientRectangle;
            return _alignment == TabAlignment.Bottom
                ? new Rectangle(4, 4, Math.Max(0, client.Width - 8), Math.Max(0, client.Height - header - 8))
                : new Rectangle(4, header + 2, Math.Max(0, client.Width - 8), Math.Max(0, client.Height - header - 6));
        }
    }

    /// <inheritdoc />
    protected override Size DefaultSize => new(200, 100);

    /// <summary>The height of the tabs.</summary>
    private int TabHeight => _itemSize.Height > 0 ? _itemSize.Height : Font.Height + (2 * _padding.Y) - 2;

    /// <summary>The bounds of a tab.</summary>
    /// <param name="index">The tab.</param>
    public Rectangle GetTabRect(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _pages.Count);
        var x = 2;
        for (var i = 0; i < index; i++)
        {
            x += TabWidth(i);
        }

        var y = _alignment == TabAlignment.Bottom ? Height - TabHeight - 2 : 2;
        return new Rectangle(x, y, TabWidth(index), TabHeight);
    }

    /// <summary>The page at an index.</summary>
    /// <param name="index">The index.</param>
    public Control GetControl(int index) => _pages[index];

    /// <summary>Selects a tab.</summary>
    /// <param name="index">The tab.</param>
    public void SelectTab(int index) => SelectedIndex = index;

    /// <summary>Selects a page.</summary>
    /// <param name="tabPage">The page.</param>
    public void SelectTab(TabPage tabPage) => SelectedTab = tabPage;

    /// <summary>Selects the page with a name.</summary>
    /// <param name="tabPageName">The name.</param>
    public void SelectTab(string tabPageName) => SelectedTab = _pages[tabPageName];

    /// <summary>Deselects a tab, selecting the next.</summary>
    /// <param name="index">The tab.</param>
    public void DeselectTab(int index)
    {
        if (index == _selectedIndex && _pages.Count > 1)
        {
            SelectedIndex = (index + 1) % _pages.Count;
        }
    }

    /// <summary>Raises SelectedIndexChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSelectedIndexChanged(EventArgs e) => ((EventHandler?)Events[EventSelectedIndexChanged])?.Invoke(this, e);

    /// <summary>Raises Selecting.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSelecting(TabControlCancelEventArgs e) => ((TabControlCancelEventHandler?)Events[EventSelecting])?.Invoke(this, e);

    /// <summary>Raises Selected.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSelected(TabControlEventArgs e) => ((TabControlEventHandler?)Events[EventSelected])?.Invoke(this, e);

    /// <summary>Raises Deselecting.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDeselecting(TabControlCancelEventArgs e) => ((TabControlCancelEventHandler?)Events[EventDeselecting])?.Invoke(this, e);

    /// <summary>Raises Deselected.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDeselected(TabControlEventArgs e) => ((TabControlEventHandler?)Events[EventDeselected])?.Invoke(this, e);

    /// <summary>Raises DrawItem.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDrawItem(DrawItemEventArgs e) => ((DrawItemEventHandler?)Events[EventDrawItem])?.Invoke(this, e);

    /// <inheritdoc />
    protected override ControlCollection CreateControlsInstance() => new TabControlControlCollection(this);

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        LayoutPages();
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var index = TabAt(e.Location);
        if (index >= 0)
        {
            SelectTab(index, byUser: true);
            Focus();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = TabAt(e.Location);
        if (hot != _hotIndex)
        {
            _hotIndex = hot;
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotIndex = -1;
        Invalidate();
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs ke)
    {
        base.OnKeyDown(ke);
        if (ke.Handled || _pages.Count == 0)
        {
            return;
        }

        int? target = ke.KeyCode switch
        {
            Keys.Left => Math.Max(0, _selectedIndex - 1),
            Keys.Right => Math.Min(_pages.Count - 1, _selectedIndex + 1),
            Keys.Home => 0,
            Keys.End => _pages.Count - 1,
            _ => null,
        };

        if (target is { } index)
        {
            SelectTab(index, byUser: true);
            ke.Handled = true;
        }
    }

    /// <summary>Ctrl+Tab and Ctrl+Shift+Tab move between the tabs, as in Windows.</summary>
    /// <inheritdoc />
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if ((keyData & Keys.KeyCode) == Keys.Tab && (keyData & Keys.Control) != 0 && _pages.Count > 1 && ContainsFocus)
        {
            var step = (keyData & Keys.Shift) != 0 ? -1 : 1;
            SelectTab((_selectedIndex + step + _pages.Count) % _pages.Count, byUser: true);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var display = DisplayRectangle;
        var frame = Rectangle.Inflate(display, 4, 4);
        frame = _alignment == TabAlignment.Bottom
            ? Rectangle.FromLTRB(0, 0, Width, frame.Bottom - 1)
            : Rectangle.FromLTRB(0, frame.Top + 2, Width, Height);
        using (var page = new SolidBrush(Theme.Window))
        {
            g.FillRectangle(page, frame);
        }

        using var border = new Pen(Theme.TabBorder);
        g.DrawRectangle(border, frame.X, frame.Y, frame.Width - 1, frame.Height - 1);
        for (var i = 0; i < _pages.Count; i++)
        {
            if (i != _selectedIndex)
            {
                PaintTab(g, i, border);
            }
        }

        if (_selectedIndex >= 0 && _selectedIndex < _pages.Count)
        {
            PaintTab(g, _selectedIndex, border);
        }
    }

    /// <summary>Called when the pages change.</summary>
    internal void OnPagesChanged()
    {
        if (_selectedIndex >= _pages.Count)
        {
            _selectedIndex = _pages.Count - 1;
        }

        if (_selectedIndex < 0 && _pages.Count > 0)
        {
            _selectedIndex = 0;
        }

        LayoutPages();
        Invalidate();
    }

    private int TabWidth(int index)
    {
        if (_itemSize.Width > 0 && SizeMode == TabSizeMode.Fixed)
        {
            return _itemSize.Width;
        }

        var text = TextMetrics.Measure(_pages[index].DisplayText, Font.Spec).Width;
        return Math.Max(42, (int)Math.Ceiling(text) + (2 * _padding.X) + 4);
    }

    private int TabAt(Point point)
    {
        for (var i = 0; i < _pages.Count; i++)
        {
            var tab = GetTabRect(i);
            if (i == _selectedIndex)
            {
                tab = Rectangle.Inflate(tab, 2, 2);
            }

            if (tab.Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    private void PaintTab(Graphics g, int index, Pen border)
    {
        var page = _pages[index];
        var selected = index == _selectedIndex;
        var tab = GetTabRect(index);
        var bottom = _alignment == TabAlignment.Bottom;
        if (selected)
        {
            tab = bottom ? Rectangle.FromLTRB(tab.Left - 2, tab.Top - 2, tab.Right + 2, tab.Bottom + 2) : Rectangle.FromLTRB(tab.Left - 2, tab.Top - 2, tab.Right + 2, tab.Bottom + 1);
        }

        if (DrawMode == TabDrawMode.OwnerDrawFixed && Events[EventDrawItem] is not null)
        {
            OnDrawItem(new DrawItemEventArgs(g, Font, tab, index, selected ? DrawItemState.Selected : DrawItemState.None, ForeColor, BackColor));
            return;
        }

        var fill = selected ? Theme.Window : index == _hotIndex ? Theme.TabHot : Theme.StripBackground;
        using (var brush = new SolidBrush(fill))
        {
            g.FillRectangle(brush, tab);
        }

        if (bottom)
        {
            g.DrawLine(border, tab.Left, tab.Top, tab.Left, tab.Bottom - 1);
            g.DrawLine(border, tab.Left, tab.Bottom - 1, tab.Right - 1, tab.Bottom - 1);
            g.DrawLine(border, tab.Right - 1, tab.Top, tab.Right - 1, tab.Bottom - 1);
        }
        else
        {
            g.DrawLine(border, tab.Left, tab.Bottom, tab.Left, tab.Top);
            g.DrawLine(border, tab.Left, tab.Top, tab.Right - 1, tab.Top);
            g.DrawLine(border, tab.Right - 1, tab.Top, tab.Right - 1, tab.Bottom);
        }

        var color = Enabled && page.Enabled ? ForeColor : Theme.DisabledText;
        ThemePainter.DrawText(g, page.Text, Font, color, tab, ContentAlignment.MiddleCenter);
        if (selected && Focused && Window?.ShowFocusCues == true)
        {
            ThemePainter.DrawFocusRectangle(g, Rectangle.Inflate(tab, -3, -3));
        }
    }

    private void SelectTab(int index, bool byUser)
    {
        if (index == _selectedIndex)
        {
            return;
        }

        var previous = SelectedTab;
        var next = index >= 0 && index < _pages.Count ? _pages[index] : null;
        if (byUser || IsHandleCreated)
        {
            var deselecting = new TabControlCancelEventArgs(previous, _selectedIndex, false, TabControlAction.Deselecting);
            if (previous is not null)
            {
                OnDeselecting(deselecting);
                if (deselecting.Cancel)
                {
                    return;
                }
            }

            var selecting = new TabControlCancelEventArgs(next, index, false, TabControlAction.Selecting);
            OnSelecting(selecting);
            if (selecting.Cancel)
            {
                return;
            }
        }

        var previousIndex = _selectedIndex;
        _selectedIndex = index;
        LayoutPages();
        Invalidate();
        if (previous is not null)
        {
            OnDeselected(new TabControlEventArgs(previous, previousIndex, TabControlAction.Deselected));
        }

        if (next is not null)
        {
            OnSelected(new TabControlEventArgs(next, index, TabControlAction.Selected));
        }

        OnSelectedIndexChanged(EventArgs.Empty);
    }

    private void LayoutPages()
    {
        var display = DisplayRectangle;
        for (var i = 0; i < _pages.Count; i++)
        {
            var page = _pages[i];
            page.SetBoundsFromLayout(display);
            page.ShownByTabControl = i == _selectedIndex;
        }
    }

    /// <summary>The children of a tab control: its pages.</summary>
    public class TabControlControlCollection : ControlCollection
    {
        private readonly TabControl _owner;

        internal TabControlControlCollection(TabControl owner)
            : base(owner)
        {
            _owner = owner;
        }

        /// <summary>Adds a page.</summary>
        /// <param name="value">The page.</param>
        public override void Add(Control? value)
        {
            if (value is not TabPage)
            {
                throw new ArgumentException($"Cannot add '{value?.GetType().Name}' to TabControl. Only TabPages can be directly added to TabControls.", nameof(value));
            }

            base.Add(value);
            _owner.OnPagesChanged();
        }

        /// <summary>Removes a page.</summary>
        /// <param name="value">The page.</param>
        public override void Remove(Control? value)
        {
            base.Remove(value);
            _owner.OnPagesChanged();
        }
    }

    /// <summary>The pages of a tab control.</summary>
    public class TabPageCollection : IList
    {
        private readonly TabControl _owner;

        /// <summary>Creates the collection.</summary>
        /// <param name="owner">The tab control.</param>
        public TabPageCollection(TabControl owner) => _owner = owner;

        /// <summary>The number of pages.</summary>
        public int Count => _owner.Controls.Count;

        /// <summary>Whether the collection is read-only.</summary>
        public bool IsReadOnly => false;

        /// <inheritdoc />
        bool IList.IsFixedSize => false;

        /// <inheritdoc />
        bool ICollection.IsSynchronized => false;

        /// <inheritdoc />
        object ICollection.SyncRoot => this;

        /// <summary>The page at an index.</summary>
        /// <param name="index">The index.</param>
        public virtual TabPage this[int index]
        {
            get => (TabPage)_owner.Controls[index];
            set
            {
                RemoveAt(index);
                Insert(index, value);
            }
        }

        /// <summary>The first page with a name.</summary>
        /// <param name="key">The name.</param>
        public virtual TabPage? this[string? key] => _owner.Controls[key] as TabPage;

        /// <inheritdoc />
        object? IList.this[int index]
        {
            get => this[index];
            set => this[index] = (TabPage)value!;
        }

        /// <summary>Adds a page.</summary>
        /// <param name="value">The page.</param>
        public void Add(TabPage value) => _owner.Controls.Add(value);

        /// <summary>Adds a page with text.</summary>
        /// <param name="text">The text.</param>
        public void Add(string? text) => Add(new TabPage { Text = text });

        /// <summary>Adds a page with a name and text.</summary>
        /// <param name="key">The name.</param>
        /// <param name="text">The text.</param>
        public void Add(string? key, string? text) => Add(new TabPage { Name = key ?? string.Empty, Text = text });

        /// <summary>Adds pages.</summary>
        /// <param name="pages">The pages.</param>
        public void AddRange(params TabPage[] pages)
        {
            ArgumentNullException.ThrowIfNull(pages);
            foreach (var page in pages)
            {
                Add(page);
            }
        }

        /// <summary>Inserts a page.</summary>
        /// <param name="index">Where.</param>
        /// <param name="tabPage">The page.</param>
        public void Insert(int index, TabPage tabPage)
        {
            Add(tabPage);
            _owner.Controls.SetChildIndex(tabPage, index);
            _owner.OnPagesChanged();
        }

        /// <summary>Inserts a page with text.</summary>
        /// <param name="index">Where.</param>
        /// <param name="text">The text.</param>
        public void Insert(int index, string? text) => Insert(index, new TabPage { Text = text });

        /// <summary>Removes a page.</summary>
        /// <param name="value">The page.</param>
        public void Remove(TabPage value) => _owner.Controls.Remove(value);

        /// <summary>Removes the page at an index.</summary>
        /// <param name="index">The index.</param>
        public void RemoveAt(int index) => _owner.Controls.RemoveAt(index);

        /// <summary>Removes the page with a name.</summary>
        /// <param name="key">The name.</param>
        public void RemoveByKey(string key) => _owner.Controls.RemoveByKey(key);

        /// <summary>Removes every page.</summary>
        public virtual void Clear() => _owner.Controls.Clear();

        /// <summary>Whether a page is in the collection.</summary>
        /// <param name="page">The page.</param>
        public bool Contains(TabPage page) => _owner.Controls.Contains(page);

        /// <summary>Whether a page has a name.</summary>
        /// <param name="key">The name.</param>
        public bool ContainsKey(string? key) => _owner.Controls.ContainsKey(key);

        /// <summary>The index of a page, or -1.</summary>
        /// <param name="page">The page.</param>
        public int IndexOf(TabPage page) => _owner.Controls.IndexOf(page);

        /// <summary>The index of the page with a name, or -1.</summary>
        /// <param name="key">The name.</param>
        public int IndexOfKey(string? key) => _owner.Controls.IndexOfKey(key);

        /// <inheritdoc />
        public IEnumerator GetEnumerator() => _owner.Controls.GetEnumerator();

        /// <inheritdoc />
        void ICollection.CopyTo(Array dest, int index) => _owner.Controls.CopyTo(dest, index);

        /// <inheritdoc />
        int IList.Add(object? value)
        {
            Add((TabPage)value!);
            return Count - 1;
        }

        /// <inheritdoc />
        bool IList.Contains(object? value) => value is TabPage page && Contains(page);

        /// <inheritdoc />
        int IList.IndexOf(object? value) => value is TabPage page ? IndexOf(page) : -1;

        /// <inheritdoc />
        void IList.Insert(int index, object? value) => Insert(index, (TabPage)value!);

        /// <inheritdoc />
        void IList.Remove(object? value)
        {
            if (value is TabPage page)
            {
                Remove(page);
            }
        }
    }
}

/// <summary>A page of a tab control.</summary>
[DefaultEvent(nameof(Click))]
[DefaultProperty(nameof(Text))]
public class TabPage : Panel
{
    private bool _shownByTabControl = true;

    /// <summary>Creates a page.</summary>
    public TabPage()
    {
        Padding = new Padding(3);
    }

    /// <summary>Creates a page with text.</summary>
    /// <param name="text">The text.</param>
    public TabPage(string? text)
        : this()
    {
        Text = text;
    }

    /// <summary>The tab's text.</summary>
    [AllowNull]
    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value;
            (Parent as TabControl)?.OnPagesChanged();
        }
    }

    /// <summary>The tab's tooltip.</summary>
    [AllowNull]
    public string ToolTipText
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>Whether the page has the visual style's background, white, rather than its own color.</summary>
    public bool UseVisualStyleBackColor { get; set; }

    /// <summary>The background: white with the visual style, unless set.</summary>
    public override Color BackColor
    {
        get => UseVisualStyleBackColor && !IsBackColorSet ? Theme.Window : base.BackColor;
        set => base.BackColor = value;
    }

    /// <summary>The index of the tab's image. Kept for compatibility: tabs show their text.</summary>
    public int ImageIndex { get; set; } = -1;

    /// <summary>The key of the tab's image. Kept for compatibility: tabs show their text.</summary>
    [AllowNull]
    public string ImageKey
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>The tab's text without access-key ampersands.</summary>
    internal string DisplayText => Graphics.Prefixed(Text, System.Drawing.Text.HotkeyPrefix.Hide);

    /// <summary>Whether the tab control shows the page, as the selected one.</summary>
    internal bool ShownByTabControl
    {
        get => _shownByTabControl;
        set
        {
            if (_shownByTabControl != value)
            {
                _shownByTabControl = value;
                Visible = value;
            }
        }
    }

    /// <summary>The page with a control on it.</summary>
    /// <param name="comp">The control.</param>
    public static TabPage? GetTabPageOfComponent(object? comp)
    {
        for (var control = comp as Control; control is not null; control = control.Parent)
        {
            if (control is TabPage page)
            {
                return page;
            }
        }

        return null;
    }
}

/// <summary>Whether the app draws a tab control's tabs.</summary>
public enum TabDrawMode
{
    /// <summary>Normal.</summary>
    Normal = 0,

    /// <summary>Owner draw fixed.</summary>
    OwnerDrawFixed = 1,
}

/// <summary>What a tab control is doing to a tab.</summary>
public enum TabControlAction
{
    /// <summary>Selecting.</summary>
    Selecting = 0,

    /// <summary>Selected.</summary>
    Selected = 1,

    /// <summary>Deselecting.</summary>
    Deselecting = 2,

    /// <summary>Deselected.</summary>
    Deselected = 3,
}

/// <summary>Data about a tab of a tab control.</summary>
/// <param name="tabPage">The page.</param>
/// <param name="tabPageIndex">Its index.</param>
/// <param name="action">What is done to it.</param>
public class TabControlEventArgs(TabPage? tabPage, int tabPageIndex, TabControlAction action) : EventArgs
{
    /// <summary>The page.</summary>
    public TabPage? TabPage { get; } = tabPage;

    /// <summary>Its index.</summary>
    public int TabPageIndex { get; } = tabPageIndex;

    /// <summary>What is done to it.</summary>
    public TabControlAction Action { get; } = action;
}

/// <summary>Data about a tab about to be selected or deselected, which handlers can cancel.</summary>
/// <param name="tabPage">The page.</param>
/// <param name="tabPageIndex">Its index.</param>
/// <param name="cancel">Whether it is canceled.</param>
/// <param name="action">What is done to it.</param>
public class TabControlCancelEventArgs(TabPage? tabPage, int tabPageIndex, bool cancel, TabControlAction action) : CancelEventArgs(cancel)
{
    /// <summary>The page.</summary>
    public TabPage? TabPage { get; } = tabPage;

    /// <summary>Its index.</summary>
    public int TabPageIndex { get; } = tabPageIndex;

    /// <summary>What is done to it.</summary>
    public TabControlAction Action { get; } = action;
}

/// <summary>Handles a tab being selected or deselected.</summary>
/// <param name="sender">The tab control.</param>
/// <param name="e">The event data.</param>
public delegate void TabControlEventHandler(object? sender, TabControlEventArgs e);

/// <summary>Handles a tab about to be selected or deselected.</summary>
/// <param name="sender">The tab control.</param>
/// <param name="e">The event data.</param>
public delegate void TabControlCancelEventHandler(object? sender, TabControlCancelEventArgs e);
