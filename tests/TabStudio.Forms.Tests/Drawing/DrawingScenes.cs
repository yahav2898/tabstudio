using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TabStudio.Forms.Tests.Drawing;

/// <summary>A picture drawn on a bitmap with <see cref="Graphics.FromImage"/>, compared with a reference PNG.</summary>
/// <param name="Name">The reference's file name, without extension.</param>
/// <param name="Width">The bitmap's width.</param>
/// <param name="Height">The bitmap's height.</param>
/// <param name="FromGdiPlus">
/// Whether the reference was drawn by GDI+ on Windows (build/gdiplus-references), so the scene is held to GDI+'s
/// output; otherwise the reference is TabStudio's own earlier output, for scenes with text, whose fonts differ.
/// </param>
/// <param name="Draw">Draws the scene on a white bitmap.</param>
public sealed record DrawingScene(string Name, int Width, int Height, bool FromGdiPlus, Action<Graphics> Draw)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// The scenes the drawing tests render. This file is compiled both into the tests, against TabStudio's Forms
/// layer, and into build/gdiplus-references, against GDI+ on Windows, which draws the references.
/// </summary>
public static class DrawingScenes
{
    /// <summary>Every scene.</summary>
    public static IReadOnlyList<DrawingScene> All { get; } =
    [
        new("lines", 160, 100, true, Lines),
        new("lines-antialiased", 160, 100, true, g =>
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Lines(g);
        }),
        new("rectangles", 160, 100, true, Rectangles),
        new("ellipses", 160, 100, true, Ellipses),
        new("polygons", 160, 100, true, Polygons),
        new("arcs-and-pies", 160, 100, true, ArcsAndPies),
        new("curves", 160, 100, true, Curves),
        new("pen-caps", 160, 120, true, PenCaps),
        new("pen-joins", 160, 100, true, PenJoins),
        new("dashes", 160, 100, true, Dashes),
        new("linear-gradients", 160, 100, true, LinearGradients),
        new("texture-brushes", 160, 100, true, TextureBrushes),
        new("hatch-brushes", 216, 168, true, HatchBrushes),
        new("path-gradient", 160, 100, true, PathGradient),
        new("transforms", 160, 120, true, Transforms),
        new("clipping", 160, 100, true, Clipping),
        new("images", 160, 100, true, Images),
        new("compositing", 160, 100, true, Compositing),
        new("alpha-blending", 160, 100, true, AlphaBlending),
        new("text", 200, 100, false, Text),
        new("text-transformed", 200, 120, false, TextTransformed),
    ];

    /// <summary>A small picture of four colored squares and a diagonal, to draw and tile.</summary>
    public static Bitmap Checker()
    {
        var bitmap = new Bitmap(8, 8);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var color = (x < 4, y < 4) switch
                {
                    (true, true) => Color.Red,
                    (false, true) => Color.Lime,
                    (true, false) => Color.Blue,
                    _ => Color.Yellow,
                };
                bitmap.SetPixel(x, y, x == y ? Color.Black : color);
            }
        }

        return bitmap;
    }

    private static void Lines(Graphics g)
    {
        using var thin = new Pen(Color.Black);
        using var red = new Pen(Color.Red, 3);
        using var blue = new Pen(Color.Blue, 8);
        g.DrawLine(thin, 10, 10, 150, 10);
        g.DrawLine(thin, 10, 15, 10, 90);
        g.DrawLine(thin, 15, 15, 150, 60);
        g.DrawLine(red, 20, 30, 150, 30);
        g.DrawLine(red, 20, 40, 120, 90);
        g.DrawLine(blue, 30, 60, 150, 90);
    }

    private static void Rectangles(Graphics g)
    {
        using var fill = new SolidBrush(Color.FromArgb(255, 30, 144, 255));
        using var pen = new Pen(Color.DarkRed, 3);
        using var thin = new Pen(Color.Black);
        g.FillRectangle(fill, 10, 10, 60, 40);
        g.DrawRectangle(thin, 10, 10, 60, 40);
        g.DrawRectangle(pen, 90, 10, 60, 40);
        g.FillRectangle(Brushes.Orange, 20, 60, 30, 30);
        g.DrawRectangle(thin, 80, 60, 0, 30);
        g.FillRectangles(Brushes.Green, [new Rectangle(100, 60, 10, 10), new Rectangle(120, 70, 10, 20)]);
    }

    private static void Ellipses(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.Purple, 4);
        g.FillEllipse(Brushes.Gold, 10, 10, 60, 40);
        g.DrawEllipse(pen, 90, 10, 60, 40);
        g.FillEllipse(Brushes.Teal, 20, 60, 30, 30);
        g.SmoothingMode = SmoothingMode.None;
        g.DrawEllipse(Pens.Black, 80, 60, 70, 30);
    }

    private static void Polygons(Graphics g)
    {
        PointF[] star = [new(40, 5), new(52, 40), new(15, 18), new(65, 18), new(28, 40)];
        g.FillPolygon(Brushes.Crimson, star, FillMode.Alternate);
        g.TranslateTransform(80, 0);
        g.FillPolygon(Brushes.Crimson, star, FillMode.Winding);
        g.ResetTransform();
        using var pen = new Pen(Color.Navy, 2);
        g.DrawPolygon(pen, new Point[] { new(10, 60), new(70, 55), new(60, 95), new(20, 90) });
        g.DrawLines(pen, new Point[] { new(90, 60), new(110, 95), new(130, 60), new(150, 95) });
    }

    private static void ArcsAndPies(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.DarkGreen, 3);
        g.DrawArc(pen, 10, 10, 60, 60, 0, 270);
        g.FillPie(Brushes.Coral, 90, 10, 60, 60, 30, 120);
        g.DrawPie(Pens.Black, 90, 10, 60, 60, 160, 90);
        g.DrawArc(Pens.Blue, 20, 75, 120, 20, 180, 180);
    }

    private static void Curves(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.Maroon, 2);
        g.DrawBezier(pen, 10, 90, 40, 10, 80, 10, 150, 90);
        PointF[] points = [new(10, 50), new(50, 20), new(90, 70), new(150, 30)];
        g.DrawCurve(Pens.Blue, points);
        g.DrawClosedCurve(Pens.Green, [new PointF(60, 60), new PointF(100, 55), new PointF(90, 95), new PointF(55, 90)]);
    }

    private static void PenCaps(Graphics g)
    {
        LineCap[] caps = [LineCap.Flat, LineCap.Square, LineCap.Round, LineCap.Triangle, LineCap.SquareAnchor, LineCap.RoundAnchor, LineCap.DiamondAnchor, LineCap.ArrowAnchor];
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (var i = 0; i < caps.Length; i++)
        {
            using var pen = new Pen(Color.Black, 6) { StartCap = caps[i], EndCap = caps[i] };
            g.DrawLine(pen, 25, 10 + (i * 13), 135, 10 + (i * 13));
        }
    }

    private static void PenJoins(Graphics g)
    {
        LineJoin[] joins = [LineJoin.Miter, LineJoin.Bevel, LineJoin.Round];
        for (var i = 0; i < joins.Length; i++)
        {
            using var pen = new Pen(Color.SteelBlue, 10) { LineJoin = joins[i] };
            var x = 15 + (i * 50);
            g.DrawLines(pen, new Point[] { new(x, 80), new(x + 15, 20), new(x + 30, 80) });
        }
    }

    private static void Dashes(Graphics g)
    {
        DashStyle[] styles = [DashStyle.Dash, DashStyle.Dot, DashStyle.DashDot, DashStyle.DashDotDot];
        for (var i = 0; i < styles.Length; i++)
        {
            using var thin = new Pen(Color.Black) { DashStyle = styles[i] };
            using var thick = new Pen(Color.DarkBlue, 3) { DashStyle = styles[i] };
            g.DrawLine(thin, 10, 10 + (i * 10), 150, 10 + (i * 10));
            g.DrawLine(thick, 10, 55 + (i * 10), 150, 55 + (i * 10));
        }

        using var custom = new Pen(Color.Red, 2) { DashPattern = [4, 2, 1, 2] };
        g.DrawLine(custom, 10, 95, 150, 95);
    }

    private static void LinearGradients(Graphics g)
    {
        using (var horizontal = new LinearGradientBrush(new Rectangle(10, 10, 60, 35), Color.Red, Color.Blue, LinearGradientMode.Horizontal))
        {
            g.FillRectangle(horizontal, 10, 10, 60, 35);
        }

        using (var vertical = new LinearGradientBrush(new Rectangle(90, 10, 60, 35), Color.White, Color.Black, LinearGradientMode.Vertical))
        {
            g.FillRectangle(vertical, 90, 10, 60, 35);
        }

        using (var tiled = new LinearGradientBrush(new Point(10, 0), new Point(30, 0), Color.Yellow, Color.Green))
        {
            g.FillRectangle(tiled, 10, 55, 60, 35);
        }

        using (var blended = new LinearGradientBrush(new Rectangle(90, 55, 60, 35), Color.Black, Color.Black, 0f))
        {
            blended.InterpolationColors = new ColorBlend(3) { Colors = [Color.Red, Color.Lime, Color.Blue], Positions = [0, 0.5f, 1] };
            g.FillRectangle(blended, 90, 55, 60, 35);
        }
    }

    private static void TextureBrushes(Graphics g)
    {
        using var checker = Checker();
        using (var tile = new TextureBrush(checker))
        {
            g.FillRectangle(tile, 10, 10, 60, 40);
        }

        using (var flip = new TextureBrush(checker, WrapMode.TileFlipXY))
        {
            g.FillRectangle(flip, 90, 10, 60, 40);
        }

        using (var shifted = new TextureBrush(checker))
        {
            shifted.TranslateTransform(3, 2);
            g.FillEllipse(shifted, 10, 55, 60, 40);
        }

        using (var clamp = new TextureBrush(checker, WrapMode.Clamp))
        {
            clamp.TranslateTransform(100, 60);
            g.FillRectangle(clamp, 90, 55, 60, 40);
        }
    }

    private static void HatchBrushes(Graphics g)
    {
        for (var style = 0; style <= 52; style++)
        {
            using var brush = new HatchBrush((HatchStyle)style, Color.Black, Color.White);
            var x = 4 + ((style % 9) * 24);
            var y = 4 + ((style / 9) * 28);
            g.FillRectangle(brush, x, y, 16, 16);
        }
    }

    private static void PathGradient(Graphics g)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(10, 10, 80, 80);
        using var brush = new PathGradientBrush(path) { CenterColor = Color.White, SurroundColors = [Color.DarkBlue] };
        g.FillEllipse(brush, 10, 10, 80, 80);
        using var square = new PathGradientBrush(new PointF[] { new(100, 20), new(150, 20), new(150, 80), new(100, 80) })
        {
            CenterColor = Color.Yellow,
            SurroundColors = [Color.Red],
        };
        g.FillRectangle(square, 100, 20, 50, 60);
    }

    private static void Transforms(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(80, 60);
        for (var i = 0; i < 6; i++)
        {
            g.RotateTransform(30);
            using var brush = new SolidBrush(Color.FromArgb(255, 40 * i, 100, 255 - (40 * i)));
            g.FillRectangle(brush, 10, -5, 45, 10);
        }

        g.ResetTransform();
        g.ScaleTransform(2, 1);
        g.DrawEllipse(Pens.Black, 5, 5, 20, 20);
        using var matrix = new Matrix();
        matrix.Shear(0.5f, 0);
        g.Transform = matrix;
        g.FillRectangle(Brushes.Orange, 100, 80, 30, 30);
    }

    private static void Clipping(Graphics g)
    {
        g.SetClip(new Rectangle(10, 10, 60, 40));
        g.FillEllipse(Brushes.Blue, 0, 0, 80, 80);
        g.ResetClip();

        using var circle = new GraphicsPath();
        circle.AddEllipse(90, 10, 60, 60);
        g.SetClip(circle);
        g.FillRectangle(Brushes.Red, 80, 0, 80, 100);
        g.ExcludeClip(new Rectangle(110, 30, 20, 20));
        g.FillRectangle(Brushes.Black, 115, 0, 10, 100);
        g.ResetClip();

        g.SetClip(new Rectangle(10, 60, 60, 30));
        g.IntersectClip(new Rectangle(30, 50, 60, 30));
        g.FillRectangle(Brushes.Green, 0, 50, 100, 50);
    }

    private static void Images(Graphics g)
    {
        using var checker = Checker();
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(checker, 10, 10, 40, 40);
        g.DrawImage(checker, new Rectangle(60, 10, 40, 40), new Rectangle(0, 0, 4, 4), GraphicsUnit.Pixel);
        g.DrawImageUnscaled(checker, 110, 10);
        g.DrawImage(checker, new Point[] { new(110, 50), new(150, 60), new(100, 90) });

        using var gray = new ImageAttributes();
        gray.SetColorMatrix(new ColorMatrix(
        [
            [0.3f, 0.3f, 0.3f, 0, 0],
            [0.59f, 0.59f, 0.59f, 0, 0],
            [0.11f, 0.11f, 0.11f, 0, 0],
            [0, 0, 0, 1, 0],
            [0, 0, 0, 0, 1],
        ]));
        g.DrawImage(checker, new Rectangle(10, 55, 40, 40), 0, 0, 8, 8, GraphicsUnit.Pixel, gray);
    }

    private static void Compositing(Graphics g)
    {
        g.FillRectangle(Brushes.Blue, 10, 10, 140, 80);
        g.CompositingMode = CompositingMode.SourceCopy;
        using var half = new SolidBrush(Color.FromArgb(128, 255, 0, 0));
        g.FillRectangle(half, 20, 20, 50, 30);
        g.CompositingMode = CompositingMode.SourceOver;
        g.FillRectangle(half, 90, 20, 50, 30);
        g.SetClip(new Rectangle(30, 60, 100, 20));
        g.Clear(Color.Transparent);
    }

    private static void AlphaBlending(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var red = new SolidBrush(Color.FromArgb(160, 255, 0, 0));
        using var green = new SolidBrush(Color.FromArgb(160, 0, 200, 0));
        using var blue = new SolidBrush(Color.FromArgb(160, 0, 0, 255));
        g.FillEllipse(red, 30, 10, 60, 60);
        g.FillEllipse(green, 60, 10, 60, 60);
        g.FillEllipse(blue, 45, 35, 60, 60);
    }

    private static void Text(Graphics g)
    {
        using var font = new Font("Arial", 14);
        using var bold = new Font("Arial", 11, FontStyle.Bold);
        g.DrawString("Hello, tablet!", font, Brushes.Black, 10, 10);
        g.DrawString("Centered", bold, Brushes.DarkRed, new RectangleF(0, 40, 200, 20), new StringFormat { Alignment = StringAlignment.Center });
        using var gradient = new LinearGradientBrush(new Rectangle(10, 65, 180, 25), Color.Blue, Color.Red, LinearGradientMode.Horizontal);
        g.DrawString("Gradient text", font, gradient, 10, 65);
    }

    private static void TextTransformed(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var font = new Font("Arial", 12);
        g.TranslateTransform(100, 60);
        for (var i = 0; i < 4; i++)
        {
            g.DrawString("Spin", font, Brushes.Navy, 10, -8);
            g.RotateTransform(90);
        }
    }
}
