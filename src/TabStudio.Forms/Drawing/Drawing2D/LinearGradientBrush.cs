using TabStudio.Forms.Rendering;

namespace System.Drawing.Drawing2D;

/// <summary>How a gradient or texture repeats beyond its area.</summary>
public enum WrapMode
{
    /// <summary>The gradient repeats.</summary>
    Tile = 0,

    /// <summary>The gradient repeats, mirrored horizontally.</summary>
    TileFlipX = 1,

    /// <summary>The gradient repeats, mirrored vertically.</summary>
    TileFlipY = 2,

    /// <summary>The gradient repeats, mirrored both ways.</summary>
    TileFlipXY = 3,

    /// <summary>The gradient does not repeat.</summary>
    Clamp = 4,
}

/// <summary>A brush that blends two colors along a line.</summary>
public sealed class LinearGradientBrush : Brush
{
    private PointF _start;
    private PointF _end;

    /// <summary>Creates a gradient between two points.</summary>
    /// <param name="point1">Where the first color is.</param>
    /// <param name="point2">Where the second color is.</param>
    /// <param name="color1">The first color.</param>
    /// <param name="color2">The second color.</param>
    public LinearGradientBrush(PointF point1, PointF point2, Color color1, Color color2)
    {
        _start = point1;
        _end = point2;
        LinearColors = [color1, color2];
        Rectangle = RectangleF.FromLTRB(Math.Min(point1.X, point2.X), Math.Min(point1.Y, point2.Y), Math.Max(point1.X, point2.X), Math.Max(point1.Y, point2.Y));
    }

    /// <summary>Creates a gradient between two points.</summary>
    /// <param name="point1">Where the first color is.</param>
    /// <param name="point2">Where the second color is.</param>
    /// <param name="color1">The first color.</param>
    /// <param name="color2">The second color.</param>
    public LinearGradientBrush(Point point1, Point point2, Color color1, Color color2)
        : this((PointF)point1, (PointF)point2, color1, color2)
    {
    }

    /// <summary>Creates a gradient across a rectangle in a direction.</summary>
    /// <param name="rect">The rectangle the gradient spans.</param>
    /// <param name="color1">The starting color.</param>
    /// <param name="color2">The ending color.</param>
    /// <param name="linearGradientMode">The direction.</param>
    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
        : this(rect, color1, color2, linearGradientMode switch
        {
            LinearGradientMode.Vertical => 90f,
            LinearGradientMode.ForwardDiagonal => 45f,
            LinearGradientMode.BackwardDiagonal => 135f,
            _ => 0f,
        })
    {
    }

    /// <summary>Creates a gradient across a rectangle in a direction.</summary>
    /// <param name="rect">The rectangle the gradient spans.</param>
    /// <param name="color1">The starting color.</param>
    /// <param name="color2">The ending color.</param>
    /// <param name="linearGradientMode">The direction.</param>
    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
        : this((RectangleF)rect, color1, color2, linearGradientMode)
    {
    }

    /// <summary>Creates a gradient across a rectangle at an angle.</summary>
    /// <param name="rect">The rectangle the gradient spans.</param>
    /// <param name="color1">The starting color.</param>
    /// <param name="color2">The ending color.</param>
    /// <param name="angle">The angle in degrees, clockwise from the x-axis.</param>
    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle)
    {
        Rectangle = rect;
        LinearColors = [color1, color2];
        var radians = angle * Math.PI / 180;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        var half = ((rect.Width * Math.Abs(cos)) + (rect.Height * Math.Abs(sin))) / 2;
        var center = new PointF(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        _start = new PointF((float)(center.X - (cos * half)), (float)(center.Y - (sin * half)));
        _end = new PointF((float)(center.X + (cos * half)), (float)(center.Y + (sin * half)));
    }

    /// <summary>Creates a gradient across a rectangle at an angle.</summary>
    /// <param name="rect">The rectangle the gradient spans.</param>
    /// <param name="color1">The starting color.</param>
    /// <param name="color2">The ending color.</param>
    /// <param name="angle">The angle in degrees, clockwise from the x-axis.</param>
    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle)
        : this((RectangleF)rect, color1, color2, angle)
    {
    }

    /// <summary>Creates a gradient across a rectangle at an angle.</summary>
    /// <param name="rect">The rectangle the gradient spans.</param>
    /// <param name="color1">The starting color.</param>
    /// <param name="color2">The ending color.</param>
    /// <param name="angle">The angle in degrees, clockwise from the x-axis.</param>
    /// <param name="isAngleScaleable">Ignored: the angle is used as given.</param>
    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle, bool isAngleScaleable)
        : this(rect, color1, color2, angle)
    {
    }

    /// <summary>Creates a gradient across a rectangle at an angle.</summary>
    /// <param name="rect">The rectangle the gradient spans.</param>
    /// <param name="color1">The starting color.</param>
    /// <param name="color2">The ending color.</param>
    /// <param name="angle">The angle in degrees, clockwise from the x-axis.</param>
    /// <param name="isAngleScaleable">Ignored: the angle is used as given.</param>
    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle, bool isAngleScaleable)
        : this((RectangleF)rect, color1, color2, angle)
    {
    }

    /// <summary>The starting and ending colors.</summary>
    public Color[] LinearColors { get; set; }

    /// <summary>The rectangle the gradient spans.</summary>
    public RectangleF Rectangle { get; }

    /// <summary>How the gradient repeats beyond its rectangle. Drawn as <see cref="WrapMode.Clamp"/> on this device.</summary>
    public WrapMode WrapMode { get; set; }

    /// <summary>Whether gamma correction is applied. Ignored on this device.</summary>
    public bool GammaCorrection { get; set; }

    /// <inheritdoc />
    public override object Clone() => new LinearGradientBrush(_start, _end, LinearColors[0], LinearColors[^1]) { WrapMode = WrapMode };

    internal override BrushSpec ToSpec() =>
        new LinearGradientBrushSpec(_start, _end, (uint)LinearColors[0].ToArgb(), (uint)LinearColors[^1].ToArgb());
}
