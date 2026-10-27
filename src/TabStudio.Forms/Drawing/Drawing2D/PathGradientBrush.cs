using System.Numerics;
using TabStudio.Forms.Rendering;

namespace System.Drawing.Drawing2D;

/// <summary>
/// A brush that blends from a center color out to the colors of a path's edge, painting only inside the path, as
/// GDI+ does: along each line from the center point to the edge.
/// </summary>
public sealed class PathGradientBrush : Brush
{
    private readonly PathData _path;
    private Color[] _surroundColors = [Color.White];
    private Blend? _blend;
    private ColorBlend? _interpolation;
    private Matrix3x2 _transform = Matrix3x2.Identity;

    /// <summary>Creates a brush filling a path.</summary>
    /// <param name="path">The path.</param>
    public PathGradientBrush(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _path = path.ToData() with { FillMode = FillMode.Winding };
        Rectangle = path.GetBounds();
        CenterPoint = new PointF(Rectangle.X + (Rectangle.Width / 2), Rectangle.Y + (Rectangle.Height / 2));
    }

    /// <summary>Creates a brush filling a polygon.</summary>
    /// <param name="points">The polygon's points.</param>
    public PathGradientBrush(PointF[] points)
        : this(Polygon(points))
    {
    }

    /// <summary>Creates a brush filling a polygon.</summary>
    /// <param name="points">The polygon's points.</param>
    public PathGradientBrush(Point[] points)
        : this(Polygon([.. (points ?? throw new ArgumentNullException(nameof(points))).Select(p => (PointF)p)]))
    {
    }

    /// <summary>Creates a brush filling a polygon.</summary>
    /// <param name="points">The polygon's points.</param>
    /// <param name="wrapMode">How the gradient repeats.</param>
    public PathGradientBrush(PointF[] points, WrapMode wrapMode)
        : this(points) => WrapMode = wrapMode;

    /// <summary>Creates a brush filling a polygon.</summary>
    /// <param name="points">The polygon's points.</param>
    /// <param name="wrapMode">How the gradient repeats.</param>
    public PathGradientBrush(Point[] points, WrapMode wrapMode)
        : this(points) => WrapMode = wrapMode;

    private PathGradientBrush(PathGradientBrush other)
    {
        _path = other._path;
        Rectangle = other.Rectangle;
        CenterPoint = other.CenterPoint;
        CenterColor = other.CenterColor;
        _surroundColors = [.. other._surroundColors];
        _blend = other._blend;
        _interpolation = other._interpolation;
        _transform = other._transform;
        FocusScales = other.FocusScales;
        WrapMode = other.WrapMode;
    }

    /// <summary>The color at the center point.</summary>
    public Color CenterColor { get; set; } = Color.White;

    /// <summary>The colors of the edge: one for all of it, or one per point of a polygon.</summary>
#pragma warning disable CA1819 // GDI+'s own shape.
    public Color[] SurroundColors
    {
        get => [.. _surroundColors];
        set => _surroundColors = value is { Length: > 0 } ? [.. value] : throw new ArgumentException("Parameter is not valid.", nameof(value));
    }
#pragma warning restore CA1819

    /// <summary>Where the center color is.</summary>
    public PointF CenterPoint { get; set; }

    /// <summary>The bounds of the path.</summary>
    public RectangleF Rectangle { get; }

    /// <summary>The size of the area filled with the center color, as fractions of the path.</summary>
    public PointF FocusScales { get; set; }

    /// <summary>How the gradient repeats. Kept for compatibility: the gradient is drawn inside its path.</summary>
    public WrapMode WrapMode { get; set; } = WrapMode.Clamp;

    /// <summary>How the colors blend from the edge (0) to the center (1).</summary>
    public Blend Blend
    {
        get => _blend ?? new Blend(1) { Factors = [1], Positions = [0] };
        set
        {
            _blend = value ?? throw new ArgumentNullException(nameof(value));
            _interpolation = null;
        }
    }

    /// <summary>The colors from the edge (position 0) to the center (position 1), for more than two colors.</summary>
    public ColorBlend InterpolationColors
    {
        get => _interpolation ?? new ColorBlend(1);
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

    /// <summary>Blends with a bell-shaped curve.</summary>
    /// <param name="focus">Where the center color is strongest, from 0 to 1.</param>
    public void SetSigmaBellShape(float focus) => SetSigmaBellShape(focus, 1);

    /// <summary>Blends with a bell-shaped curve.</summary>
    /// <param name="focus">Where the center color is strongest, from 0 to 1.</param>
    /// <param name="scale">How strong it is there, from 0 to 1.</param>
    public void SetSigmaBellShape(float focus, float scale) => Blend = Blend.SigmaBell(focus, scale);

    /// <summary>Blends linearly.</summary>
    /// <param name="focus">Where the center color is strongest, from 0 to 1.</param>
    public void SetBlendTriangularShape(float focus) => SetBlendTriangularShape(focus, 1);

    /// <summary>Blends linearly.</summary>
    /// <param name="focus">Where the center color is strongest, from 0 to 1.</param>
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
    public override object Clone() => new PathGradientBrush(this);

    internal override BrushSpec ToSpec()
    {
        GradientStopSpec[] stops;
        var edge = _surroundColors[0];
        if (_interpolation is { Colors.Length: >= 2 } interpolation)
        {
            stops = [.. interpolation.Colors.Select((color, i) => new GradientStopSpec(interpolation.Positions.Length > i ? interpolation.Positions[i] : i / (float)(interpolation.Colors.Length - 1), SystemColorValues.Argb(color)))];
        }
        else if (_blend is { Factors.Length: > 0 } blend)
        {
            stops = [.. blend.Factors.Select((factor, i) => new GradientStopSpec(blend.Positions.Length > i ? blend.Positions[i] : i / (float)Math.Max(1, blend.Factors.Length - 1), Blend.Mix(edge, CenterColor, factor)))];
        }
        else
        {
            stops = [new GradientStopSpec(0, SystemColorValues.Argb(edge)), new GradientStopSpec(1, SystemColorValues.Argb(CenterColor))];
        }

        // The focus scales fill a smaller copy of the path with the center color.
        var focus = Math.Clamp((FocusScales.X + FocusScales.Y) / 2, 0, 0.99f);
        if (focus > 0)
        {
            stops = [.. stops.Select(stop => stop with { Offset = stop.Offset * (1 - focus) }), new GradientStopSpec(1, stops[^1].Argb)];
        }

        return new PathGradientBrushSpec(Rectangle, _path, CenterPoint, stops, _transform)
        {
            SurroundArgb = _surroundColors.Length > 1 && _interpolation is null && _blend is null && focus == 0
                ? [.. _surroundColors.Select(SystemColorValues.Argb)]
                : null,
        };
    }

    private static GraphicsPath Polygon(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var path = new GraphicsPath();
        path.AddPolygon(points);
        return path;
    }
}
