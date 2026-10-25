using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using TabStudio.App.Editing.Extensions.Svg;

namespace TabStudio.App.Tests.Editing;

/// <summary>The SVG reader behind file icon themes: path data as optimizers write it, shapes, paint and transforms.</summary>
public sealed class SvgReaderTests
{
    [AvaloniaTheory]
    [InlineData("M2 2h20v20H2z", 2, 2, 20, 20)]
    [InlineData("M0 0L10 0 10 10z", 0, 0, 10, 10)]
    [InlineData("m10 10 5 0 0 5", 10, 10, 5, 5)]
    [InlineData("M1.5.5h10v-.5", 1.5, 0, 10, 0.5)]
    [InlineData("M0 10a10 10 0 0120 0", 0, 0, 20, 10)]
    [InlineData("M0 0C0 0 10 0 10 10S20 20 20 20", 0, 0, 20, 20)]
    [InlineData("M0 0Q10 0 10 10T20 20", 0, 0, 20, 20)]
    [InlineData("M0 0h1e1v1E1", 0, 0, 10, 10)]
    [InlineData("M0 0L10 10 x 30 30", 0, 0, 10, 10)]
    public void Path_data_is_read_as_the_SVG_grammar_says(string data, double x, double y, double width, double height)
    {
        var bounds = SvgPathReader.Read(data).Bounds;

        Assert.Equal(x, bounds.X, 3);
        Assert.Equal(y, bounds.Y, 3);
        Assert.Equal(width, bounds.Width, 3);
        Assert.Equal(height, bounds.Height, 3);
    }

    [AvaloniaFact]
    public void An_icon_is_drawn_at_its_view_box_size_with_its_colors()
    {
        var image = SvgDrawingReader.Read("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16" height="16">
              <style>.accent { fill: #26a69a }</style>
              <path fill="#ffca28" d="M10 4H4c-1.11 0-2 .89-2 2v12a2 2 0 002 2h16a2 2 0 002-2V8a2 2 0 00-2-2h-8l-2-2z"/>
              <rect class="accent" x="6" y="10" width="12" height="4" rx="1"/>
              <polygon points="2,2 6,2 4,6" fill="rgb(255, 0, 0)" fill-opacity="0.5"/>
            </svg>
            """);

        Assert.NotNull(image);
        Assert.Equal(new Size(24, 24), image.Size);
        var drawings = Flatten(image.Drawing!).OfType<GeometryDrawing>().ToList();
        Assert.Contains(drawings, d => (d.Brush as ISolidColorBrush)?.Color == Color.Parse("#ffca28"));
        Assert.Contains(drawings, d => (d.Brush as ISolidColorBrush)?.Color == Color.Parse("#26a69a"));
        Assert.Contains(drawings, d => (d.Brush as ISolidColorBrush)?.Color == Color.FromArgb(128, 255, 0, 0));
    }

    [AvaloniaFact]
    public void Gradients_uses_transforms_and_strokes_are_drawn()
    {
        var image = SvgDrawingReader.Read("""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 32 32">
              <defs>
                <linearGradient id="base"><stop offset="0%" stop-color="#ffd54f"/><stop offset="100%" stop-color="#ff8f00"/></linearGradient>
                <linearGradient id="glow" xlink:href="#base" x1="0" y1="0" x2="1" y2="1"/>
                <circle id="dot" r="2" fill="#fff"/>
              </defs>
              <circle cx="16" cy="16" r="9" fill="url(#glow)"/>
              <circle cx="16" cy="16" r="13" style="fill:none;stroke:#26a69a;stroke-width:3"/>
              <use xlink:href="#dot" x="16" y="3"/>
              <g transform="translate(2 2) scale(2)"><path d="M0 0h2v2H0z"/></g>
            </svg>
            """);

        Assert.NotNull(image);
        var drawings = Flatten(image.Drawing!).OfType<GeometryDrawing>().ToList();
        var gradient = Assert.IsType<LinearGradientBrush>(drawings.Select(d => d.Brush).OfType<LinearGradientBrush>().Single());
        Assert.Equal(2, gradient.GradientStops.Count);
        Assert.Equal(new RelativePoint(1, 1, RelativeUnit.Relative), gradient.EndPoint);
        Assert.Contains(drawings, d => d.Pen is { Thickness: 3 } && d.Brush is null);
        Assert.Contains(Flatten(image.Drawing!).OfType<DrawingGroup>(), g => g.Transform is MatrixTransform { Matrix.M11: 2 });
        Assert.Contains(drawings, d => (d.Brush as ISolidColorBrush)?.Color == Colors.White);
    }

    [AvaloniaTheory]
    [InlineData("<html><body /></html>")]
    [InlineData("not xml at all")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 0 0\"/>")]
    public void What_is_not_an_SVG_image_is_not_drawn(string text) => Assert.Null(SvgDrawingReader.Read(text));

    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
        {
            foreach (var child in group.Children.SelectMany(Flatten))
            {
                yield return child;
            }
        }
    }
}
