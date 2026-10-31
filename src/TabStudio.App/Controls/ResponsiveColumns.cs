using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace TabStudio.App.Controls;

/// <summary>
/// A grid's columns for a wide and a compact layout, such as a list's header and rows on a tablet and on a phone. Fixed
/// columns that fit a tablet leave a phone's star column nothing: the Error List's 564 pixels of Code, Project, File
/// and Line left a 352-pixel panel no Description at all.
/// </summary>
public sealed class ResponsiveColumns
{
    /// <summary>The columns of the wide layout, as <see cref="Grid.ColumnDefinitions"/> are written in XAML.</summary>
    public static readonly AttachedProperty<string?> WideProperty =
        AvaloniaProperty.RegisterAttached<Grid, string?>("Wide", typeof(ResponsiveColumns));

    /// <summary>The columns of the compact layout, with the same number of columns.</summary>
    public static readonly AttachedProperty<string?> CompactProperty =
        AvaloniaProperty.RegisterAttached<Grid, string?>("Compact", typeof(ResponsiveColumns));

    /// <summary>Whether the grid uses its compact columns.</summary>
    public static readonly AttachedProperty<bool> IsCompactProperty =
        AvaloniaProperty.RegisterAttached<Grid, bool>("IsCompact", typeof(ResponsiveColumns));

    /// <summary>The width below which a panel is compact, as a phone's is.</summary>
    public const double CompactWidth = 640;

    /// <summary>Whether a width is a compact panel's: bind <see cref="IsCompactProperty"/> to a view's <c>Bounds.Width</c> with it.</summary>
    public static IValueConverter IsNarrow { get; } = new FuncValueConverter<double, bool>(width => width is > 0 and < CompactWidth);

    static ResponsiveColumns()
    {
        WideProperty.Changed.AddClassHandler<Grid>((grid, _) => Apply(grid));
        CompactProperty.Changed.AddClassHandler<Grid>((grid, _) => Apply(grid));
        IsCompactProperty.Changed.AddClassHandler<Grid>((grid, _) => Apply(grid));
        Control.LoadedEvent.AddClassHandler<Grid>((grid, _) =>
        {
            if (grid.GetValue(WideProperty) is not null)
            {
                Fold(grid);
            }
        });
    }

    private ResponsiveColumns()
    {
    }

    /// <summary>Gets the wide columns.</summary>
    /// <param name="grid">The grid.</param>
    public static string? GetWide(Grid grid) => grid.GetValue(WideProperty);

    /// <summary>Sets the wide columns.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="value">The columns, such as <c>34,150,*,130</c>.</param>
    public static void SetWide(Grid grid, string? value) => grid.SetValue(WideProperty, value);

    /// <summary>Gets the compact columns.</summary>
    /// <param name="grid">The grid.</param>
    public static string? GetCompact(Grid grid) => grid.GetValue(CompactProperty);

    /// <summary>Sets the compact columns.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="value">The columns, such as <c>34,70,*,0</c>.</param>
    public static void SetCompact(Grid grid, string? value) => grid.SetValue(CompactProperty, value);

    /// <summary>Gets whether the grid is compact.</summary>
    /// <param name="grid">The grid.</param>
    public static bool GetIsCompact(Grid grid) => grid.GetValue(IsCompactProperty);

    /// <summary>Sets whether the grid is compact.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="value">Whether it is.</param>
    public static void SetIsCompact(Grid grid, bool value) => grid.SetValue(IsCompactProperty, value);

    private static void Apply(Grid grid)
    {
        var columns = grid.GetValue(IsCompactProperty) ? grid.GetValue(CompactProperty) ?? grid.GetValue(WideProperty) : grid.GetValue(WideProperty);
        if (columns is not null)
        {
            grid.ColumnDefinitions = ColumnDefinitions.Parse(columns);
        }

        Fold(grid);
    }

    /// <summary>
    /// The class of what sits only in columns a layout gives no width: it is hidden (Theme/Accessibility.axaml), so
    /// a screen reader does not read it and nothing is drawn squeezed to nothing.
    /// </summary>
    public const string FoldedClass = "columnFolded";

    private static void Fold(Grid grid)
    {
        var definitions = grid.ColumnDefinitions;
        foreach (var child in grid.Children)
        {
            var first = Math.Min(Grid.GetColumn(child), Math.Max(0, definitions.Count - 1));
            var span = Math.Max(1, Grid.GetColumnSpan(child));
            var folded = definitions.Count > 0;
            for (var i = first; i < Math.Min(definitions.Count, first + span); i++)
            {
                folded &= definitions[i].Width is { IsAbsolute: true, Value: 0 };
            }

            child.Classes.Set(FoldedClass, folded);
        }
    }
}
