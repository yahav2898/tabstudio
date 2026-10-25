using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>A component with data bindings: a control.</summary>
public interface IBindableComponent : IComponent
{
    /// <summary>The bindings of the component's properties.</summary>
    ControlBindingsCollection DataBindings { get; }

    /// <summary>The binding managers of the component's data sources.</summary>
    BindingContext? BindingContext { get; set; }
}

/// <summary>When a binding writes the data source's value to its control.</summary>
public enum ControlUpdateMode
{
    /// <summary>When the value changes.</summary>
    OnPropertyChanged = 0,

    /// <summary>Never.</summary>
    Never = 1,
}

/// <summary>The path of a binding's data member, such as "Customers.Name".</summary>
/// <param name="dataMember">The path.</param>
public readonly struct BindingMemberInfo(string? dataMember) : IEquatable<BindingMemberInfo>
{
    private readonly string _member = dataMember ?? string.Empty;

    /// <summary>The property, the last part of the path.</summary>
    public string BindingField => _member[(_member.LastIndexOf('.') + 1)..];

    /// <summary>The list, the path before the property.</summary>
    public string BindingPath => _member.LastIndexOf('.') is var dot and > 0 ? _member[..dot] : string.Empty;

    /// <summary>The whole path.</summary>
    public string BindingMember => _member ?? string.Empty;

    /// <summary>Whether two paths are the same.</summary>
    public static bool operator ==(BindingMemberInfo a, BindingMemberInfo b) => a.Equals(b);

    /// <summary>Whether two paths differ.</summary>
    public static bool operator !=(BindingMemberInfo a, BindingMemberInfo b) => !a.Equals(b);

    /// <inheritdoc />
    public bool Equals(BindingMemberInfo other) => string.Equals(BindingMember, other.BindingMember, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BindingMemberInfo other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(BindingMember);
}

/// <summary>
/// The binding managers of a form's data sources: one for each list, which keeps the list's current item for every
/// control bound to it, so a grid and text boxes bound to the same list show the same item.
/// </summary>
public class BindingContext : ICollection
{
    private readonly Dictionary<(object Source, string Member), BindingManagerBase> _managers = [];

    /// <summary>Creates a binding context.</summary>
    public BindingContext()
    {
    }

    /// <summary>Whether the context is read-only.</summary>
    public bool IsReadOnly => false;

    /// <inheritdoc />
    int ICollection.Count => _managers.Count;

    /// <inheritdoc />
    bool ICollection.IsSynchronized => false;

    /// <inheritdoc />
    object ICollection.SyncRoot => this;

    /// <summary>The manager of a data source.</summary>
    /// <param name="dataSource">The data source.</param>
    public BindingManagerBase this[object dataSource] => this[dataSource, string.Empty];

    /// <summary>The manager of a list of a data source.</summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The list, such as a table of a data set.</param>
    public BindingManagerBase this[object dataSource, string? dataMember]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(dataSource);
            var key = (dataSource, dataMember ?? string.Empty);
            if (!_managers.TryGetValue(key, out var manager))
            {
                manager = Create(dataSource, dataMember ?? string.Empty);
                _managers[key] = manager;
            }

            return manager;
        }
    }

    /// <summary>Whether a data source has a manager.</summary>
    /// <param name="dataSource">The data source.</param>
    public bool Contains(object dataSource) => Contains(dataSource, string.Empty);

    /// <summary>Whether a list of a data source has a manager.</summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The list.</param>
    public bool Contains(object dataSource, string? dataMember) => _managers.ContainsKey((dataSource, dataMember ?? string.Empty));

    /// <inheritdoc />
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_managers.Values.ToArray()).CopyTo(array, index);

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _managers.Values.GetEnumerator();

    private static BindingManagerBase Create(object source, string member)
    {
        var list = source switch
        {
            DataSet set when member.Length > 0 => set.Tables[member.Split('.')[0]]?.DefaultView,
            DataSet set => set.Tables.Count > 0 ? set.Tables[0].DefaultView : null,
            IListSource listSource => listSource.GetList(),
            IList items => items,
            _ => null,
        };

        if (list is null && member.Length > 0 && TypeDescriptor.GetProperties(source).Find(member, true)?.GetValue(source) is IList nested)
        {
            list = nested;
        }

        return list is null ? new PropertyManager(source) : new CurrencyManager(list);
    }
}

/// <summary>Keeps the current item of a data source for the controls bound to it.</summary>
public abstract class BindingManagerBase
{
    private static readonly object EventPositionChanged = new();
    private static readonly object EventCurrentChanged = new();
    private static readonly object EventCurrentItemChanged = new();
    private readonly EventHandlerList _events = new();

    /// <summary>Creates a binding manager.</summary>
    protected BindingManagerBase()
    {
        Bindings = new BindingsCollection();
    }

    /// <summary>Occurs when the position changes.</summary>
    public event EventHandler? PositionChanged
    {
        add => _events.AddHandler(EventPositionChanged, value);
        remove => _events.RemoveHandler(EventPositionChanged, value);
    }

    /// <summary>Occurs when the current item changes to another item.</summary>
    public event EventHandler? CurrentChanged
    {
        add => _events.AddHandler(EventCurrentChanged, value);
        remove => _events.RemoveHandler(EventCurrentChanged, value);
    }

    /// <summary>Occurs when the current item or one of its values changes.</summary>
    public event EventHandler? CurrentItemChanged
    {
        add => _events.AddHandler(EventCurrentItemChanged, value);
        remove => _events.RemoveHandler(EventCurrentItemChanged, value);
    }

    /// <summary>The bindings the manager serves.</summary>
    public BindingsCollection Bindings { get; }

    /// <summary>The position of the current item.</summary>
    public abstract int Position { get; set; }

    /// <summary>The current item.</summary>
    public abstract object? Current { get; }

    /// <summary>The number of items.</summary>
    public abstract int Count { get; }

    /// <summary>Whether binding is suspended.</summary>
    public bool IsBindingSuspended { get; private set; }

    /// <summary>Adds an item and makes it current.</summary>
    public abstract void AddNew();

    /// <summary>Removes the item at a position.</summary>
    /// <param name="index">The position.</param>
    public abstract void RemoveAt(int index);

    /// <summary>Commits the current item's edit.</summary>
    public virtual void EndCurrentEdit()
    {
        if (Current is IEditableObject editable)
        {
            editable.EndEdit();
        }
    }

    /// <summary>Cancels the current item's edit.</summary>
    public virtual void CancelCurrentEdit()
    {
        if (Current is IEditableObject editable)
        {
            editable.CancelEdit();
        }
    }

    /// <summary>Stops updating the bound controls.</summary>
    public virtual void SuspendBinding() => IsBindingSuspended = true;

    /// <summary>Updates the bound controls again.</summary>
    public virtual void ResumeBinding()
    {
        IsBindingSuspended = false;
        OnCurrentChanged(EventArgs.Empty);
    }

    /// <summary>The properties of the items.</summary>
    public virtual PropertyDescriptorCollection GetItemProperties() =>
        Current is { } current ? TypeDescriptor.GetProperties(current) : PropertyDescriptorCollection.Empty;

    /// <summary>Raises PositionChanged.</summary>
    /// <param name="e">The event data.</param>
    protected void OnPositionChanged(EventArgs e) => ((EventHandler?)_events[EventPositionChanged])?.Invoke(this, e);

    /// <summary>Raises CurrentChanged and CurrentItemChanged.</summary>
    /// <param name="e">The event data.</param>
    protected void OnCurrentChanged(EventArgs e)
    {
        if (IsBindingSuspended)
        {
            return;
        }

        ((EventHandler?)_events[EventCurrentChanged])?.Invoke(this, e);
        OnCurrentItemChanged(e);
    }

    /// <summary>Raises CurrentItemChanged.</summary>
    /// <param name="e">The event data.</param>
    protected void OnCurrentItemChanged(EventArgs e)
    {
        if (!IsBindingSuspended)
        {
            ((EventHandler?)_events[EventCurrentItemChanged])?.Invoke(this, e);
        }
    }
}

/// <summary>The binding manager of a list: the list's current item, which follows the list's changes.</summary>
public class CurrencyManager : BindingManagerBase
{
    private static readonly object EventListChanged = new();
    private static readonly object EventItemChanged = new();
    private readonly EventHandlerList _listEvents = new();
    private int _position;

    internal CurrencyManager(IList list)
    {
        List = list;
        _position = list.Count > 0 ? 0 : -1;
        if (list is BindingSource source)
        {
            _position = source.Position;
            source.PositionChanged += (_, _) => SetPosition(source.Position, fromSource: true);
        }

        if (list is IBindingList bindingList)
        {
            bindingList.ListChanged += OnListChanged;
        }
    }

    /// <summary>Occurs when the list changes.</summary>
    public event ListChangedEventHandler? ListChanged
    {
        add => _listEvents.AddHandler(EventListChanged, value);
        remove => _listEvents.RemoveHandler(EventListChanged, value);
    }

    /// <summary>Occurs when an item changes.</summary>
    public event ItemChangedEventHandler? ItemChanged
    {
        add => _listEvents.AddHandler(EventItemChanged, value);
        remove => _listEvents.RemoveHandler(EventItemChanged, value);
    }

    /// <summary>The list.</summary>
    public IList List { get; }

    /// <inheritdoc />
    public override int Position
    {
        get => _position;
        set => SetPosition(value, fromSource: false);
    }

    /// <inheritdoc />
    public override object? Current => _position >= 0 && _position < List.Count ? List[_position] : null;

    /// <inheritdoc />
    public override int Count => List.Count;

    /// <inheritdoc />
    public override void AddNew()
    {
        if (List is IBindingList { AllowNew: true } bindingList)
        {
            var item = bindingList.AddNew();
            Position = List.IndexOf(item);
            return;
        }

        throw new NotSupportedException("The list of the CurrencyManager cannot add new items.");
    }

    /// <inheritdoc />
    public override void RemoveAt(int index)
    {
        List.RemoveAt(index);
        if (List is not IBindingList)
        {
            Refresh();
        }
    }

    /// <inheritdoc />
    public override PropertyDescriptorCollection GetItemProperties() =>
        List is ITypedList typed ? typed.GetItemProperties(null) : base.GetItemProperties();

    /// <summary>Tells the bound controls to read the list again, for a list that does not tell of its changes.</summary>
    public void Refresh()
    {
        _position = List.Count == 0 ? -1 : Math.Clamp(_position, 0, List.Count - 1);
        ((ListChangedEventHandler?)_listEvents[EventListChanged])?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
        OnCurrentChanged(EventArgs.Empty);
    }

    private void SetPosition(int value, bool fromSource)
    {
        var position = List.Count == 0 ? -1 : Math.Clamp(value, 0, List.Count - 1);
        if (position == _position)
        {
            return;
        }

        EndCurrentEdit();
        _position = position;
        if (!fromSource && List is BindingSource source)
        {
            source.Position = position;
        }

        OnPositionChanged(EventArgs.Empty);
        OnCurrentChanged(EventArgs.Empty);
    }

    private void OnListChanged(object? sender, ListChangedEventArgs e)
    {
        var previous = Current;
        if (List.Count == 0)
        {
            _position = -1;
        }
        else if (_position < 0 || _position >= List.Count)
        {
            _position = Math.Clamp(_position, 0, List.Count - 1);
        }

        ((ListChangedEventHandler?)_listEvents[EventListChanged])?.Invoke(this, e);
        if (e.ListChangedType == ListChangedType.ItemChanged)
        {
            ((ItemChangedEventHandler?)_listEvents[EventItemChanged])?.Invoke(this, new ItemChangedEventArgs(e.NewIndex));
            if (e.NewIndex == _position)
            {
                OnCurrentItemChanged(EventArgs.Empty);
            }
        }
        else if (!ReferenceEquals(previous, Current) || e.ListChangedType == ListChangedType.Reset)
        {
            OnPositionChanged(EventArgs.Empty);
            OnCurrentChanged(EventArgs.Empty);
        }
    }
}

/// <summary>The binding manager of a single object.</summary>
public class PropertyManager : BindingManagerBase
{
    private readonly object _source;

    internal PropertyManager(object source) => _source = source;

    /// <inheritdoc />
    public override int Position
    {
        get => 0;
        set
        {
        }
    }

    /// <inheritdoc />
    public override object? Current => _source;

    /// <inheritdoc />
    public override int Count => 1;

    /// <inheritdoc />
    public override void AddNew() => throw new NotSupportedException("AddNew is not supported for property to property binding.");

    /// <inheritdoc />
    public override void RemoveAt(int index) => throw new NotSupportedException("RemoveAt is not supported for property to property binding.");
}

/// <summary>Data about an item of a list that changed.</summary>
/// <param name="index">The item.</param>
public class ItemChangedEventArgs(int index) : EventArgs
{
    /// <summary>The item.</summary>
    public int Index { get; } = index;
}

/// <summary>Handles an item of a list that changed.</summary>
/// <param name="sender">The manager.</param>
/// <param name="e">The event data.</param>
public delegate void ItemChangedEventHandler(object? sender, ItemChangedEventArgs e);

/// <summary>Bindings.</summary>
public class BindingsCollection : BaseCollection
{
    private readonly List<Binding> _bindings = [];

    internal BindingsCollection()
    {
    }

    /// <inheritdoc />
    public override int Count => _bindings.Count;

    /// <summary>The binding at an index.</summary>
    /// <param name="index">The index.</param>
    public Binding this[int index] => _bindings[index];

    /// <inheritdoc />
    protected override ArrayList List => [.. _bindings];

    /// <summary>The bindings, in order.</summary>
    internal List<Binding> Items => _bindings;
}

/// <summary>The bindings of a control's properties.</summary>
public class ControlBindingsCollection : BindingsCollection
{
    /// <summary>Creates the bindings of a control.</summary>
    /// <param name="control">The control.</param>
    public ControlBindingsCollection(IBindableComponent control)
    {
        BindableComponent = control;
    }

    /// <summary>The control.</summary>
    public IBindableComponent BindableComponent { get; }

    /// <summary>The control, as a control.</summary>
    public Control? Control => BindableComponent as Control;

    /// <summary>The update mode new bindings get.</summary>
    public DataSourceUpdateMode DefaultDataSourceUpdateMode { get; set; } = DataSourceUpdateMode.OnValidation;

    /// <summary>The binding of a property.</summary>
    /// <param name="propertyName">The property.</param>
    public Binding? this[string propertyName] => Items.Find(binding => string.Equals(binding.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Binds a property to a member of a data source.</summary>
    /// <param name="propertyName">The control's property, such as "Text".</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member, such as "Name".</param>
    public Binding Add(string propertyName, object dataSource, string? dataMember) =>
        Add(propertyName, dataSource, dataMember, false, DefaultDataSourceUpdateMode, null, string.Empty, null);

    /// <summary>Binds a property to a member of a data source, with formatting.</summary>
    /// <param name="propertyName">The control's property.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member.</param>
    /// <param name="formattingEnabled">Whether values are formatted.</param>
    public Binding Add(string propertyName, object dataSource, string? dataMember, bool formattingEnabled) =>
        Add(propertyName, dataSource, dataMember, formattingEnabled, DefaultDataSourceUpdateMode, null, string.Empty, null);

    /// <summary>Binds a property to a member of a data source, with formatting and an update mode.</summary>
    /// <param name="propertyName">The control's property.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member.</param>
    /// <param name="formattingEnabled">Whether values are formatted.</param>
    /// <param name="updateMode">When the control's value is written to the data source.</param>
    public Binding Add(string propertyName, object dataSource, string? dataMember, bool formattingEnabled, DataSourceUpdateMode updateMode) =>
        Add(propertyName, dataSource, dataMember, formattingEnabled, updateMode, null, string.Empty, null);

    /// <summary>Binds a property to a member of a data source, with formatting, an update mode and a null value.</summary>
    /// <param name="propertyName">The control's property.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member.</param>
    /// <param name="formattingEnabled">Whether values are formatted.</param>
    /// <param name="updateMode">When the control's value is written to the data source.</param>
    /// <param name="nullValue">What a null value shows as.</param>
    public Binding Add(string propertyName, object dataSource, string? dataMember, bool formattingEnabled, DataSourceUpdateMode updateMode, object? nullValue) =>
        Add(propertyName, dataSource, dataMember, formattingEnabled, updateMode, nullValue, string.Empty, null);

    /// <summary>Binds a property to a member of a data source, with formatting, an update mode, a null value and a format.</summary>
    /// <param name="propertyName">The control's property.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member.</param>
    /// <param name="formattingEnabled">Whether values are formatted.</param>
    /// <param name="updateMode">When the control's value is written to the data source.</param>
    /// <param name="nullValue">What a null value shows as.</param>
    /// <param name="formatString">The format, such as "C2".</param>
    public Binding Add(string propertyName, object dataSource, string? dataMember, bool formattingEnabled, DataSourceUpdateMode updateMode, object? nullValue, string formatString) =>
        Add(propertyName, dataSource, dataMember, formattingEnabled, updateMode, nullValue, formatString, null);

    /// <summary>Binds a property to a member of a data source, with every setting.</summary>
    /// <param name="propertyName">The control's property.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member.</param>
    /// <param name="formattingEnabled">Whether values are formatted.</param>
    /// <param name="updateMode">When the control's value is written to the data source.</param>
    /// <param name="nullValue">What a null value shows as.</param>
    /// <param name="formatString">The format.</param>
    /// <param name="formatInfo">The culture or format provider.</param>
    public Binding Add(string propertyName, object dataSource, string? dataMember, bool formattingEnabled, DataSourceUpdateMode updateMode, object? nullValue, string formatString, IFormatProvider? formatInfo)
    {
        var binding = new Binding(propertyName, dataSource, dataMember, formattingEnabled, updateMode, nullValue, formatString, formatInfo);
        Add(binding);
        return binding;
    }

    /// <summary>Adds a binding.</summary>
    /// <param name="binding">The binding.</param>
    public void Add(Binding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (this[binding.PropertyName] is not null)
        {
            throw new ArgumentException($"This causes two bindings in the collection to bind to the same property. Parameter name: {binding.PropertyName}", nameof(binding));
        }

        Items.Add(binding);
        binding.Attach(BindableComponent);
    }

    /// <summary>Removes a binding.</summary>
    /// <param name="binding">The binding.</param>
    public void Remove(Binding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (Items.Remove(binding))
        {
            binding.Detach();
        }
    }

    /// <summary>Removes the binding at an index.</summary>
    /// <param name="index">The index.</param>
    public void RemoveAt(int index) => Remove(Items[index]);

    /// <summary>Removes every binding.</summary>
    public void Clear()
    {
        foreach (var binding in Items.ToList())
        {
            Remove(binding);
        }
    }

    /// <summary>Connects every binding to its data source again, after the control's binding context changed.</summary>
    internal void Reattach()
    {
        foreach (var binding in Items)
        {
            binding.Attach(BindableComponent);
        }
    }
}

/// <summary>
/// A binding of a control's property, such as a text box's Text, to a property of a data source's current item:
/// the control shows the value, and writes it back when it is validated or changes.
/// </summary>
public class Binding
{
    private static readonly object EventFormat = new();
    private static readonly object EventParse = new();
    private readonly EventHandlerList _events = new();
    private IBindableComponent? _component;
    private BindingManagerBase? _manager;
    private PropertyDescriptor? _controlProperty;
    private EventDescriptor? _changedEvent;
    private bool _reading;

    /// <summary>Creates a binding.</summary>
    /// <param name="propertyName">The control's property.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member.</param>
    public Binding(string propertyName, object? dataSource, string? dataMember)
        : this(propertyName, dataSource, dataMember, false, DataSourceUpdateMode.OnValidation, null, string.Empty, null)
    {
    }

    /// <summary>Creates a binding with formatting.</summary>
    /// <param name="propertyName">The control's property.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member.</param>
    /// <param name="formattingEnabled">Whether values are formatted.</param>
    public Binding(string propertyName, object? dataSource, string? dataMember, bool formattingEnabled)
        : this(propertyName, dataSource, dataMember, formattingEnabled, DataSourceUpdateMode.OnValidation, null, string.Empty, null)
    {
    }

    /// <summary>Creates a binding with formatting and an update mode.</summary>
    /// <param name="propertyName">The control's property.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member.</param>
    /// <param name="formattingEnabled">Whether values are formatted.</param>
    /// <param name="dataSourceUpdateMode">When the control's value is written to the data source.</param>
    public Binding(string propertyName, object? dataSource, string? dataMember, bool formattingEnabled, DataSourceUpdateMode dataSourceUpdateMode)
        : this(propertyName, dataSource, dataMember, formattingEnabled, dataSourceUpdateMode, null, string.Empty, null)
    {
    }

    /// <summary>Creates a binding with formatting, an update mode and a null value.</summary>
    /// <param name="propertyName">The control's property.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member.</param>
    /// <param name="formattingEnabled">Whether values are formatted.</param>
    /// <param name="dataSourceUpdateMode">When the control's value is written to the data source.</param>
    /// <param name="nullValue">What a null value shows as.</param>
    public Binding(string propertyName, object? dataSource, string? dataMember, bool formattingEnabled, DataSourceUpdateMode dataSourceUpdateMode, object? nullValue)
        : this(propertyName, dataSource, dataMember, formattingEnabled, dataSourceUpdateMode, nullValue, string.Empty, null)
    {
    }

    /// <summary>Creates a binding with formatting, an update mode, a null value and a format.</summary>
    /// <param name="propertyName">The control's property.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member.</param>
    /// <param name="formattingEnabled">Whether values are formatted.</param>
    /// <param name="dataSourceUpdateMode">When the control's value is written to the data source.</param>
    /// <param name="nullValue">What a null value shows as.</param>
    /// <param name="formatString">The format.</param>
    public Binding(string propertyName, object? dataSource, string? dataMember, bool formattingEnabled, DataSourceUpdateMode dataSourceUpdateMode, object? nullValue, string formatString)
        : this(propertyName, dataSource, dataMember, formattingEnabled, dataSourceUpdateMode, nullValue, formatString, null)
    {
    }

    /// <summary>Creates a binding with every setting.</summary>
    /// <param name="propertyName">The control's property.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The member.</param>
    /// <param name="formattingEnabled">Whether values are formatted.</param>
    /// <param name="dataSourceUpdateMode">When the control's value is written to the data source.</param>
    /// <param name="nullValue">What a null value shows as.</param>
    /// <param name="formatString">The format.</param>
    /// <param name="formatInfo">The culture or format provider.</param>
    public Binding(string propertyName, object? dataSource, string? dataMember, bool formattingEnabled, DataSourceUpdateMode dataSourceUpdateMode, object? nullValue, string formatString, IFormatProvider? formatInfo)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        PropertyName = propertyName;
        DataSource = dataSource;
        BindingMemberInfo = new BindingMemberInfo(dataMember);
        FormattingEnabled = formattingEnabled;
        DataSourceUpdateMode = dataSourceUpdateMode;
        NullValue = nullValue;
        FormatString = formatString ?? string.Empty;
        FormatInfo = formatInfo;
    }

    /// <summary>Occurs when a value is formatted for the control.</summary>
    public event ConvertEventHandler? Format
    {
        add => _events.AddHandler(EventFormat, value);
        remove => _events.RemoveHandler(EventFormat, value);
    }

    /// <summary>Occurs when the control's value is parsed for the data source.</summary>
    public event ConvertEventHandler? Parse
    {
        add => _events.AddHandler(EventParse, value);
        remove => _events.RemoveHandler(EventParse, value);
    }

    /// <summary>The control's property.</summary>
    public string PropertyName { get; }

    /// <summary>The data source.</summary>
    public object? DataSource { get; }

    /// <summary>The member of the data source.</summary>
    public BindingMemberInfo BindingMemberInfo { get; }

    /// <summary>The control.</summary>
    public Control? Control => _component as Control;

    /// <summary>The component.</summary>
    public IBindableComponent? BindableComponent => _component;

    /// <summary>The manager of the data source.</summary>
    public BindingManagerBase? BindingManagerBase => _manager;

    /// <summary>Whether the binding is connected to its data source.</summary>
    public bool IsBinding => _manager is not null && _controlProperty is not null;

    /// <summary>Whether values are formatted.</summary>
    public bool FormattingEnabled { get; set; }

    /// <summary>The format, such as "C2".</summary>
    [AllowNull]
    public string FormatString
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>The culture or format provider.</summary>
    public IFormatProvider? FormatInfo { get; set; }

    /// <summary>What a null value shows as.</summary>
    public object? NullValue { get; set; }

    /// <summary>What an empty control value is stored as.</summary>
    public object? DataSourceNullValue { get; set; } = DBNull.Value;

    /// <summary>When the control's value is written to the data source.</summary>
    public DataSourceUpdateMode DataSourceUpdateMode { get; set; }

    /// <summary>When the data source's value is written to the control.</summary>
    public ControlUpdateMode ControlUpdateMode { get; set; }

    /// <summary>Shows the data source's value in the control.</summary>
    public void ReadValue()
    {
        if (_manager is not { Current: { } item } || _controlProperty is not { } property || ControlUpdateMode == ControlUpdateMode.Never)
        {
            return;
        }

        var field = FieldOf(item);
        var value = field is null ? item : field.GetValue(item);
        var formatted = FormatValue(value, property.PropertyType);
        _reading = true;
        try
        {
            if (!Equals(property.GetValue(_component), formatted))
            {
                property.SetValue(_component, formatted);
            }
        }
        finally
        {
            _reading = false;
        }
    }

    /// <summary>Writes the control's value to the data source.</summary>
    public void WriteValue()
    {
        if (_reading || _manager is not { Current: { } item } || _controlProperty is not { } property || FieldOf(item) is not { IsReadOnly: false } field)
        {
            return;
        }

        try
        {
            var value = ParseValue(property.GetValue(_component), field.PropertyType);
            if (!Equals(field.GetValue(item), value))
            {
                field.SetValue(item, value);
            }
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or ArgumentException or OverflowException or NotSupportedException)
        {
            // A value that does not parse is put back, as Windows Forms does.
            ReadValue();
        }
    }

    /// <summary>Raises Format.</summary>
    /// <param name="cevent">The event data.</param>
    protected virtual void OnFormat(ConvertEventArgs cevent) => ((ConvertEventHandler?)_events[EventFormat])?.Invoke(this, cevent);

    /// <summary>Raises Parse.</summary>
    /// <param name="cevent">The event data.</param>
    protected virtual void OnParse(ConvertEventArgs cevent) => ((ConvertEventHandler?)_events[EventParse])?.Invoke(this, cevent);

    /// <summary>Connects the binding to a component and, through its binding context, to the data source.</summary>
    internal void Attach(IBindableComponent component)
    {
        Detach();
        _component = component;
        _controlProperty = TypeDescriptor.GetProperties(component).Find(PropertyName, ignoreCase: true)
            ?? throw new ArgumentException($"Cannot bind to the property '{PropertyName}' on the target control.", nameof(component));
        if (component is Control control)
        {
            control.Validated += OnControlValidated;
        }

        _changedEvent = TypeDescriptor.GetEvents(component).Find(_controlProperty.Name + "Changed", ignoreCase: false);
        _changedEvent?.AddEventHandler(component, new EventHandler(OnControlPropertyChanged));
        if (DataSource is null || component.BindingContext is not { } context)
        {
            return;
        }

        _manager = context[DataSource, BindingMemberInfo.BindingPath];
        _manager.Bindings.Items.Add(this);
        _manager.CurrentItemChanged += OnCurrentItemChanged;
        ReadValue();
    }

    /// <summary>Disconnects the binding.</summary>
    internal void Detach()
    {
        if (_component is Control control)
        {
            control.Validated -= OnControlValidated;
        }

        if (_component is not null)
        {
            _changedEvent?.RemoveEventHandler(_component, new EventHandler(OnControlPropertyChanged));
        }

        if (_manager is not null)
        {
            _manager.CurrentItemChanged -= OnCurrentItemChanged;
            _manager.Bindings.Items.Remove(this);
        }

        _manager = null;
        _changedEvent = null;
    }

    private PropertyDescriptor? FieldOf(object item)
    {
        var field = BindingMemberInfo.BindingField;
        if (field.Length == 0)
        {
            return null;
        }

        var properties = _manager is CurrencyManager { List: ITypedList typed } ? typed.GetItemProperties(null) : TypeDescriptor.GetProperties(item);
        return properties.Find(field, ignoreCase: true) ?? throw new ArgumentException($"Cannot bind to the property or column {field} on the DataSource.", nameof(item));
    }

    private object? FormatValue(object? value, Type target)
    {
        var e = new ConvertEventArgs(value, target);
        OnFormat(e);
        if (!ReferenceEquals(e.Value, value))
        {
            return e.Value;
        }

        if (value is null || value == DBNull.Value)
        {
            return NullValue ?? (target == typeof(string) ? string.Empty : null);
        }

        if (target.IsInstanceOfType(value))
        {
            return value;
        }

        var provider = FormatInfo ?? CultureInfo.CurrentCulture;
        if (target == typeof(string))
        {
            return FormattingEnabled && FormatString.Length > 0 && value is IFormattable formattable
                ? formattable.ToString(FormatString, provider)
                : TypeDescriptor.GetConverter(value.GetType()).ConvertToString(null, provider as CultureInfo ?? CultureInfo.CurrentCulture, value);
        }

        var converter = TypeDescriptor.GetConverter(target);
        return converter.CanConvertFrom(value.GetType()) ? converter.ConvertFrom(null, provider as CultureInfo ?? CultureInfo.CurrentCulture, value) : Convert.ChangeType(value, target, provider);
    }

    private object? ParseValue(object? value, Type target)
    {
        var e = new ConvertEventArgs(value, target);
        OnParse(e);
        if (!ReferenceEquals(e.Value, value))
        {
            return e.Value;
        }

        if (value is null || (value is string { Length: 0 } && target != typeof(string)) || Equals(value, NullValue))
        {
            if (target.IsValueType && Nullable.GetUnderlyingType(target) is null)
            {
                return DataSourceNullValue == DBNull.Value ? Activator.CreateInstance(target) : DataSourceNullValue;
            }

            return DataSourceNullValue == DBNull.Value && _manager is CurrencyManager { List: DataView } ? DBNull.Value : null;
        }

        var type = Nullable.GetUnderlyingType(target) ?? target;
        if (type.IsInstanceOfType(value))
        {
            return value;
        }

        var provider = FormatInfo ?? CultureInfo.CurrentCulture;
        if (value is string text)
        {
            var converter = TypeDescriptor.GetConverter(type);
            return converter.ConvertFromString(null, provider as CultureInfo ?? CultureInfo.CurrentCulture, text);
        }

        return Convert.ChangeType(value, type, provider);
    }

    private void OnControlValidated(object? sender, EventArgs e)
    {
        if (DataSourceUpdateMode == DataSourceUpdateMode.OnValidation)
        {
            WriteValue();
        }
    }

    private void OnControlPropertyChanged(object? sender, EventArgs e)
    {
        if (DataSourceUpdateMode == DataSourceUpdateMode.OnPropertyChanged)
        {
            WriteValue();
        }
    }

    private void OnCurrentItemChanged(object? sender, EventArgs e) => ReadValue();
}
