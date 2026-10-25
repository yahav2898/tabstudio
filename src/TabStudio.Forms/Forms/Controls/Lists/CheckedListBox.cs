using System.Collections;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>A list box with a check box beside each item.</summary>
public class CheckedListBox : ListBox
{
    private static readonly object EventItemCheck = new();
    private readonly Dictionary<object, CheckState> _states = new(ReferenceEqualityComparer.Instance);
    private int _lastClicked = -1;

    /// <summary>Creates a checked list box.</summary>
    public CheckedListBox()
    {
    }

    /// <summary>Occurs before an item's check state changes; handlers may change the new state.</summary>
    public event ItemCheckEventHandler? ItemCheck
    {
        add => Events.AddHandler(EventItemCheck, value);
        remove => Events.RemoveHandler(EventItemCheck, value);
    }

    /// <summary>Whether a tap checks an item at once, rather than selecting it first.</summary>
    public bool CheckOnClick { get; set; }

    /// <summary>Whether the check boxes have a three-dimensional look. Kept for compatibility.</summary>
    public bool ThreeDCheckBoxes { get; set; }

    /// <summary>The indexes of the checked items, and of the indeterminate ones.</summary>
    public CheckedIndexCollection CheckedIndices => new(this);

    /// <summary>The checked items, and the indeterminate ones.</summary>
    public CheckedItemCollection CheckedItems => new(this);

    /// <inheritdoc />
    public override SelectionMode SelectionMode
    {
        get => base.SelectionMode;
        set
        {
            if (value is SelectionMode.MultiSimple or SelectionMode.MultiExtended)
            {
                throw new ArgumentException("Multi selection is not supported on CheckedListBox.", nameof(value));
            }

            base.SelectionMode = value;
        }
    }

    /// <summary>Whether an item is checked or indeterminate.</summary>
    /// <param name="index">The item.</param>
    public bool GetItemChecked(int index) => GetItemCheckState(index) != CheckState.Unchecked;

    /// <summary>An item's check state.</summary>
    /// <param name="index">The item.</param>
    public CheckState GetItemCheckState(int index) => _states.GetValueOrDefault(Items[index], CheckState.Unchecked);

    /// <summary>Checks or unchecks an item.</summary>
    /// <param name="index">The item.</param>
    /// <param name="value">Whether it is checked.</param>
    public void SetItemChecked(int index, bool value) => SetItemCheckState(index, value ? CheckState.Checked : CheckState.Unchecked);

    /// <summary>Sets an item's check state, raising ItemCheck first.</summary>
    /// <param name="index">The item.</param>
    /// <param name="value">The state.</param>
    public void SetItemCheckState(int index, CheckState value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Items.Count);
        var current = GetItemCheckState(index);
        if (current == value)
        {
            return;
        }

        var e = new ItemCheckEventArgs(index, value, current);
        OnItemCheck(e);
        if (e.NewValue == current)
        {
            return;
        }

        _states[Items[index]] = e.NewValue;
        Invalidate();
    }

    /// <summary>Raises ItemCheck.</summary>
    /// <param name="ice">The event data.</param>
    protected virtual void OnItemCheck(ItemCheckEventArgs ice) => ((ItemCheckEventHandler?)Events[EventItemCheck])?.Invoke(this, ice);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && SelectedIndex >= 0)
        {
            Toggle(SelectedIndex);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    private protected override void SelectByUser(int index, Keys modifiers)
    {
        var wasSelected = SelectedIndex == index && _lastClicked == index;
        base.SelectByUser(index, modifiers);
        if (CheckOnClick || wasSelected)
        {
            Toggle(index);
        }

        _lastClicked = index;
    }

    /// <inheritdoc />
    private protected override void PaintRows(Graphics g, int focusIndex)
    {
        View.Paint(g, Font, ForeColor, index => GetItemText(Items[index]), index => index == SelectedIndex, -1, focusIndex, Enabled, textLeft: ThemePainter.GlyphSize + 5);
        foreach (var index in View.VisibleRows())
        {
            var row = View.RowBounds(index);
            var box = new Rectangle(row.X + 2, row.Y + ((row.Height - ThemePainter.GlyphSize) / 2), ThemePainter.GlyphSize, ThemePainter.GlyphSize);
            ThemePainter.DrawCheckBox(g, box, GetItemCheckState(index), Enabled ? PushState.Normal : PushState.Disabled);
        }
    }

    private void Toggle(int index) =>
        SetItemCheckState(index, GetItemCheckState(index) == CheckState.Unchecked ? CheckState.Checked : CheckState.Unchecked);

    private List<int> Checked() => [.. Enumerable.Range(0, Items.Count).Where(GetItemChecked)];

    /// <summary>The indexes of a checked list box's checked items.</summary>
    public class CheckedIndexCollection : IList
    {
        private readonly CheckedListBox _owner;

        internal CheckedIndexCollection(CheckedListBox owner) => _owner = owner;

        /// <summary>The number of checked items.</summary>
        public int Count => _owner.Checked().Count;

        /// <summary>Whether the collection is read-only.</summary>
        public bool IsReadOnly => true;

        /// <inheritdoc />
        bool IList.IsFixedSize => true;

        /// <inheritdoc />
        bool ICollection.IsSynchronized => false;

        /// <inheritdoc />
        object ICollection.SyncRoot => this;

        /// <summary>The index of the nth checked item.</summary>
        /// <param name="index">n.</param>
        public int this[int index] => _owner.Checked()[index];

        /// <inheritdoc />
        object? IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException("The collection is read-only.");
        }

        /// <summary>Whether an item is checked.</summary>
        /// <param name="index">The item.</param>
        public bool Contains(int index) => _owner.Checked().Contains(index);

        /// <summary>The position of an item among the checked ones, or -1.</summary>
        /// <param name="index">The item.</param>
        public int IndexOf(int index) => _owner.Checked().IndexOf(index);

        /// <summary>Copies the indexes into an array.</summary>
        /// <param name="dest">The array.</param>
        /// <param name="index">Where to start.</param>
        public void CopyTo(Array dest, int index) => ((ICollection)_owner.Checked()).CopyTo(dest, index);

        /// <inheritdoc />
        public IEnumerator GetEnumerator() => _owner.Checked().GetEnumerator();

        /// <inheritdoc />
        int IList.Add(object? value) => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        void IList.Clear() => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        bool IList.Contains(object? value) => value is int index && Contains(index);

        /// <inheritdoc />
        int IList.IndexOf(object? value) => value is int index ? IndexOf(index) : -1;

        /// <inheritdoc />
        void IList.Insert(int index, object? value) => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        void IList.Remove(object? value) => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        void IList.RemoveAt(int index) => throw new NotSupportedException("The collection is read-only.");
    }

    /// <summary>A checked list box's checked items.</summary>
    public class CheckedItemCollection : IList
    {
        private readonly CheckedListBox _owner;

        internal CheckedItemCollection(CheckedListBox owner) => _owner = owner;

        /// <summary>The number of checked items.</summary>
        public int Count => _owner.Checked().Count;

        /// <summary>Whether the collection is read-only.</summary>
        public bool IsReadOnly => true;

        /// <inheritdoc />
        bool IList.IsFixedSize => true;

        /// <inheritdoc />
        bool ICollection.IsSynchronized => false;

        /// <inheritdoc />
        object ICollection.SyncRoot => this;

        /// <summary>The nth checked item.</summary>
        /// <param name="index">n.</param>
        public object this[int index] => _owner.Items[_owner.Checked()[index]];

        /// <inheritdoc />
        object? IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException("The collection is read-only.");
        }

        /// <summary>Whether an item is checked.</summary>
        /// <param name="item">The item.</param>
        public bool Contains(object item) => IndexOf(item) >= 0;

        /// <summary>The position of an item among the checked ones, or -1.</summary>
        /// <param name="item">The item.</param>
        public int IndexOf(object item) => Items().IndexOf(item);

        /// <summary>Copies the items into an array.</summary>
        /// <param name="dest">The array.</param>
        /// <param name="index">Where to start.</param>
        public void CopyTo(Array dest, int index) => ((ICollection)Items()).CopyTo(dest, index);

        /// <inheritdoc />
        public IEnumerator GetEnumerator() => Items().GetEnumerator();

        /// <inheritdoc />
        int IList.Add(object? value) => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        void IList.Clear() => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        bool IList.Contains(object? value) => value is not null && Contains(value);

        /// <inheritdoc />
        int IList.IndexOf(object? value) => value is null ? -1 : IndexOf(value);

        /// <inheritdoc />
        void IList.Insert(int index, object? value) => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        void IList.Remove(object? value) => throw new NotSupportedException("The collection is read-only.");

        /// <inheritdoc />
        void IList.RemoveAt(int index) => throw new NotSupportedException("The collection is read-only.");

        private List<object> Items() => [.. _owner.Checked().Select(index => _owner.Items[index])];
    }
}
