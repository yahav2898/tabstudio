using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Controls;

namespace TabStudio.App.Tests.DesktopMode;

/// <summary>The Toolbox's tiles: as many equal columns as fit, each tile as wide as its column.</summary>
public sealed class FlowGridTests
{
    [Theory]
    [InlineData(1000, 240, 8, 4)]
    [InlineData(600, 240, 8, 2)]
    [InlineData(487, 240, 8, 1)]
    [InlineData(488, 240, 8, 2)]
    [InlineData(100, 240, 8, 1)]
    [InlineData(double.PositiveInfinity, 240, 8, 1)]
    public void As_many_columns_as_fit_at_their_least_width(double width, double minColumnWidth, double spacing, int columns)
    {
        Assert.Equal(columns, FlowGrid.ColumnsFor(width, minColumnWidth, spacing));
    }

    [AvaloniaFact]
    public void Tiles_fill_the_width_and_wrap_into_rows_as_tall_as_their_tallest_tile()
    {
        var grid = new FlowGrid { MinColumnWidth = 240, Spacing = 8 };
        foreach (var height in new[] { 60, 70, 60, 60, 50 })
        {
            grid.Children.Add(new Border { Height = height, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top });
        }

        grid.Children.Add(new Border { Height = 500, IsVisible = false });
        grid.Measure(new Size(600, double.PositiveInfinity));
        grid.Arrange(new Rect(0, 0, 600, grid.DesiredSize.Height));

        // Two columns of 296: rows of 70, 60 and 50 with 8 between them.
        Assert.Equal(new Size(600, 70 + 8 + 60 + 8 + 50), grid.DesiredSize);
        Assert.Equal(new Rect(0, 0, 296, 60), grid.Children[0].Bounds);
        Assert.Equal(new Rect(304, 0, 296, 70), grid.Children[1].Bounds);
        Assert.Equal(new Rect(0, 78, 296, 60), grid.Children[2].Bounds);
        Assert.Equal(new Rect(0, 146, 296, 50), grid.Children[4].Bounds);
    }
}
