using System.Drawing.Drawing2D;

namespace System.Drawing;

/// <summary>
/// An area: a rectangle or a path's shape, with parts excluded. Used for clipping and hit testing. A union or xor of
/// shapes keeps their bounding rectangle.
/// </summary>
public sealed class Region : MarshalByRefObject, IDisposable
{
    private const float Infinite = 4194304;

    private GraphicsPath? _path;
    private List<GraphicsPath> _exclusions = [];

    /// <summary>Creates an infinite region.</summary>
    public Region() => MakeInfinite();

    /// <summary>Creates a region of a rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public Region(RectangleF rect) => Bounds = rect;

    /// <summary>Creates a region of a rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public Region(Rectangle rect) => Bounds = rect;

    /// <summary>Creates a region of a path's shape.</summary>
    /// <param name="path">The path.</param>
    public Region(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _path = (GraphicsPath)path.Clone();
        Bounds = path.GetBounds();
    }

    /// <summary>The bounding rectangle.</summary>
    internal RectangleF Bounds { get; private set; }

    /// <summary>The shape, when the region is a path's shape rather than its bounding rectangle.</summary>
    internal GraphicsPath? Path => _path;

    /// <summary>The shapes excluded from the region.</summary>
    internal IReadOnlyList<GraphicsPath> Exclusions => _exclusions;

    /// <summary>Whether the region is infinite.</summary>
    internal bool IsInfiniteRegion => Bounds.Width >= 2 * Infinite && _path is null && _exclusions.Count == 0;

    /// <summary>The bounding rectangle.</summary>
    /// <param name="g">The surface.</param>
    public RectangleF GetBounds(Graphics g) => Bounds;

    /// <summary>Whether the region is infinite.</summary>
    /// <param name="g">The surface.</param>
    public bool IsInfinite(Graphics g) => IsInfiniteRegion;

    /// <summary>Whether the region is empty.</summary>
    /// <param name="g">The surface.</param>
    public bool IsEmpty(Graphics g) => Bounds.Width <= 0 || Bounds.Height <= 0;

    /// <summary>Whether a point is in the region.</summary>
    /// <param name="point">The point.</param>
    public bool IsVisible(PointF point) =>
        Bounds.Contains(point) && (_path is null || _path.IsVisible(point)) && !_exclusions.Any(excluded => excluded.IsVisible(point));

    /// <summary>Whether a point is in the region.</summary>
    /// <param name="point">The point.</param>
    public bool IsVisible(Point point) => IsVisible((PointF)point);

    /// <summary>Whether a point is in the region.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    public bool IsVisible(float x, float y) => IsVisible(new PointF(x, y));

    /// <summary>Whether a point is in the region.</summary>
    /// <param name="point">The point.</param>
    /// <param name="g">The surface.</param>
    public bool IsVisible(PointF point, Graphics? g) => IsVisible(point);

    /// <summary>Whether a point is in the region.</summary>
    /// <param name="point">The point.</param>
    /// <param name="g">The surface.</param>
    public bool IsVisible(Point point, Graphics? g) => IsVisible(point);

    /// <summary>Whether a rectangle overlaps the region's bounds.</summary>
    /// <param name="rect">The rectangle.</param>
    public bool IsVisible(RectangleF rect) => Bounds.IntersectsWith(rect);

    /// <summary>Whether a rectangle overlaps the region's bounds.</summary>
    /// <param name="rect">The rectangle.</param>
    public bool IsVisible(Rectangle rect) => Bounds.IntersectsWith(rect);

    /// <summary>Limits the region to a rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Intersect(RectangleF rect) => Bounds = RectangleF.Intersect(Bounds, rect);

    /// <summary>Limits the region to a rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Intersect(Rectangle rect) => Intersect((RectangleF)rect);

    /// <summary>Limits the region to a path's shape.</summary>
    /// <param name="path">The path.</param>
    public void Intersect(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        Bounds = RectangleF.Intersect(Bounds, path.GetBounds());
        _path ??= (GraphicsPath)path.Clone();
    }

    /// <summary>Limits the region to another region.</summary>
    /// <param name="region">The region.</param>
    public void Intersect(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        Bounds = RectangleF.Intersect(Bounds, region.Bounds);
        _path ??= (GraphicsPath?)region._path?.Clone();
        _exclusions.AddRange(region._exclusions.Select(excluded => (GraphicsPath)excluded.Clone()));
    }

    /// <summary>Extends the region by a rectangle, to their bounding rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Union(RectangleF rect)
    {
        Bounds = RectangleF.Union(Bounds, rect);
        _path = null;
        _exclusions = [];
    }

    /// <summary>Extends the region by a rectangle, to their bounding rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Union(Rectangle rect) => Union((RectangleF)rect);

    /// <summary>Extends the region by a path's shape.</summary>
    /// <param name="path">The path.</param>
    public void Union(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_path is not null && _exclusions.Count == 0)
        {
            _path.AddPath(path, connect: false);
            _path.FillMode = FillMode.Winding;
            Bounds = _path.GetBounds();
            return;
        }

        Union(path.GetBounds());
    }

    /// <summary>Extends the region by another region.</summary>
    /// <param name="region">The region.</param>
    public void Union(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (region._path is { } path)
        {
            Union(path);
        }
        else
        {
            Union(region.Bounds);
        }
    }

    /// <summary>Removes a rectangle from the region.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Exclude(RectangleF rect)
    {
        var path = new GraphicsPath();
        path.AddRectangle(rect);
        _exclusions.Add(path);
    }

    /// <summary>Removes a rectangle from the region.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Exclude(Rectangle rect) => Exclude((RectangleF)rect);

    /// <summary>Removes a path's shape from the region.</summary>
    /// <param name="path">The path.</param>
    public void Exclude(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _exclusions.Add((GraphicsPath)path.Clone());
    }

    /// <summary>Removes another region from this one.</summary>
    /// <param name="region">The region.</param>
    public void Exclude(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (region._path is { } path)
        {
            Exclude(path);
        }
        else
        {
            Exclude(region.Bounds);
        }
    }

    /// <summary>Combines with a rectangle by exclusive or, kept as the bounding rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Xor(RectangleF rect) => Union(rect);

    /// <summary>Combines with a rectangle by exclusive or, kept as the bounding rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Xor(Rectangle rect) => Union((RectangleF)rect);

    /// <summary>Becomes the part of a rectangle outside the region.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Complement(RectangleF rect)
    {
        var inside = _path is { } path ? (GraphicsPath)path.Clone() : Rectangle(Bounds);
        Bounds = rect;
        _path = null;
        _exclusions = [inside];
    }

    /// <summary>Becomes the part of a rectangle outside the region.</summary>
    /// <param name="rect">The rectangle.</param>
    public void Complement(Rectangle rect) => Complement((RectangleF)rect);

    /// <summary>Empties the region.</summary>
    public void MakeEmpty()
    {
        Bounds = RectangleF.Empty;
        _path = null;
        _exclusions = [];
    }

    /// <summary>Makes the region infinite.</summary>
    public void MakeInfinite()
    {
        Bounds = new RectangleF(-Infinite, -Infinite, 2 * Infinite, 2 * Infinite);
        _path = null;
        _exclusions = [];
    }

    /// <summary>Moves the region.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    public void Translate(float dx, float dy)
    {
        Bounds = new RectangleF(Bounds.X + dx, Bounds.Y + dy, Bounds.Width, Bounds.Height);
        using var matrix = new Matrix();
        matrix.Translate(dx, dy);
        _path?.Transform(matrix);
        foreach (var excluded in _exclusions)
        {
            excluded.Transform(matrix);
        }
    }

    /// <summary>Moves the region.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    public void Translate(int dx, int dy) => Translate((float)dx, dy);

    /// <summary>Transforms the region's shape.</summary>
    /// <param name="matrix">The transform.</param>
    public void Transform(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        _path ??= Rectangle(Bounds);
        _path.Transform(matrix);
        foreach (var excluded in _exclusions)
        {
            excluded.Transform(matrix);
        }

        Bounds = _path.GetBounds();
    }

    /// <summary>Whether two regions have the same bounds.</summary>
    /// <param name="region">The other region.</param>
    /// <param name="g">The surface.</param>
    public bool Equals(Region region, Graphics g) => region is not null && region.Bounds == Bounds;

    /// <summary>A copy of the region.</summary>
    public Region Clone() => new(Bounds) { _path = (GraphicsPath?)_path?.Clone(), _exclusions = [.. _exclusions.Select(excluded => (GraphicsPath)excluded.Clone())] };

    /// <inheritdoc />
    public void Dispose()
    {
        // A region holds nothing to release.
    }

    private static GraphicsPath Rectangle(RectangleF rect)
    {
        var path = new GraphicsPath();
        path.AddRectangle(rect);
        return path;
    }
}
