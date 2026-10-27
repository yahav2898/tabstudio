namespace System.Drawing.Drawing2D;

/// <summary>An arrowhead cap, with a width and height in multiples of the pen's width, filled by default.</summary>
public sealed class AdjustableArrowCap : CustomLineCap
{
    private float _width;
    private float _height;
    private float _middleInset;
    private bool _filled;

    /// <summary>Creates a filled arrowhead.</summary>
    /// <param name="width">The width of its base, in multiples of the pen's width.</param>
    /// <param name="height">Its length, in multiples of the pen's width.</param>
    public AdjustableArrowCap(float width, float height)
        : this(width, height, isFilled: true)
    {
    }

    /// <summary>Creates an arrowhead.</summary>
    /// <param name="width">The width of its base, in multiples of the pen's width.</param>
    /// <param name="height">Its length, in multiples of the pen's width.</param>
    /// <param name="isFilled">Whether it is filled, or drawn as two lines.</param>
    public AdjustableArrowCap(float width, float height, bool isFilled)
    {
        _width = width;
        _height = height;
        _filled = isFilled;
        Update();
    }

    /// <summary>The width of the base, in multiples of the pen's width.</summary>
    public float Width
    {
        get => _width;
        set
        {
            _width = value;
            Update();
        }
    }

    /// <summary>The length, in multiples of the pen's width.</summary>
    public float Height
    {
        get => _height;
        set
        {
            _height = value;
            Update();
        }
    }

    /// <summary>How far the base is pushed toward the tip, in multiples of the pen's width.</summary>
    public float MiddleInset
    {
        get => _middleInset;
        set
        {
            _middleInset = value;
            Update();
        }
    }

    /// <summary>Whether the arrowhead is filled.</summary>
    public bool Filled
    {
        get => _filled;
        set
        {
            _filled = value;
            Update();
        }
    }

    private void Update()
    {
        // The line ends at the origin and points up the negative y-axis; the tip is at the origin.
        var path = new GraphicsPath();
        var half = _width / 2;
        if (_filled)
        {
            path.AddPolygon([new PointF(0, 0), new PointF(-half, -_height), new PointF(0, -_height + _middleInset), new PointF(half, -_height)]);
            FillPath = path;
            StrokePath = null;
            BaseInset = _height;
        }
        else
        {
            path.AddLines([new PointF(-half, -_height), new PointF(0, 0), new PointF(half, -_height)]);
            StrokePath = path;
            FillPath = null;
            BaseInset = 0;
        }
    }
}
