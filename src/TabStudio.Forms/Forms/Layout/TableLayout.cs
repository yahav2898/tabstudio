using System.Drawing;

namespace System.Windows.Forms.Layout;

/// <summary>
/// The layout of a <see cref="TableLayoutPanel"/>: each control in its cell, the cells flowing row by row for controls
/// without a position; absolute columns and rows at their size, automatic ones at their content's, and percentage
/// ones sharing the space left; each control placed in its cell by its dock and anchors.
/// </summary>
internal sealed class TableLayout : LayoutEngine
{
    /// <summary>The shared instance.</summary>
    public static TableLayout Instance { get; } = new();

    /// <inheritdoc />
    public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
    {
        var panel = (TableLayoutPanel)container;
        var display = panel.DisplayRectangle;
        var grid = Assign(panel);
        var border = panel.CellBorderWidth;
        var widths = Sizes(panel.ColumnStyles.Styles, grid.Columns, display.Width - (border * (grid.Columns + 1)), grid, horizontal: true);
        var heights = Sizes(panel.RowStyles.Styles, grid.Rows, display.Height - (border * (grid.Rows + 1)), grid, horizontal: false);
        panel.ColumnWidths = widths;
        panel.RowHeights = heights;

        foreach (var cell in grid.Cells)
        {
            var x = display.Left + border + widths.Take(cell.Column).Sum() + (border * cell.Column);
            var y = display.Top + border + heights.Take(cell.Row).Sum() + (border * cell.Row);
            var width = widths.Skip(cell.Column).Take(cell.ColumnSpan).Sum() + (border * (cell.ColumnSpan - 1));
            var height = heights.Skip(cell.Row).Take(cell.RowSpan).Sum() + (border * (cell.RowSpan - 1));
            cell.Control.SetBoundsFromLayout(PlaceInCell(cell.Control, new Rectangle(x, y, width, height)));
            panel.SetPlaced(cell.Control, new TableLayoutPanelCellPosition(cell.Column, cell.Row));
        }

        return panel.AutoSize;
    }

    /// <inheritdoc />
    public override void InitLayout(object child, BoundsSpecified specified)
    {
    }

    /// <inheritdoc />
    internal override Size GetPreferredSize(Control container, Size proposedSize)
    {
        var panel = (TableLayoutPanel)container;
        var grid = Assign(panel);
        var border = panel.CellBorderWidth;
        var widths = Sizes(panel.ColumnStyles.Styles, grid.Columns, available: -1, grid, horizontal: true);
        var heights = Sizes(panel.RowStyles.Styles, grid.Rows, available: -1, grid, horizontal: false);
        var padding = panel.Padding;
        var insets = panel.NonClientInsets;
        return new Size(
            widths.Sum() + (border * (grid.Columns + 1)) + padding.Horizontal + insets.Horizontal,
            heights.Sum() + (border * (grid.Rows + 1)) + padding.Vertical + insets.Vertical);
    }

    /// <summary>Puts every control in a cell: those with a position first, then the others in the free cells, row by row.</summary>
    private static Grid Assign(TableLayoutPanel panel)
    {
        var children = Participants(panel);
        var growColumns = panel.GrowStyle == TableLayoutPanelGrowStyle.AddColumns;
        var columns = Math.Max(1, panel.ColumnCount);
        if (growColumns && panel.ColumnCount == 0)
        {
            columns = 1;
        }

        var occupied = new HashSet<(int Column, int Row)>();
        var cells = new List<Cell>();
        var rows = Math.Max(0, panel.RowCount);

        void Occupy(Cell cell)
        {
            for (var c = cell.Column; c < cell.Column + cell.ColumnSpan; c++)
            {
                for (var r = cell.Row; r < cell.Row + cell.RowSpan; r++)
                {
                    occupied.Add((c, r));
                }
            }

            columns = Math.Max(columns, cell.Column + cell.ColumnSpan);
            rows = Math.Max(rows, cell.Row + cell.RowSpan);
            cells.Add(cell);
        }

        bool Free(int column, int row, int columnSpan, int rowSpan)
        {
            for (var c = column; c < column + columnSpan; c++)
            {
                for (var r = row; r < row + rowSpan; r++)
                {
                    if (occupied.Contains((c, r)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        var pending = new List<Control>();
        foreach (var child in children)
        {
            var column = panel.GetColumn(child);
            var row = panel.GetRow(child);
            if (column >= 0 && row >= 0)
            {
                Occupy(new Cell(child, column, row, Math.Max(1, panel.GetColumnSpan(child)), Math.Max(1, panel.GetRowSpan(child))));
            }
            else
            {
                pending.Add(child);
            }
        }

        var fixedColumns = Math.Max(1, panel.ColumnCount == 0 ? columns : panel.ColumnCount);
        foreach (var child in pending)
        {
            var columnSpan = Math.Min(Math.Max(1, panel.GetColumnSpan(child)), fixedColumns);
            var rowSpan = Math.Max(1, panel.GetRowSpan(child));
            var wantedColumn = panel.GetColumn(child);
            var wantedRow = panel.GetRow(child);
            (int Column, int Row)? spot = null;
            if (wantedColumn >= 0)
            {
                for (var row = 0; spot is null; row++)
                {
                    if (Free(wantedColumn, row, columnSpan, rowSpan))
                    {
                        spot = (wantedColumn, row);
                    }
                }
            }
            else
            {
                for (var index = wantedRow >= 0 ? wantedRow * fixedColumns : 0; spot is null; index++)
                {
                    var row = index / fixedColumns;
                    var column = index % fixedColumns;
                    if (column + columnSpan <= fixedColumns && Free(column, row, columnSpan, rowSpan))
                    {
                        spot = (column, row);
                    }
                }
            }

            Occupy(new Cell(child, spot.Value.Column, spot.Value.Row, columnSpan, rowSpan));
        }

        return new Grid(Math.Max(1, columns), Math.Max(1, rows), cells);
    }

    /// <summary>The sizes of the columns or rows: absolute, automatic from the content, and percentages of what is left.</summary>
    /// <param name="styles">The styles.</param>
    /// <param name="count">The number of columns or rows.</param>
    /// <param name="available">The space for them, or -1 to size percentage ones to their content.</param>
    /// <param name="grid">The cells.</param>
    /// <param name="horizontal">Whether these are columns.</param>
    private static int[] Sizes(IReadOnlyList<TableLayoutStyle> styles, int count, int available, Grid grid, bool horizontal)
    {
        var sizes = new int[count];
        var content = new int[count];
        foreach (var cell in grid.Cells)
        {
            if ((horizontal ? cell.ColumnSpan : cell.RowSpan) != 1)
            {
                continue;
            }

            var index = horizontal ? cell.Column : cell.Row;
            content[index] = Math.Max(content[index], ContentSize(cell.Control, horizontal));
        }

        float percentTotal = 0;
        var used = 0;
        for (var i = 0; i < count; i++)
        {
            var style = i < styles.Count ? styles[i] : null;
            switch (style?.SizeType ?? SizeType.AutoSize)
            {
                case SizeType.Absolute:
                    sizes[i] = (int)Math.Round(style!.SizeValue);
                    used += sizes[i];
                    break;
                case SizeType.Percent:
                    percentTotal += style!.SizeValue;
                    break;
                default:
                    sizes[i] = content[i];
                    used += sizes[i];
                    break;
            }
        }

        // Controls spanning several automatic columns or rows widen the last of them if they do not fit.
        foreach (var cell in grid.Cells)
        {
            var span = horizontal ? cell.ColumnSpan : cell.RowSpan;
            var start = horizontal ? cell.Column : cell.Row;
            if (span <= 1 || start + span > count)
            {
                continue;
            }

            var need = ContentSize(cell.Control, horizontal);
            var have = sizes.Skip(start).Take(span).Sum();
            var last = start + span - 1;
            if (need > have && (last >= styles.Count || styles[last].SizeType == SizeType.AutoSize))
            {
                sizes[last] += need - have;
                used += need - have;
            }
        }

        if (percentTotal <= 0)
        {
            return sizes;
        }

        if (available < 0)
        {
            // Sized to the content, as an automatically sized table is: percentage columns get their content's size.
            for (var i = 0; i < count; i++)
            {
                if (i < styles.Count && styles[i].SizeType == SizeType.Percent)
                {
                    sizes[i] = content[i];
                }
            }

            return sizes;
        }

        var remaining = Math.Max(0, available - used);
        var given = 0;
        var lastPercent = -1;
        for (var i = 0; i < count; i++)
        {
            if (i < styles.Count && styles[i].SizeType == SizeType.Percent)
            {
                sizes[i] = (int)(remaining * styles[i].SizeValue / percentTotal);
                given += sizes[i];
                lastPercent = i;
            }
        }

        if (lastPercent >= 0)
        {
            sizes[lastPercent] += remaining - given;
        }

        return sizes;
    }

    private static int ContentSize(Control control, bool horizontal)
    {
        var size = control.AutoSize ? control.GetPreferredSize(Size.Empty) : control.Size;
        return horizontal ? size.Width + control.Margin.Horizontal : size.Height + control.Margin.Vertical;
    }

    /// <summary>A control's bounds in its cell, by its dock or anchors.</summary>
    private static Rectangle PlaceInCell(Control control, Rectangle cell)
    {
        var margin = control.Margin;
        var inner = Rectangle.FromLTRB(cell.Left + margin.Left, cell.Top + margin.Top, Math.Max(cell.Left + margin.Left, cell.Right - margin.Right), Math.Max(cell.Top + margin.Top, cell.Bottom - margin.Bottom));
        var size = control.AutoSize ? control.GetPreferredSize(Size.Empty) : control.Size;
        switch (control.Dock)
        {
            case DockStyle.Fill:
                return inner;
            case DockStyle.Top:
                return new Rectangle(inner.X, inner.Y, inner.Width, Math.Min(size.Height, inner.Height));
            case DockStyle.Bottom:
                return new Rectangle(inner.X, inner.Bottom - Math.Min(size.Height, inner.Height), inner.Width, Math.Min(size.Height, inner.Height));
            case DockStyle.Left:
                return new Rectangle(inner.X, inner.Y, Math.Min(size.Width, inner.Width), inner.Height);
            case DockStyle.Right:
                return new Rectangle(inner.Right - Math.Min(size.Width, inner.Width), inner.Y, Math.Min(size.Width, inner.Width), inner.Height);
        }

        var anchor = control.Anchor;
        var (x, width) = Align(inner.X, inner.Width, size.Width, (anchor & AnchorStyles.Left) != 0, (anchor & AnchorStyles.Right) != 0);
        var (y, height) = Align(inner.Y, inner.Height, size.Height, (anchor & AnchorStyles.Top) != 0, (anchor & AnchorStyles.Bottom) != 0);
        return new Rectangle(x, y, width, height);
    }

    private static (int Start, int Length) Align(int start, int room, int length, bool toStart, bool toEnd) => (toStart, toEnd) switch
    {
        (true, true) => (start, room),
        (false, true) => (start + room - length, length),
        (true, false) => (start, length),
        _ => (start + ((room - length) / 2), length),
    };

    /// <summary>The cells of a table.</summary>
    private sealed record Grid(int Columns, int Rows, List<Cell> Cells);

    /// <summary>A control's cell.</summary>
    private sealed record Cell(Control Control, int Column, int Row, int ColumnSpan, int RowSpan);
}
