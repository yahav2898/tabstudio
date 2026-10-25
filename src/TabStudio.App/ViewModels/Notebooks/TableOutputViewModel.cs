using System.Globalization;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>A collection or object shown as a grid, with a note saying how many rows there are.</summary>
public sealed class TableOutputViewModel : NotebookOutputViewModel
{
    /// <summary>Creates the output.</summary>
    /// <param name="table">The table.</param>
    public TableOutputViewModel(TableOutput table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Table = table;
    }

    /// <summary>The table.</summary>
    public TableOutput Table { get; }

    /// <summary>"3 rows", or "Showing the first 100 of 2,500 rows" when the value had more.</summary>
    public string RowCountText
    {
        get
        {
            var shown = Table.Rows.Count;
            if (!Table.IsTruncated)
            {
                return shown == 1 ? "1 row" : string.Create(CultureInfo.CurrentCulture, $"{shown:N0} rows");
            }

            return Table.TotalRowCount is { } total
                ? string.Create(CultureInfo.CurrentCulture, $"Showing the first {shown:N0} of {total:N0} rows")
                : string.Create(CultureInfo.CurrentCulture, $"Showing the first {shown:N0} rows");
        }
    }
}
