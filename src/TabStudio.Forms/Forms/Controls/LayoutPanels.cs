using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms.Layout;

namespace System.Windows.Forms;

/// <summary>A panel that lays its controls out one after another, wrapping to a new row or column.</summary>
[DefaultProperty(nameof(FlowDirection))]
public class FlowLayoutPanel : Panel, IExtenderProvider
{
    private readonly HashSet<Control> _flowBreaks = [];
    private FlowDirection _flowDirection = FlowDirection.LeftToRight;
    private bool _wrapContents = true;

    /// <summary>Creates a flow layout panel.</summary>
    public FlowLayoutPanel()
    {
    }

    /// <summary>The direction the controls flow in.</summary>
    public FlowDirection FlowDirection
    {
        get => _flowDirection;
        set
        {
            _flowDirection = value;
            PerformLayout(this, nameof(FlowDirection));
        }
    }

    /// <summary>Whether the controls wrap to a new row or column when they do not fit.</summary>
    public bool WrapContents
    {
        get => _wrapContents;
        set
        {
            _wrapContents = value;
            PerformLayout(this, nameof(WrapContents));
        }
    }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => FlowLayout.Instance;

    /// <summary>Whether the flow breaks after a control.</summary>
    /// <param name="control">The control.</param>
    public bool GetFlowBreak(Control control) => _flowBreaks.Contains(control);

    /// <summary>Breaks the flow after a control, or not.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">Whether the flow breaks after it.</param>
    public void SetFlowBreak(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (value ? _flowBreaks.Add(control) : _flowBreaks.Remove(control))
        {
            PerformLayout(control, "FlowBreak");
        }
    }

    /// <inheritdoc />
    bool IExtenderProvider.CanExtend(object extendee) => extendee is Control control && control.Parent == this;

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        if (e.Control is { } control)
        {
            _flowBreaks.Remove(control);
        }

        base.OnControlRemoved(e);
    }
}

/// <summary>A panel that lays its controls out in a grid of rows and columns of fixed, automatic or percentage sizes.</summary>
[DefaultProperty(nameof(ColumnCount))]
public class TableLayoutPanel : Panel, IExtenderProvider
{
    private static readonly object EventCellPaint = new();

    private readonly Dictionary<Control, CellSettings> _cells = [];
    private readonly TableLayoutColumnStyleCollection _columnStyles;
    private readonly TableLayoutRowStyleCollection _rowStyles;
    private int _columnCount;
    private int _rowCount;
    private TableLayoutPanelCellBorderStyle _cellBorderStyle;

    /// <summary>Creates a table layout panel.</summary>
    public TableLayoutPanel()
    {
        _columnStyles = new TableLayoutColumnStyleCollection(this);
        _rowStyles = new TableLayoutRowStyleCollection(this);
    }

    /// <summary>Occurs when a cell is drawn, to draw on it.</summary>
    public event TableLayoutCellPaintEventHandler? CellPaint
    {
        add => Events.AddHandler(EventCellPaint, value);
        remove => Events.RemoveHandler(EventCellPaint, value);
    }

    /// <summary>The controls, which can be added to a cell.</summary>
    public new TableLayoutControlCollection Controls => (TableLayoutControlCollection)base.Controls;

    /// <summary>The number of columns.</summary>
    public int ColumnCount
    {
        get => _columnCount;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _columnCount = value;
            PerformLayout(this, nameof(ColumnCount));
        }
    }

    /// <summary>The number of rows.</summary>
    public int RowCount
    {
        get => _rowCount;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _rowCount = value;
            PerformLayout(this, nameof(RowCount));
        }
    }

    /// <summary>The sizes of the columns.</summary>
    public TableLayoutColumnStyleCollection ColumnStyles => _columnStyles;

    /// <summary>The sizes of the rows.</summary>
    public TableLayoutRowStyleCollection RowStyles => _rowStyles;

    /// <summary>How the table grows when a control is added to a full table.</summary>
    public TableLayoutPanelGrowStyle GrowStyle { get; set; } = TableLayoutPanelGrowStyle.AddRows;

    /// <summary>The borders between the cells.</summary>
    public TableLayoutPanelCellBorderStyle CellBorderStyle
    {
        get => _cellBorderStyle;
        set
        {
            _cellBorderStyle = value;
            PerformLayout(this, nameof(CellBorderStyle));
            Invalidate();
        }
    }

    /// <summary>The settings of the table's layout, as one object. Kept for compatibility.</summary>
    public TableLayoutSettings? LayoutSettings { get; set; }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => TableLayout.Instance;

    /// <summary>The width of the cell borders.</summary>
    internal int CellBorderWidth => _cellBorderStyle switch
    {
        TableLayoutPanelCellBorderStyle.None => 0,
        TableLayoutPanelCellBorderStyle.Single => 1,
        TableLayoutPanelCellBorderStyle.InsetDouble or TableLayoutPanelCellBorderStyle.OutsetDouble => 3,
        _ => 2,
    };

    /// <summary>The widths of the columns, as last laid out.</summary>
    internal int[] ColumnWidths { get; set; } = [];

    /// <summary>The heights of the rows, as last laid out.</summary>
    internal int[] RowHeights { get; set; } = [];

    /// <summary>The column a control is in, or -1 to place it in the next free cell.</summary>
    /// <param name="control">The control.</param>
    public int GetColumn(Control control) => Settings(control).Column;

    /// <summary>Puts a control in a column.</summary>
    /// <param name="control">The control.</param>
    /// <param name="column">The column, or -1.</param>
    public void SetColumn(Control control, int column)
    {
        Settings(control).Column = column;
        PerformLayout(control, "Column");
    }

    /// <summary>The row a control is in, or -1 to place it in the next free cell.</summary>
    /// <param name="control">The control.</param>
    public int GetRow(Control control) => Settings(control).Row;

    /// <summary>Puts a control in a row.</summary>
    /// <param name="control">The control.</param>
    /// <param name="row">The row, or -1.</param>
    public void SetRow(Control control, int row)
    {
        Settings(control).Row = row;
        PerformLayout(control, "Row");
    }

    /// <summary>The cell a control is in.</summary>
    /// <param name="control">The control.</param>
    public TableLayoutPanelCellPosition GetCellPosition(Control control) => new(GetColumn(control), GetRow(control));

    /// <summary>Puts a control in a cell.</summary>
    /// <param name="control">The control.</param>
    /// <param name="position">The cell.</param>
    public void SetCellPosition(Control control, TableLayoutPanelCellPosition position)
    {
        var settings = Settings(control);
        settings.Column = position.Column;
        settings.Row = position.Row;
        PerformLayout(control, "CellPosition");
    }

    /// <summary>How many columns a control spans.</summary>
    /// <param name="control">The control.</param>
    public int GetColumnSpan(Control control) => Settings(control).ColumnSpan;

    /// <summary>Makes a control span columns.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">The number of columns.</param>
    public void SetColumnSpan(Control control, int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        Settings(control).ColumnSpan = value;
        PerformLayout(control, "ColumnSpan");
    }

    /// <summary>How many rows a control spans.</summary>
    /// <param name="control">The control.</param>
    public int GetRowSpan(Control control) => Settings(control).RowSpan;

    /// <summary>Makes a control span rows.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">The number of rows.</param>
    public void SetRowSpan(Control control, int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        Settings(control).RowSpan = value;
        PerformLayout(control, "RowSpan");
    }

    /// <summary>The cell a control was laid out in.</summary>
    /// <param name="control">The control.</param>
    public TableLayoutPanelCellPosition GetPositionFromControl(Control? control) =>
        control is not null && _cells.TryGetValue(control, out var settings) ? settings.Placed : new TableLayoutPanelCellPosition(-1, -1);

    /// <summary>The control laid out in a cell, if any.</summary>
    /// <param name="column">The column.</param>
    /// <param name="row">The row.</param>
    public Control? GetControlFromPosition(int column, int row)
    {
        foreach (var (control, settings) in _cells)
        {
            if (control.Parent == this && settings.Placed.Column == column && settings.Placed.Row == row)
            {
                return control;
            }
        }

        return null;
    }

    /// <summary>The widths of the columns, as laid out.</summary>
    public int[] GetColumnWidths() => [.. ColumnWidths];

    /// <summary>The heights of the rows, as laid out.</summary>
    public int[] GetRowHeights() => [.. RowHeights];

    /// <inheritdoc />
    bool IExtenderProvider.CanExtend(object extendee) => extendee is Control control && control.Parent == this;

    /// <summary>Raises CellPaint.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCellPaint(TableLayoutCellPaintEventArgs e) => ((TableLayoutCellPaintEventHandler?)Events[EventCellPaint])?.Invoke(this, e);

    /// <inheritdoc />
    protected override ControlCollection CreateControlsInstance() => new TableLayoutControlCollection(this);

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        if (e.Control is { } control)
        {
            _cells.Remove(control);
        }

        base.OnControlRemoved(e);
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        base.PaintContent(g);
        var border = CellBorderWidth;
        var display = DisplayRectangle;
        if (border == 0 && Events[EventCellPaint] is null)
        {
            return;
        }

        using var pen = new Pen(_cellBorderStyle == TableLayoutPanelCellBorderStyle.Single ? Color.FromArgb(160, 160, 160) : SystemColors.ControlDark, border);
        var y = display.Top;
        for (var row = 0; row < RowHeights.Length; row++)
        {
            var x = display.Left;
            for (var column = 0; column < ColumnWidths.Length; column++)
            {
                var cell = new Rectangle(x + border, y + border, ColumnWidths[column], RowHeights[row]);
                OnCellPaint(new TableLayoutCellPaintEventArgs(g, display, cell, column, row));
                x += ColumnWidths[column] + border;
            }

            y += RowHeights[row] + border;
        }

        if (border == 0)
        {
            return;
        }

        var offset = border / 2f;
        var right = display.Left + ColumnWidths.Sum() + (border * (ColumnWidths.Length + 1));
        var bottom = display.Top + RowHeights.Sum() + (border * (RowHeights.Length + 1));
        var lineX = (float)display.Left;
        for (var column = 0; column <= ColumnWidths.Length; column++)
        {
            g.DrawLine(pen, lineX + offset, display.Top, lineX + offset, bottom);
            lineX += border + (column < ColumnWidths.Length ? ColumnWidths[column] : 0);
        }

        var lineY = (float)display.Top;
        for (var row = 0; row <= RowHeights.Length; row++)
        {
            g.DrawLine(pen, display.Left, lineY + offset, right, lineY + offset);
            lineY += border + (row < RowHeights.Length ? RowHeights[row] : 0);
        }
    }

    /// <summary>Records the cell a control was laid out in.</summary>
    internal void SetPlaced(Control control, TableLayoutPanelCellPosition position) => Settings(control).Placed = position;

    private CellSettings Settings(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!_cells.TryGetValue(control, out var settings))
        {
            settings = new CellSettings();
            _cells[control] = settings;
        }

        return settings;
    }

    /// <summary>Where a control goes in the table.</summary>
    private sealed class CellSettings
    {
        public int Column { get; set; } = -1;

        public int Row { get; set; } = -1;

        public int ColumnSpan { get; set; } = 1;

        public int RowSpan { get; set; } = 1;

        public TableLayoutPanelCellPosition Placed { get; set; } = new(-1, -1);
    }
}

/// <summary>The controls of a table layout panel, which can be added to a cell.</summary>
public class TableLayoutControlCollection : Control.ControlCollection
{
    /// <summary>Creates the collection.</summary>
    /// <param name="container">The panel.</param>
    public TableLayoutControlCollection(TableLayoutPanel container)
        : base(container)
    {
        Container = container;
    }

    /// <summary>The panel.</summary>
    public TableLayoutPanel Container { get; }

    /// <summary>Adds a control to a cell.</summary>
    /// <param name="control">The control.</param>
    /// <param name="column">The column, or -1 for the next free one.</param>
    /// <param name="row">The row, or -1 for the next free one.</param>
    public virtual void Add(Control control, int column, int row)
    {
        ArgumentNullException.ThrowIfNull(control);
        Container.SuspendLayout();
        Container.SetColumn(control, column);
        Container.SetRow(control, row);
        Add(control);
        Container.ResumeLayout();
    }
}

/// <summary>A cell of a table layout panel.</summary>
/// <param name="column">The column.</param>
/// <param name="row">The row.</param>
[TypeConverter(typeof(TypeConverter))]
public struct TableLayoutPanelCellPosition(int column, int row) : IEquatable<TableLayoutPanelCellPosition>
{
    /// <summary>The column.</summary>
    public int Column { get; set; } = column;

    /// <summary>The row.</summary>
    public int Row { get; set; } = row;

    /// <summary>Whether two cells are the same.</summary>
    public static bool operator ==(TableLayoutPanelCellPosition p1, TableLayoutPanelCellPosition p2) => p1.Equals(p2);

    /// <summary>Whether two cells differ.</summary>
    public static bool operator !=(TableLayoutPanelCellPosition p1, TableLayoutPanelCellPosition p2) => !p1.Equals(p2);

    /// <inheritdoc />
    public readonly bool Equals(TableLayoutPanelCellPosition other) => Column == other.Column && Row == other.Row;

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is TableLayoutPanelCellPosition other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(Column, Row);

    /// <inheritdoc />
    public override readonly string ToString() => $"{Column},{Row}";
}

/// <summary>The size of a row or a column of a table layout panel.</summary>
public abstract class TableLayoutStyle
{
    private SizeType _sizeType = SizeType.AutoSize;

    /// <summary>How the row or column is sized.</summary>
    public SizeType SizeType
    {
        get => _sizeType;
        set
        {
            _sizeType = value;
            Owner?.PerformLayout(Owner, nameof(SizeType));
        }
    }

    /// <summary>The panel the style belongs to.</summary>
    internal TableLayoutPanel? Owner { get; set; }

    /// <summary>The size: pixels or a percentage.</summary>
    internal float SizeValue { get; set; }
}

/// <summary>The width of a column of a table layout panel.</summary>
public class ColumnStyle : TableLayoutStyle
{
    /// <summary>Creates an automatically sized column.</summary>
    public ColumnStyle()
    {
    }

    /// <summary>Creates a column sized by a type.</summary>
    /// <param name="sizeType">The size type.</param>
    public ColumnStyle(SizeType sizeType)
    {
        SizeType = sizeType;
    }

    /// <summary>Creates a column with a size.</summary>
    /// <param name="sizeType">The size type.</param>
    /// <param name="width">The width: pixels or a percentage.</param>
    public ColumnStyle(SizeType sizeType, float width)
    {
        SizeType = sizeType;
        Width = width;
    }

    /// <summary>The width: pixels or a percentage.</summary>
    public float Width
    {
        get => SizeValue;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            SizeValue = value;
            Owner?.PerformLayout(Owner, nameof(Width));
        }
    }
}

/// <summary>The height of a row of a table layout panel.</summary>
public class RowStyle : TableLayoutStyle
{
    /// <summary>Creates an automatically sized row.</summary>
    public RowStyle()
    {
    }

    /// <summary>Creates a row sized by a type.</summary>
    /// <param name="sizeType">The size type.</param>
    public RowStyle(SizeType sizeType)
    {
        SizeType = sizeType;
    }

    /// <summary>Creates a row with a size.</summary>
    /// <param name="sizeType">The size type.</param>
    /// <param name="height">The height: pixels or a percentage.</param>
    public RowStyle(SizeType sizeType, float height)
    {
        SizeType = sizeType;
        Height = height;
    }

    /// <summary>The height: pixels or a percentage.</summary>
    public float Height
    {
        get => SizeValue;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            SizeValue = value;
            Owner?.PerformLayout(Owner, nameof(Height));
        }
    }
}

/// <summary>The styles of a table layout panel's rows or columns.</summary>
public abstract class TableLayoutStyleCollection : System.Collections.IList
{
    private readonly List<TableLayoutStyle> _styles = [];

    private protected TableLayoutStyleCollection(TableLayoutPanel owner) => Owner = owner;

    /// <summary>The number of styles.</summary>
    public int Count => _styles.Count;

    /// <inheritdoc />
    bool System.Collections.IList.IsReadOnly => false;

    /// <inheritdoc />
    bool System.Collections.IList.IsFixedSize => false;

    /// <inheritdoc />
    bool System.Collections.ICollection.IsSynchronized => false;

    /// <inheritdoc />
    object System.Collections.ICollection.SyncRoot => this;

    /// <summary>The panel.</summary>
    internal TableLayoutPanel Owner { get; }

    /// <summary>The styles.</summary>
    internal IReadOnlyList<TableLayoutStyle> Styles => _styles;

    /// <summary>The style at an index.</summary>
    /// <param name="index">The index.</param>
    public TableLayoutStyle this[int index]
    {
        get => _styles[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _styles[index].Owner = null;
            value.Owner = Owner;
            _styles[index] = value;
            Owner.PerformLayout(Owner, "Style");
        }
    }

    /// <inheritdoc />
    object? System.Collections.IList.this[int index]
    {
        get => this[index];
        set => this[index] = (TableLayoutStyle)value!;
    }

    /// <summary>Adds a style.</summary>
    /// <param name="style">The style.</param>
    /// <returns>Its index.</returns>
    public int Add(TableLayoutStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        style.Owner = Owner;
        _styles.Add(style);
        Owner.PerformLayout(Owner, "Style");
        return _styles.Count - 1;
    }

    /// <summary>Removes every style.</summary>
    public void Clear()
    {
        foreach (var style in _styles)
        {
            style.Owner = null;
        }

        _styles.Clear();
        Owner.PerformLayout(Owner, "Style");
    }

    /// <summary>Removes the style at an index.</summary>
    /// <param name="index">The index.</param>
    public void RemoveAt(int index)
    {
        _styles[index].Owner = null;
        _styles.RemoveAt(index);
        Owner.PerformLayout(Owner, "Style");
    }

    /// <inheritdoc />
    public System.Collections.IEnumerator GetEnumerator() => _styles.GetEnumerator();

    /// <inheritdoc />
    void System.Collections.ICollection.CopyTo(Array array, int index) => ((System.Collections.ICollection)_styles).CopyTo(array, index);

    /// <inheritdoc />
    int System.Collections.IList.Add(object? value) => Add((TableLayoutStyle)value!);

    /// <inheritdoc />
    bool System.Collections.IList.Contains(object? value) => value is TableLayoutStyle style && _styles.Contains(style);

    /// <inheritdoc />
    int System.Collections.IList.IndexOf(object? value) => value is TableLayoutStyle style ? _styles.IndexOf(style) : -1;

    /// <inheritdoc />
    void System.Collections.IList.Insert(int index, object? value)
    {
        var style = (TableLayoutStyle)value!;
        style.Owner = Owner;
        _styles.Insert(index, style);
        Owner.PerformLayout(Owner, "Style");
    }

    /// <inheritdoc />
    void System.Collections.IList.Remove(object? value)
    {
        if (value is TableLayoutStyle style && _styles.Remove(style))
        {
            style.Owner = null;
            Owner.PerformLayout(Owner, "Style");
        }
    }
}

/// <summary>The column styles of a table layout panel.</summary>
public class TableLayoutColumnStyleCollection : TableLayoutStyleCollection
{
    internal TableLayoutColumnStyleCollection(TableLayoutPanel owner)
        : base(owner)
    {
    }

    /// <summary>The style at an index.</summary>
    /// <param name="index">The index.</param>
    public new ColumnStyle this[int index]
    {
        get => (ColumnStyle)base[index];
        set => base[index] = value;
    }

    /// <summary>Adds a column style.</summary>
    /// <param name="columnStyle">The style.</param>
    /// <returns>Its index.</returns>
    public int Add(ColumnStyle columnStyle) => base.Add(columnStyle);
}

/// <summary>The row styles of a table layout panel.</summary>
public class TableLayoutRowStyleCollection : TableLayoutStyleCollection
{
    internal TableLayoutRowStyleCollection(TableLayoutPanel owner)
        : base(owner)
    {
    }

    /// <summary>The style at an index.</summary>
    /// <param name="index">The index.</param>
    public new RowStyle this[int index]
    {
        get => (RowStyle)base[index];
        set => base[index] = value;
    }

    /// <summary>Adds a row style.</summary>
    /// <param name="rowStyle">The style.</param>
    /// <returns>Its index.</returns>
    public int Add(RowStyle rowStyle) => base.Add(rowStyle);
}

/// <summary>The settings of a table layout. Kept for compatibility.</summary>
public sealed class TableLayoutSettings
{
    internal TableLayoutSettings()
    {
    }
}

/// <summary>Data for drawing on a cell of a table layout panel.</summary>
public class TableLayoutCellPaintEventArgs : PaintEventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="clipRectangle">The area drawn.</param>
    /// <param name="cellBounds">The cell's bounds.</param>
    /// <param name="column">The column.</param>
    /// <param name="row">The row.</param>
    public TableLayoutCellPaintEventArgs(Graphics g, Rectangle clipRectangle, Rectangle cellBounds, int column, int row)
        : base(g, clipRectangle)
    {
        CellBounds = cellBounds;
        Column = column;
        Row = row;
    }

    /// <summary>The cell's bounds.</summary>
    public Rectangle CellBounds { get; }

    /// <summary>The column.</summary>
    public int Column { get; }

    /// <summary>The row.</summary>
    public int Row { get; }
}

/// <summary>Handles drawing on a cell.</summary>
/// <param name="sender">The panel.</param>
/// <param name="e">The event data.</param>
public delegate void TableLayoutCellPaintEventHandler(object? sender, TableLayoutCellPaintEventArgs e);
