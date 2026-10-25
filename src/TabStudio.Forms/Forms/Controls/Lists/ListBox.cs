using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>A list of items to pick one or several of, which scrolls by dragging with a finger.</summary>
[DefaultEvent(nameof(SelectedIndexChanged))]
[DefaultProperty(nameof(Items))]
public class ListBox : ListControl
{
    private static readonly object EventSelectedIndexChanged = new();
    private static readonly object EventDrawItem = new();
    private static readonly object EventMeasureItem = new();

    private readonly ObjectCollection _items;
    private readonly SortedSet<int> _selected = [];
    private readonly ItemListView _view = new();
    private SelectionMode _selectionMode = SelectionMode.One;
    private BorderStyle _borderStyle = BorderStyle.Fixed3D;
    private int _itemHeight = -1;
    private int _focusIndex = -1;
    private int _anchorIndex = -1;
    private int _updateCount;
    private bool _integralHeight = true;
    private bool _scrollAlwaysVisible;
    private bool _sorted;
    private bool _barShown;

    /// <summary>Creates a list box.</summary>
    public ListBox()
    {
        _items = new ObjectCollection(this);
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
        UpdateView();
    }

    /// <summary>Occurs when the selected item changes.</summary>
    public event EventHandler? SelectedIndexChanged
    {
        add => Events.AddHandler(EventSelectedIndexChanged, value);
        remove => Events.RemoveHandler(EventSelectedIndexChanged, value);
    }

    /// <summary>Occurs when an item of an owner-drawn list box is drawn.</summary>
    public event DrawItemEventHandler? DrawItem
    {
        add => Events.AddHandler(EventDrawItem, value);
        remove => Events.RemoveHandler(EventDrawItem, value);
    }

    /// <summary>Occurs when the height of an item of an owner-drawn list box is needed. Rows have one height.</summary>
    public event MeasureItemEventHandler? MeasureItem
    {
        add => Events.AddHandler(EventMeasureItem, value);
        remove => Events.RemoveHandler(EventMeasureItem, value);
    }

    /// <summary>The items.</summary>
    public ObjectCollection Items => _items;

    /// <summary>The indexes of the selected items.</summary>
    public SelectedIndexCollection SelectedIndices => new(this);

    /// <summary>The selected items.</summary>
    public SelectedObjectCollection SelectedItems => new(this);

    /// <summary>How many items can be selected.</summary>
    public virtual SelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            _selectionMode = value;
            if (value == SelectionMode.None)
            {
                ClearSelected();
            }
            else if (value == SelectionMode.One && _selected.Count > 1)
            {
                var first = _selected.Min;
                _selected.Clear();
                _selected.Add(first);
                Invalidate();
            }
        }
    }

    /// <inheritdoc />
    public override int SelectedIndex
    {
        get => _selected.Count == 0 ? -1 : _selected.Min;
        set
        {
            if (value < -1 || value >= _items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"InvalidArgument=Value of '{value}' is not valid for 'SelectedIndex'.");
            }

            if (_selectionMode == SelectionMode.None)
            {
                throw new ArgumentException("Cannot call this method when SelectionMode is SelectionMode.NONE.", nameof(value));
            }

            if (_selectionMode == SelectionMode.One || value == -1)
            {
                if (SelectedIndex == value && _selected.Count <= 1)
                {
                    return;
                }

                _selected.Clear();
            }

            if (value >= 0)
            {
                _selected.Add(value);
                _focusIndex = value;
                _anchorIndex = value;
                _view.EnsureVisible(value);
            }

            Invalidate();
            OnSelectedIndexChanged(EventArgs.Empty);
        }
    }

    /// <summary>The selected item.</summary>
    public object? SelectedItem
    {
        get => SelectedIndex >= 0 ? _items[SelectedIndex] : null;
        set => SelectedIndex = value is null ? -1 : _items.IndexOf(value);
    }

    /// <summary>The text of the selected item; setting it selects the item with that text.</summary>
    [AllowNull]
    public override string Text
    {
        get => SelectedIndex >= 0 ? GetItemText(_items[SelectedIndex]) : base.Text;
        set
        {
            base.Text = value;
            var index = FindStringExact(value ?? string.Empty);
            if (index >= 0 && _selectionMode != SelectionMode.None)
            {
                SelectedIndex = index;
            }
        }
    }

    /// <summary>The first item in view.</summary>
    public int TopIndex
    {
        get => _view.TopIndex;
        set
        {
            _view.TopIndex = value;
            Invalidate();
        }
    }

    /// <summary>The height of an item.</summary>
    public virtual int ItemHeight
    {
        get => _itemHeight > 0 ? _itemHeight : Font.Height;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _itemHeight = value;
            UpdateView();
        }
    }

    /// <summary>Whether the height snaps to a whole number of items.</summary>
    public bool IntegralHeight
    {
        get => _integralHeight;
        set
        {
            _integralHeight = value;
            SetBoundsCore(Left, Top, Width, Height, BoundsSpecified.Height);
        }
    }

    /// <summary>Whether the vertical scroll bar shows even when every item fits.</summary>
    public bool ScrollAlwaysVisible
    {
        get => _scrollAlwaysVisible;
        set
        {
            _scrollAlwaysVisible = value;
            UpdateView();
        }
    }

    /// <summary>Whether the items are sorted by their text.</summary>
    public bool Sorted
    {
        get => _sorted;
        set
        {
            _sorted = value;
            _items.Store.Sorted = value;
            if (value && !IsBound)
            {
                _items.Store.Sort();
            }
        }
    }

    /// <summary>The border.</summary>
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            UpdateClientSize();
            UpdateView();
        }
    }

    /// <summary>Whether the app draws the items, in the DrawItem event.</summary>
    public virtual DrawMode DrawMode { get; set; } = DrawMode.Normal;

    /// <summary>Whether a horizontal scroll bar shows for long items. Long items are cut off.</summary>
    public bool HorizontalScrollbar { get; set; }

    /// <summary>The width of the horizontal scroll range. Kept for compatibility.</summary>
    public int HorizontalExtent { get; set; }

    /// <summary>Whether the items flow into columns. Kept for compatibility: items are shown in one column.</summary>
    public bool MultiColumn { get; set; }

    /// <summary>The width of a column. Kept for compatibility.</summary>
    public int ColumnWidth { get; set; }

    /// <summary>Whether tab characters in items line up at tab stops. Kept for compatibility.</summary>
    public bool UseTabStops { get; set; } = true;

    /// <summary>Whether the items' height follows their font. Kept for compatibility.</summary>
    public bool UseCustomTabOffsets { get; set; }

    /// <summary>The height a list of a number of items needs.</summary>
    public int PreferredHeight => (_items.Count * ItemHeight) + (2 * ThemePainter.BorderWidth(_borderStyle));

    /// <inheritdoc />
    protected override Size DefaultSize => new(120, 96);

    /// <inheritdoc />
    internal override int ItemCount => _items.Count;

    /// <inheritdoc />
    internal override Padding NonClientInsets
    {
        get
        {
            var border = ThemePainter.BorderWidth(_borderStyle);
            return new Padding(border, border, border + (_barShown ? Theme.ScrollBarSize : 0), border);
        }
    }

    /// <inheritdoc />
    internal override bool ScrollsByTouch => _view.Bar.MaximumValue > 0;

    /// <summary>The focused item, which the keyboard moves.</summary>
    internal int FocusIndex => _focusIndex;

    /// <inheritdoc />
    internal override object? ItemAt(int index) => _items[index];

    /// <summary>Starts adding many items without drawing after each.</summary>
    public void BeginUpdate() => _updateCount++;

    /// <summary>Ends adding many items, and draws the list.</summary>
    public void EndUpdate()
    {
        if (_updateCount > 0 && --_updateCount == 0)
        {
            UpdateView();
        }
    }

    /// <summary>Deselects every item.</summary>
    public void ClearSelected()
    {
        if (_selected.Count == 0)
        {
            return;
        }

        _selected.Clear();
        Invalidate();
        OnSelectedIndexChanged(EventArgs.Empty);
    }

    /// <summary>Whether an item is selected.</summary>
    /// <param name="index">The item.</param>
    public bool GetSelected(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _items.Count);
        return _selected.Contains(index);
    }

    /// <summary>Selects or deselects an item.</summary>
    /// <param name="index">The item.</param>
    /// <param name="value">Whether it is selected.</param>
    public void SetSelected(int index, bool value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _items.Count);
        if (_selectionMode == SelectionMode.None)
        {
            throw new InvalidOperationException("Cannot call this method when SelectionMode is SelectionMode.NONE.");
        }

        if (_selectionMode == SelectionMode.One && value)
        {
            SelectedIndex = index;
            return;
        }

        var changed = value ? _selected.Add(index) : _selected.Remove(index);
        if (changed)
        {
            Invalidate();
            OnSelectedIndexChanged(EventArgs.Empty);
        }
    }

    /// <summary>The first item whose text starts with a string, ignoring case, or -1.</summary>
    /// <param name="s">The string.</param>
    public int FindString(string s) => FindString(s, -1);

    /// <summary>The first item after an index whose text starts with a string, ignoring case, or -1.</summary>
    /// <param name="s">The string.</param>
    /// <param name="startIndex">The index to search after, or -1 for the start.</param>
    public int FindString(string s, int startIndex) => Find(s, startIndex, exact: false);

    /// <summary>The first item whose text is a string, ignoring case, or -1.</summary>
    /// <param name="s">The string.</param>
    public int FindStringExact(string s) => FindStringExact(s, -1);

    /// <summary>The first item after an index whose text is a string, ignoring case, or -1.</summary>
    /// <param name="s">The string.</param>
    /// <param name="startIndex">The index to search after, or -1 for the start.</param>
    public int FindStringExact(string s, int startIndex) => Find(s, startIndex, exact: true);

    /// <summary>The item at a point, or -1.</summary>
    /// <param name="p">The point, in client coordinates.</param>
    public int IndexFromPoint(Point p) => _view.IndexAt(p);

    /// <summary>The item at a point, or -1.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    public int IndexFromPoint(int x, int y) => IndexFromPoint(new Point(x, y));

    /// <summary>An item's bounds, in client coordinates.</summary>
    /// <param name="index">The item.</param>
    public Rectangle GetItemRectangle(int index) => _view.RowBounds(index);

    /// <summary>The height of an item.</summary>
    /// <param name="index">The item.</param>
    public int GetItemHeight(int index) => ItemHeight;

    /// <summary>Raises SelectedIndexChanged, then SelectedValueChanged.</summary>
    /// <param name="e">The event data.</param>
    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        ((EventHandler?)Events[EventSelectedIndexChanged])?.Invoke(this, e);
        base.OnSelectedIndexChanged(e);
    }

    /// <summary>Raises DrawItem.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDrawItem(DrawItemEventArgs e) => ((DrawItemEventHandler?)Events[EventDrawItem])?.Invoke(this, e);

    /// <summary>Raises MeasureItem.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMeasureItem(MeasureItemEventArgs e) => ((MeasureItemEventHandler?)Events[EventMeasureItem])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void SetItemsCore(IList items)
    {
        _selected.Clear();
        _items.Store.SetBound(items);
        if (_items.Count > 0 && _selectionMode != SelectionMode.None)
        {
            _selected.Add(0);
            _focusIndex = 0;
        }

        OnSelectedIndexChanged(EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        if (_integralHeight && DrawMode != DrawMode.OwnerDrawVariable)
        {
            var border = 2 * ThemePainter.BorderWidth(_borderStyle);
            var rows = Math.Max(1, (height - border) / ItemHeight);
            height = (rows * ItemHeight) + border;
        }

        base.SetBoundsCore(x, y, width, height, specified);
        UpdateView();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateView();
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        var index = _view.IndexAt(e.Location);
        if (index >= 0 && e.Button == MouseButtons.Left)
        {
            SelectByUser(index, ModifierKeys);
        }

        base.OnMouseDown(e);
    }

    /// <inheritdoc />
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        _view.Offset -= e.Delta / 120 * 3 * ItemHeight;
        Invalidate();
        base.OnMouseWheel(e);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _items.Count == 0)
        {
            return;
        }

        var page = Math.Max(1, ClientSize.Height / ItemHeight);
        var current = _focusIndex < 0 ? -1 : _focusIndex;
        int? target = e.KeyCode switch
        {
            Keys.Up or Keys.Left => Math.Max(0, current - 1),
            Keys.Down or Keys.Right => Math.Min(_items.Count - 1, current + 1),
            Keys.Home => 0,
            Keys.End => _items.Count - 1,
            Keys.PageUp => Math.Max(0, current - page),
            Keys.PageDown => Math.Min(_items.Count - 1, current + page),
            _ => null,
        };

        if (target is { } index)
        {
            if (_selectionMode == SelectionMode.MultiSimple)
            {
                _focusIndex = index;
                _view.EnsureVisible(index);
                Invalidate();
            }
            else
            {
                SelectByUser(index, e.Modifiers & ~Keys.Control);
            }

            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Space && _selectionMode == SelectionMode.MultiSimple && _focusIndex >= 0)
        {
            SetSelected(_focusIndex, !_selected.Contains(_focusIndex));
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    internal override void ScrollByTouch(int dx, int dy)
    {
        _view.Offset += dy;
        Invalidate();
    }

    /// <inheritdoc />
    internal override bool NonClientMouseDown(Point point)
    {
        if (!_view.Bar.PointerDown(point))
        {
            return false;
        }

        Invalidate();
        return true;
    }

    /// <inheritdoc />
    internal override void NonClientMouseMove(Point point)
    {
        _view.Bar.PointerMove(point);
        Invalidate();
    }

    /// <inheritdoc />
    internal override void NonClientMouseUp(Point point)
    {
        _view.Bar.PointerUp();
        Invalidate();
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        using (var background = new SolidBrush(Enabled ? BackColor : SystemColors.Control))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        var focusIndex = Focused && Window?.ShowFocusCues == true ? Math.Max(0, _focusIndex) : -1;
        if (DrawMode != DrawMode.Normal && Events[EventDrawItem] is not null)
        {
            foreach (var index in _view.VisibleRows())
            {
                var state = (_selected.Contains(index) ? DrawItemState.Selected : DrawItemState.None)
                    | (index == focusIndex ? DrawItemState.Focus : DrawItemState.None)
                    | (Enabled ? DrawItemState.None : DrawItemState.Disabled);
                OnDrawItem(new DrawItemEventArgs(g, Font, _view.RowBounds(index), index, state, ForeColor, BackColor));
            }

            return;
        }

        PaintRows(g, focusIndex);
    }

    /// <summary>Draws the rows in the normal look.</summary>
    private protected virtual void PaintRows(Graphics g, int focusIndex) =>
        _view.Paint(g, Font, ForeColor, index => GetItemText(_items[index]), _selected.Contains, -1, focusIndex, Enabled);

    /// <summary>The rows' layout.</summary>
    private protected ItemListView View => _view;

    /// <inheritdoc />
    internal override void PaintNonClient(Graphics g)
    {
        ThemePainter.DrawEditBorder(g, new Rectangle(Point.Empty, Size), _borderStyle, focused: false, Enabled);
        _view.Bar.Paint(g, Enabled);
    }

    /// <summary>Called when the items change.</summary>
    internal void OnItemsChanged()
    {
        _selected.RemoveWhere(index => index >= _items.Count);
        if (_focusIndex >= _items.Count)
        {
            _focusIndex = _items.Count - 1;
        }

        if (_updateCount == 0)
        {
            UpdateView();
        }
    }

    /// <summary>Selects an item as a tap or a key does, with Ctrl and Shift in extended mode.</summary>
    private protected virtual void SelectByUser(int index, Keys modifiers)
    {
        _focusIndex = index;
        _view.EnsureVisible(index);
        switch (_selectionMode)
        {
            case SelectionMode.None:
                Invalidate();
                return;
            case SelectionMode.One:
                SelectedIndex = index;
                return;
            case SelectionMode.MultiSimple:
                SetSelected(index, !_selected.Contains(index));
                return;
        }

        if ((modifiers & Keys.Shift) != 0 && _anchorIndex >= 0)
        {
            _selected.Clear();
            for (var i = Math.Min(_anchorIndex, index); i <= Math.Max(_anchorIndex, index); i++)
            {
                _selected.Add(i);
            }
        }
        else if ((modifiers & Keys.Control) != 0)
        {
            _anchorIndex = index;
            if (!_selected.Remove(index))
            {
                _selected.Add(index);
            }
        }
        else
        {
            _anchorIndex = index;
            _selected.Clear();
            _selected.Add(index);
        }

        Invalidate();
        OnSelectedIndexChanged(EventArgs.Empty);
    }

    private int Find(string s, int startIndex, bool exact)
    {
        ArgumentNullException.ThrowIfNull(s);
        for (var step = 1; step <= _items.Count; step++)
        {
            var i = (startIndex + step) % _items.Count;
            var text = GetItemText(_items[i]);
            if (exact ? string.Equals(text, s, StringComparison.OrdinalIgnoreCase) : text.StartsWith(s, StringComparison.CurrentCultureIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void UpdateView()
    {
        _view.ItemHeight = ItemHeight;
        _view.Count = _items?.Count ?? 0;
        var border = ThemePainter.BorderWidth(_borderStyle);
        var inner = Height - (2 * border);
        var needsBar = _scrollAlwaysVisible || _view.Count * ItemHeight > inner;
        if (needsBar != _barShown)
        {
            _barShown = needsBar;
            UpdateClientSize();
        }

        _view.Bar.Visible = _barShown;
        _view.Bar.Bounds = new Rectangle(Width - border - Theme.ScrollBarSize, border, Theme.ScrollBarSize, Math.Max(0, inner));
        _view.LayoutRows(ClientRectangle);
        Invalidate();
    }

    /// <summary>The items of a list box.</summary>
    [ListBindable(false)]
    public class ObjectCollection : IList
    {
        private readonly ListBox _owner;

        /// <summary>Creates the items of a list box.</summary>
        /// <param name="owner">The list box.</param>
        public ObjectCollection(ListBox owner)
        {
            _owner = owner;
            Store = new ItemStore(owner, owner.OnItemsChanged);
        }

        /// <summary>Creates the items of a list box, with items.</summary>
        /// <param name="owner">The list box.</param>
        /// <param name="value">The items.</param>
        public ObjectCollection(ListBox owner, object[] value)
            : this(owner)
        {
            AddRange(value);
        }

        /// <summary>The number of items.</summary>
        public int Count => Store.Count;

        /// <summary>Whether the collection is read-only.</summary>
        public bool IsReadOnly => false;

        /// <inheritdoc />
        bool IList.IsFixedSize => false;

        /// <inheritdoc />
        bool ICollection.IsSynchronized => false;

        /// <inheritdoc />
        object ICollection.SyncRoot => this;

        internal ItemStore Store { get; }

        /// <summary>The item at an index.</summary>
        /// <param name="index">The index.</param>
        public virtual object this[int index]
        {
            get => Store[index];
            set
            {
                Store[index] = value;
                _owner.Invalidate();
            }
        }

        /// <summary>Adds an item.</summary>
        /// <param name="item">The item.</param>
        /// <returns>Its index.</returns>
        public int Add(object item) => Store.Add(item);

        /// <summary>Adds items.</summary>
        /// <param name="items">The items.</param>
        public void AddRange(object[] items) => Store.AddRange(items);

        /// <summary>Adds the items of another collection.</summary>
        /// <param name="value">The items.</param>
        public void AddRange(ObjectCollection value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Store.AddRange(value.Cast<object>().ToList());
        }

        /// <summary>Inserts an item.</summary>
        /// <param name="index">Where.</param>
        /// <param name="item">The item.</param>
        public void Insert(int index, object item) => Store.Insert(index, item);

        /// <summary>Removes an item.</summary>
        /// <param name="value">The item.</param>
        public void Remove(object value) => Store.Remove(value);

        /// <summary>Removes the item at an index.</summary>
        /// <param name="index">The index.</param>
        public void RemoveAt(int index) => Store.RemoveAt(index);

        /// <summary>Removes every item.</summary>
        public virtual void Clear() => Store.Clear();

        /// <summary>Whether an item is in the list.</summary>
        /// <param name="value">The item.</param>
        public bool Contains(object value) => Store.Contains(value);

        /// <summary>The index of an item, or -1.</summary>
        /// <param name="value">The item.</param>
        public int IndexOf(object value) => Store.IndexOf(value);

        /// <summary>Copies the items into an array.</summary>
        /// <param name="destination">The array.</param>
        /// <param name="arrayIndex">Where to start.</param>
        public void CopyTo(object[] destination, int arrayIndex) => Store.CopyTo(destination, arrayIndex);

        /// <inheritdoc />
        void ICollection.CopyTo(Array destination, int index) => Store.CopyTo(destination, index);

        /// <inheritdoc />
        public IEnumerator GetEnumerator() => Store.GetEnumerator();

        /// <inheritdoc />
        object? IList.this[int index]
        {
            get => this[index];
            set => this[index] = value!;
        }

        /// <inheritdoc />
        int IList.Add(object? value) => Add(value!);

        /// <inheritdoc />
        bool IList.Contains(object? value) => value is not null && Contains(value);

        /// <inheritdoc />
        int IList.IndexOf(object? value) => value is null ? -1 : IndexOf(value);

        /// <inheritdoc />
        void IList.Insert(int index, object? value) => Insert(index, value!);

        /// <inheritdoc />
        void IList.Remove(object? value)
        {
            if (value is not null)
            {
                Remove(value);
            }
        }
    }

    /// <summary>The indexes of the selected items of a list box.</summary>
    public class SelectedIndexCollection : IList
    {
        private readonly ListBox _owner;

        /// <summary>Creates the collection.</summary>
        /// <param name="owner">The list box.</param>
        public SelectedIndexCollection(ListBox owner) => _owner = owner;

        /// <summary>The number of selected items.</summary>
        public int Count => _owner._selected.Count;

        /// <summary>Whether the collection is read-only.</summary>
        public bool IsReadOnly => true;

        /// <inheritdoc />
        bool IList.IsFixedSize => true;

        /// <inheritdoc />
        bool ICollection.IsSynchronized => false;

        /// <inheritdoc />
        object ICollection.SyncRoot => this;

        /// <summary>The index of the nth selected item.</summary>
        /// <param name="index">n.</param>
        public int this[int index] => _owner._selected.ElementAt(index);

        /// <inheritdoc />
        object? IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException("The collection is read-only.");
        }

        /// <summary>Selects an item.</summary>
        /// <param name="index">The item.</param>
        public void Add(int index) => _owner.SetSelected(index, true);

        /// <summary>Deselects an item.</summary>
        /// <param name="index">The item.</param>
        public void Remove(int index) => _owner.SetSelected(index, false);

        /// <summary>Deselects every item.</summary>
        public void Clear() => _owner.ClearSelected();

        /// <summary>Whether an item is selected.</summary>
        /// <param name="selectedIndex">The item.</param>
        public bool Contains(int selectedIndex) => _owner._selected.Contains(selectedIndex);

        /// <summary>The position of an item among the selected ones, or -1.</summary>
        /// <param name="selectedIndex">The item.</param>
        public int IndexOf(int selectedIndex) => _owner._selected.ToList().IndexOf(selectedIndex);

        /// <summary>Copies the indexes into an array.</summary>
        /// <param name="destination">The array.</param>
        /// <param name="index">Where to start.</param>
        public void CopyTo(Array destination, int index) => ((ICollection)_owner._selected.ToArray()).CopyTo(destination, index);

        /// <inheritdoc />
        public IEnumerator GetEnumerator() => _owner._selected.ToList().GetEnumerator();

        /// <inheritdoc />
        int IList.Add(object? value) => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        void IList.Clear() => Clear();

        /// <inheritdoc />
        bool IList.Contains(object? value) => value is int index && Contains(index);

        /// <inheritdoc />
        int IList.IndexOf(object? value) => value is int index ? IndexOf(index) : -1;

        /// <inheritdoc />
        void IList.Insert(int index, object? value) => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        void IList.Remove(object? value)
        {
            if (value is int index)
            {
                Remove(index);
            }
        }

        /// <inheritdoc />
        void IList.RemoveAt(int index) => throw new NotSupportedException("The collection is read-only.");
    }

    /// <summary>The selected items of a list box.</summary>
    public class SelectedObjectCollection : IList
    {
        private readonly ListBox _owner;

        /// <summary>Creates the collection.</summary>
        /// <param name="owner">The list box.</param>
        public SelectedObjectCollection(ListBox owner) => _owner = owner;

        /// <summary>The number of selected items.</summary>
        public int Count => _owner._selected.Count;

        /// <summary>Whether the collection is read-only.</summary>
        public bool IsReadOnly => true;

        /// <inheritdoc />
        bool IList.IsFixedSize => true;

        /// <inheritdoc />
        bool ICollection.IsSynchronized => false;

        /// <inheritdoc />
        object ICollection.SyncRoot => this;

        /// <summary>The nth selected item.</summary>
        /// <param name="index">n.</param>
        public object this[int index] => _owner._items[_owner._selected.ElementAt(index)];

        /// <inheritdoc />
        object? IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException("The collection is read-only.");
        }

        /// <summary>Selects an item.</summary>
        /// <param name="value">The item.</param>
        public void Add(object value) => _owner.SetSelected(_owner._items.IndexOf(value), true);

        /// <summary>Deselects an item.</summary>
        /// <param name="value">The item.</param>
        public void Remove(object value)
        {
            var index = _owner._items.IndexOf(value);
            if (index >= 0)
            {
                _owner.SetSelected(index, false);
            }
        }

        /// <summary>Deselects every item.</summary>
        public void Clear() => _owner.ClearSelected();

        /// <summary>Whether an item is selected.</summary>
        /// <param name="selectedObject">The item.</param>
        public bool Contains(object selectedObject) => IndexOf(selectedObject) >= 0;

        /// <summary>The position of an item among the selected ones, or -1.</summary>
        /// <param name="selectedObject">The item.</param>
        public int IndexOf(object selectedObject) => Items().IndexOf(selectedObject);

        /// <summary>Copies the items into an array.</summary>
        /// <param name="destination">The array.</param>
        /// <param name="index">Where to start.</param>
        public void CopyTo(Array destination, int index) => ((ICollection)Items()).CopyTo(destination, index);

        /// <inheritdoc />
        public IEnumerator GetEnumerator() => Items().GetEnumerator();

        /// <inheritdoc />
        int IList.Add(object? value) => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        void IList.Clear() => Clear();

        /// <inheritdoc />
        bool IList.Contains(object? value) => value is not null && Contains(value);

        /// <inheritdoc />
        int IList.IndexOf(object? value) => value is null ? -1 : IndexOf(value);

        /// <inheritdoc />
        void IList.Insert(int index, object? value) => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        void IList.Remove(object? value)
        {
            if (value is not null)
            {
                Remove(value);
            }
        }

        /// <inheritdoc />
        void IList.RemoveAt(int index) => throw new NotSupportedException("The collection is read-only.");

        private List<object> Items() => [.. _owner._selected.Select(index => _owner._items[index])];
    }
}
