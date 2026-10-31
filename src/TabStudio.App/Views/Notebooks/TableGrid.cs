using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TabStudio.Notebooks.Outputs;
using TabStudio.App.Accessibility;

namespace TabStudio.App.Views.Notebooks;

/// <summary>
/// A table output as a native grid: a header row and one row per item, scrolling sideways when the columns do not
/// fit, as Polyglot Notebooks shows collections.
/// </summary>
public sealed class TableGrid : Border
{
    /// <summary>The table to show.</summary>
    public static readonly StyledProperty<TableOutput?> TableProperty =
        AvaloniaProperty.Register<TableGrid, TableOutput?>(nameof(Table));

    /// <summary>Text wider than this is cut short with an ellipsis; the full text is in the cell's tooltip.</summary>
    public const double MaxColumnWidth = 360;

    /// <summary>Taller tables scroll inside the output.</summary>
    public const double MaxTableHeight = 420;

    static TableGrid()
    {
        TableProperty.Changed.AddClassHandler<TableGrid>((grid, _) => grid.Build());
    }

    /// <summary>Creates the grid.</summary>
    public TableGrid()
    {
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        ClipToBounds = true;
        HorizontalAlignment = HorizontalAlignment.Left;
        BorderBrush = ThemeResources.Brush("Brush.Border");
    }

    /// <inheritdoc cref="TableProperty"/>
    public TableOutput? Table
    {
        get => GetValue(TableProperty);
        set => SetValue(TableProperty, value);
    }

    private void Build()
    {
        if (Table is not { } table)
        {
            Child = null;
            return;
        }

        var header = table.Columns.Select(c => (Control)CreateText(c, isHeader: true)).ToList();
        var rows = table.Rows.Select(row => (IReadOnlyList<Control>)row.Select(c => (Control)CreateText(c, isHeader: false)).ToList());
        Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = MaxTableHeight,
            Content = TableLayout.Create(header, rows),
        };
    }

    private static TextBlock CreateText(string text, bool isHeader)
    {
        var block = new TextBlock
        {
            Text = text.ReplaceLineEndings(" "),
            MaxWidth = MaxColumnWidth,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = !isHeader && TableLayout.IsNumber(text) ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Foreground = ThemeResources.Brush(isHeader ? "Brush.TextBright" : "Brush.Text"),
            FontWeight = isHeader ? FontWeight.SemiBold : FontWeight.Normal,
        };
        TypeScale.SetBaseFontSize(block, 12.5);

        if (text.Length > 40)
        {
            ToolTip.SetTip(block, text);
        }

        return block;
    }
}
