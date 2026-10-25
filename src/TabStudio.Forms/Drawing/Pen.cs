using System.Drawing.Drawing2D;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>Draws lines and the outlines of shapes.</summary>
public sealed class Pen : MarshalByRefObject, ICloneable, IDisposable
{
    private readonly bool _immutable;
    private Brush _brush;
    private float _width;
    private DashStyle _dashStyle;
    private float[]? _dashPattern;
    private LineCap _startCap;
    private LineCap _endCap;
    private LineJoin _lineJoin;

    /// <summary>Creates a one-pixel pen of a color.</summary>
    /// <param name="color">The color.</param>
    public Pen(Color color)
        : this(color, 1f)
    {
    }

    /// <summary>Creates a pen of a color and width.</summary>
    /// <param name="color">The color.</param>
    /// <param name="width">The width.</param>
    public Pen(Color color, float width)
        : this(new SolidBrush(color), width)
    {
    }

    /// <summary>Creates a one-pixel pen that draws with a brush.</summary>
    /// <param name="brush">The brush.</param>
    public Pen(Brush brush)
        : this(brush, 1f)
    {
    }

    /// <summary>Creates a pen that draws with a brush.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="width">The width.</param>
    public Pen(Brush brush, float width)
    {
        ArgumentNullException.ThrowIfNull(brush);
        _brush = brush;
        _width = width;
    }

    internal Pen(Color color, float width, bool immutable)
        : this(color, width) => _immutable = immutable;

    /// <summary>The color, for a pen of one color.</summary>
    public Color Color
    {
        get => _brush is SolidBrush solid ? solid.Color : Color.FromArgb((int)_brush.ToSpec().MainColor);
        set => Change(() => _brush = new SolidBrush(value));
    }

    /// <summary>The brush the pen draws with.</summary>
    public Brush Brush
    {
        get => _brush;
        set => Change(() => _brush = value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>The width.</summary>
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
    public float[] DashPattern
    {
        get => _dashPattern ?? [];
        set => Change(() =>
        {
            _dashPattern = value;
            _dashStyle = DashStyle.Custom;
        });
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

    /// <summary>How segments are joined.</summary>
    public LineJoin LineJoin
    {
        get => _lineJoin;
        set => Change(() => _lineJoin = value);
    }

    /// <summary>Sets the caps of both ends.</summary>
    /// <param name="startCap">The start cap.</param>
    /// <param name="endCap">The end cap.</param>
    /// <param name="dashCap">The cap of dashes; ignored.</param>
    public void SetLineCap(LineCap startCap, LineCap endCap, DashCap dashCap)
    {
        StartCap = startCap;
        EndCap = endCap;
    }

    /// <inheritdoc />
    public object Clone() => new Pen((Brush)_brush.Clone(), _width)
    {
        _dashStyle = _dashStyle,
        _dashPattern = _dashPattern,
        _startCap = _startCap,
        _endCap = _endCap,
        _lineJoin = _lineJoin,
    };

    /// <inheritdoc />
    public void Dispose()
    {
        // A pen holds nothing to release.
    }

    /// <summary>The pen as a display list records it.</summary>
    internal PenSpec ToSpec() => new(_brush.ToSpec(), _width, _dashStyle, _dashPattern, _startCap == LineCap.Round || _endCap == LineCap.Round ? LineCap.Round : _startCap, _lineJoin);

    private void Change(Action change)
    {
        if (_immutable)
        {
            throw new ArgumentException("Changes cannot be made to Pen because permissions are not valid.");
        }

        change();
    }
}
