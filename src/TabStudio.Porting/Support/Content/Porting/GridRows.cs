using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace $rootnamespace$.Porting;

/// <summary>
/// The rows of a Windows Forms <c>DataGridView</c> filled in code, ported. Avalonia's <c>DataGrid</c> shows them
/// through its <c>ItemsSource</c>, and each column shows one cell (<c>{Binding Cells[0].Value}</c>).
/// </summary>
public sealed class GridRows : ObservableCollection<GridRow>
{
    private readonly string[] _columns;

    /// <summary>Creates the rows of a grid with these columns.</summary>
    /// <param name="columns">The names of the columns, in order.</param>
    public GridRows(params string[] columns) => _columns = columns;

    /// <summary>The number of columns.</summary>
    public int ColumnCount => _columns.Length;

    /// <summary>Adds a row with a value for each column, as <c>Rows.Add</c> did.</summary>
    /// <param name="values">The values, in column order.</param>
    /// <returns>The index of the new row.</returns>
    public int Add(params object?[] values)
    {
        Add(new GridRow(this, values));
        return Count - 1;
    }

    /// <summary>The position of a column by its name, or -1.</summary>
    /// <param name="name">The column's name.</param>
    public int ColumnIndex(string name) => Array.IndexOf(_columns, name);
}

/// <summary>A row of a ported <c>DataGridView</c>.</summary>
public sealed class GridRow
{
    private readonly GridRows _rows;

    internal GridRow(GridRows rows, object?[] values)
    {
        _rows = rows;
        Cells = new GridCells(rows, values);
    }

    /// <summary>The row's cells, by column position or name.</summary>
    public GridCells Cells { get; }

    /// <summary>The row's position in the grid.</summary>
    public int Index => _rows.IndexOf(this);

    /// <summary>Any data the app keeps with the row.</summary>
    public object? Tag { get; set; }
}

/// <summary>The cells of a row, by column position; <see cref="Named"/> finds one by the column's name.</summary>
public sealed class GridCells : IReadOnlyList<GridCell>
{
    private readonly GridRows _rows;
    private readonly List<GridCell> _cells;

    internal GridCells(GridRows rows, object?[] values)
    {
        _rows = rows;
        _cells = [.. Enumerable.Range(0, Math.Max(rows.ColumnCount, values.Length)).Select(i => new GridCell(i < values.Length ? values[i] : null))];
    }

    /// <inheritdoc />
    public int Count => _cells.Count;

    /// <summary>The cell of a column, by position.</summary>
    /// <param name="index">The column's position.</param>
    public GridCell this[int index] => _cells[index];

    /// <summary>The cell of a column, by the column's name, as <c>Cells["name"]</c> did.</summary>
    /// <param name="column">The column's name.</param>
    /// <remarks>Not an indexer: the grid's bindings need a single indexer, by position.</remarks>
    public GridCell Named(string column) =>
        _cells[_rows.ColumnIndex(column) is var index and >= 0 ? index : throw new ArgumentException($"There is no column named {column}.", nameof(column))];

    /// <inheritdoc />
    public IEnumerator<GridCell> GetEnumerator() => _cells.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>A cell of a row: its value, which the grid shows as it changes.</summary>
/// <param name="value">The first value.</param>
public sealed class GridCell(object? value) : INotifyPropertyChanged
{
    private object? _value = value;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The cell's value.</summary>
    public object? Value
    {
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    /// <inheritdoc />
    public override string ToString() => _value?.ToString() ?? string.Empty;
}
