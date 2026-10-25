using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>
/// A box that shows the selected item and opens a list to pick another. With <see cref="ComboBoxStyle.DropDown"/>
/// the text can also be typed, in the device's own text box.
/// </summary>
[DefaultEvent(nameof(SelectedIndexChanged))]
[DefaultProperty(nameof(Items))]
public class ComboBox : ListControl, INativeTextEditor
{
    private static readonly object EventSelectedIndexChanged = new();
    private static readonly object EventSelectionChangeCommitted = new();
    private static readonly object EventDropDown = new();
    private static readonly object EventDropDownClosed = new();
    private static readonly object EventTextUpdate = new();
    private static readonly object EventDrawItem = new();
    private static readonly object EventMeasureItem = new();
    private static readonly object EventDropDownStyleChanged = new();

    /// <summary>The width of the arrow button.</summary>
    internal const int ButtonWidth = 17;

    private readonly ObjectCollection _items;
    private readonly EditableText _edit = new();
    private ComboBoxStyle _style = ComboBoxStyle.DropDown;
    private int _selectedIndex = -1;
    private int _maxDropDownItems = 8;
    private int _dropDownWidth = -1;
    private int _dropDownHeight = 106;
    private int _itemHeight = -1;
    private int _maxLength;
    private int _updateCount;
    private bool _sorted;
    private bool _hot;
    private ComboListPopup? _popup;

    /// <summary>Creates a combo box.</summary>
    public ComboBox()
    {
        _items = new ObjectCollection(this);
        SetStyle(ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
    }

    /// <summary>Occurs when the selected item changes.</summary>
    public event EventHandler? SelectedIndexChanged
    {
        add => Events.AddHandler(EventSelectedIndexChanged, value);
        remove => Events.RemoveHandler(EventSelectedIndexChanged, value);
    }

    /// <summary>Occurs when the user picks an item, not when the app selects one.</summary>
    public event EventHandler? SelectionChangeCommitted
    {
        add => Events.AddHandler(EventSelectionChangeCommitted, value);
        remove => Events.RemoveHandler(EventSelectionChangeCommitted, value);
    }

    /// <summary>Occurs when the list opens.</summary>
    public event EventHandler? DropDown
    {
        add => Events.AddHandler(EventDropDown, value);
        remove => Events.RemoveHandler(EventDropDown, value);
    }

    /// <summary>Occurs when the list closes.</summary>
    public event EventHandler? DropDownClosed
    {
        add => Events.AddHandler(EventDropDownClosed, value);
        remove => Events.RemoveHandler(EventDropDownClosed, value);
    }

    /// <summary>Occurs when the user changes the text, before it is shown.</summary>
    public event EventHandler? TextUpdate
    {
        add => Events.AddHandler(EventTextUpdate, value);
        remove => Events.RemoveHandler(EventTextUpdate, value);
    }

    /// <summary>Occurs when an item of an owner-drawn combo box is drawn.</summary>
    public event DrawItemEventHandler? DrawItem
    {
        add => Events.AddHandler(EventDrawItem, value);
        remove => Events.RemoveHandler(EventDrawItem, value);
    }

    /// <summary>Occurs when the height of an item of an owner-drawn combo box is needed. Rows have one height.</summary>
    public event MeasureItemEventHandler? MeasureItem
    {
        add => Events.AddHandler(EventMeasureItem, value);
        remove => Events.RemoveHandler(EventMeasureItem, value);
    }

    /// <summary>Occurs when <see cref="DropDownStyle"/> changes.</summary>
    public event EventHandler? DropDownStyleChanged
    {
        add => Events.AddHandler(EventDropDownStyleChanged, value);
        remove => Events.RemoveHandler(EventDropDownStyleChanged, value);
    }

    /// <summary>The items.</summary>
    public ObjectCollection Items => _items;

    /// <summary>Whether the text can be typed, or only picked from the list.</summary>
    public ComboBoxStyle DropDownStyle
    {
        get => _style;
        set
        {
            if (_style == value)
            {
                return;
            }

            _style = value;
            if (value == ComboBoxStyle.DropDownList)
            {
                _edit.SetFromApp(SelectedIndex >= 0 ? GetItemText(_items[SelectedIndex]) : string.Empty);
            }

            Invalidate();
            ((EventHandler?)Events[EventDropDownStyleChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public override int SelectedIndex
    {
        get => _selectedIndex;
        set => SelectIndex(value, byUser: false);
    }

    /// <summary>The selected item.</summary>
    public object? SelectedItem
    {
        get => _selectedIndex >= 0 ? _items[_selectedIndex] : null;
        set => SelectedIndex = value is null ? -1 : _items.IndexOf(value);
    }

    /// <summary>The text: the selected item's, or what was typed.</summary>
    [AllowNull]
    public override string Text
    {
        get => _style == ComboBoxStyle.DropDownList ? (_selectedIndex >= 0 ? GetItemText(_items[_selectedIndex]) : string.Empty) : _edit.Text;
        set
        {
            value ??= string.Empty;
            var index = FindStringExact(value);
            if (_style == ComboBoxStyle.DropDownList)
            {
                if (index >= 0 || value.Length == 0)
                {
                    SelectedIndex = index;
                }

                return;
            }

            if (index >= 0 && index != _selectedIndex)
            {
                SelectedIndex = index;
                return;
            }

            if (_edit.SetFromApp(value))
            {
                if (_selectedIndex >= 0 && !string.Equals(GetItemText(_items[_selectedIndex]), value, StringComparison.Ordinal))
                {
                    _selectedIndex = -1;
                }

                Invalidate();
                OnTextChanged(EventArgs.Empty);
            }
        }
    }

    /// <summary>The selected part of the typed text.</summary>
    [AllowNull]
    public string SelectedText
    {
        get => _style == ComboBoxStyle.DropDownList ? string.Empty : Text.Substring(SelectionStart, SelectionLength);
        set
        {
            if (_style == ComboBoxStyle.DropDownList)
            {
                return;
            }

            var start = SelectionStart;
            var insert = value ?? string.Empty;
            Text = Text.Remove(start, SelectionLength).Insert(start, insert);
            Select(start + insert.Length, 0);
        }
    }

    /// <summary>Where the selection of the typed text starts.</summary>
    public int SelectionStart
    {
        get => _edit.SelectionStart;
        set => Select(value, SelectionLength);
    }

    /// <summary>The length of the selection of the typed text.</summary>
    public int SelectionLength
    {
        get => _edit.SelectionLength;
        set => Select(SelectionStart, value);
    }

    /// <summary>Whether the list is open.</summary>
    public bool DroppedDown
    {
        get => _popup is not null;
        set
        {
            if (value)
            {
                OpenList();
            }
            else
            {
                _popup?.Close();
            }
        }
    }

    /// <summary>The most items the list shows before it scrolls.</summary>
    public int MaxDropDownItems
    {
        get => _maxDropDownItems;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
            _maxDropDownItems = value;
        }
    }

    /// <summary>The width of the list: the box's width unless set wider.</summary>
    public int DropDownWidth
    {
        get => _dropDownWidth < 0 ? Width : _dropDownWidth;
        set => _dropDownWidth = value;
    }

    /// <summary>The height of the list, when <see cref="IntegralHeight"/> is off.</summary>
    public int DropDownHeight
    {
        get => _dropDownHeight;
        set => _dropDownHeight = value;
    }

    /// <summary>Whether the list's height is a whole number of items, up to <see cref="MaxDropDownItems"/>.</summary>
    public bool IntegralHeight { get; set; } = true;

    /// <summary>The height of an item of the list.</summary>
    public int ItemHeight
    {
        get => _itemHeight > 0 ? _itemHeight : Font.Height;
        set => _itemHeight = value;
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

    /// <summary>The most characters that can be typed; 0 for no limit.</summary>
    public int MaxLength
    {
        get => _maxLength;
        set => _maxLength = Math.Max(0, value);
    }

    /// <summary>Whether the app draws the items, in the DrawItem event.</summary>
    public DrawMode DrawMode { get; set; } = DrawMode.Normal;

    /// <summary>The look. Every style draws the Windows 10 look.</summary>
    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

    /// <summary>How typing completes items. Kept for compatibility.</summary>
    public AutoCompleteMode AutoCompleteMode { get; set; }

    /// <summary>Where completions come from. Kept for compatibility.</summary>
    public AutoCompleteSource AutoCompleteSource { get; set; } = AutoCompleteSource.None;

    /// <summary>The completions of a custom source. Kept for compatibility.</summary>
    public AutoCompleteStringCollection AutoCompleteCustomSource { get; set; } = [];

    /// <summary>The height of the box in its font.</summary>
    public int PreferredHeight => Font.Height + 8;

    /// <inheritdoc />
    protected override Size DefaultSize => new(121, 23);

    /// <inheritdoc />
    internal override int ItemCount => _items.Count;

    /// <summary>The text area, which the native text box covers while typing.</summary>
    internal Rectangle TextArea => new(3, 2, Math.Max(0, Width - ButtonWidth - 5), Math.Max(0, Height - 4));

    private bool IsEditable => _style != ComboBoxStyle.DropDownList;

    /// <inheritdoc />
    internal override object? ItemAt(int index) => _items[index];

    /// <summary>Starts adding many items without drawing after each.</summary>
    public void BeginUpdate() => _updateCount++;

    /// <summary>Ends adding many items.</summary>
    public void EndUpdate()
    {
        if (_updateCount > 0 && --_updateCount == 0)
        {
            Invalidate();
        }
    }

    /// <summary>The first item whose text starts with a string, ignoring case, or -1.</summary>
    /// <param name="s">The string.</param>
    public int FindString(string? s) => FindString(s, -1);

    /// <summary>The first item after an index whose text starts with a string, ignoring case, or -1.</summary>
    /// <param name="s">The string.</param>
    /// <param name="startIndex">The index to search after, or -1 for the start.</param>
    public int FindString(string? s, int startIndex) => Find(s, startIndex, exact: false);

    /// <summary>The first item whose text is a string, ignoring case, or -1.</summary>
    /// <param name="s">The string.</param>
    public int FindStringExact(string? s) => FindStringExact(s, -1);

    /// <summary>The first item after an index whose text is a string, ignoring case, or -1.</summary>
    /// <param name="s">The string.</param>
    /// <param name="startIndex">The index to search after, or -1 for the start.</param>
    public int FindStringExact(string? s, int startIndex) => Find(s, startIndex, exact: true);

    /// <summary>The height of an item.</summary>
    /// <param name="index">The item.</param>
    public int GetItemHeight(int index) => ItemHeight;

    /// <summary>Selects part of the typed text.</summary>
    /// <param name="start">The start.</param>
    /// <param name="length">The length.</param>
    public void Select(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _edit.Select(start, length);
    }

    /// <summary>Selects all the typed text.</summary>
    public void SelectAll() => Select(0, Text.Length);

    /// <summary>Raises SelectedIndexChanged, then SelectedValueChanged.</summary>
    /// <param name="e">The event data.</param>
    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        ((EventHandler?)Events[EventSelectedIndexChanged])?.Invoke(this, e);
        base.OnSelectedIndexChanged(e);
    }

    /// <summary>Raises SelectionChangeCommitted.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSelectionChangeCommitted(EventArgs e) => ((EventHandler?)Events[EventSelectionChangeCommitted])?.Invoke(this, e);

    /// <summary>Raises DropDown.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDropDown(EventArgs e) => ((EventHandler?)Events[EventDropDown])?.Invoke(this, e);

    /// <summary>Raises DropDownClosed.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDropDownClosed(EventArgs e) => ((EventHandler?)Events[EventDropDownClosed])?.Invoke(this, e);

    /// <summary>Raises TextUpdate.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnTextUpdate(EventArgs e) => ((EventHandler?)Events[EventTextUpdate])?.Invoke(this, e);

    /// <summary>Raises DrawItem.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDrawItem(DrawItemEventArgs e) => ((DrawItemEventHandler?)Events[EventDrawItem])?.Invoke(this, e);

    /// <summary>Raises MeasureItem.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMeasureItem(MeasureItemEventArgs e) => ((MeasureItemEventHandler?)Events[EventMeasureItem])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void SetItemsCore(IList items)
    {
        _items.Store.SetBound(items);
        SelectIndex(_items.Count > 0 ? 0 : -1, byUser: false);
    }

    /// <inheritdoc />
    protected override void RefreshItem(int index)
    {
        if (index == _selectedIndex)
        {
            _edit.SetFromApp(GetItemText(_items[index]));
        }

        base.RefreshItem(index);
    }

    /// <summary>Keeps the height at the font's.</summary>
    /// <inheritdoc />
    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        if (_style != ComboBoxStyle.Simple)
        {
            height = PreferredHeight;
        }

        base.SetBoundsCore(x, y, width, height, specified);
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Height = PreferredHeight;
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData)
    {
        if ((keyData & Keys.Alt) != 0)
        {
            return (keyData & Keys.KeyCode) is Keys.Down or Keys.Up;
        }

        return (keyData & Keys.KeyCode) switch
        {
            Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.F4 => true,
            Keys.Home or Keys.End or Keys.Left or Keys.Right or Keys.Delete or Keys.Back => IsEditable,
            Keys.Enter or Keys.Escape => DroppedDown,
            _ => base.IsInputKey(keyData),
        };
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        if (e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode is Keys.Down or Keys.Up))
        {
            DroppedDown = !DroppedDown;
            e.Handled = true;
            return;
        }

        if (_items.Count == 0)
        {
            return;
        }

        int? target = e.KeyCode switch
        {
            Keys.Up => Math.Max(0, _selectedIndex - 1),
            Keys.Down => Math.Min(_items.Count - 1, _selectedIndex + 1),
            Keys.PageUp => Math.Max(0, _selectedIndex - _maxDropDownItems),
            Keys.PageDown => Math.Min(_items.Count - 1, Math.Max(0, _selectedIndex) + _maxDropDownItems),
            Keys.Home when !IsEditable => 0,
            Keys.End when !IsEditable => _items.Count - 1,
            _ => null,
        };

        if (target is { } index)
        {
            SelectIndex(index, byUser: true);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (e.Handled || IsEditable || char.IsControl(e.KeyChar))
        {
            return;
        }

        // A list-only combo box selects the next item starting with a typed letter.
        var index = FindString(new string(e.KeyChar, 1), _selectedIndex);
        if (index >= 0)
        {
            SelectIndex(index, byUser: true);
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();
        if (!IsEditable || e.X >= Width - ButtonWidth - 2)
        {
            DroppedDown = !DroppedDown;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hot = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = false;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        if (IsEditable)
        {
            SelectAll();
        }

        base.OnGotFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _popup?.Close();
        Invalidate();
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var bounds = ClientRectangle;
        var state = !Enabled ? PushState.Disabled : DroppedDown ? PushState.Pressed : _hot ? PushState.Hot : PushState.Normal;
        if (IsEditable)
        {
            using (var background = new SolidBrush(Enabled ? BackColor : SystemColors.Control))
            {
                g.FillRectangle(background, bounds);
            }

            ThemePainter.DrawEditBorder(g, bounds, BorderStyle.Fixed3D, Focused || DroppedDown || _hot, Enabled);
            var button = new Rectangle(bounds.Right - ButtonWidth - 1, 1, ButtonWidth, bounds.Height - 2);
            if (state is PushState.Hot or PushState.Pressed)
            {
                using var face = new SolidBrush(state == PushState.Pressed ? Theme.ButtonPressedFace : Theme.ButtonHotFace);
                g.FillRectangle(face, button);
            }

            ThemePainter.DrawArrow(g, button, ArrowDirection.Down, Enabled ? Theme.Glyph : Theme.DisabledText);
            if (!(Focused && Window is not null))
            {
                ThemePainter.DrawText(g, _edit.Text, Font, Enabled ? ForeColor : Theme.DisabledText, TextArea, ContentAlignment.MiddleLeft, mnemonics: false);
            }

            return;
        }

        ThemePainter.DrawButton(g, bounds, state, emphasized: false, face: IsBackColorSet ? BackColor : null);
        var arrow = new Rectangle(bounds.Right - ButtonWidth - 1, 0, ButtonWidth, bounds.Height);
        ThemePainter.DrawArrow(g, arrow, ArrowDirection.Down, Enabled ? Theme.Glyph : Theme.DisabledText);
        var text = Rectangle.FromLTRB(3, 3, bounds.Right - ButtonWidth - 2, bounds.Bottom - 3);
        var focused = Focused && !DroppedDown;
        if (DrawMode != DrawMode.Normal && Events[EventDrawItem] is not null && _selectedIndex >= 0)
        {
            var itemState = DrawItemState.ComboBoxEdit | (focused ? DrawItemState.Selected | DrawItemState.Focus : DrawItemState.None);
            OnDrawItem(new DrawItemEventArgs(g, Font, text, _selectedIndex, itemState, ForeColor, BackColor));
            return;
        }

        if (focused)
        {
            using var highlight = new SolidBrush(Theme.Selection);
            g.FillRectangle(highlight, text);
        }

        var color = !Enabled ? Theme.DisabledText : focused ? Theme.SelectionText : ForeColor;
        ThemePainter.DrawText(g, Text, Font, color, Rectangle.FromLTRB(text.Left + 1, text.Top, text.Right, text.Bottom), ContentAlignment.MiddleLeft, mnemonics: false);
    }

    /// <summary>Selects an item, raising SelectionChangeCommitted when the user picked it.</summary>
    internal void SelectIndex(int index, bool byUser)
    {
        if (index < -1 || index >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"InvalidArgument=Value of '{index}' is not valid for 'SelectedIndex'.");
        }

        if (index == _selectedIndex)
        {
            return;
        }

        _selectedIndex = index;
        var textChanged = _edit.SetFromApp(index >= 0 ? GetItemText(_items[index]) : string.Empty);
        if (IsEditable && Focused)
        {
            _edit.Select(0, _edit.Text.Length);
        }

        _popup?.SyncSelection();
        Invalidate();
        if (byUser)
        {
            OnSelectionChangeCommitted(EventArgs.Empty);
        }

        if (textChanged)
        {
            OnTextChanged(EventArgs.Empty);
        }

        OnSelectedIndexChanged(EventArgs.Empty);
    }

    /// <summary>Called when the list closes.</summary>
    internal void OnListClosed()
    {
        _popup = null;
        Invalidate();
        OnDropDownClosed(EventArgs.Empty);
    }

    /// <summary>Draws an item in the open list, through DrawItem when the app draws the items.</summary>
    internal bool DrawListItem(Graphics g, int index, Rectangle bounds, bool selected)
    {
        if (DrawMode == DrawMode.Normal || Events[EventDrawItem] is null)
        {
            return false;
        }

        OnDrawItem(new DrawItemEventArgs(g, Font, bounds, index, selected ? DrawItemState.Selected : DrawItemState.None, ForeColor, BackColor));
        return true;
    }

    /// <summary>Called when the items change.</summary>
    internal void OnItemsChanged()
    {
        if (_selectedIndex >= _items.Count)
        {
            _selectedIndex = _items.Count - 1;
        }

        _popup?.SyncItems();
        if (_updateCount == 0)
        {
            Invalidate();
        }
    }

    /// <inheritdoc />
    EditorFrame? INativeTextEditor.GetEditorFrame(Point clientOrigin) =>
        IsEditable && Enabled ? _edit.Frame(this, clientOrigin, TextArea, HorizontalAlignment.Left, readOnly: false, _maxLength == 0 ? 32767 : _maxLength, BackColor) : null;

    /// <inheritdoc />
    void INativeTextEditor.ApplyEditorText(string text, int selectionStart, int selectionLength, int revision)
    {
        if (!_edit.ApplyFromEditor(text, selectionStart, selectionLength, revision))
        {
            return;
        }

        OnTextUpdate(EventArgs.Empty);
        if (_selectedIndex >= 0 && !string.Equals(GetItemText(_items[_selectedIndex]), text, StringComparison.Ordinal))
        {
            _selectedIndex = -1;
            _popup?.SyncSelection();
        }

        OnTextChanged(EventArgs.Empty);
    }

    /// <inheritdoc />
    void INativeTextEditor.ApplyEditorSelection(int selectionStart, int selectionLength) => _edit.ApplySelection(selectionStart, selectionLength);

    private void OpenList()
    {
        if (_popup is not null || Window is not { } window || !Enabled)
        {
            return;
        }

        OnDropDown(EventArgs.Empty);
        var origin = ClientOriginInWindow();
        var rows = Math.Clamp(_items.Count, 1, _maxDropDownItems);
        var height = IntegralHeight ? (rows * ItemHeight) + 2 : Math.Min(_dropDownHeight, (Math.Max(1, _items.Count) * ItemHeight) + 2);
        var width = Math.Max(DropDownWidth, Width);
        var screen = window.Form.ClientOriginOnScreen;
        var desktop = window.Runtime.DesktopSize;
        var y = origin.Y + Height;
        if (screen.Y + y + height > desktop.Height && screen.Y + origin.Y - height >= 0)
        {
            y = origin.Y - height;
        }

        _popup = new ComboListPopup(this) { Bounds = new Rectangle(origin.X, y, width, height) };
        window.OpenPopup(_popup);
        _popup.SyncItems();
        Invalidate();
    }

    private int Find(string? s, int startIndex, bool exact)
    {
        if (string.IsNullOrEmpty(s) && !exact)
        {
            return -1;
        }

        s ??= string.Empty;
        for (var step = 1; step <= _items.Count; step++)
        {
            var i = (startIndex + step + _items.Count) % _items.Count;
            var text = GetItemText(_items[i]);
            if (exact ? string.Equals(text, s, StringComparison.CurrentCultureIgnoreCase) : text.StartsWith(s, StringComparison.CurrentCultureIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The items of a combo box.</summary>
    [ListBindable(false)]
    public class ObjectCollection : IList
    {
        private readonly ComboBox _owner;

        /// <summary>Creates the items of a combo box.</summary>
        /// <param name="owner">The combo box.</param>
        public ObjectCollection(ComboBox owner)
        {
            _owner = owner;
            Store = new ItemStore(owner, owner.OnItemsChanged);
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
                if (index == _owner._selectedIndex)
                {
                    _owner._edit.SetFromApp(_owner.GetItemText(value));
                }
            }
        }

        /// <summary>Adds an item.</summary>
        /// <param name="item">The item.</param>
        /// <returns>Its index.</returns>
        public int Add(object item) => Store.Add(item);

        /// <summary>Adds items.</summary>
        /// <param name="items">The items.</param>
        public void AddRange(object[] items) => Store.AddRange(items);

        /// <summary>Adds items.</summary>
        /// <param name="items">The items.</param>
        public void AddRange(IList items) => Store.AddRange(items);

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
        public void Clear()
        {
            Store.Clear();
            _owner.SelectIndex(-1, byUser: false);
        }

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
}
