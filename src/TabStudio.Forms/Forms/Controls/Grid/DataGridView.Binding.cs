using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace System.Windows.Forms;

/// <summary>
/// Binding: the grid shows a list's items as rows and their properties as columns, reads and writes the values
/// through the properties, follows the list's changes, and adds items from the row for new records.
/// </summary>
public partial class DataGridView
{
    private object? _dataSource;
    private string _dataMember = string.Empty;
    private IList? _boundList;
    private PropertyDescriptorCollection? _boundProperties;
    private bool _rebinding;
    private BindingManagerBase? _manager;

    /// <summary>The data source: a list, an array, a binding list, a data table or set, or a binding source.</summary>
    public object? DataSource
    {
        get => _dataSource;
        set
        {
            if (value is not null and not IList and not IListSource and not IBindingList)
            {
                throw new ArgumentException("Complex DataBinding accepts as a data source either an IList or an IListSource.", nameof(value));
            }

            if (ReferenceEquals(_dataSource, value))
            {
                return;
            }

            EndEdit();
            DetachDataSource();
            _dataSource = value;
            Bind();
            OnDataSourceChanged(EventArgs.Empty);
        }
    }

    /// <summary>The table or list of the data source to show, such as a table of a data set.</summary>
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
            if (_dataSource is not null)
            {
                DetachDataSource();
                Bind();
            }

            OnDataMemberChanged(EventArgs.Empty);
        }
    }

    /// <summary>Whether the grid shows a data table's rows.</summary>
    internal bool IsBoundToDataTable => _boundList is DataView;

    /// <summary>Whether the grid shows a data source.</summary>
    internal bool IsBound => _boundList is not null;

    /// <summary>Whether users can add items to the data source.</summary>
    private bool CanAddToSource => _boundList switch
    {
        null => true,
        IBindingList bindingList => bindingList.AllowNew,
        _ => false,
    };

    /// <summary>Whether a column shows a property of the data source.</summary>
    internal bool IsColumnBound(DataGridViewColumn column) => PropertyOf(column) is not null;

    /// <summary>The type of the property a column shows, if it is bound.</summary>
    internal Type? BoundTypeOf(DataGridViewColumn? column) => column is null ? null : PropertyOf(column)?.PropertyType;

    /// <summary>Called when a column's data property changes.</summary>
    internal void OnColumnDataPropertyNameChanged(DataGridViewColumn column) => Invalidate();

    /// <summary>Throws when rows are added or removed by code in a bound grid, as in Windows Forms.</summary>
    internal void ThrowIfBoundForRowChange()
    {
        if (IsBound && !_rebinding)
        {
            throw new InvalidOperationException("Rows cannot be programmatically added to the DataGridView's rows collection when the control is data-bound.");
        }
    }

    /// <summary>Reads a bound cell's value from its row's item.</summary>
    internal bool TryGetBoundValue(DataGridViewCell cell, out object? value)
    {
        value = null;
        if (VirtualMode && !IsBound && cell.RowIndex >= 0 && cell.ColumnIndex >= 0 && cell is not DataGridViewHeaderCell)
        {
            var e = new DataGridViewCellValueEventArgs(cell.ColumnIndex, cell.RowIndex);
            OnCellValueNeeded(e);
            value = e.Value;
            return true;
        }

        if (cell.OwningRow is not { DataBoundItem: { } item } || cell.OwningColumn is not { } column || PropertyOf(column) is not { } property)
        {
            return false;
        }

        value = property.GetValue(item);
        return true;
    }

    /// <summary>Writes a bound cell's value to its row's item.</summary>
    internal bool TrySetBoundValue(DataGridViewCell cell, object? value)
    {
        if (VirtualMode && !IsBound && cell.RowIndex >= 0 && cell.ColumnIndex >= 0)
        {
            var e = new DataGridViewCellValueEventArgs(cell.ColumnIndex, cell.RowIndex) { Value = value };
            OnCellValuePushed(e);
            Invalidate();
            return true;
        }

        if (cell.OwningRow is not { DataBoundItem: { } item } || cell.OwningColumn is not { } column || PropertyOf(column) is not { } property)
        {
            return false;
        }

        if (property.IsReadOnly)
        {
            throw new InvalidOperationException($"The property {property.Name} is read-only.");
        }

        property.SetValue(item, value);
        if (item is IEditableObject editable)
        {
            editable.EndEdit();
        }

        Invalidate();
        return true;
    }

    /// <summary>Removes the item a row shows from the data source, for a bound grid.</summary>
    /// <returns>Whether the grid is bound, so the row is removed when the list changes.</returns>
    internal bool RemoveBoundRow(DataGridViewRow row)
    {
        if (_boundList is not { } list || row.DataBoundItem is null)
        {
            return false;
        }

        var index = list.IndexOf(row.DataBoundItem);
        if (index >= 0)
        {
            list.RemoveAt(index);
        }

        if (list is not IBindingList)
        {
            Rebind();
        }

        return true;
    }

    /// <summary>Adds an item to the data source for the row for new records, as the user starts typing in it.</summary>
    /// <returns>The new item's row, or -1.</returns>
    private int AddBoundItem()
    {
        if (_boundList is not IBindingList { AllowNew: true } list)
        {
            return -1;
        }

        var item = list.AddNew();
        return item is null ? -1 : list.IndexOf(item);
    }

    /// <summary>Commits or cancels the item added for the row for new records.</summary>
    private void FinishBoundItem(int index, bool keep)
    {
        if (_boundList is ICancelAddNew cancel)
        {
            if (keep)
            {
                cancel.EndNew(index);
            }
            else
            {
                cancel.CancelNew(index);
            }
        }
    }

    private PropertyDescriptor? PropertyOf(DataGridViewColumn column)
    {
        if (_boundProperties is null || column.DataPropertyName.Length == 0)
        {
            return null;
        }

        return _boundProperties.Find(column.DataPropertyName, ignoreCase: true);
    }

    private void Bind()
    {
        _boundList = ResolveList(_dataSource, _dataMember);
        _boundProperties = _boundList is null ? null : PropertiesOf(_boundList);
        if (_boundList is IBindingList bindingList)
        {
            bindingList.ListChanged += OnBoundListChanged;
        }

        if (_boundList is not null && AutoGenerateColumns)
        {
            GenerateColumns();
        }

        Rebind();
        ConnectManager();
        OnDataBindingComplete(new DataGridViewBindingCompleteEventArgs(ListChangedType.Reset));
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged(EventArgs e)
    {
        ConnectManager();
        base.OnBindingContextChanged(e);
    }

    /// <summary>Moves the data source's current item to the current row, so controls bound to the same source follow it.</summary>
    private void SyncManagerPosition()
    {
        if (_manager is { } manager && _currentCell?.OwningRow is { IsNewRow: false, DataBoundItem: not null } row && manager.Position != row.Index)
        {
            manager.Position = row.Index;
        }
    }

    private void ConnectManager()
    {
        if (_manager is not null)
        {
            _manager.PositionChanged -= OnManagerPositionChanged;
            _manager = null;
        }

        if (_dataSource is null || _boundList is null || BindingContext is not { } context)
        {
            return;
        }

        _manager = context[_dataSource, _dataMember];
        _manager.PositionChanged += OnManagerPositionChanged;
    }

    private void OnManagerPositionChanged(object? sender, EventArgs e)
    {
        if (_manager is not { Position: var position } || position < 0 || position >= _rows.Count || _currentCell?.RowIndex == position)
        {
            return;
        }

        var column = _currentCell?.ColumnIndex ?? FirstVisibleColumn();
        if (column >= 0)
        {
            CurrentCell = _rows[position].Cells[column];
        }
    }

    private void DetachDataSource()
    {
        if (_manager is not null)
        {
            _manager.PositionChanged -= OnManagerPositionChanged;
            _manager = null;
        }

        if (_boundList is IBindingList bindingList)
        {
            bindingList.ListChanged -= OnBoundListChanged;
        }

        if (_boundList is not null)
        {
            _rebinding = true;
            try
            {
                CancelEdit();
                _currentCell = null;
                _selectedCells.Clear();
                _rows.ClearInternal();
            }
            finally
            {
                _rebinding = false;
            }

            if (AutoGenerateColumns)
            {
                foreach (var column in _columns.Items.Where(c => c.IsGenerated).ToList())
                {
                    _columns.Remove(column);
                }
            }
        }

        _boundList = null;
        _boundProperties = null;
    }

    /// <summary>Builds the rows again from the data source's items, keeping the current cell's position.</summary>
    private void Rebind()
    {
        if (_boundList is not { } list)
        {
            UpdateNewRow();
            return;
        }

        var currentColumn = _currentCell?.ColumnIndex ?? -1;
        var currentRow = _currentCell?.RowIndex ?? -1;
        var editing = _editingControl is not null;
        if (editing)
        {
            HideEditingControl();
        }

        _rebinding = true;
        try
        {
            _selectedCells.Clear();
            _currentCell = null;
            _rows.ClearInternal();
            if (_columns.Count > 0)
            {
                foreach (var item in list)
                {
                    var row = (DataGridViewRow)_rowTemplate.Clone();
                    row.DataBoundItem = item;
                    _rows.InsertInternal(_rows.Count, row);
                }
            }
        }
        finally
        {
            _rebinding = false;
        }

        UpdateNewRow();
        LayoutGrid();
        if (currentRow >= 0 && _rows.Count > 0 && currentColumn >= 0 && currentColumn < _columns.Count)
        {
            SetCurrentCellCore(_rows[Math.Min(currentRow, _rows.Count - 1)].Cells[currentColumn], raiseEvents: false);
            SelectFromCurrent(_currentCell!, extend: false, toggle: false);
        }
        else
        {
            EnsureCurrentCell();
        }

        Invalidate();
    }

    private void OnBoundListChanged(object? sender, ListChangedEventArgs e)
    {
        if (e.ListChangedType == ListChangedType.ItemChanged && e.NewIndex >= 0 && e.NewIndex < _rows.Count && _rows[e.NewIndex].DataBoundItem == _boundList?[e.NewIndex])
        {
            Invalidate();
            return;
        }

        if (e.ListChangedType is ListChangedType.PropertyDescriptorAdded or ListChangedType.PropertyDescriptorDeleted or ListChangedType.PropertyDescriptorChanged)
        {
            // The items are of another type, as when a binding source gets its data source after the grid bound to it.
            _boundProperties = _boundList is null ? null : PropertiesOf(_boundList);
            if (AutoGenerateColumns)
            {
                foreach (var column in _columns.Items.Where(c => c.IsGenerated).ToList())
                {
                    _columns.Remove(column);
                }

                GenerateColumns();
            }
        }
        else if (e.ListChangedType == ListChangedType.Reset && AutoGenerateColumns && _columns.Count == 0 && _boundList is not null)
        {
            _boundProperties = PropertiesOf(_boundList);
            GenerateColumns();
        }

        Rebind();
        OnDataBindingComplete(new DataGridViewBindingCompleteEventArgs(e.ListChangedType));
    }

    private void GenerateColumns()
    {
        if (_boundProperties is null)
        {
            return;
        }

        foreach (PropertyDescriptor property in _boundProperties)
        {
            if (!property.IsBrowsable || _columns.Items.Exists(column => string.Equals(column.DataPropertyName, property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            DataGridViewColumn column = type == typeof(bool) || type == typeof(CheckState)
                ? new DataGridViewCheckBoxColumn(type == typeof(CheckState))
                : typeof(System.Drawing.Image).IsAssignableFrom(type) || type == typeof(byte[])
                    ? new DataGridViewImageColumn()
                    : new DataGridViewTextBoxColumn();
            column.Name = property.Name;
            column.DataPropertyName = property.Name;
            column.HeaderText = property.DisplayName;
            column.ValueType = property.PropertyType;
            column.ReadOnly = property.IsReadOnly;
            column.IsGenerated = true;
            _columns.Add(column);
        }
    }

    private static IList? ResolveList(object? source, string member)
    {
        switch (source)
        {
            case null:
                return null;
            case DataSet set when member.Length > 0:
                return set.Tables[member]?.DefaultView;
            case DataSet set:
                return set.Tables.Count > 0 ? set.Tables[0].DefaultView : null;
            case IListSource listSource:
            {
                var list = listSource.GetList();
                return member.Length > 0 && list is ITypedList typed && typed.GetItemProperties(null).Find(member, true) is { } property && list.Count > 0
                    ? property.GetValue(list[0]) as IList
                    : list;
            }

            case IList list when member.Length > 0 && list.Count > 0:
            {
                var property = TypeDescriptor.GetProperties(list[0]!).Find(member, true);
                return property?.GetValue(list[0]) as IList ?? list;
            }

            case IList list:
                return list;
            default:
                return null;
        }
    }

    private static PropertyDescriptorCollection PropertiesOf(IList list)
    {
        if (list is ITypedList typed)
        {
            return typed.GetItemProperties(null);
        }

        var itemType = ItemTypeOf(list);
        if (itemType is not null && itemType != typeof(object))
        {
            return TypeDescriptor.GetProperties(itemType);
        }

        return list.Count > 0 && list[0] is { } first ? TypeDescriptor.GetProperties(first) : PropertyDescriptorCollection.Empty;
    }

    private static Type? ItemTypeOf(IList list)
    {
        var type = list.GetType();
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        foreach (var implemented in type.GetInterfaces().Prepend(type))
        {
            if (implemented.IsGenericType && implemented.GetGenericTypeDefinition() is var definition
                && (definition == typeof(IList<>) || definition == typeof(IReadOnlyList<>) || definition == typeof(ICollection<>)))
            {
                return implemented.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
