using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>
/// The base of list boxes and combo boxes: items that come from the Items collection or from a data source, with
/// the text shown for each item and the value selected.
/// </summary>
public abstract class ListControl : Control
{
    private static readonly object EventDataSourceChanged = new();
    private static readonly object EventDisplayMemberChanged = new();
    private static readonly object EventValueMemberChanged = new();
    private static readonly object EventSelectedValueChanged = new();
    private static readonly object EventFormat = new();

    private object? _dataSource;
    private IList? _boundList;
    private string _displayMember = string.Empty;
    private string _valueMember = string.Empty;

    /// <summary>Creates a list control.</summary>
    private protected ListControl()
    {
    }

    /// <summary>Occurs when the data source changes.</summary>
    public event EventHandler? DataSourceChanged
    {
        add => Events.AddHandler(EventDataSourceChanged, value);
        remove => Events.RemoveHandler(EventDataSourceChanged, value);
    }

    /// <summary>Occurs when the display member changes.</summary>
    public event EventHandler? DisplayMemberChanged
    {
        add => Events.AddHandler(EventDisplayMemberChanged, value);
        remove => Events.RemoveHandler(EventDisplayMemberChanged, value);
    }

    /// <summary>Occurs when the value member changes.</summary>
    public event EventHandler? ValueMemberChanged
    {
        add => Events.AddHandler(EventValueMemberChanged, value);
        remove => Events.RemoveHandler(EventValueMemberChanged, value);
    }

    /// <summary>Occurs when the selected value changes.</summary>
    public event EventHandler? SelectedValueChanged
    {
        add => Events.AddHandler(EventSelectedValueChanged, value);
        remove => Events.RemoveHandler(EventSelectedValueChanged, value);
    }

    /// <summary>Occurs when an item's text is needed, to format it.</summary>
    public event ListControlConvertEventHandler? Format
    {
        add => Events.AddHandler(EventFormat, value);
        remove => Events.RemoveHandler(EventFormat, value);
    }

    /// <summary>
    /// The data source: a list, an array, a binding list, a <c>DataTable</c> or a <c>BindingSource</c>. Its items replace the
    /// Items collection.
    /// </summary>
    public object? DataSource
    {
        get => _dataSource;
        set
        {
            if (value is not null and not IList and not IListSource)
            {
                throw new ArgumentException("Complex DataBinding accepts as a data source either an IList or an IListSource.", nameof(value));
            }

            if (ReferenceEquals(_dataSource, value))
            {
                return;
            }

            if (_boundList is IBindingList old)
            {
                old.ListChanged -= OnBoundListChanged;
            }

            _dataSource = value;
            _boundList = value switch
            {
                IListSource source => source.GetList(),
                IList list => list,
                _ => null,
            };
            if (_boundList is IBindingList bindingList)
            {
                bindingList.ListChanged += OnBoundListChanged;
            }

            RefreshBoundItems();
            OnDataSourceChanged(EventArgs.Empty);
        }
    }

    /// <summary>The property of the items whose value is shown; empty to show each item's ToString.</summary>
    [AllowNull]
    public string DisplayMember
    {
        get => _displayMember;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_displayMember, value, StringComparison.Ordinal))
            {
                return;
            }

            _displayMember = value;
            Invalidate();
            OnDisplayMemberChanged(EventArgs.Empty);
        }
    }

    /// <summary>The property of the items whose value <see cref="SelectedValue"/> is; empty for the item itself.</summary>
    [AllowNull]
    public string ValueMember
    {
        get => _valueMember;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_valueMember, value, StringComparison.Ordinal))
            {
                return;
            }

            _valueMember = value;
            OnValueMemberChanged(EventArgs.Empty);
        }
    }

    /// <summary>The value of the selected item, through <see cref="ValueMember"/>.</summary>
    public object? SelectedValue
    {
        get => SelectedIndex >= 0 ? GetItemValue(ItemAt(SelectedIndex)) : null;
        set
        {
            for (var i = 0; i < ItemCount; i++)
            {
                if (Equals(GetItemValue(ItemAt(i)), value))
                {
                    SelectedIndex = i;
                    return;
                }
            }

            SelectedIndex = -1;
        }
    }

    /// <summary>The index of the selected item, or -1.</summary>
    public abstract int SelectedIndex { get; set; }

    /// <summary>Whether <see cref="FormatString"/> and the Format event format the items' text.</summary>
    public bool FormattingEnabled { get; set; }

    /// <summary>The format of the items' text, such as "C2".</summary>
    [AllowNull]
    public string FormatString
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>The culture or format provider of the items' text.</summary>
    public IFormatProvider? FormatInfo { get; set; }

    /// <summary>Whether the items come from a data source.</summary>
    internal bool IsBound => _boundList is not null;

    /// <summary>The number of items.</summary>
    internal abstract int ItemCount { get; }

    /// <summary>The item at an index.</summary>
    internal abstract object? ItemAt(int index);

    /// <summary>The text shown for an item.</summary>
    /// <param name="item">The item.</param>
    public string GetItemText(object? item)
    {
        var value = GetMemberValue(item, _displayMember);
        if (FormattingEnabled)
        {
            var e = new ListControlConvertEventArgs(value, typeof(string), item);
            OnFormat(e);
            if (!ReferenceEquals(e.Value, value) || e.Value is string)
            {
                return Convert.ToString(e.Value, FormatInfo ?? CultureInfo.CurrentCulture) ?? string.Empty;
            }

            if (FormatString.Length > 0 && value is IFormattable formattable)
            {
                return formattable.ToString(FormatString, FormatInfo ?? CultureInfo.CurrentCulture);
            }
        }

        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    /// <summary>Raises DataSourceChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDataSourceChanged(EventArgs e) => ((EventHandler?)Events[EventDataSourceChanged])?.Invoke(this, e);

    /// <summary>Raises DisplayMemberChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDisplayMemberChanged(EventArgs e) => ((EventHandler?)Events[EventDisplayMemberChanged])?.Invoke(this, e);

    /// <summary>Raises ValueMemberChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnValueMemberChanged(EventArgs e) => ((EventHandler?)Events[EventValueMemberChanged])?.Invoke(this, e);

    /// <summary>Raises SelectedValueChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSelectedValueChanged(EventArgs e) => ((EventHandler?)Events[EventSelectedValueChanged])?.Invoke(this, e);

    /// <summary>Raises SelectedIndexChanged and SelectedValueChanged, as the derived controls do when the selection changes.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSelectedIndexChanged(EventArgs e) => OnSelectedValueChanged(e);

    /// <summary>Raises Format.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnFormat(ListControlConvertEventArgs e) => ((ListControlConvertEventHandler?)Events[EventFormat])?.Invoke(this, e);

    /// <summary>Replaces the items with those of the data source.</summary>
    /// <param name="items">The data source's items.</param>
    protected abstract void SetItemsCore(IList items);

    /// <summary>Called when an item of the data source changed.</summary>
    /// <param name="index">The item.</param>
    protected virtual void RefreshItem(int index) => Invalidate();

    /// <summary>Throws when the Items collection is changed while a data source supplies the items.</summary>
    internal void ThrowIfBound()
    {
        if (IsBound)
        {
            throw new ArgumentException("Items collection cannot be modified when the DataSource property is set.");
        }
    }

    /// <summary>The value of an item through <see cref="ValueMember"/>.</summary>
    internal object? GetItemValue(object? item) => GetMemberValue(item, _valueMember);

    /// <summary>The value of a property of an item, through its type descriptor so that data rows work.</summary>
    internal static object? GetMemberValue(object? item, string member)
    {
        if (item is null || member.Length == 0)
        {
            return item;
        }

        // A member can be a path into the data source, such as "Customers.Name"; the last part names the property.
        var name = member[(member.LastIndexOf('.') + 1)..];
        var property = TypeDescriptor.GetProperties(item).Find(name, ignoreCase: true);
        return property is null ? item : property.GetValue(item);
    }

    private void OnBoundListChanged(object? sender, ListChangedEventArgs e)
    {
        if (e.ListChangedType == ListChangedType.ItemChanged && e.NewIndex >= 0)
        {
            RefreshItem(e.NewIndex);
            return;
        }

        RefreshBoundItems();
    }

    private void RefreshBoundItems()
    {
        if (_boundList is { } list)
        {
            SetItemsCore(list);
        }
        else
        {
            SetItemsCore(Array.Empty<object>());
        }
    }
}

/// <summary>Data for formatting an item's text.</summary>
public class ListControlConvertEventArgs : ConvertEventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="value">The value.</param>
    /// <param name="desiredType">The type wanted.</param>
    /// <param name="listItem">The item.</param>
    public ListControlConvertEventArgs(object? value, Type? desiredType, object? listItem)
        : base(value, desiredType)
    {
        ListItem = listItem;
    }

    /// <summary>The item.</summary>
    public object? ListItem { get; }
}

/// <summary>Data for converting a value to another type, for display or for its source.</summary>
public class ConvertEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="value">The value.</param>
    /// <param name="desiredType">The type wanted.</param>
    public ConvertEventArgs(object? value, Type? desiredType)
    {
        Value = value;
        DesiredType = desiredType;
    }

    /// <summary>The value; handlers replace it with the converted one.</summary>
    public object? Value { get; set; }

    /// <summary>The type wanted.</summary>
    public Type? DesiredType { get; }
}

/// <summary>Handles formatting an item's text.</summary>
/// <param name="sender">The control.</param>
/// <param name="e">The event data.</param>
public delegate void ListControlConvertEventHandler(object? sender, ListControlConvertEventArgs e);

/// <summary>Handles converting a value.</summary>
/// <param name="sender">The binding.</param>
/// <param name="e">The event data.</param>
public delegate void ConvertEventHandler(object? sender, ConvertEventArgs e);

/// <summary>The items of a list box or combo box, kept sorted when asked, telling the control of each change.</summary>
internal sealed class ItemStore
{
    private readonly List<object> _items = [];
    private readonly ListControl _owner;
    private readonly Action _changed;

    public ItemStore(ListControl owner, Action changed)
    {
        _owner = owner;
        _changed = changed;
    }

    public bool Sorted { get; set; }

    public int Count => _items.Count;

    public object this[int index]
    {
        get => _items[index];
        set
        {
            _owner.ThrowIfBound();
            _items[index] = value ?? throw new ArgumentNullException(nameof(value));
            _changed();
        }
    }

    public int Add(object item)
    {
        _owner.ThrowIfBound();
        ArgumentNullException.ThrowIfNull(item);
        var index = Sorted ? SortedIndex(item) : _items.Count;
        _items.Insert(index, item);
        _changed();
        return index;
    }

    public void AddRange(IEnumerable items)
    {
        _owner.ThrowIfBound();
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            _items.Insert(Sorted ? SortedIndex(item) : _items.Count, item);
        }

        _changed();
    }

    public void Insert(int index, object item)
    {
        _owner.ThrowIfBound();
        ArgumentNullException.ThrowIfNull(item);
        _items.Insert(Sorted ? SortedIndex(item) : Math.Clamp(index, 0, _items.Count), item);
        _changed();
    }

    public void RemoveAt(int index)
    {
        _owner.ThrowIfBound();
        _items.RemoveAt(index);
        _changed();
    }

    public void Remove(object? item)
    {
        var index = IndexOf(item);
        if (index >= 0)
        {
            RemoveAt(index);
        }
    }

    public void Clear()
    {
        _owner.ThrowIfBound();
        _items.Clear();
        _changed();
    }

    public int IndexOf(object? item) => item is null ? -1 : _items.IndexOf(item);

    public bool Contains(object? item) => IndexOf(item) >= 0;

    public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

    public IEnumerator GetEnumerator() => _items.GetEnumerator();

    /// <summary>Replaces the items with a data source's, without the bound check.</summary>
    public void SetBound(IList items)
    {
        _items.Clear();
        foreach (var item in items)
        {
            if (item is not null)
            {
                _items.Add(item);
            }
        }

        _changed();
    }

    /// <summary>Sorts the items by their text.</summary>
    public void Sort()
    {
        _items.Sort((a, b) => Compare(_owner.GetItemText(a), _owner.GetItemText(b)));
        _changed();
    }

    /// <summary>Compares item texts as Windows sorts lists: by the culture's rules, ignoring case.</summary>
    private static int Compare(string a, string b) => CultureInfo.CurrentCulture.CompareInfo.Compare(a, b, CompareOptions.IgnoreCase);

    private int SortedIndex(object item)
    {
        var text = _owner.GetItemText(item);
        var index = 0;
        while (index < _items.Count && Compare(_owner.GetItemText(_items[index]), text) <= 0)
        {
            index++;
        }

        return index;
    }
}
