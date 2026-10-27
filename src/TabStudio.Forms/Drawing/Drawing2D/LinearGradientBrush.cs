using System.Numerics;
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

/// <summary>A brush that blends colors along a line.</summary>
public sealed class LinearGradientBrush : Brush
{
    private readonly PointF _start;
    private readonly PointF _end;
    private Color[] _colors;
    private Blend? _blend;
    private ColorBlend? _interpolation;
    private WrapMode _wrapMode = WrapMode.Tile;
    private Matrix3x2 _transform = Matrix3x2.Identity;

    /// <summary>Creates a gradient between two points.</summary>
    /// <param name="point1">Where the first color is.</param>
    /// <param name="point2">Where the second color is.</param>
    /// <param name="color1">The first color.</param>
    /// <param name="color2">The second color.</param>
    public LinearGradientBrush(PointF point1, PointF point2, Color color1, Color color2)
    {
        _start = point1;
        _end = point2;
        _colors = [color1, color2];
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
    /// <param name="rect">The rectangle.</param>
    /// <param name="color1">The first color.</param>
    /// <param name="color2">The second color.</param>
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
    /// <param name="rect">The rectangle.</param>
    /// <param name="color1">The first color.</param>
    /// <param name="color2">The second color.</param>
    /// <param name="linearGradientMode">The direction.</param>
    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
        : this((RectangleF)rect, color1, color2, linearGradientMode)
    {
    }

    /// <summary>Creates a gradient across a rectangle at an angle, clockwise from the x-axis.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="color1">The first color.</param>
    /// <param name="color2">The second color.</param>
    /// <param name="angle">The angle in degrees.</param>
    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle)
    {
        if (rect.Width == 0 || rect.Height == 0)
        {
            throw new ArgumentException($"Rectangle '{rect}' cannot have a width or height equal to 0.", nameof(rect));
        }

        Rectangle = rect;
        _colors = [color1, color2];
        var radians = angle * Math.PI / 180;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        var half = ((rect.Width * Math.Abs(cos)) + (rect.Height * Math.Abs(sin))) / 2;
        var center = new PointF(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        _start = new PointF((float)(center.X - (cos * half)), (float)(center.Y - (sin * half)));
        _end = new PointF((float)(center.X + (cos * half)), (float)(center.Y + (sin * half)));
    }

    /// <summary>Creates a gradient across a rectangle at an angle.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="color1">The first color.</param>
    /// <param name="color2">The second color.</param>
    /// <param name="angle">The angle in degrees.</param>
    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle)
        : this((RectangleF)rect, color1, color2, angle)
    {
    }

    /// <summary>Creates a gradient across a rectangle at an angle.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="color1">The first color.</param>
    /// <param name="color2">The second color.</param>
    /// <param name="angle">The angle in degrees.</param>
    /// <param name="isAngleScaleable">Ignored.</param>
    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle, bool isAngleScaleable)
        : this(rect, color1, color2, angle)
    {
    }

    /// <summary>Creates a gradient across a rectangle at an angle.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="color1">The first color.</param>
    /// <param name="color2">The second color.</param>
    /// <param name="angle">The angle in degrees.</param>
    /// <param name="isAngleScaleable">Ignored.</param>
    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle, bool isAngleScaleable)
        : this((RectangleF)rect, color1, color2, angle)
    {
    }

    private LinearGradientBrush(LinearGradientBrush other)
    {
        _start = other._start;
        _end = other._end;
        _colors = [.. other._colors];
        _blend = other._blend;
        _interpolation = other._interpolation;
        _wrapMode = other._wrapMode;
        _transform = other._transform;
        Rectangle = other.Rectangle;
        GammaCorrection = other.GammaCorrection;
    }

    /// <summary>The start and end colors.</summary>
#pragma warning disable CA1819 // GDI+'s own shape.
    public Color[] LinearColors
    {
        get => [.. _colors];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _colors = value.Length >= 2 ? [value[0], value[1]] : throw new ArgumentException("Two colors are required.", nameof(value));
        }
    }
#pragma warning restore CA1819

    /// <summary>The rectangle the gradient spans.</summary>
    public RectangleF Rectangle { get; }

    /// <summary>How the gradient repeats beyond its two points. <see cref="WrapMode.Clamp"/> is not allowed, as in GDI+.</summary>
    public WrapMode WrapMode
    {
        get => _wrapMode;
        set => _wrapMode = value == WrapMode.Clamp ? throw new ArgumentException("Parameter is not valid: a linear gradient cannot clamp.", nameof(value)) : value;
    }

    /// <summary>Whether gamma correction is applied. Kept for compatibility.</summary>
    public bool GammaCorrection { get; set; }

    /// <summary>How the two colors blend along the gradient: factors of the second color at positions.</summary>
    public Blend Blend
    {
        get => _blend ?? new Blend(1) { Factors = [1], Positions = [0] };
        set
        {
            _blend = value ?? throw new ArgumentNullException(nameof(value));
            _interpolation = null;
        }
    }

    /// <summary>The colors of a gradient of more than two colors, with their positions.</summary>
    public ColorBlend InterpolationColors
    {
        get => _interpolation ?? throw new ArgumentException("Property must be set to a valid ColorBlend object to use interpolation colors.");
        set
        {
            _interpolation = value ?? throw new ArgumentNullException(nameof(value));
            _blend = null;
        }
    }

    /// <summary>The brush's own transformation.</summary>
    public Matrix Transform
    {
        get => new(_transform);
        set => _transform = (value ?? throw new ArgumentNullException(nameof(value))).Value;
    }

    /// <summary>Blends with a bell-shaped curve centered at a position.</summary>
    /// <param name="focus">Where the second color is strongest, from 0 to 1.</param>
    public void SetSigmaBellShape(float focus) => SetSigmaBellShape(focus, 1);

    /// <summary>Blends with a bell-shaped curve centered at a position.</summary>
    /// <param name="focus">Where the second color is strongest, from 0 to 1.</param>
    /// <param name="scale">How strong it is there, from 0 to 1.</param>
    public void SetSigmaBellShape(float focus, float scale) => Blend = Blend.SigmaBell(focus, scale);

    /// <summary>Blends linearly from the first color to the second at a position and back.</summary>
    /// <param name="focus">Where the second color is strongest, from 0 to 1.</param>
    public void SetBlendTriangularShape(float focus) => SetBlendTriangularShape(focus, 1);

    /// <summary>Blends linearly from the first color to the second at a position and back.</summary>
    /// <param name="focus">Where the second color is strongest, from 0 to 1.</param>
    /// <param name="scale">How strong it is there, from 0 to 1.</param>
    public void SetBlendTriangularShape(float focus, float scale) => Blend = Blend.Triangle(focus, scale);

    /// <summary>Resets the brush's transformation.</summary>
    public void ResetTransform() => _transform = Matrix3x2.Identity;

    /// <summary>Multiplies the brush's transformation.</summary>
    /// <param name="matrix">The matrix.</param>
    public void MultiplyTransform(Matrix matrix) => MultiplyTransform(matrix, MatrixOrder.Prepend);

    /// <summary>Multiplies the brush's transformation.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="order">The order.</param>
    public void MultiplyTransform(Matrix matrix, MatrixOrder order) =>
        _transform = BrushTransforms.Apply(_transform, (matrix ?? throw new ArgumentNullException(nameof(matrix))).Value, order);

    /// <summary>Translates the brush's transformation.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    public void TranslateTransform(float dx, float dy) => TranslateTransform(dx, dy, MatrixOrder.Prepend);

    /// <summary>Translates the brush's transformation.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    /// <param name="order">The order.</param>
    public void TranslateTransform(float dx, float dy, MatrixOrder order) => _transform = BrushTransforms.Apply(_transform, Matrix3x2.CreateTranslation(dx, dy), order);

    /// <summary>Scales the brush's transformation.</summary>
    /// <param name="sx">The horizontal factor.</param>
    /// <param name="sy">The vertical factor.</param>
    public void ScaleTransform(float sx, float sy) => ScaleTransform(sx, sy, MatrixOrder.Prepend);

    /// <summary>Scales the brush's transformation.</summary>
    /// <param name="sx">The horizontal factor.</param>
    /// <param name="sy">The vertical factor.</param>
    /// <param name="order">The order.</param>
    public void ScaleTransform(float sx, float sy, MatrixOrder order) => _transform = BrushTransforms.Apply(_transform, Matrix3x2.CreateScale(sx, sy), order);

    /// <summary>Rotates the brush's transformation.</summary>
    /// <param name="angle">The angle in degrees.</param>
    public void RotateTransform(float angle) => RotateTransform(angle, MatrixOrder.Prepend);

    /// <summary>Rotates the brush's transformation.</summary>
    /// <param name="angle">The angle in degrees.</param>
    /// <param name="order">The order.</param>
    public void RotateTransform(float angle, MatrixOrder order) => _transform = BrushTransforms.Apply(_transform, Matrix3x2.CreateRotation(angle * MathF.PI / 180), order);

    /// <inheritdoc />
    public override object Clone() => new LinearGradientBrush(this);

    internal override BrushSpec ToSpec() => new LinearGradientBrushSpec(_start, _end, Stops(), _wrapMode, _transform);

    private GradientStopSpec[] Stops()
    {
        if (_interpolation is { Colors.Length: >= 2 } interpolation)
        {
            return [.. interpolation.Colors.Select((color, i) => new GradientStopSpec(interpolation.Positions.Length > i ? interpolation.Positions[i] : i / (float)(interpolation.Colors.Length - 1), SystemColorValues.Argb(color)))];
        }

        var (first, second) = (_colors[0], _colors[1]);
        if (_blend is { Factors.Length: > 0 } blend)
        {
            return [.. blend.Factors.Select((factor, i) => new GradientStopSpec(blend.Positions.Length > i ? blend.Positions[i] : i / (float)Math.Max(1, blend.Factors.Length - 1), Blend.Mix(first, second, factor)))];
        }

        return [new GradientStopSpec(0, SystemColorValues.Argb(first)), new GradientStopSpec(1, SystemColorValues.Argb(second))];
    }
}
