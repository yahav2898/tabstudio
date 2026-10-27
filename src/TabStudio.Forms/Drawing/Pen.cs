using System.Drawing.Drawing2D;
using System.Numerics;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>What lines and outlines are drawn with: a brush, a width, dashes, caps and joins.</summary>
public sealed class Pen : MarshalByRefObject, ICloneable, IDisposable
{
    private readonly bool _immutable;
    private Brush _brush;
    private float _width;
    private DashStyle _dashStyle;
    private float[]? _dashPattern;
    private float _dashOffset;
    private DashCap _dashCap;
    private LineCap _startCap;
    private LineCap _endCap;
    private LineJoin _lineJoin;
    private float _miterLimit = 10;
    private PenAlignment _alignment;
    private CustomLineCap? _customStartCap;
    private CustomLineCap? _customEndCap;
    private float[] _compoundArray = [];
    private Matrix3x2 _transform = Matrix3x2.Identity;

    /// <summary>Creates a pen of a color, one pixel wide.</summary>
    /// <param name="color">The color.</param>
    public Pen(Color color)
        : this(color, 1f)
    {
    }

    /// <summary>Creates a pen of a color and width.</summary>
    /// <param name="color">The color.</param>
    /// <param name="width">The width, in world units.</param>
    public Pen(Color color, float width)
        : this(new SolidBrush(color), width)
    {
    }

    /// <summary>Creates a pen of a brush, one pixel wide.</summary>
    /// <param name="brush">The brush.</param>
    public Pen(Brush brush)
        : this(brush, 1f)
    {
    }

    /// <summary>Creates a pen of a brush and width.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="width">The width, in world units.</param>
    public Pen(Brush brush, float width)
    {
        ArgumentNullException.ThrowIfNull(brush);
        _brush = brush;
        _width = width;
    }

    internal Pen(Color color, float width, bool immutable)
        : this(color, width) => _immutable = immutable;

    /// <summary>The color, for a pen of a solid color.</summary>
    public Color Color
    {
        get => _brush is SolidBrush solid ? solid.Color : Color.FromArgb((int)_brush.ToSpec().MainColor);
        set => Change(() => _brush = new SolidBrush(value));
    }

    /// <summary>The brush the pen paints with.</summary>
    public Brush Brush
    {
        get => _brush;
        set => Change(() => _brush = value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>The kind of brush: solid, hatch, texture, path gradient or linear gradient.</summary>
    public PenType PenType => _brush switch
    {
        HatchBrush => PenType.HatchFill,
        TextureBrush => PenType.TextureFill,
        PathGradientBrush => PenType.PathGradient,
        LinearGradientBrush => PenType.LinearGradient,
        _ => PenType.SolidColor,
    };

    /// <summary>The width, in world units.</summary>
    public float Width
    {
        get => _width;
        set => Change(() => _width = value);
    }

    /// <summary>The dash style.</summary>
    public DashStyle DashStyle
    {
        get => _dashStyle;
        set => Change(() => _dashStyle = value);
    }

    /// <summary>The dashes and gaps of a custom dash style, in multiples of the width.</summary>
#pragma warning disable CA1819 // GDI+'s own shape.
    public float[] DashPattern
    {
        get => _dashPattern ?? [];
        set => Change(() =>
        {
            _dashPattern = value is { Length: > 0 } && value.All(v => v > 0) ? [.. value] : throw new ArgumentException("Parameter is not valid: dash lengths must be positive.", nameof(value));
            _dashStyle = DashStyle.Custom;
        });
    }

    /// <summary>The widths of parallel lines a compound pen draws. Kept for compatibility: one line is drawn.</summary>
    public float[] CompoundArray
    {
        get => [.. _compoundArray];
        set => Change(() => _compoundArray = value ?? throw new ArgumentNullException(nameof(value)));
    }
#pragma warning restore CA1819

    /// <summary>How far into the dash pattern lines start, in multiples of the width.</summary>
    public float DashOffset
    {
        get => _dashOffset;
        set => Change(() => _dashOffset = value);
    }

    /// <summary>The caps of the dashes.</summary>
    public DashCap DashCap
    {
        get => _dashCap;
        set => Change(() => _dashCap = value);
    }

    /// <summary>The cap at the start of lines.</summary>
    public LineCap StartCap
    {
        get => _startCap;
        set => Change(() => _startCap = value);
    }

    /// <summary>The cap at the end of lines.</summary>
    public LineCap EndCap
    {
        get => _endCap;
        set => Change(() => _endCap = value);
    }

    /// <summary>A cap of the app's own at the start of lines, such as an <see cref="AdjustableArrowCap"/>.</summary>
    public CustomLineCap? CustomStartCap
    {
        get => _customStartCap;
        set => Change(() =>
        {
            _customStartCap = value;
            _startCap = LineCap.Custom;
        });
    }

    /// <summary>A cap of the app's own at the end of lines, such as an <see cref="AdjustableArrowCap"/>.</summary>
    public CustomLineCap? CustomEndCap
    {
        get => _customEndCap;
        set => Change(() =>
        {
            _customEndCap = value;
            _endCap = LineCap.Custom;
        });
    }

    /// <summary>How segments are joined.</summary>
    public LineJoin LineJoin
    {
        get => _lineJoin;
        set => Change(() => _lineJoin = value);
    }

    /// <summary>How far a mitered join may reach, in multiples of the width.</summary>
    public float MiterLimit
    {
        get => _miterLimit;
        set => Change(() => _miterLimit = value);
    }

    /// <summary>Where the line is drawn relative to an outline: centered on it, or inside it.</summary>
    public PenAlignment Alignment
    {
        get => _alignment;
        set => Change(() => _alignment = value);
    }

    /// <summary>The pen's own transformation. Kept for compatibility: the width is scaled by the surface's transformation.</summary>
    public Matrix Transform
    {
        get => new(_transform);
        set => Change(() => _transform = (value ?? throw new ArgumentNullException(nameof(value))).Value);
    }

    /// <summary>Sets the caps of lines and dashes.</summary>
    /// <param name="startCap">The cap at the start.</param>
    /// <param name="endCap">The cap at the end.</param>
    /// <param name="dashCap">The caps of dashes.</param>
    public void SetLineCap(LineCap startCap, LineCap endCap, DashCap dashCap)
    {
        StartCap = startCap;
        EndCap = endCap;
        DashCap = dashCap;
    }

    /// <summary>Resets the pen's transformation.</summary>
    public void ResetTransform() => Change(() => _transform = Matrix3x2.Identity);

    /// <summary>Scales the pen's transformation.</summary>
    /// <param name="sx">The horizontal factor.</param>
    /// <param name="sy">The vertical factor.</param>
    public void ScaleTransform(float sx, float sy) => Change(() => _transform = Matrix3x2.CreateScale(sx, sy) * _transform);

    /// <summary>Rotates the pen's transformation.</summary>
    /// <param name="angle">The angle in degrees.</param>
    public void RotateTransform(float angle) => Change(() => _transform = Matrix3x2.CreateRotation(angle * MathF.PI / 180) * _transform);

    /// <summary>Translates the pen's transformation.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    public void TranslateTransform(float dx, float dy) => Change(() => _transform = Matrix3x2.CreateTranslation(dx, dy) * _transform);

    /// <summary>Multiplies the pen's transformation.</summary>
    /// <param name="matrix">The matrix.</param>
    public void MultiplyTransform(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        Change(() => _transform = matrix.Value * _transform);
    }

    /// <inheritdoc />
    public object Clone() => new Pen((Brush)_brush.Clone(), _width)
    {
        _dashStyle = _dashStyle,
        _dashPattern = _dashPattern,
        _dashOffset = _dashOffset,
        _dashCap = _dashCap,
        _startCap = _startCap,
        _endCap = _endCap,
        _lineJoin = _lineJoin,
        _miterLimit = _miterLimit,
        _alignment = _alignment,
        _customStartCap = _customStartCap,
        _customEndCap = _customEndCap,
        _compoundArray = _compoundArray,
        _transform = _transform,
    };

    /// <inheritdoc />
    public void Dispose()
    {
        // A pen holds nothing to release.
    }

    /// <summary>Whether a line's ends need cap shapes drawn as geometry, rather than the stroke's own caps.</summary>
    internal bool HasShapedCaps => !(_startCap == _endCap && _startCap is LineCap.Flat or LineCap.Square or LineCap.Round);

    /// <summary>The pen as a display list records it.</summary>
    internal PenSpec ToSpec() => new(
        _brush.ToSpec(),
        _width,
        _dashStyle,
        _dashPattern,
        HasShapedCaps ? LineCap.Flat : _startCap,
        _lineJoin,
        _dashOffset,
        _dashCap,
        _miterLimit);

    private void Change(Action change)
    {
        if (_immutable)
        {
            throw new ArgumentException("Changes cannot be made to Pen because permissions are not valid.");
        }

        change();
    }
}
