using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TabStudio.App.Views.Notebooks;

/// <summary>Lays out table cells as a grid with a header row, column lines and striped rows.</summary>
internal static class TableLayout
{
    /// <summary>Creates the grid.</summary>
    /// <param name="header">The header cells; their number sets the number of columns.</param>
    /// <param name="rows">The body rows. Missing cells are left empty, extra cells are ignored.</param>
    public static Grid Create(IReadOnlyList<Control> header, IEnumerable<IReadOnlyList<Control>> rows)
    {
        var grid = new Grid();
        for (var column = 0; column < header.Count; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        AddRow(grid, header, isHeader: true, isStriped: false);
        var index = 0;
        foreach (var row in rows)
        {
            AddRow(grid, row, isHeader: false, isStriped: index % 2 == 1);
            index++;
        }

        return grid;
    }

    /// <summary>Whether a cell's text is a number, which is aligned to the right.</summary>
    /// <param name="text">The cell's text.</param>
    public static bool IsNumber(string text) =>
        text.Length > 0 && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);

    private static void AddRow(Grid grid, IReadOnlyList<Control> cells, bool isHeader, bool isStriped)
    {
        var row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var columns = grid.ColumnDefinitions.Count;
        var background = isHeader
            ? ThemeResources.Brush("Brush.Notebook.TableHeader", "#262A33")
            : isStriped ? ThemeResources.Brush("Brush.Notebook.TableStripe", "#1F2128") : null;
        var line = ThemeResources.Brush("Brush.Border", "#2A2E37");

        for (var column = 0; column < columns; column++)
        {
            var cell = new Border
            {
                Background = background,
                BorderBrush = line,
                BorderThickness = new Thickness(0, 0, column < columns - 1 ? 1 : 0, 1),
                Padding = new Thickness(10, 5),
                Child = column < cells.Count ? cells[column] : null,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }
    }
}
