using System.Drawing.Drawing2D;

namespace System.Drawing;

/// <summary>
/// An area of a drawing surface. On this device a region is used for clipping and hit testing; it clips to its
/// bounding rectangle.
/// </summary>
public sealed class Region : MarshalByRefObject, IDisposable
{
    private GraphicsPath? _path;

    /// <summary>Creates an infinite region.</summary>
    public Region() => Bounds = new RectangleF(-4194304, -4194304, 8388608, 8388608);

    /// <summary>Creates a rectangular region.</summary>
    /// <param name="rect">The rectangle.</param>
    public Region(RectangleF rect) => Bounds = rect;

    /// <summary>Creates a rectangular region.</summary>
    /// <param name="rect">The rectangle.</param>
    public Region(Rectangle rect) => Bounds = rect;

    /// <summary>Creates a region from the interior of a path.</summary>
    /// <param name="path">The path.</param>
    public Region(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _path = (GraphicsPath)path.Clone();
        Bounds = path.GetBounds();
    }

    /// <summary>The bounding rectangle.</summary>
    internal RectangleF Bounds { get; private set; }

    /// <summary>The bounding rectangle on a drawing surface.</summary>
    /// <param name="g">The surface.</param>
    public RectangleF GetBounds(Graphics g) => Bounds;

    /// <summary>Whether the region is infinite.</summary>
    /// <param name="g">The surface.</param>
    public bool IsInfinite(Graphics g) => Bounds.Width >= 8388608;

    /// <summary>Whether the region is empty.</summary>
    /// <param name="g">The surface.</param>
    public bool IsEmpty(Graphics g) => Bounds.Width <= 0 || Bounds.Height <= 0;

    /// <summary>Whether a point is in the region.</summary>
    /// <param name="point">The point.</param>
    public bool IsVisible(PointF point) => Bounds.Contains(point) && (_path is null || _path.IsVisible(point));

    /// <summary>Whether a point is in the region.</summary>
    /// <param name="point">The point.</param>
    public bool IsVisible(Point point) => IsVisible((PointF)point);

    /// <summary>Whether a point is in the region.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    public bool IsVisible(float x, float y) => IsVisible(new PointF(x, y));

    /// <summary>Whether a rectangle overlaps the region.</summary>
    /// <param name="rect">The rectangle.</param>
    public bool IsVisible(RectangleF rect) => Bounds.IntersectsWith(rect);

    /// <summary>Whether a rectangle overlaps the region.</summary>
    /// <param name="rect">The rectangle.</param>
    public bool IsVisible(Rectangle rect) => Bounds.IntersectsWith(rect);

    /// <summary>Limits the region to a rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Intersect(RectangleF rect)
    {
        Bounds = RectangleF.Intersect(Bounds, rect);
        _path = null;
    }

    /// <summary>Limits the region to a rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Intersect(Rectangle rect) => Intersect((RectangleF)rect);

    /// <summary>Limits the region to another region.</summary>
    /// <param name="region">The other region.</param>
    public void Intersect(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        Intersect(region.Bounds);
    }

    /// <summary>Extends the region to include a rectangle, as their bounding rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Union(RectangleF rect)
    {
        Bounds = RectangleF.Union(Bounds, rect);
        _path = null;
    }

    /// <summary>Extends the region to include a rectangle, as their bounding rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Union(Rectangle rect) => Union((RectangleF)rect);

    /// <summary>Makes the region empty.</summary>
    public void MakeEmpty()
    {
        Bounds = RectangleF.Empty;
        _path = null;
    }

    /// <summary>Makes the region infinite.</summary>
    public void MakeInfinite()
    {
        Bounds = new RectangleF(-4194304, -4194304, 8388608, 8388608);
        _path = null;
    }

    /// <summary>Moves the region.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    public void Translate(float dx, float dy)
    {
        Bounds = new RectangleF(Bounds.X + dx, Bounds.Y + dy, Bounds.Width, Bounds.Height);
        if (_path is not null)
        {
            using var matrix = new Matrix();
            matrix.Translate(dx, dy);
            _path.Transform(matrix);
        }
    }

    /// <summary>Creates a copy.</summary>
    public Region Clone() => new(Bounds) { _path = (GraphicsPath?)_path?.Clone() };

    /// <inheritdoc />
    public void Dispose()
    {
        // A region holds nothing to release.
    }
}
