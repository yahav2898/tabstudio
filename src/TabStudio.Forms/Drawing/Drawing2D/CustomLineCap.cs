namespace System.Drawing.Drawing2D;

/// <summary>
/// A line cap of an app's own, made of a path. The path is in a frame where the line ends at the origin and goes
/// up the negative y-axis, in multiples of the pen's width, as GDI+ defines it.
/// </summary>
public class CustomLineCap : MarshalByRefObject, ICloneable, IDisposable
{
    /// <summary>Creates a cap of a fill path, a stroke path, or both.</summary>
    /// <param name="fillPath">The filled shape, or <see langword="null"/>.</param>
    /// <param name="strokePath">The outlined shape, or <see langword="null"/>.</param>
    public CustomLineCap(GraphicsPath? fillPath, GraphicsPath? strokePath)
        : this(fillPath, strokePath, LineCap.Flat, 0)
    {
    }

    /// <summary>Creates a cap of paths, over a base cap.</summary>
    /// <param name="fillPath">The filled shape, or <see langword="null"/>.</param>
    /// <param name="strokePath">The outlined shape, or <see langword="null"/>.</param>
    /// <param name="baseCap">The cap under the shape.</param>
    public CustomLineCap(GraphicsPath? fillPath, GraphicsPath? strokePath, LineCap baseCap)
        : this(fillPath, strokePath, baseCap, 0)
    {
    }

    /// <summary>Creates a cap of paths, over a base cap, with the line shortened.</summary>
    /// <param name="fillPath">The filled shape, or <see langword="null"/>.</param>
    /// <param name="strokePath">The outlined shape, or <see langword="null"/>.</param>
    /// <param name="baseCap">The cap under the shape.</param>
    /// <param name="baseInset">How much shorter the line is, in multiples of the width.</param>
    public CustomLineCap(GraphicsPath? fillPath, GraphicsPath? strokePath, LineCap baseCap, float baseInset)
    {
        FillPath = (GraphicsPath?)fillPath?.Clone();
        StrokePath = (GraphicsPath?)strokePath?.Clone();
        BaseCap = baseCap;
        BaseInset = baseInset;
    }

    private protected CustomLineCap()
    {
    }

    /// <summary>The cap under the shape.</summary>
    public LineCap BaseCap { get; set; }

    /// <summary>How much shorter the line is, in multiples of the width.</summary>
    public float BaseInset { get; set; }

    /// <summary>The scale of the shape, times the pen's width.</summary>
    public float WidthScale { get; set; } = 1;

    /// <summary>The cap's join style.</summary>
    public LineJoin StrokeJoin { get; set; }

    /// <summary>The filled shape.</summary>
    internal GraphicsPath? FillPath { get; private protected set; }

    /// <summary>The outlined shape.</summary>
    internal GraphicsPath? StrokePath { get; private protected set; }

    /// <summary>Sets the caps of the shape's own strokes. Kept for compatibility.</summary>
    /// <param name="startCap">The start cap.</param>
    /// <param name="endCap">The end cap.</param>
    public void SetStrokeCaps(LineCap startCap, LineCap endCap)
    {
        // The shape's strokes use flat caps.
    }

    /// <summary>Gets the caps of the shape's own strokes.</summary>
    /// <param name="startCap">The start cap.</param>
    /// <param name="endCap">The end cap.</param>
    public void GetStrokeCaps(out LineCap startCap, out LineCap endCap)
    {
        startCap = LineCap.Flat;
        endCap = LineCap.Flat;
    }

    /// <inheritdoc />
    public object Clone() => MemberwiseClone();

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the cap.</summary>
    /// <param name="disposing">Whether this is called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
