using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>A column of a grid: its header, width, sort mode and the template of its cells.</summary>
[DesignTimeVisible(false)]
[ToolboxItem(false)]
public class DataGridViewColumn : DataGridViewBand, IComponent
{
    private DataGridViewColumnHeaderCell _headerCell;
    private DataGridViewCell? _cellTemplate;
    private string _name = string.Empty;
    private string _dataPropertyName = string.Empty;
    private int _width = 100;
    private int _minimumWidth = 5;
    private float _fillWeight = 100f;
    private DataGridViewAutoSizeColumnMode _autoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;
    private int _displayIndex = -1;

    /// <summary>Creates a column with no cell template.</summary>
    public DataGridViewColumn()
        : this(null)
    {
    }

    /// <summary>Creates a column whose cells copy a template.</summary>
    /// <param name="cellTemplate">The template.</param>
    public DataGridViewColumn(DataGridViewCell? cellTemplate)
    {
        _cellTemplate = cellTemplate;
        _headerCell = new DataGridViewColumnHeaderCell { OwningColumnInternal = this };
    }

    /// <inheritdoc />
    public event EventHandler? Disposed;

    /// <summary>The template the column's new cells copy.</summary>
    public virtual DataGridViewCell? CellTemplate
    {
        get => _cellTemplate;
        set => _cellTemplate = value;
    }

    /// <summary>The type of the column's cells.</summary>
    public Type? CellType => _cellTemplate?.GetType();

    /// <summary>The header cell.</summary>
    [AllowNull]
    public DataGridViewColumnHeaderCell HeaderCell
    {
        get => _headerCell;
        set
        {
            _headerCell = value ?? new DataGridViewColumnHeaderCell();
            _headerCell.OwningColumnInternal = this;

            // The header takes its style from the grid it is in, like the column's cells.
            _headerCell.DataGridView = DataGridView;
            DataGridView?.Invalidate();
        }
    }

    /// <summary>The header's text.</summary>
    [AllowNull]
    public string HeaderText
    {
        get => _headerCell.Value?.ToString() ?? string.Empty;
        set
        {
            _headerCell.Value = value;
            DataGridView?.OnColumnLayoutChanged();
        }
    }

    /// <summary>The column's name, by which the grid's Columns collection and a row's Cells find it.</summary>
    [AllowNull]
    public string Name
    {
        get => _name;
        set => _name = value ?? string.Empty;
    }

    /// <summary>The property of the data source's items the column shows.</summary>
    [AllowNull]
    public string DataPropertyName
    {
        get => _dataPropertyName;
        set
        {
            _dataPropertyName = value ?? string.Empty;
            DataGridView?.OnColumnDataPropertyNameChanged(this);
        }
    }

    /// <summary>Whether the column shows a property of the data source.</summary>
    public bool IsDataBound => DataGridView?.IsColumnBound(this) == true;

    /// <summary>The width.</summary>
    public int Width
    {
        get => _width;
        set
        {
            var width = Math.Max(_minimumWidth, value);
            if (width == _width)
            {
                return;
            }

            _width = width;
            DataGridView?.OnColumnWidthChanged(this);
        }
    }

    /// <summary>The least width.</summary>
    public int MinimumWidth
    {
        get => _minimumWidth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 2);
            _minimumWidth = value;
            if (_width < value)
            {
                Width = value;
            }
        }
    }

    /// <summary>The share of the space the column takes when columns fill the grid.</summary>
    public float FillWeight
    {
        get => _fillWeight;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _fillWeight = value;
            DataGridView?.OnColumnLayoutChanged();
        }
    }

    /// <summary>How the column sizes itself.</summary>
    public DataGridViewAutoSizeColumnMode AutoSizeMode
    {
        get => _autoSizeMode;
        set
        {
            _autoSizeMode = value;
            DataGridView?.OnColumnLayoutChanged();
        }
    }

    /// <summary>How the column sizes itself, with the grid's mode when it has none.</summary>
    public DataGridViewAutoSizeColumnMode InheritedAutoSizeMode =>
        _autoSizeMode != DataGridViewAutoSizeColumnMode.NotSet || DataGridView is null
            ? _autoSizeMode
            : (DataGridViewAutoSizeColumnMode)DataGridView.AutoSizeColumnsMode;

    /// <summary>How the column sorts.</summary>
    public DataGridViewColumnSortMode SortMode { get; set; } = DataGridViewColumnSortMode.NotSortable;

    /// <summary>The position the column is shown at.</summary>
    public int DisplayIndex
    {
        get => _displayIndex < 0 ? Index : _displayIndex;
        set
        {
            _displayIndex = value;
            DataGridView?.OnColumnLayoutChanged();
        }
    }

    /// <summary>The type of the column's values.</summary>
    public Type? ValueType { get; set; }

    /// <summary>The header's tooltip.</summary>
    [AllowNull]
    public string ToolTipText
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>The width of the line after the column.</summary>
    public int DividerWidth { get; set; }

    /// <inheritdoc />
    public ISite? Site { get; set; }

    /// <summary>Whether the grid generated the column from its data source, so it removes it when the source changes.</summary>
    internal bool IsGenerated { get; set; }

    /// <summary>The column's own display index setting.</summary>
    internal int OwnDisplayIndex => _displayIndex;

    /// <summary>Sets the width from the grid's layout, without telling the grid.</summary>
    internal void SetWidthFromLayout(int width) => _width = Math.Max(_minimumWidth, width);

    /// <inheritdoc />
    public override object Clone()
    {
        var column = (DataGridViewColumn)base.Clone();
        column._cellTemplate = (DataGridViewCell?)_cellTemplate?.Clone();
        column._headerCell = (DataGridViewColumnHeaderCell)_headerCell.Clone();
        column._headerCell.OwningColumnInternal = column;
        column.Site = null;
        return column;
    }

    /// <inheritdoc />
    public override string ToString() => $"DataGridViewColumn {{ Name={_name}, Index={Index} }}";

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        base.Dispose(disposing);
    }
}

/// <summary>A column of text cells.</summary>
public class DataGridViewTextBoxColumn : DataGridViewColumn
{
    /// <summary>Creates a column of text cells.</summary>
    public DataGridViewTextBoxColumn()
        : base(new DataGridViewTextBoxCell())
    {
        SortMode = DataGridViewColumnSortMode.Automatic;
    }

    /// <summary>The most characters a cell takes.</summary>
    public int MaxInputLength
    {
        get => (CellTemplate as DataGridViewTextBoxCell)?.MaxInputLength ?? 32767;
        set
        {
            if (CellTemplate is DataGridViewTextBoxCell cell)
            {
                cell.MaxInputLength = value;
            }
        }
    }
}

/// <summary>A column of check box cells.</summary>
public class DataGridViewCheckBoxColumn : DataGridViewColumn
{
    /// <summary>Creates a column of check box cells.</summary>
    public DataGridViewCheckBoxColumn()
        : this(false)
    {
    }

    /// <summary>Creates a column of check box cells, with two or three states.</summary>
    /// <param name="threeState">Whether cells have a third, indeterminate state.</param>
    public DataGridViewCheckBoxColumn(bool threeState)
        : base(new DataGridViewCheckBoxCell(threeState))
    {
        DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        ValueType = threeState ? typeof(CheckState) : typeof(bool);
    }

    /// <summary>Whether cells have a third, indeterminate state.</summary>
    public bool ThreeState
    {
        get => Template.ThreeState;
        set => Template.ThreeState = value;
    }

    /// <summary>The value of a checked cell.</summary>
    public object? TrueValue
    {
        get => Template.TrueValue;
        set => Template.TrueValue = value;
    }

    /// <summary>The value of an unchecked cell.</summary>
    public object? FalseValue
    {
        get => Template.FalseValue;
        set => Template.FalseValue = value;
    }

    /// <summary>The value of an indeterminate cell.</summary>
    public object? IndeterminateValue
    {
        get => Template.IndeterminateValue;
        set => Template.IndeterminateValue = value;
    }

    /// <summary>The look of the check boxes. Every style draws the Windows 10 look.</summary>
    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

    private DataGridViewCheckBoxCell Template => (DataGridViewCheckBoxCell)(CellTemplate ?? throw new InvalidOperationException("The column has no cell template."));
}

/// <summary>A column of combo box cells, to pick a value from a list.</summary>
public class DataGridViewComboBoxColumn : DataGridViewColumn
{
    /// <summary>Creates a column of combo box cells.</summary>
    public DataGridViewComboBoxColumn()
        : base(new DataGridViewComboBoxCell())
    {
    }

    /// <summary>The items of the list.</summary>
    public DataGridViewComboBoxCell.ObjectCollection Items => Template.Items;

    /// <summary>The data source of the list.</summary>
    public object? DataSource
    {
        get => Template.DataSource;
        set => Template.DataSource = value;
    }

    /// <summary>The property of the list's items shown.</summary>
    [AllowNull]
    public string DisplayMember
    {
        get => Template.DisplayMember;
        set => Template.DisplayMember = value;
    }

    /// <summary>The property of the list's items stored in the cells.</summary>
    [AllowNull]
    public string ValueMember
    {
        get => Template.ValueMember;
        set => Template.ValueMember = value;
    }

    /// <summary>How the cells show their drop-down button.</summary>
    public DataGridViewComboBoxDisplayStyle DisplayStyle
    {
        get => Template.DisplayStyle;
        set => Template.DisplayStyle = value;
    }

    /// <summary>Whether only the current cell shows the button.</summary>
    public bool DisplayStyleForCurrentCellOnly { get; set; }

    /// <summary>The most items the list shows before it scrolls.</summary>
    public int MaxDropDownItems { get; set; } = 8;

    /// <summary>The width of the list.</summary>
    public int DropDownWidth { get; set; } = 1;

    /// <summary>Whether the list is sorted.</summary>
    public bool Sorted { get; set; }

    /// <summary>Whether typing selects matching items. Kept for compatibility.</summary>
    public bool AutoComplete { get; set; } = true;

    /// <summary>The look. Every style draws the Windows 10 look.</summary>
    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

    private DataGridViewComboBoxCell Template => (DataGridViewComboBoxCell)(CellTemplate ?? throw new InvalidOperationException("The column has no cell template."));
}

/// <summary>A column of buttons.</summary>
public class DataGridViewButtonColumn : DataGridViewColumn
{
    /// <summary>Creates a column of buttons.</summary>
    public DataGridViewButtonColumn()
        : base(new DataGridViewButtonCell())
    {
        DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
    }

    /// <summary>The text of every button, when <see cref="UseColumnTextForButtonValue"/> is on.</summary>
    public string? Text { get; set; }

    /// <summary>Whether every button shows <see cref="Text"/> rather than its cell's value.</summary>
    public bool UseColumnTextForButtonValue { get; set; }

    /// <summary>The look. Every style draws the Windows 10 look.</summary>
    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;
}

/// <summary>A column of links.</summary>
public class DataGridViewLinkColumn : DataGridViewColumn
{
    /// <summary>Creates a column of links.</summary>
    public DataGridViewLinkColumn()
        : base(new DataGridViewLinkCell())
    {
    }

    /// <summary>The text of every link, when <see cref="UseColumnTextForLinkValue"/> is on.</summary>
    public string? Text { get; set; }

    /// <summary>Whether every link shows <see cref="Text"/> rather than its cell's value.</summary>
    public bool UseColumnTextForLinkValue { get; set; }

    /// <summary>The color of links.</summary>
    public Color LinkColor { get; set; } = Color.FromArgb(0, 102, 204);

    /// <summary>The color of visited links.</summary>
    public Color VisitedLinkColor { get; set; } = Color.FromArgb(128, 0, 128);

    /// <summary>The color of a link being pressed.</summary>
    public Color ActiveLinkColor { get; set; } = Color.Red;

    /// <summary>When links are underlined.</summary>
    public LinkBehavior LinkBehavior { get; set; }

    /// <summary>Whether a link turns visited when tapped.</summary>
    public bool TrackVisitedState { get; set; } = true;
}

/// <summary>A column of images.</summary>
public class DataGridViewImageColumn : DataGridViewColumn
{
    /// <summary>Creates a column of images.</summary>
    public DataGridViewImageColumn()
        : this(false)
    {
    }

    /// <summary>Creates a column of images or icons.</summary>
    /// <param name="valuesAreIcons">Whether the values are icons.</param>
    public DataGridViewImageColumn(bool valuesAreIcons)
        : base(new DataGridViewImageCell(valuesAreIcons))
    {
        DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        ValueType = valuesAreIcons ? typeof(Icon) : typeof(Image);
    }

    /// <summary>How images are fitted.</summary>
    public DataGridViewImageCellLayout ImageLayout
    {
        get => (CellTemplate as DataGridViewImageCell)?.ImageLayout ?? DataGridViewImageCellLayout.Normal;
        set
        {
            if (CellTemplate is DataGridViewImageCell cell)
            {
                cell.ImageLayout = value;
            }
        }
    }

    /// <summary>The image of a cell with no value.</summary>
    public Image? Image { get; set; }

    /// <summary>Whether the values are icons.</summary>
    public bool ValuesAreIcons { get; set; }

    /// <summary>The description of the images, for accessibility.</summary>
    public string? Description { get; set; }
}

/// <summary>The columns of a grid.</summary>
[ListBindable(false)]
public class DataGridViewColumnCollection : BaseCollection, IList
{
    private readonly List<DataGridViewColumn> _columns = [];

    /// <summary>Creates the columns of a grid.</summary>
    /// <param name="dataGridView">The grid.</param>
    public DataGridViewColumnCollection(DataGridView dataGridView) => DataGridView = dataGridView;

    /// <summary>The grid.</summary>
    protected DataGridView DataGridView { get; }

    /// <inheritdoc />
    public override int Count => _columns.Count;

    /// <inheritdoc />
    protected override ArrayList List => [.. _columns];

    /// <inheritdoc />
    bool IList.IsFixedSize => false;

    /// <inheritdoc />
    bool IList.IsReadOnly => false;

    /// <summary>The column at an index.</summary>
    /// <param name="index">The index.</param>
    public DataGridViewColumn this[int index] => _columns[index];

    /// <summary>The column with a name.</summary>
    /// <param name="columnName">The name.</param>
    public DataGridViewColumn? this[string columnName] =>
        _columns.Find(column => string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException("Columns are added with Add or Insert.");
    }

    /// <summary>The columns, in order.</summary>
    internal List<DataGridViewColumn> Items => _columns;

    /// <summary>Adds a column.</summary>
    /// <param name="dataGridViewColumn">The column.</param>
    /// <returns>Its index.</returns>
    public virtual int Add(DataGridViewColumn dataGridViewColumn)
    {
        Insert(_columns.Count, dataGridViewColumn);
        return _columns.Count - 1;
    }

    /// <summary>Adds a text column.</summary>
    /// <param name="columnName">The name.</param>
    /// <param name="headerText">The header's text.</param>
    /// <returns>Its index.</returns>
    public virtual int Add(string? columnName, string? headerText) =>
        Add(new DataGridViewTextBoxColumn { Name = columnName, HeaderText = headerText });

    /// <summary>Adds columns.</summary>
    /// <param name="dataGridViewColumns">The columns.</param>
    public virtual void AddRange(params DataGridViewColumn[] dataGridViewColumns)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewColumns);
        foreach (var column in dataGridViewColumns)
        {
            Add(column);
        }
    }

    /// <summary>Inserts a column.</summary>
    /// <param name="columnIndex">Where.</param>
    /// <param name="dataGridViewColumn">The column.</param>
    public virtual void Insert(int columnIndex, DataGridViewColumn dataGridViewColumn)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewColumn);
        if (dataGridViewColumn.DataGridView is not null)
        {
            throw new InvalidOperationException("Provided column already belongs to the DataGridView control.");
        }

        if (dataGridViewColumn.CellTemplate is null)
        {
            throw new InvalidOperationException("Column cannot be added because its CellType property is null.");
        }

        _columns.Insert(Math.Clamp(columnIndex, 0, _columns.Count), dataGridViewColumn);
        dataGridViewColumn.DataGridView = DataGridView;
        dataGridViewColumn.HeaderCell.DataGridView = DataGridView;
        Reindex();
        DataGridView.OnColumnAdded(dataGridViewColumn);
    }

    /// <summary>Removes a column.</summary>
    /// <param name="dataGridViewColumn">The column.</param>
    public virtual void Remove(DataGridViewColumn dataGridViewColumn)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewColumn);
        var index = _columns.IndexOf(dataGridViewColumn);
        if (index >= 0)
        {
            RemoveAt(index);
        }
    }

    /// <summary>Removes the column with a name.</summary>
    /// <param name="columnName">The name.</param>
    public virtual void Remove(string columnName)
    {
        if (this[columnName] is { } column)
        {
            Remove(column);
        }
    }

    /// <summary>Removes the column at an index.</summary>
    /// <param name="index">The index.</param>
    public virtual void RemoveAt(int index)
    {
        var column = _columns[index];
        _columns.RemoveAt(index);
        Reindex();
        DataGridView.OnColumnRemoved(column, index);
        column.DataGridView = null;
        column.HeaderCell.DataGridView = null;
        column.Index = -1;
    }

    /// <summary>Removes every column.</summary>
    public virtual void Clear()
    {
        for (var i = _columns.Count - 1; i >= 0; i--)
        {
            RemoveAt(i);
        }
    }

    /// <summary>Whether a column is in the grid.</summary>
    /// <param name="dataGridViewColumn">The column.</param>
    public virtual bool Contains(DataGridViewColumn dataGridViewColumn) => _columns.Contains(dataGridViewColumn);

    /// <summary>Whether a column has a name.</summary>
    /// <param name="columnName">The name.</param>
    public virtual bool Contains(string columnName) => this[columnName] is not null;

    /// <summary>The index of a column, or -1.</summary>
    /// <param name="dataGridViewColumn">The column.</param>
    public int IndexOf(DataGridViewColumn dataGridViewColumn) => _columns.IndexOf(dataGridViewColumn);

    /// <summary>The number of columns in a state.</summary>
    /// <param name="includeFilter">The state.</param>
    public int GetColumnCount(DataGridViewElementStates includeFilter) =>
        _columns.Count(column => (includeFilter & DataGridViewElementStates.Visible) == 0 || column.Visible);

    /// <summary>The first column in a state, by display order.</summary>
    /// <param name="includeFilter">The state.</param>
    public DataGridViewColumn? GetFirstColumn(DataGridViewElementStates includeFilter) =>
        DataGridView.DisplayedColumns().FirstOrDefault(column => (includeFilter & DataGridViewElementStates.Visible) == 0 || column.Visible);

    /// <summary>The last column in a state, by display order.</summary>
    /// <param name="includeFilter">The state.</param>
    /// <param name="excludeFilter">The state to leave out.</param>
    public DataGridViewColumn? GetLastColumn(DataGridViewElementStates includeFilter, DataGridViewElementStates excludeFilter) =>
        DataGridView.DisplayedColumns().LastOrDefault(column => (includeFilter & DataGridViewElementStates.Visible) == 0 || column.Visible);

    /// <summary>Copies the columns into an array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="index">Where to start.</param>
    public void CopyTo(DataGridViewColumn[] array, int index) => _columns.CopyTo(array, index);

    /// <inheritdoc />
    int IList.Add(object? value) => Add((DataGridViewColumn)value!);

    /// <inheritdoc />
    void IList.Clear() => Clear();

    /// <inheritdoc />
    bool IList.Contains(object? value) => value is DataGridViewColumn column && Contains(column);

    /// <inheritdoc />
    int IList.IndexOf(object? value) => value is DataGridViewColumn column ? IndexOf(column) : -1;

    /// <inheritdoc />
    void IList.Insert(int index, object? value) => Insert(index, (DataGridViewColumn)value!);

    /// <inheritdoc />
    void IList.Remove(object? value)
    {
        if (value is DataGridViewColumn column)
        {
            Remove(column);
        }
    }

    /// <inheritdoc />
    void IList.RemoveAt(int index) => RemoveAt(index);

    private void Reindex()
    {
        for (var i = 0; i < _columns.Count; i++)
        {
            _columns[i].Index = i;
        }
    }
}
