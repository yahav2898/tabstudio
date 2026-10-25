using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>
/// A cell of a grid: its value, which a bound grid reads from and writes to its data source, the text it shows,
/// its style, and how it is drawn and edited.
/// </summary>
public abstract class DataGridViewCell : DataGridViewElement, ICloneable, IDisposable
{
    private object? _value;
    private DataGridViewCellStyle? _style;
    private Type? _valueType;
    private bool _readOnly;

    /// <summary>Creates a cell.</summary>
    protected DataGridViewCell()
    {
    }

    /// <summary>The cell's column.</summary>
    public DataGridViewColumn? OwningColumn { get; internal set; }

    /// <summary>The cell's row.</summary>
    public DataGridViewRow? OwningRow { get; internal set; }

    /// <summary>The index of the cell's column, or -1.</summary>
    public int ColumnIndex => OwningColumn?.Index ?? -1;

    /// <summary>The index of the cell's row, or -1.</summary>
    public int RowIndex => OwningRow?.Index ?? -1;

    /// <summary>The value: from the data source in a bound grid.</summary>
    public object? Value
    {
        get => GetValue(RowIndex);
        set
        {
            if (SetValue(RowIndex, value) && DataGridView is { } grid && RowIndex >= 0 && ColumnIndex >= 0)
            {
                grid.RaiseCellValueChanged(new DataGridViewCellEventArgs(ColumnIndex, RowIndex));
            }
        }
    }

    /// <summary>The value as shown, after formatting.</summary>
    public object? FormattedValue => FormattedValueFor(Value, InheritedStyle);

    /// <summary>The value as typed while editing, or as shown otherwise.</summary>
    public object? EditedFormattedValue => DataGridView?.EditedFormattedValueOf(this) ?? FormattedValue;

    /// <summary>The type of the value as shown.</summary>
    public virtual Type? FormattedValueType => typeof(string);

    /// <summary>The type of the value.</summary>
    public virtual Type? ValueType
    {
        get => _valueType ?? OwningColumn?.ValueType ?? DataGridView?.BoundTypeOf(OwningColumn) ?? typeof(object);
        set => _valueType = value;
    }

    /// <summary>The type of the control that edits the cell, or <see langword="null"/> for cells edited in place.</summary>
    public virtual Type? EditType => typeof(DataGridViewTextBoxEditingControl);

    /// <summary>The value of the cell in the row for new records.</summary>
    public virtual object? DefaultNewRowValue => null;

    /// <summary>The cell's own style.</summary>
    [AllowNull]
    public DataGridViewCellStyle Style
    {
        get => _style ??= new DataGridViewCellStyle();
        set
        {
            _style = value;
            DataGridView?.Invalidate();
        }
    }

    /// <summary>Whether the cell has a style of its own.</summary>
    public bool HasStyle => _style is not null;

    /// <summary>The style the cell is drawn with: its own over its row's, its column's and the grid's.</summary>
    public DataGridViewCellStyle InheritedStyle => DataGridView?.InheritedStyleOf(this) ?? Style;

    /// <summary>Whether the cell cannot be edited.</summary>
    public virtual bool ReadOnly
    {
        get => _readOnly || (OwningRow?.ReadOnly ?? false) || (OwningColumn?.ReadOnly ?? false) || (DataGridView?.ReadOnly ?? false);
        set
        {
            _readOnly = value;
            DataGridView?.Invalidate();
        }
    }

    /// <summary>Whether the cell is selected.</summary>
    public virtual bool Selected
    {
        get => DataGridView?.IsCellSelected(this) ?? false;
        set => DataGridView?.SetCellSelected(this, value);
    }

    /// <summary>Whether the cell is being edited.</summary>
    public bool IsInEditMode => DataGridView?.IsEditing(this) ?? false;

    /// <summary>Whether the cell is shown.</summary>
    public bool Visible => (OwningRow?.Visible ?? true) && (OwningColumn?.Visible ?? true);

    /// <summary>Whether the cell is on the screen now.</summary>
    public bool Displayed => Visible && DataGridView is not null;

    /// <summary>Whether the cell stays in view when the grid scrolls. Kept for compatibility.</summary>
    public bool Frozen => false;

    /// <summary>The cell's state.</summary>
    public override DataGridViewElementStates State =>
        (Visible ? DataGridViewElementStates.Visible : DataGridViewElementStates.None)
        | (ReadOnly ? DataGridViewElementStates.ReadOnly : DataGridViewElementStates.None)
        | (Selected ? DataGridViewElementStates.Selected : DataGridViewElementStates.None)
        | (Displayed ? DataGridViewElementStates.Displayed : DataGridViewElementStates.None);

    /// <summary>The cell's tooltip.</summary>
    [AllowNull]
    public string ToolTipText
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>An error shown with an icon in the cell.</summary>
    [AllowNull]
    public string ErrorText
    {
        get => field ?? string.Empty;
        set
        {
            field = value;
            DataGridView?.Invalidate();
        }
    }

    /// <summary>Data about the cell.</summary>
    public object? Tag { get; set; }

    /// <summary>The cell's size.</summary>
    public Size Size => new(OwningColumn?.Width ?? 0, OwningRow?.Height ?? 0);

    /// <summary>The bounds of the cell's content, in the cell.</summary>
    public Rectangle ContentBounds => new(Point.Empty, Size);

    /// <summary>The menu a long press on the cell opens.</summary>
    public virtual ContextMenuStrip? ContextMenuStrip { get; set; }

    /// <summary>The value the cell stores itself, in an unbound grid.</summary>
    internal object? StoredValue
    {
        get => _value;
        set => _value = value;
    }

    /// <summary>Converts a typed value to the cell's value type.</summary>
    /// <param name="formattedValue">The typed value.</param>
    /// <param name="cellStyle">The cell's style.</param>
    /// <param name="formattedValueTypeConverter">A converter for the typed value's type.</param>
    /// <param name="valueTypeConverter">A converter for the value type.</param>
    public virtual object? ParseFormattedValue(object? formattedValue, DataGridViewCellStyle cellStyle, TypeConverter? formattedValueTypeConverter, TypeConverter? valueTypeConverter)
    {
        ArgumentNullException.ThrowIfNull(cellStyle);
        var valueType = ValueType ?? typeof(object);
        var provider = cellStyle.FormatProvider ?? CultureInfo.CurrentCulture;
        if (formattedValue is string text)
        {
            if (text.Length == 0 || Equals(text, cellStyle.NullValue))
            {
                if (cellStyle.IsDataSourceNullValueDefault)
                {
                    return DataGridView?.IsBoundToDataTable == true ? DBNull.Value
                        : !valueType.IsValueType || Nullable.GetUnderlyingType(valueType) is not null ? null
                        : throw new FormatException($"'{text}' is not a valid value for {valueType.Name}.");
                }

                return cellStyle.DataSourceNullValue;
            }

            if (valueType == typeof(string) || valueType == typeof(object))
            {
                return text;
            }

            var target = Nullable.GetUnderlyingType(valueType) ?? valueType;
            var converter = valueTypeConverter ?? TypeDescriptor.GetConverter(target);
            if (converter.CanConvertFrom(typeof(string)))
            {
                return converter.ConvertFromString(null, provider as CultureInfo ?? CultureInfo.CurrentCulture, text);
            }

            return Convert.ChangeType(text, target, provider);
        }

        return formattedValue;
    }

    /// <summary>Copies the cell, without its row and column.</summary>
    public virtual object Clone()
    {
        var cell = (DataGridViewCell)MemberwiseClone();
        cell.DataGridView = null;
        cell.OwningColumn = null;
        cell.OwningRow = null;
        cell._style = _style?.Clone();
        return cell;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public override string ToString() => $"{GetType().Name} {{ ColumnIndex={ColumnIndex}, RowIndex={RowIndex} }}";

    /// <summary>Releases the cell.</summary>
    /// <param name="disposing">Whether called from Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
    }

    /// <summary>The cell's value: from the data source in a bound grid, or its own.</summary>
    /// <param name="rowIndex">The cell's row.</param>
    protected virtual object? GetValue(int rowIndex)
    {
        if (DataGridView is { } grid && grid.TryGetBoundValue(this, out var bound))
        {
            return bound;
        }

        return OwningRow is { IsNewRow: true } ? DefaultNewRowValue : _value;
    }

    /// <summary>Sets the cell's value: in the data source in a bound grid, or its own.</summary>
    /// <param name="rowIndex">The cell's row.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the value was set.</returns>
    protected virtual bool SetValue(int rowIndex, object? value)
    {
        if (DataGridView is { } grid && grid.TrySetBoundValue(this, value))
        {
            return true;
        }

        if (Equals(_value, value))
        {
            return false;
        }

        _value = value;
        DataGridView?.InvalidateCell(this);
        return true;
    }

    /// <summary>The value as shown, as the grid formats it without the CellFormatting event.</summary>
    /// <param name="value">The value.</param>
    /// <param name="rowIndex">The cell's row.</param>
    /// <param name="cellStyle">The cell's style.</param>
    /// <param name="valueTypeConverter">A converter for the value type.</param>
    /// <param name="formattedValueTypeConverter">A converter for the shown type.</param>
    /// <param name="context">What the grid is doing.</param>
    protected virtual object? GetFormattedValue(object? value, int rowIndex, ref DataGridViewCellStyle cellStyle, TypeConverter? valueTypeConverter, TypeConverter? formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        ArgumentNullException.ThrowIfNull(cellStyle);
        if (value is null || value == DBNull.Value)
        {
            return cellStyle.NullValue;
        }

        var provider = cellStyle.FormatProvider ?? CultureInfo.CurrentCulture;
        if (cellStyle.Format.Length > 0 && value is IFormattable formattable)
        {
            return formattable.ToString(cellStyle.Format, provider);
        }

        if (value is string text)
        {
            return text;
        }

        var converter = valueTypeConverter ?? TypeDescriptor.GetConverter(value.GetType());
        return converter.CanConvertTo(typeof(string))
            ? converter.ConvertToString(null, provider as CultureInfo ?? CultureInfo.CurrentCulture, value)
            : Convert.ToString(value, provider);
    }

    /// <summary>The value as shown, through the grid's CellFormatting event and then the cell's formatting.</summary>
    internal object? FormattedValueFor(object? value, DataGridViewCellStyle style)
    {
        if (DataGridView is { } grid && RowIndex >= 0 && ColumnIndex >= 0)
        {
            var e = new DataGridViewCellFormattingEventArgs(ColumnIndex, RowIndex, value, FormattedValueType, style);
            grid.RaiseCellFormatting(e);
            if (e.FormattingApplied)
            {
                return e.Value;
            }

            value = e.Value;
            style = e.CellStyle;
        }

        try
        {
            return GetFormattedValue(value, RowIndex, ref style, null, null, DataGridViewDataErrorContexts.Formatting);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or ArgumentException or NotSupportedException)
        {
            DataGridView?.RaiseDataError(new DataGridViewDataErrorEventArgs(exception, ColumnIndex, RowIndex, DataGridViewDataErrorContexts.Formatting | DataGridViewDataErrorContexts.Display), showDialog: false);
            return string.Empty;
        }
    }

    /// <summary>Draws the cell's content.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="bounds">The cell's bounds, without its borders.</param>
    /// <param name="style">The cell's style.</param>
    /// <param name="formatted">The value as shown.</param>
    /// <param name="selected">Whether the cell is drawn selected.</param>
    internal virtual void PaintContent(Graphics g, Rectangle bounds, DataGridViewCellStyle style, object? formatted, bool selected)
    {
        if (formatted is not string text || text.Length == 0)
        {
            return;
        }

        var color = selected ? style.SelectionForeColor : style.ForeColor;
        var font = style.Font ?? DataGridView?.Font ?? Control.DefaultFont;
        var padding = style.Padding;
        var area = Rectangle.FromLTRB(bounds.Left + 2 + padding.Left, bounds.Top + 1 + padding.Top, bounds.Right - 2 - padding.Right, bounds.Bottom - 1 - padding.Bottom);
        ThemePainter.DrawText(g, text, font, color, area, ToContentAlignment(style.Alignment), wrap: style.WrapMode == DataGridViewTriState.True, ellipsis: true, mnemonics: false);
    }

    /// <summary>Called when the cell is tapped, for cells that act on a tap, such as check boxes and buttons.</summary>
    /// <param name="point">Where, in the cell.</param>
    /// <returns>Whether the tap was on the cell's content.</returns>
    internal virtual bool OnTap(Point point) => false;

    /// <summary>Called when Space is pressed on the current cell, for cells that act on it.</summary>
    /// <returns>Whether the cell took the key.</returns>
    internal virtual bool OnSpace() => false;

    /// <summary>The content alignment of a grid alignment.</summary>
    internal static ContentAlignment ToContentAlignment(DataGridViewContentAlignment alignment) => alignment switch
    {
        DataGridViewContentAlignment.TopLeft => ContentAlignment.TopLeft,
        DataGridViewContentAlignment.TopCenter => ContentAlignment.TopCenter,
        DataGridViewContentAlignment.TopRight => ContentAlignment.TopRight,
        DataGridViewContentAlignment.MiddleCenter => ContentAlignment.MiddleCenter,
        DataGridViewContentAlignment.MiddleRight => ContentAlignment.MiddleRight,
        DataGridViewContentAlignment.BottomLeft => ContentAlignment.BottomLeft,
        DataGridViewContentAlignment.BottomCenter => ContentAlignment.BottomCenter,
        DataGridViewContentAlignment.BottomRight => ContentAlignment.BottomRight,
        _ => ContentAlignment.MiddleLeft,
    };
}

/// <summary>A cell of text, edited in a text box.</summary>
public class DataGridViewTextBoxCell : DataGridViewCell
{
    /// <summary>Creates a text cell.</summary>
    public DataGridViewTextBoxCell()
    {
    }

    /// <summary>The most characters the cell takes.</summary>
    public virtual int MaxInputLength { get; set; } = 32767;
}

/// <summary>A cell with a check box, toggled by a tap or Space.</summary>
public class DataGridViewCheckBoxCell : DataGridViewCell, IDataGridViewEditingCell
{
    /// <summary>Creates a two-state check box cell.</summary>
    public DataGridViewCheckBoxCell()
        : this(false)
    {
    }

    /// <summary>Creates a check box cell with two or three states.</summary>
    /// <param name="threeState">Whether the cell has a third, indeterminate state.</param>
    public DataGridViewCheckBoxCell(bool threeState)
    {
        ThreeState = threeState;
    }

    /// <summary>Whether the cell has a third, indeterminate state.</summary>
    public bool ThreeState { get; set; }

    /// <summary>The value of a checked cell.</summary>
    public object? TrueValue { get; set; }

    /// <summary>The value of an unchecked cell.</summary>
    public object? FalseValue { get; set; }

    /// <summary>The value of an indeterminate cell.</summary>
    public object? IndeterminateValue { get; set; }

    /// <summary>The look. Every style draws the Windows 10 look.</summary>
    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

    /// <inheritdoc />
    public override Type? FormattedValueType => ThreeState ? typeof(CheckState) : typeof(bool);

    /// <inheritdoc />
    public override Type? EditType => null;

    /// <inheritdoc />
    public override object? DefaultNewRowValue => ThreeState ? CheckState.Indeterminate : false;

    /// <inheritdoc />
    public object? EditingCellFormattedValue
    {
        get => GetEditingCellFormattedValue(DataGridViewDataErrorContexts.Formatting);
        set => Value = value;
    }

    /// <inheritdoc />
    public bool EditingCellValueChanged { get; set; }

    /// <inheritdoc />
    public object GetEditingCellFormattedValue(DataGridViewDataErrorContexts context) =>
        ThreeState ? StateOf(Value) : StateOf(Value) == CheckState.Checked;

    /// <inheritdoc />
    public void PrepareEditingCellForEdit(bool selectAll)
    {
    }

    /// <inheritdoc />
    public override object? ParseFormattedValue(object? formattedValue, DataGridViewCellStyle cellStyle, TypeConverter? formattedValueTypeConverter, TypeConverter? valueTypeConverter)
    {
        var state = formattedValue switch
        {
            CheckState checkState => checkState,
            bool flag => flag ? CheckState.Checked : CheckState.Unchecked,
            _ => CheckState.Unchecked,
        };

        return state switch
        {
            CheckState.Checked => TrueValue ?? (ValueType == typeof(CheckState) ? CheckState.Checked : true),
            CheckState.Indeterminate => IndeterminateValue ?? (ValueType == typeof(CheckState) ? CheckState.Indeterminate : null),
            _ => FalseValue ?? (ValueType == typeof(CheckState) ? CheckState.Unchecked : false),
        };
    }

    /// <inheritdoc />
    protected override object? GetFormattedValue(object? value, int rowIndex, ref DataGridViewCellStyle cellStyle, TypeConverter? valueTypeConverter, TypeConverter? formattedValueTypeConverter, DataGridViewDataErrorContexts context) =>
        ThreeState ? StateOf(value) : StateOf(value) == CheckState.Checked;

    /// <inheritdoc />
    internal override void PaintContent(Graphics g, Rectangle bounds, DataGridViewCellStyle style, object? formatted, bool selected)
    {
        var state = formatted switch
        {
            CheckState checkState => checkState,
            true => CheckState.Checked,
            _ => CheckState.Unchecked,
        };
        var size = new Size(ThemePainter.GlyphSize, ThemePainter.GlyphSize);
        var box = ThemePainter.Align(bounds, size, ToContentAlignment(style.Alignment == DataGridViewContentAlignment.NotSet ? DataGridViewContentAlignment.MiddleCenter : style.Alignment));
        ThemePainter.DrawCheckBox(g, box, state, ReadOnly && !(DataGridView?.Enabled ?? true) ? PushState.Disabled : PushState.Normal);
    }

    /// <inheritdoc />
    internal override bool OnTap(Point point)
    {
        Toggle();
        return true;
    }

    /// <inheritdoc />
    internal override bool OnSpace()
    {
        Toggle();
        return true;
    }

    private void Toggle()
    {
        if (ReadOnly)
        {
            return;
        }

        var current = StateOf(Value);
        var next = current switch
        {
            CheckState.Unchecked => CheckState.Checked,
            CheckState.Checked => ThreeState ? CheckState.Indeterminate : CheckState.Unchecked,
            _ => CheckState.Unchecked,
        };

        DataGridView?.CommitCellValue(this, ParseFormattedValue(ThreeState ? next : next == CheckState.Checked, InheritedStyle, null, null));
    }

    private CheckState StateOf(object? value)
    {
        if (value is null || value == DBNull.Value)
        {
            return ThreeState ? CheckState.Indeterminate : CheckState.Unchecked;
        }

        if (TrueValue is not null && Equals(value, TrueValue))
        {
            return CheckState.Checked;
        }

        if (FalseValue is not null && Equals(value, FalseValue))
        {
            return CheckState.Unchecked;
        }

        if (IndeterminateValue is not null && Equals(value, IndeterminateValue))
        {
            return CheckState.Indeterminate;
        }

        return value switch
        {
            CheckState state => state,
            bool flag => flag ? CheckState.Checked : CheckState.Unchecked,
            string text when bool.TryParse(text, out var parsed) => parsed ? CheckState.Checked : CheckState.Unchecked,
            IConvertible convertible => Convert.ToInt32(convertible, CultureInfo.InvariantCulture) != 0 ? CheckState.Checked : CheckState.Unchecked,
            _ => CheckState.Unchecked,
        };
    }
}

/// <summary>A cell that picks its value from a list, edited in a combo box.</summary>
public class DataGridViewComboBoxCell : DataGridViewCell
{
    private ObjectCollection _items;
    private object? _dataSource;

    /// <summary>Creates a combo box cell.</summary>
    public DataGridViewComboBoxCell()
    {
        _items = new ObjectCollection(this);
    }

    /// <summary>The items of the list.</summary>
    public virtual ObjectCollection Items => _items;

    /// <summary>The data source of the list.</summary>
    public virtual object? DataSource
    {
        get => _dataSource;
        set
        {
            if (value is not null and not System.Collections.IList and not IListSource)
            {
                throw new ArgumentException("Value provided for DataSource is not valid. The value should implement IList or IListSource.", nameof(value));
            }

            _dataSource = value;
            DataGridView?.Invalidate();
        }
    }

    /// <summary>The property of the list's items shown.</summary>
    [AllowNull]
    public virtual string DisplayMember
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>The property of the list's items stored in the cell.</summary>
    [AllowNull]
    public virtual string ValueMember
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>How the cell shows its drop-down button.</summary>
    public DataGridViewComboBoxDisplayStyle DisplayStyle { get; set; } = DataGridViewComboBoxDisplayStyle.DropDownButton;

    /// <summary>Whether the list is sorted.</summary>
    public virtual bool Sorted { get; set; }

    /// <summary>The most items the list shows before it scrolls.</summary>
    public virtual int MaxDropDownItems { get; set; } = 8;

    /// <summary>The width of the list.</summary>
    public virtual int DropDownWidth { get; set; } = 1;

    /// <inheritdoc />
    public override Type? EditType => typeof(DataGridViewComboBoxEditingControl);

    /// <summary>The look. Every style draws the Windows 10 look.</summary>
    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

    /// <summary>The items of the list: from the data source, or the Items collection.</summary>
    internal IReadOnlyList<object> ListItems
    {
        get
        {
            var source = DataSource ?? (OwningColumn as DataGridViewComboBoxColumn)?.DataSource;
            var list = source switch
            {
                IListSource listSource => listSource.GetList(),
                System.Collections.IList items => items,
                _ => null,
            };

            return list is null ? [.. _items.Cast<object>()] : [.. list.Cast<object?>().Where(item => item is not null).Cast<object>()];
        }
    }

    /// <summary>The text an item of the list shows.</summary>
    internal string ItemText(object? item) => Convert.ToString(ListControl.GetMemberValue(item, DisplayMember), CultureInfo.CurrentCulture) ?? string.Empty;

    /// <summary>The value an item of the list stores in the cell.</summary>
    internal object? ItemValue(object? item) => ListControl.GetMemberValue(item, ValueMember);

    /// <inheritdoc />
    public override object? ParseFormattedValue(object? formattedValue, DataGridViewCellStyle cellStyle, TypeConverter? formattedValueTypeConverter, TypeConverter? valueTypeConverter)
    {
        if (formattedValue is string text)
        {
            foreach (var item in ListItems)
            {
                if (string.Equals(ItemText(item), text, StringComparison.Ordinal))
                {
                    return ItemValue(item);
                }
            }
        }

        return base.ParseFormattedValue(formattedValue, cellStyle, formattedValueTypeConverter, valueTypeConverter);
    }

    /// <inheritdoc />
    public override object Clone()
    {
        var clone = (DataGridViewComboBoxCell)base.Clone();
        clone._items = new ObjectCollection(clone);
        clone._items.Store.AddRange(_items.Store);
        return clone;
    }

    /// <inheritdoc />
    protected override object? GetFormattedValue(object? value, int rowIndex, ref DataGridViewCellStyle cellStyle, TypeConverter? valueTypeConverter, TypeConverter? formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        if (value is not null && value != DBNull.Value)
        {
            foreach (var item in ListItems)
            {
                if (Equals(ItemValue(item), value))
                {
                    return ItemText(item);
                }
            }
        }

        return base.GetFormattedValue(value, rowIndex, ref cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g, Rectangle bounds, DataGridViewCellStyle style, object? formatted, bool selected)
    {
        if (DisplayStyle == DataGridViewComboBoxDisplayStyle.Nothing)
        {
            base.PaintContent(g, bounds, style, formatted, selected);
            return;
        }

        var button = new Rectangle(bounds.Right - ComboBox.ButtonWidth, bounds.Top + 1, ComboBox.ButtonWidth - 1, bounds.Height - 2);
        if (DisplayStyle == DataGridViewComboBoxDisplayStyle.ComboBox)
        {
            ThemePainter.DrawButton(g, Rectangle.Inflate(bounds, -1, -1), PushState.Normal, emphasized: false);
        }

        ThemePainter.DrawArrow(g, button, ArrowDirection.Down, selected && DisplayStyle == DataGridViewComboBoxDisplayStyle.DropDownButton ? style.SelectionForeColor : Theme.Glyph);
        base.PaintContent(g, Rectangle.FromLTRB(bounds.Left, bounds.Top, button.Left, bounds.Bottom), style, formatted, selected && DisplayStyle != DataGridViewComboBoxDisplayStyle.ComboBox);
    }

    /// <summary>The items of a combo box cell's list.</summary>
    public class ObjectCollection : System.Collections.IList
    {
        internal ObjectCollection(DataGridViewComboBoxCell owner) => Owner = owner;

        /// <summary>The number of items.</summary>
        public int Count => Store.Count;

        /// <summary>Whether the collection is read-only.</summary>
        public bool IsReadOnly => false;

        /// <inheritdoc />
        bool System.Collections.IList.IsFixedSize => false;

        /// <inheritdoc />
        bool System.Collections.ICollection.IsSynchronized => false;

        /// <inheritdoc />
        object System.Collections.ICollection.SyncRoot => this;

        internal List<object> Store { get; } = [];

        private DataGridViewComboBoxCell Owner { get; }

        /// <summary>The item at an index.</summary>
        /// <param name="index">The index.</param>
        public virtual object this[int index]
        {
            get => Store[index];
            set => Store[index] = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <inheritdoc />
        object? System.Collections.IList.this[int index]
        {
            get => this[index];
            set => this[index] = value!;
        }

        /// <summary>Adds an item.</summary>
        /// <param name="item">The item.</param>
        /// <returns>Its index.</returns>
        public int Add(object item)
        {
            ArgumentNullException.ThrowIfNull(item);
            Store.Add(item);
            Owner.DataGridView?.Invalidate();
            return Store.Count - 1;
        }

        /// <summary>Adds items.</summary>
        /// <param name="items">The items.</param>
        public void AddRange(params object[] items)
        {
            ArgumentNullException.ThrowIfNull(items);
            Store.AddRange(items);
            Owner.DataGridView?.Invalidate();
        }

        /// <summary>Adds the items of another collection.</summary>
        /// <param name="value">The items.</param>
        public void AddRange(ObjectCollection value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Store.AddRange(value.Store);
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
        public void Clear() => Store.Clear();

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
        void System.Collections.ICollection.CopyTo(Array destination, int index) => ((System.Collections.ICollection)Store).CopyTo(destination, index);

        /// <inheritdoc />
        public System.Collections.IEnumerator GetEnumerator() => Store.GetEnumerator();

        /// <inheritdoc />
        int System.Collections.IList.Add(object? value) => Add(value!);

        /// <inheritdoc />
        bool System.Collections.IList.Contains(object? value) => value is not null && Contains(value);

        /// <inheritdoc />
        int System.Collections.IList.IndexOf(object? value) => value is null ? -1 : IndexOf(value);

        /// <inheritdoc />
        void System.Collections.IList.Insert(int index, object? value) => Insert(index, value!);

        /// <inheritdoc />
        void System.Collections.IList.Remove(object? value)
        {
            if (value is not null)
            {
                Remove(value);
            }
        }
    }
}

/// <summary>A cell with a button, which raises CellContentClick when tapped.</summary>
public class DataGridViewButtonCell : DataGridViewCell
{
    /// <summary>Creates a button cell.</summary>
    public DataGridViewButtonCell()
    {
    }

    /// <summary>Whether the cell shows its column's text rather than its value.</summary>
    public bool UseColumnTextForButtonValue { get; set; }

    /// <summary>The look. Every style draws the Windows 10 look.</summary>
    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

    /// <inheritdoc />
    public override Type? EditType => null;

    /// <inheritdoc />
    protected override object? GetValue(int rowIndex) =>
        (UseColumnTextForButtonValue || (OwningColumn as DataGridViewButtonColumn)?.UseColumnTextForButtonValue == true) && OwningColumn is DataGridViewButtonColumn column
            ? column.Text
            : base.GetValue(rowIndex);

    /// <inheritdoc />
    internal override void PaintContent(Graphics g, Rectangle bounds, DataGridViewCellStyle style, object? formatted, bool selected)
    {
        var face = Rectangle.Inflate(bounds, -2, -2);
        ThemePainter.DrawButton(g, face, PushState.Normal, emphasized: false);
        var font = style.Font ?? DataGridView?.Font ?? Control.DefaultFont;
        ThemePainter.DrawText(g, formatted as string ?? string.Empty, font, style.ForeColor, face, ToContentAlignment(style.Alignment == DataGridViewContentAlignment.NotSet ? DataGridViewContentAlignment.MiddleCenter : style.Alignment), ellipsis: true, mnemonics: false);
    }

    /// <inheritdoc />
    internal override bool OnTap(Point point) => true;

    /// <inheritdoc />
    internal override bool OnSpace() => true;
}

/// <summary>A cell with a link, which raises CellContentClick when tapped.</summary>
public class DataGridViewLinkCell : DataGridViewCell
{
    /// <summary>Creates a link cell.</summary>
    public DataGridViewLinkCell()
    {
    }

    /// <summary>Whether the link was visited.</summary>
    public bool LinkVisited { get; set; }

    /// <summary>Whether the cell shows its column's text rather than its value.</summary>
    public bool UseColumnTextForLinkValue { get; set; }

    /// <summary>Whether tapping the link marks it visited.</summary>
    public bool TrackVisitedState { get; set; } = true;

    /// <summary>The color of the link.</summary>
    public Color LinkColor { get; set; } = Color.FromArgb(0, 102, 204);

    /// <summary>The color of a visited link.</summary>
    public Color VisitedLinkColor { get; set; } = Color.FromArgb(128, 0, 128);

    /// <summary>The color of a link being pressed.</summary>
    public Color ActiveLinkColor { get; set; } = Color.Red;

    /// <summary>When the link is underlined.</summary>
    public LinkBehavior LinkBehavior { get; set; }

    /// <inheritdoc />
    public override Type? EditType => null;

    /// <inheritdoc />
    protected override object? GetValue(int rowIndex) =>
        (UseColumnTextForLinkValue || (OwningColumn as DataGridViewLinkColumn)?.UseColumnTextForLinkValue == true) && OwningColumn is DataGridViewLinkColumn column
            ? column.Text
            : base.GetValue(rowIndex);

    /// <inheritdoc />
    internal override void PaintContent(Graphics g, Rectangle bounds, DataGridViewCellStyle style, object? formatted, bool selected)
    {
        var font = style.Font ?? DataGridView?.Font ?? Control.DefaultFont;
        using var link = new Font(font, font.Style | (LinkBehavior == LinkBehavior.NeverUnderline ? FontStyle.Regular : FontStyle.Underline));
        var color = selected ? style.SelectionForeColor : LinkVisited ? VisitedLinkColor : LinkColor;
        ThemePainter.DrawText(g, formatted as string ?? string.Empty, link, color, Rectangle.Inflate(bounds, -2, -1), ToContentAlignment(style.Alignment), ellipsis: true, mnemonics: false);
    }

    /// <inheritdoc />
    internal override bool OnTap(Point point)
    {
        if (TrackVisitedState)
        {
            LinkVisited = true;
        }

        return true;
    }
}

/// <summary>A cell with an image.</summary>
public class DataGridViewImageCell : DataGridViewCell
{
    /// <summary>Creates an image cell.</summary>
    public DataGridViewImageCell()
        : this(false)
    {
    }

    /// <summary>Creates an image or icon cell.</summary>
    /// <param name="valueIsIcon">Whether the value is an icon.</param>
    public DataGridViewImageCell(bool valueIsIcon)
    {
        ValueIsIcon = valueIsIcon;
    }

    /// <summary>Whether the value is an icon.</summary>
    public bool ValueIsIcon { get; set; }

    /// <summary>How the image is fitted.</summary>
    public DataGridViewImageCellLayout ImageLayout { get; set; } = DataGridViewImageCellLayout.Normal;

    /// <summary>The description of the image, for accessibility.</summary>
    public string? Description { get; set; }

    /// <inheritdoc />
    public override Type? FormattedValueType => ValueIsIcon ? typeof(Icon) : typeof(Image);

    /// <inheritdoc />
    public override Type? EditType => null;

    /// <inheritdoc />
    protected override object? GetFormattedValue(object? value, int rowIndex, ref DataGridViewCellStyle cellStyle, TypeConverter? valueTypeConverter, TypeConverter? formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        switch (value)
        {
            case Image image:
                return image;
            case Icon icon:
                return icon.ToBitmap();
            case byte[] bytes:
                using (var stream = new MemoryStream(bytes))
                {
                    return Image.FromStream(stream);
                }

            default:
                return (OwningColumn as DataGridViewImageColumn)?.Image;
        }
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g, Rectangle bounds, DataGridViewCellStyle style, object? formatted, bool selected)
    {
        if (formatted is not Image image)
        {
            return;
        }

        var area = Rectangle.Inflate(bounds, -1, -1);
        var layout = ImageLayout == DataGridViewImageCellLayout.NotSet ? DataGridViewImageCellLayout.Normal : ImageLayout;
        var target = layout switch
        {
            DataGridViewImageCellLayout.Stretch => area,
            DataGridViewImageCellLayout.Zoom => Control.Zoom(area, image.Size),
            _ => ThemePainter.Align(area, image.Size, ToContentAlignment(style.Alignment == DataGridViewContentAlignment.NotSet ? DataGridViewContentAlignment.MiddleCenter : style.Alignment)),
        };

        var state = g.Save();
        g.IntersectClip(area);
        g.DrawImage(image, target);
        g.Restore(state);
    }
}

/// <summary>A header cell of a grid.</summary>
public class DataGridViewHeaderCell : DataGridViewCell
{
    private object? _headerValue;

    /// <summary>Creates a header cell.</summary>
    public DataGridViewHeaderCell()
    {
    }

    /// <summary>The header's value, such as its text.</summary>
    public new object? Value
    {
        get => _headerValue;
        set
        {
            _headerValue = value;
            DataGridView?.Invalidate();
        }
    }

    /// <inheritdoc />
    public override Type? EditType => null;

    /// <inheritdoc />
    public override bool ReadOnly
    {
        get => true;
        set
        {
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{GetType().Name} {{ {(_headerValue ?? string.Empty)} }}";
}

/// <summary>The header cell of a column.</summary>
public class DataGridViewColumnHeaderCell : DataGridViewHeaderCell
{
    /// <summary>Creates a column header cell.</summary>
    public DataGridViewColumnHeaderCell()
    {
    }

    /// <summary>The direction of the sort glyph.</summary>
    public SortOrder SortGlyphDirection
    {
        get => field;
        set
        {
            field = value;
            DataGridView?.Invalidate();
        }
    }

    /// <summary>The column the header belongs to.</summary>
    internal DataGridViewColumn? OwningColumnInternal
    {
        get => OwningColumn;
        set => OwningColumn = value;
    }
}

/// <summary>The header cell of a row.</summary>
public class DataGridViewRowHeaderCell : DataGridViewHeaderCell
{
    /// <summary>Creates a row header cell.</summary>
    public DataGridViewRowHeaderCell()
    {
    }
}

/// <summary>The header cell at the top left of a grid.</summary>
public class DataGridViewTopLeftHeaderCell : DataGridViewColumnHeaderCell
{
    /// <summary>Creates the top left header cell.</summary>
    public DataGridViewTopLeftHeaderCell()
    {
    }
}

/// <summary>A cell edited in place, without an editing control, such as a check box cell.</summary>
public interface IDataGridViewEditingCell
{
    /// <summary>The value as being edited.</summary>
    object? EditingCellFormattedValue { get; set; }

    /// <summary>Whether the value was changed.</summary>
    bool EditingCellValueChanged { get; set; }

    /// <summary>The value as being edited.</summary>
    /// <param name="context">What the grid is doing.</param>
    object GetEditingCellFormattedValue(DataGridViewDataErrorContexts context);

    /// <summary>Prepares the cell for editing.</summary>
    /// <param name="selectAll">Whether to select all of it.</param>
    void PrepareEditingCellForEdit(bool selectAll);
}
