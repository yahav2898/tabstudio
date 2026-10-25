using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace System.Windows.Forms;

/// <summary>
/// A list between a data source and the controls bound to it: it keeps the current item, which controls bound to
/// it show, and passes the source's changes on. The designer creates one for each data source of a form.
/// </summary>
[DefaultEvent(nameof(CurrentChanged))]
[DefaultProperty(nameof(DataSource))]
public class BindingSource : Component, IBindingListView, ITypedList, ICancelAddNew, ISupportInitialize
{
    private static readonly object EventListChanged = new();
    private static readonly object EventCurrentChanged = new();
    private static readonly object EventCurrentItemChanged = new();
    private static readonly object EventPositionChanged = new();
    private static readonly object EventDataSourceChanged = new();
    private static readonly object EventDataMemberChanged = new();
    private static readonly object EventAddingNew = new();

    private object? _dataSource;
    private string _dataMember = string.Empty;
    private IList _list = new BindingList<object>();
    private Type? _itemType;
    private int _position = -1;
    private int _addNewIndex = -1;
    private bool _initializing;
    private bool _suspended;
    private bool? _allowNew;

    /// <summary>Creates a binding source with an empty list.</summary>
    public BindingSource()
    {
    }

    /// <summary>Creates a binding source of a data source.</summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The list of the source to show.</param>
    public BindingSource(object? dataSource, string? dataMember)
    {
        _dataMember = dataMember ?? string.Empty;
        DataSource = dataSource;
    }

    /// <summary>Creates a binding source that is disposed with a container.</summary>
    /// <param name="container">The container.</param>
    public BindingSource(IContainer container)
        : this()
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    /// <summary>Occurs when the list changes.</summary>
    public event ListChangedEventHandler ListChanged
    {
        add => Events.AddHandler(EventListChanged, value);
        remove => Events.RemoveHandler(EventListChanged, value);
    }

    /// <summary>Occurs when the current item changes to another item.</summary>
    public event EventHandler? CurrentChanged
    {
        add => Events.AddHandler(EventCurrentChanged, value);
        remove => Events.RemoveHandler(EventCurrentChanged, value);
    }

    /// <summary>Occurs when the current item or one of its values changes.</summary>
    public event EventHandler? CurrentItemChanged
    {
        add => Events.AddHandler(EventCurrentItemChanged, value);
        remove => Events.RemoveHandler(EventCurrentItemChanged, value);
    }

    /// <summary>Occurs when the position changes.</summary>
    public event EventHandler? PositionChanged
    {
        add => Events.AddHandler(EventPositionChanged, value);
        remove => Events.RemoveHandler(EventPositionChanged, value);
    }

    /// <summary>Occurs when the data source changes.</summary>
    public event EventHandler? DataSourceChanged
    {
        add => Events.AddHandler(EventDataSourceChanged, value);
        remove => Events.RemoveHandler(EventDataSourceChanged, value);
    }

    /// <summary>Occurs when the data member changes.</summary>
    public event EventHandler? DataMemberChanged
    {
        add => Events.AddHandler(EventDataMemberChanged, value);
        remove => Events.RemoveHandler(EventDataMemberChanged, value);
    }

    /// <summary>Occurs before an item is added with <see cref="AddNew"/>; handlers can supply it.</summary>
    public event AddingNewEventHandler? AddingNew
    {
        add => Events.AddHandler(EventAddingNew, value);
        remove => Events.RemoveHandler(EventAddingNew, value);
    }

    /// <summary>The data source: a list, a data table or set, a type for an empty list of that type, or an object.</summary>
    [AllowNull]
    public object? DataSource
    {
        get => _dataSource;
        set
        {
            if (ReferenceEquals(_dataSource, value))
            {
                return;
            }

            _dataSource = value;
            Reset();
            ((EventHandler?)Events[EventDataSourceChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The list of the data source to show, such as a table of a data set.</summary>
    [AllowNull]
    public string DataMember
    {
        get => _dataMember;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_dataMember, value, StringComparison.Ordinal))
            {
                return;
            }

            _dataMember = value;
            Reset();
            ((EventHandler?)Events[EventDataMemberChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The list shown.</summary>
    public IList List => _list;

    /// <summary>The position of the current item, or -1.</summary>
    public int Position
    {
        get => _position;
        set
        {
            var position = _list.Count == 0 ? -1 : Math.Clamp(value, 0, _list.Count - 1);
            if (position == _position)
            {
                return;
            }

            _position = position;
            OnPositionChanged(EventArgs.Empty);
            OnCurrentChanged(EventArgs.Empty);
            OnCurrentItemChanged(EventArgs.Empty);
        }
    }

    /// <summary>The current item, or <see langword="null"/>.</summary>
    public object? Current => _position >= 0 && _position < _list.Count ? _list[_position] : null;

    /// <summary>The number of items.</summary>
    public virtual int Count => _list.Count;

    /// <summary>Whether items can be added.</summary>
    public virtual bool AllowNew
    {
        get => _allowNew ?? (_list is IBindingList bindingList ? bindingList.AllowNew : !_list.IsFixedSize && !_list.IsReadOnly && ItemType?.GetConstructor(Type.EmptyTypes) is not null);
        set => _allowNew = value;
    }

    /// <summary>Whether items can be changed.</summary>
    public bool AllowEdit => _list is IBindingList bindingList ? bindingList.AllowEdit : !_list.IsReadOnly;

    /// <summary>Whether items can be removed.</summary>
    public bool AllowRemove => _list is IBindingList bindingList ? bindingList.AllowRemove : !_list.IsFixedSize && !_list.IsReadOnly;

    /// <summary>A filter of the items, for data tables, such as "Age &gt; 30".</summary>
    public string? Filter
    {
        get => field;
        set
        {
            field = value;
            ApplyFilter(value);
        }
    }

    /// <summary>The sort of the items, such as "Name ASC", for lists that sort.</summary>
    public string? Sort
    {
        get => field;
        set
        {
            field = value;
            ApplySortString(value);
        }
    }

    /// <summary>Whether the binding source raises ListChanged.</summary>
    public bool RaiseListChangedEvents { get; set; } = true;

    /// <summary>Whether binding is suspended.</summary>
    public bool IsBindingSuspended => _suspended;

    /// <inheritdoc />
    public bool IsReadOnly => _list.IsReadOnly;

    /// <inheritdoc />
    public bool IsFixedSize => _list.IsFixedSize;

    /// <inheritdoc />
    public bool IsSynchronized => false;

    /// <inheritdoc />
    public object SyncRoot => this;

    /// <inheritdoc />
    public bool SupportsChangeNotification => true;

    /// <inheritdoc />
    public bool SupportsSearching => _list is IBindingList { SupportsSearching: true };

    /// <inheritdoc />
    public bool SupportsSorting => _list is IBindingList { SupportsSorting: true };

    /// <inheritdoc />
    public bool IsSorted => _list is IBindingList { IsSorted: true };

    /// <inheritdoc />
    public PropertyDescriptor? SortProperty => (_list as IBindingList)?.SortProperty;

    /// <inheritdoc />
    public ListSortDirection SortDirection => (_list as IBindingList)?.SortDirection ?? ListSortDirection.Ascending;

    /// <inheritdoc />
    public bool SupportsAdvancedSorting => _list is IBindingListView { SupportsAdvancedSorting: true };

    /// <inheritdoc />
    public bool SupportsFiltering => _list is IBindingListView { SupportsFiltering: true };

    /// <inheritdoc />
    public ListSortDescriptionCollection SortDescriptions => (_list as IBindingListView)?.SortDescriptions ?? new ListSortDescriptionCollection();

    /// <inheritdoc />
    bool IBindingList.AllowNew => AllowNew;

    /// <inheritdoc />
    bool IBindingList.AllowEdit => AllowEdit;

    /// <inheritdoc />
    bool IBindingList.AllowRemove => AllowRemove;

    /// <inheritdoc />
    string? IBindingListView.Filter
    {
        get => Filter;
        set => Filter = value;
    }

    private Type? ItemType => _itemType ?? (_list.Count > 0 ? _list[0]?.GetType() : null);

    /// <summary>The item at an index.</summary>
    /// <param name="index">The index.</param>
    public virtual object? this[int index]
    {
        get => _list[index];
        set => _list[index] = value;
    }

    /// <summary>Adds an item.</summary>
    /// <param name="value">The item.</param>
    /// <returns>Its index.</returns>
    public virtual int Add(object? value)
    {
        if (_dataSource is null && _list is BindingList<object> { Count: 0 } && value is not null)
        {
            // An empty binding source takes the type of its first item, as in Windows Forms.
            _itemType = value.GetType();
            _list = (IList)Activator.CreateInstance(typeof(BindingList<>).MakeGenericType(_itemType))!;
            Subscribe();
        }

        var index = _list.Add(value);
        AfterUnnotifiedChange(ListChangedType.ItemAdded, index);
        return index;
    }

    /// <summary>Adds a new item, from AddingNew handlers or the list.</summary>
    /// <returns>The item.</returns>
    public virtual object? AddNew()
    {
        var adding = new AddingNewEventArgs();
        ((AddingNewEventHandler?)Events[EventAddingNew])?.Invoke(this, adding);
        object? item;
        if (adding.NewObject is { } supplied)
        {
            Add(supplied);
            item = supplied;
        }
        else if (_list is IBindingList bindingList)
        {
            item = bindingList.AddNew();
        }
        else
        {
            item = Activator.CreateInstance(ItemType ?? throw new InvalidOperationException("AddNew cannot be called on a BindingSource whose item type is unknown."));
            Add(item);
        }

        _addNewIndex = item is null ? -1 : _list.IndexOf(item);
        Position = _addNewIndex;
        return item;
    }

    /// <inheritdoc />
    public virtual void CancelNew(int itemIndex)
    {
        if (_list is ICancelAddNew cancel)
        {
            cancel.CancelNew(itemIndex);
        }
        else if (itemIndex == _addNewIndex && itemIndex >= 0 && itemIndex < _list.Count)
        {
            RemoveAt(itemIndex);
        }

        _addNewIndex = -1;
    }

    /// <inheritdoc />
    public virtual void EndNew(int itemIndex)
    {
        if (_list is ICancelAddNew cancel)
        {
            cancel.EndNew(itemIndex);
        }

        _addNewIndex = -1;
    }

    /// <summary>Removes every item.</summary>
    public virtual void Clear()
    {
        _list.Clear();
        AfterUnnotifiedChange(ListChangedType.Reset, -1);
    }

    /// <summary>Whether an item is in the list.</summary>
    /// <param name="value">The item.</param>
    public virtual bool Contains(object? value) => _list.Contains(value);

    /// <summary>The index of an item, or -1.</summary>
    /// <param name="value">The item.</param>
    public virtual int IndexOf(object? value) => _list.IndexOf(value);

    /// <summary>Inserts an item.</summary>
    /// <param name="index">Where.</param>
    /// <param name="value">The item.</param>
    public virtual void Insert(int index, object? value)
    {
        _list.Insert(index, value);
        AfterUnnotifiedChange(ListChangedType.ItemAdded, index);
    }

    /// <summary>Removes an item.</summary>
    /// <param name="value">The item.</param>
    public virtual void Remove(object? value)
    {
        var index = _list.IndexOf(value);
        if (index >= 0)
        {
            RemoveAt(index);
        }
    }

    /// <summary>Removes the item at an index.</summary>
    /// <param name="index">The index.</param>
    public virtual void RemoveAt(int index)
    {
        _list.RemoveAt(index);
        AfterUnnotifiedChange(ListChangedType.ItemDeleted, index);
    }

    /// <summary>Removes the current item.</summary>
    public void RemoveCurrent()
    {
        if (_position < 0)
        {
            throw new InvalidOperationException("Cannot remove the current item because there is no current item.");
        }

        RemoveAt(_position);
    }

    /// <summary>Moves to the first item.</summary>
    public void MoveFirst() => Position = 0;

    /// <summary>Moves to the last item.</summary>
    public void MoveLast() => Position = _list.Count - 1;

    /// <summary>Moves to the next item.</summary>
    public void MoveNext() => Position = _position + 1;

    /// <summary>Moves to the previous item.</summary>
    public void MovePrevious() => Position = Math.Max(0, _position - 1);

    /// <summary>The index of the first item whose property has a value, or -1.</summary>
    /// <param name="propertyName">The property.</param>
    /// <param name="key">The value.</param>
    public int Find(string propertyName, object key)
    {
        var property = GetItemProperties(null).Find(propertyName, ignoreCase: true)
            ?? throw new ArgumentException($"DataMember property '{propertyName}' cannot be found on the DataSource.", nameof(propertyName));
        return Find(property, key);
    }

    /// <inheritdoc />
    public int Find(PropertyDescriptor property, object key)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (_list is IBindingList { SupportsSearching: true } bindingList)
        {
            return bindingList.Find(property, key);
        }

        for (var i = 0; i < _list.Count; i++)
        {
            if (Equals(property.GetValue(_list[i]), key))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Tells the bound controls to read every value again.</summary>
    /// <param name="metadataChanged">Whether the list's properties changed too.</param>
    public void ResetBindings(bool metadataChanged)
    {
        if (metadataChanged)
        {
            OnListChanged(new ListChangedEventArgs(ListChangedType.PropertyDescriptorChanged, null));
        }

        OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
    }

    /// <summary>Tells the bound controls the current item changed.</summary>
    public void ResetCurrentItem()
    {
        if (_position >= 0)
        {
            OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, _position));
        }
    }

    /// <summary>Tells the bound controls an item changed.</summary>
    /// <param name="itemIndex">The item.</param>
    public void ResetItem(int itemIndex) => OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, itemIndex));

    /// <summary>Ends editing the current item.</summary>
    public void EndEdit()
    {
        if (Current is IEditableObject editable)
        {
            editable.EndEdit();
        }
    }

    /// <summary>Cancels editing the current item.</summary>
    public void CancelEdit()
    {
        if (Current is IEditableObject editable)
        {
            editable.CancelEdit();
        }
    }

    /// <summary>Stops telling the bound controls of changes.</summary>
    public void SuspendBinding() => _suspended = true;

    /// <summary>Tells the bound controls of changes again.</summary>
    public void ResumeBinding()
    {
        _suspended = false;
        ResetBindings(false);
    }

    /// <inheritdoc />
    public void BeginInit() => _initializing = true;

    /// <inheritdoc />
    public void EndInit()
    {
        _initializing = false;
        Reset();
    }

    /// <inheritdoc />
    public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[]? listAccessors)
    {
        if (_list is ITypedList typed)
        {
            return typed.GetItemProperties(listAccessors);
        }

        return ItemType is { } type ? TypeDescriptor.GetProperties(type) : PropertyDescriptorCollection.Empty;
    }

    /// <inheritdoc />
    public string GetListName(PropertyDescriptor[]? listAccessors) => (_list as ITypedList)?.GetListName(listAccessors) ?? ItemType?.Name ?? string.Empty;

    /// <inheritdoc />
    public void ApplySort(PropertyDescriptor property, ListSortDirection direction)
    {
        if (_list is not IBindingList { SupportsSorting: true } bindingList)
        {
            throw new NotSupportedException("The list of the BindingSource does not support sorting.");
        }

        bindingList.ApplySort(property, direction);
    }

    /// <inheritdoc />
    public void ApplySort(ListSortDescriptionCollection sorts)
    {
        if (_list is not IBindingListView { SupportsAdvancedSorting: true } view)
        {
            throw new NotSupportedException("The list of the BindingSource does not support advanced sorting.");
        }

        view.ApplySort(sorts);
    }

    /// <inheritdoc />
    public void RemoveSort()
    {
        (_list as IBindingList)?.RemoveSort();
        Sort = null;
    }

    /// <inheritdoc />
    public void RemoveFilter() => Filter = null;

    /// <inheritdoc />
    public void AddIndex(PropertyDescriptor property) => (_list as IBindingList)?.AddIndex(property);

    /// <inheritdoc />
    public void RemoveIndex(PropertyDescriptor property) => (_list as IBindingList)?.RemoveIndex(property);

    /// <inheritdoc />
    public void CopyTo(Array arr, int index) => _list.CopyTo(arr, index);

    /// <inheritdoc />
    public virtual IEnumerator GetEnumerator() => _list.GetEnumerator();

    /// <summary>Raises ListChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnListChanged(ListChangedEventArgs e)
    {
        if (!RaiseListChangedEvents || _suspended)
        {
            return;
        }

        ((ListChangedEventHandler?)Events[EventListChanged])?.Invoke(this, e);
    }

    /// <summary>Raises CurrentChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCurrentChanged(EventArgs e) => ((EventHandler?)Events[EventCurrentChanged])?.Invoke(this, e);

    /// <summary>Raises CurrentItemChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCurrentItemChanged(EventArgs e) => ((EventHandler?)Events[EventCurrentItemChanged])?.Invoke(this, e);

    /// <summary>Raises PositionChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnPositionChanged(EventArgs e) => ((EventHandler?)Events[EventPositionChanged])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Unsubscribe();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    object? IList.this[int index]
    {
        get => this[index];
        set => this[index] = value;
    }

    /// <inheritdoc />
    int IList.Add(object? value) => Add(value);

    /// <inheritdoc />
    object? IBindingList.AddNew() => AddNew();

    private void Reset()
    {
        if (_initializing)
        {
            return;
        }

        Unsubscribe();
        _itemType = null;
        _list = ResolveList(_dataSource, _dataMember, out _itemType);
        Subscribe();
        ApplySortString(Sort);
        ApplyFilter(Filter);

        var previous = _position;
        _position = _list.Count > 0 ? Math.Clamp(Math.Max(previous, 0), 0, _list.Count - 1) : -1;
        OnListChanged(new ListChangedEventArgs(ListChangedType.PropertyDescriptorChanged, null));
        OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        OnPositionChanged(EventArgs.Empty);
        OnCurrentChanged(EventArgs.Empty);
        OnCurrentItemChanged(EventArgs.Empty);
    }

    private static IList ResolveList(object? source, string member, out Type? itemType)
    {
        itemType = null;
        switch (source)
        {
            case null:
                return new BindingList<object>();
            case Type type:
                itemType = type;
                return (IList)Activator.CreateInstance(typeof(BindingList<>).MakeGenericType(type))!;
            case DataSet set when member.Length > 0:
                return set.Tables[member]?.DefaultView ?? throw new ArgumentException($"DataMember property '{member}' cannot be found on the DataSource.", nameof(member));
            case IListSource listSource:
            {
                var list = listSource.GetList();
                if (member.Length > 0 && list is ITypedList typed && typed.GetItemProperties(null).Find(member, true) is { } property && list.Count > 0 && property.GetValue(list[0]) is IList nested)
                {
                    return nested;
                }

                return list;
            }

            case IList list when member.Length > 0 && list.Count > 0 && TypeDescriptor.GetProperties(list[0]!).Find(member, true)?.GetValue(list[0]) is IList nested:
                return nested;
            case IList list:
                return list;
            case IEnumerable enumerable and not string:
                return new BindingList<object>([.. enumerable.Cast<object>()]);
            default:
                if (member.Length > 0 && TypeDescriptor.GetProperties(source).Find(member, true)?.GetValue(source) is IList memberList)
                {
                    return memberList;
                }

                itemType = source.GetType();
                var single = (IList)Activator.CreateInstance(typeof(BindingList<>).MakeGenericType(itemType))!;
                single.Add(source);
                return single;
        }
    }

    private void Subscribe()
    {
        if (_list is IBindingList bindingList)
        {
            bindingList.ListChanged += OnInnerListChanged;
        }
    }

    private void Unsubscribe()
    {
        if (_list is IBindingList bindingList)
        {
            bindingList.ListChanged -= OnInnerListChanged;
        }
    }

    private void OnInnerListChanged(object? sender, ListChangedEventArgs e)
    {
        var previous = Current;
        if (_list.Count == 0)
        {
            _position = -1;
        }
        else if (_position < 0 || _position >= _list.Count)
        {
            _position = Math.Clamp(_position, 0, _list.Count - 1);
        }

        OnListChanged(e);
        if (!ReferenceEquals(previous, Current))
        {
            OnPositionChanged(EventArgs.Empty);
            OnCurrentChanged(EventArgs.Empty);
        }

        if (e.ListChangedType == ListChangedType.ItemChanged && e.NewIndex == _position || !ReferenceEquals(previous, Current))
        {
            OnCurrentItemChanged(EventArgs.Empty);
        }
    }

    /// <summary>Raises ListChanged for a change to a list that does not tell of its changes itself.</summary>
    private void AfterUnnotifiedChange(ListChangedType type, int index)
    {
        if (_list is IBindingList)
        {
            return;
        }

        OnInnerListChanged(_list, new ListChangedEventArgs(type, index));
    }

    private void ApplyFilter(string? filter)
    {
        if (_list is IBindingListView { SupportsFiltering: true } view)
        {
            if (string.IsNullOrEmpty(filter))
            {
                view.RemoveFilter();
            }
            else
            {
                view.Filter = filter;
            }
        }
    }

    private void ApplySortString(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return;
        }

        if (_list is DataView view)
        {
            view.Sort = sort;
            return;
        }

        var parts = sort.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (_list is IBindingList { SupportsSorting: true } bindingList && GetItemProperties(null).Find(parts[0], true) is { } property)
        {
            var direction = parts.Length > 1 && parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase) ? ListSortDirection.Descending : ListSortDirection.Ascending;
            bindingList.ApplySort(property, direction);
        }
    }
}
