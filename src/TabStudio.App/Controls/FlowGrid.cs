using Avalonia;
using Avalonia.Controls;

namespace TabStudio.App.Controls;

/// <summary>
/// Lays its children out in rows of equal columns, as many columns as fit at least <see cref="MinColumnWidth"/>, each
/// child as wide as its column: tiles that fill the width of a wide pane and wrap to fewer columns in a narrow one,
/// such as the Toolbox's controls. A row is as tall as its tallest child.
/// </summary>
public sealed class FlowGrid : Panel
{
    /// <summary>The narrowest a column may be.</summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<FlowGrid, double>(nameof(MinColumnWidth), 200);

    /// <summary>The space between columns and between rows.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<FlowGrid, double>(nameof(Spacing), 8);

    static FlowGrid()
    {
        AffectsMeasure<FlowGrid>(MinColumnWidthProperty, SpacingProperty);
    }

    /// <inheritdoc cref="MinColumnWidthProperty"/>
    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>How many columns fit a width, at least one.</summary>
    /// <param name="width">The width.</param>
    /// <param name="minColumnWidth">The narrowest a column may be.</param>
    /// <param name="spacing">The space between columns.</param>
    public static int ColumnsFor(double width, double minColumnWidth, double spacing) =>
        double.IsFinite(width) ? Math.Max(1, (int)Math.Floor((width + spacing) / (Math.Max(1, minColumnWidth) + spacing))) : 1;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : MinColumnWidth;
        var (columns, columnWidth) = Layout(width);
        var height = 0.0;
        var rowHeight = 0.0;
        var column = 0;
        foreach (var child in Visible())
        {
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            if (++column == columns)
            {
                height += rowHeight + Spacing;
                rowHeight = 0;
                column = 0;
            }
        }

        height += column > 0 ? rowHeight : -Math.Min(height, Spacing);
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, columnWidth) = Layout(finalSize.Width);
        var children = Visible().ToList();
        var top = 0.0;
        for (var first = 0; first < children.Count; first += columns)
        {
            var row = children.GetRange(first, Math.Min(columns, children.Count - first));
            var rowHeight = row.Max(child => child.DesiredSize.Height);
            for (var i = 0; i < row.Count; i++)
            {
                row[i].Arrange(new Rect(i * (columnWidth + Spacing), top, columnWidth, rowHeight));
            }

            top += rowHeight + Spacing;
        }

        return finalSize;
    }

    private (int Columns, double ColumnWidth) Layout(double width)
    {
        var columns = ColumnsFor(width, MinColumnWidth, Spacing);
        return (columns, Math.Max(0, (width - (Spacing * (columns - 1))) / columns));
    }

    private IEnumerable<Control> Visible() => Children.Where(child => child.IsVisible);
}
