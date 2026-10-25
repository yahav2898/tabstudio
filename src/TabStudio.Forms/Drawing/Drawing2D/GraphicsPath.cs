using System.Numerics;
using TabStudio.Forms.Rendering;

namespace System.Drawing.Drawing2D;

/// <summary>A series of connected lines and curves, filled or outlined by <see cref="Graphics"/>.</summary>
public sealed class GraphicsPath : MarshalByRefObject, ICloneable, IDisposable
{
    private readonly List<Figure> _figures = [];
    private Figure? _open;

    /// <summary>Creates an empty path.</summary>
    public GraphicsPath()
    {
    }

    /// <summary>Creates an empty path with a fill mode.</summary>
    /// <param name="fillMode">How overlapping figures are filled.</param>
    public GraphicsPath(FillMode fillMode) => FillMode = fillMode;

    /// <summary>How overlapping figures are filled.</summary>
    public FillMode FillMode { get; set; }

    /// <summary>The number of points that define the path, counting the control points of curves.</summary>
    public int PointCount => _figures.Sum(f => 1 + f.Segments.Sum(s => s is BezierSegmentData ? 3 : 1));

    /// <summary>The last point of the path.</summary>
    public PointF GetLastPoint() => _figures.Count == 0 ? PointF.Empty : _figures[^1].Current;

    /// <summary>Starts a new figure: the next shape is not connected to the previous one.</summary>
    public void StartFigure() => _open = null;

    /// <summary>Closes the current figure back to its start, and starts a new one.</summary>
    public void CloseFigure()
    {
        if (_open is not null)
        {
            _open.Closed = true;
            _open = null;
        }
    }

    /// <summary>Closes every figure of the path.</summary>
    public void CloseAllFigures()
    {
        foreach (var figure in _figures)
        {
            figure.Closed = true;
        }

        _open = null;
    }

    /// <summary>Removes every figure.</summary>
    public void Reset()
    {
        _figures.Clear();
        _open = null;
        FillMode = FillMode.Alternate;
    }

    /// <summary>Adds a line, connected to the current figure.</summary>
    /// <param name="pt1">The start.</param>
    /// <param name="pt2">The end.</param>
    public void AddLine(PointF pt1, PointF pt2)
    {
        var figure = Continue(pt1);
        figure.Segments.Add(new LineSegmentData(pt2));
    }

    /// <summary>Adds a line, connected to the current figure.</summary>
    /// <param name="pt1">The start.</param>
    /// <param name="pt2">The end.</param>
    public void AddLine(Point pt1, Point pt2) => AddLine((PointF)pt1, pt2);

    /// <summary>Adds a line, connected to the current figure.</summary>
    /// <param name="x1">The start's x.</param>
    /// <param name="y1">The start's y.</param>
    /// <param name="x2">The end's x.</param>
    /// <param name="y2">The end's y.</param>
    public void AddLine(float x1, float y1, float x2, float y2) => AddLine(new PointF(x1, y1), new PointF(x2, y2));

    /// <summary>Adds a line, connected to the current figure.</summary>
    /// <param name="x1">The start's x.</param>
    /// <param name="y1">The start's y.</param>
    /// <param name="x2">The end's x.</param>
    /// <param name="y2">The end's y.</param>
    public void AddLine(int x1, int y1, int x2, int y2) => AddLine(new PointF(x1, y1), new PointF(x2, y2));

    /// <summary>Adds connected lines through points.</summary>
    /// <param name="points">The points.</param>
    public void AddLines(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length == 0)
        {
            return;
        }

        var figure = Continue(points[0]);
        foreach (var point in points.Skip(1))
        {
            figure.Segments.Add(new LineSegmentData(point));
        }
    }

    /// <summary>Adds connected lines through points.</summary>
    /// <param name="points">The points.</param>
    public void AddLines(Point[] points) => AddLines(ToFloat(points));

    /// <summary>Adds a rectangle as a closed figure.</summary>
    /// <param name="rect">The rectangle.</param>
    public void AddRectangle(RectangleF rect) =>
        AddClosed(rect.Location, [new PointF(rect.Right, rect.Top), new PointF(rect.Right, rect.Bottom), new PointF(rect.Left, rect.Bottom)]);

    /// <summary>Adds a rectangle as a closed figure.</summary>
    /// <param name="rect">The rectangle.</param>
    public void AddRectangle(Rectangle rect) => AddRectangle((RectangleF)rect);

    /// <summary>Adds rectangles, each as a closed figure.</summary>
    /// <param name="rects">The rectangles.</param>
    public void AddRectangles(RectangleF[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
        {
            AddRectangle(rect);
        }
    }

    /// <summary>Adds rectangles, each as a closed figure.</summary>
    /// <param name="rects">The rectangles.</param>
    public void AddRectangles(Rectangle[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
        {
            AddRectangle(rect);
        }
    }

    /// <summary>Adds an ellipse as a closed figure.</summary>
    /// <param name="rect">The ellipse's bounding rectangle.</param>
    public void AddEllipse(RectangleF rect)
    {
        StartFigure();
        var figure = Continue(GeometryMath.PointOnEllipse(rect, 0));
        GeometryMath.AppendArc(figure.Segments, rect, 0, 360);
        CloseFigure();
    }

    /// <summary>Adds an ellipse as a closed figure.</summary>
    /// <param name="rect">The ellipse's bounding rectangle.</param>
    public void AddEllipse(Rectangle rect) => AddEllipse((RectangleF)rect);

    /// <summary>Adds an ellipse as a closed figure.</summary>
    /// <param name="x">The bounding rectangle's left.</param>
    /// <param name="y">The bounding rectangle's top.</param>
    /// <param name="width">The bounding rectangle's width.</param>
    /// <param name="height">The bounding rectangle's height.</param>
    public void AddEllipse(float x, float y, float width, float height) => AddEllipse(new RectangleF(x, y, width, height));

    /// <summary>Adds an ellipse as a closed figure.</summary>
    /// <param name="x">The bounding rectangle's left.</param>
    /// <param name="y">The bounding rectangle's top.</param>
    /// <param name="width">The bounding rectangle's width.</param>
    /// <param name="height">The bounding rectangle's height.</param>
    public void AddEllipse(int x, int y, int width, int height) => AddEllipse(new RectangleF(x, y, width, height));

    /// <summary>Adds an elliptical arc, connected to the current figure.</summary>
    /// <param name="rect">The ellipse's bounding rectangle.</param>
    /// <param name="startAngle">The start angle in degrees, clockwise from the x-axis.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void AddArc(RectangleF rect, float startAngle, float sweepAngle)
    {
        var figure = Continue(GeometryMath.PointOnEllipse(rect, startAngle));
        GeometryMath.AppendArc(figure.Segments, rect, startAngle, sweepAngle);
    }

    /// <summary>Adds an elliptical arc, connected to the current figure.</summary>
    /// <param name="rect">The ellipse's bounding rectangle.</param>
    /// <param name="startAngle">The start angle in degrees, clockwise from the x-axis.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void AddArc(Rectangle rect, float startAngle, float sweepAngle) => AddArc((RectangleF)rect, startAngle, sweepAngle);

    /// <summary>Adds an elliptical arc, connected to the current figure.</summary>
    /// <param name="x">The bounding rectangle's left.</param>
    /// <param name="y">The bounding rectangle's top.</param>
    /// <param name="width">The bounding rectangle's width.</param>
    /// <param name="height">The bounding rectangle's height.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void AddArc(float x, float y, float width, float height, float startAngle, float sweepAngle) =>
        AddArc(new RectangleF(x, y, width, height), startAngle, sweepAngle);

    /// <summary>Adds an elliptical arc, connected to the current figure.</summary>
    /// <param name="x">The bounding rectangle's left.</param>
    /// <param name="y">The bounding rectangle's top.</param>
    /// <param name="width">The bounding rectangle's width.</param>
    /// <param name="height">The bounding rectangle's height.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void AddArc(int x, int y, int width, int height, float startAngle, float sweepAngle) =>
        AddArc(new RectangleF(x, y, width, height), startAngle, sweepAngle);

    /// <summary>Adds a pie slice as a closed figure.</summary>
    /// <param name="rect">The ellipse's bounding rectangle.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void AddPie(Rectangle rect, float startAngle, float sweepAngle)
    {
        StartFigure();
        var center = new PointF(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
        var figure = Continue(center);
        figure.Segments.Add(new LineSegmentData(GeometryMath.PointOnEllipse(rect, startAngle)));
        GeometryMath.AppendArc(figure.Segments, rect, startAngle, sweepAngle);
        CloseFigure();
    }

    /// <summary>Adds a pie slice as a closed figure.</summary>
    /// <param name="x">The bounding rectangle's left.</param>
    /// <param name="y">The bounding rectangle's top.</param>
    /// <param name="width">The bounding rectangle's width.</param>
    /// <param name="height">The bounding rectangle's height.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void AddPie(float x, float y, float width, float height, float startAngle, float sweepAngle) =>
        AddPie(Rectangle.Round(new RectangleF(x, y, width, height)), startAngle, sweepAngle);

    /// <summary>Adds a polygon as a closed figure.</summary>
    /// <param name="points">The corners.</param>
    public void AddPolygon(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length > 0)
        {
            AddClosed(points[0], points[1..]);
        }
    }

    /// <summary>Adds a polygon as a closed figure.</summary>
    /// <param name="points">The corners.</param>
    public void AddPolygon(Point[] points) => AddPolygon(ToFloat(points));

    /// <summary>Adds a cubic Bézier curve, connected to the current figure.</summary>
    /// <param name="pt1">The start.</param>
    /// <param name="pt2">The first control point.</param>
    /// <param name="pt3">The second control point.</param>
    /// <param name="pt4">The end.</param>
    public void AddBezier(PointF pt1, PointF pt2, PointF pt3, PointF pt4)
    {
        var figure = Continue(pt1);
        figure.Segments.Add(new BezierSegmentData(pt2, pt3, pt4));
    }

    /// <summary>Adds a cubic Bézier curve, connected to the current figure.</summary>
    /// <param name="pt1">The start.</param>
    /// <param name="pt2">The first control point.</param>
    /// <param name="pt3">The second control point.</param>
    /// <param name="pt4">The end.</param>
    public void AddBezier(Point pt1, Point pt2, Point pt3, Point pt4) => AddBezier((PointF)pt1, pt2, pt3, pt4);

    /// <summary>Adds connected cubic Bézier curves: a start point, then three points per curve.</summary>
    /// <param name="points">The points.</param>
    public void AddBeziers(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 4)
        {
            return;
        }

        var figure = Continue(points[0]);
        for (var i = 1; i + 2 < points.Length; i += 3)
        {
            figure.Segments.Add(new BezierSegmentData(points[i], points[i + 1], points[i + 2]));
        }
    }

    /// <summary>Adds a cardinal spline through points, connected to the current figure.</summary>
    /// <param name="points">The points.</param>
    public void AddCurve(PointF[] points) => AddCurve(points, 0.5f);

    /// <summary>Adds a cardinal spline through points, connected to the current figure.</summary>
    /// <param name="points">The points.</param>
    /// <param name="tension">How tightly the curve bends.</param>
    public void AddCurve(PointF[] points, float tension)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length == 0)
        {
            return;
        }

        var figure = Continue(points[0]);
        GeometryMath.AppendCardinalSpline(figure.Segments, points, tension, closed: false);
    }

    /// <summary>Adds a cardinal spline through points, connected to the current figure.</summary>
    /// <param name="points">The points.</param>
    public void AddCurve(Point[] points) => AddCurve(ToFloat(points));

    /// <summary>Adds a closed cardinal spline through points as a closed figure.</summary>
    /// <param name="points">The points.</param>
    public void AddClosedCurve(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length == 0)
        {
            return;
        }

        StartFigure();
        var figure = Continue(points[0]);
        GeometryMath.AppendCardinalSpline(figure.Segments, points, 0.5f, closed: true);
        CloseFigure();
    }

    /// <summary>Adds a closed cardinal spline through points as a closed figure.</summary>
    /// <param name="points">The points.</param>
    public void AddClosedCurve(Point[] points) => AddClosedCurve(ToFloat(points));

    /// <summary>Adds the figures of another path.</summary>
    /// <param name="addingPath">The path to add.</param>
    /// <param name="connect">Whether its first figure continues the current figure.</param>
    public void AddPath(GraphicsPath addingPath, bool connect)
    {
        ArgumentNullException.ThrowIfNull(addingPath);
        foreach (var figure in addingPath._figures)
        {
            if (connect && _open is { } open)
            {
                open.Segments.Add(new LineSegmentData(figure.Start));
                open.Segments.AddRange(figure.Segments);
                open.Closed = figure.Closed;
                connect = false;
                continue;
            }

            _figures.Add(figure.Copy());
        }

        _open = _figures.Count > 0 && !_figures[^1].Closed ? _figures[^1] : null;
    }

    /// <summary>Text cannot be added to a path on this device.</summary>
    /// <param name="s">The text.</param>
    /// <param name="family">The font family.</param>
    /// <param name="style">The font style.</param>
    /// <param name="emSize">The size.</param>
    /// <param name="origin">Where the text starts.</param>
    /// <param name="format">The layout.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void AddString(string s, FontFamily family, int style, float emSize, PointF origin, StringFormat? format) =>
        throw new NotSupportedException("GraphicsPath.AddString is not supported by TabStudio's Forms layer: draw text with Graphics.DrawString.");

    /// <summary>Applies a transformation to every point of the path.</summary>
    /// <param name="matrix">The transformation.</param>
    public void Transform(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        foreach (var figure in _figures)
        {
            figure.Transform(matrix.Value);
        }
    }

    /// <summary>The rectangle that bounds the path.</summary>
    public RectangleF GetBounds()
    {
        var points = _figures.SelectMany(f => GeometryMath.Flatten(f.ToData())).ToList();
        if (points.Count == 0)
        {
            return RectangleF.Empty;
        }

        return RectangleF.FromLTRB(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
    }

    /// <summary>Whether a point is inside the path.</summary>
    /// <param name="point">The point.</param>
    public bool IsVisible(PointF point) => _figures.Any(f => GeometryMath.Contains(GeometryMath.Flatten(f.ToData()), point));

    /// <summary>Whether a point is inside the path.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    public bool IsVisible(float x, float y) => IsVisible(new PointF(x, y));

    /// <summary>Whether a point is inside the path.</summary>
    /// <param name="point">The point.</param>
    public bool IsVisible(Point point) => IsVisible((PointF)point);

    /// <summary>Whether a point is inside the path.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    public bool IsVisible(int x, int y) => IsVisible(new PointF(x, y));

    /// <inheritdoc />
    public object Clone()
    {
        var copy = new GraphicsPath(FillMode);
        copy._figures.AddRange(_figures.Select(f => f.Copy()));
        return copy;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // A path holds nothing to release.
    }

    /// <summary>The path as a display list records it.</summary>
    internal PathData ToData() => new([.. _figures.Select(f => f.ToData())], FillMode);

    private static PointF[] ToFloat(Point[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        return [.. points.Select(p => (PointF)p)];
    }

    /// <summary>The open figure, continued from its last point to a point, or a new figure starting there.</summary>
    private Figure Continue(PointF start)
    {
        if (_open is { } open)
        {
            if (open.Current != start)
            {
                open.Segments.Add(new LineSegmentData(start));
            }

            return open;
        }

        var figure = new Figure(start);
        _figures.Add(figure);
        _open = figure;
        return figure;
    }

    private void AddClosed(PointF start, IEnumerable<PointF> points)
    {
        StartFigure();
        var figure = Continue(start);
        foreach (var point in points)
        {
            figure.Segments.Add(new LineSegmentData(point));
        }

        CloseFigure();
    }

    /// <summary>A figure being built.</summary>
    private sealed class Figure(PointF start)
    {
        public PointF Start { get; private set; } = start;

        public List<PathSegmentData> Segments { get; private set; } = [];

        public bool Closed { get; set; }

        public PointF Current => Segments.Count == 0
            ? Start
            : Segments[^1] switch
            {
                LineSegmentData line => line.To,
                BezierSegmentData bezier => bezier.To,
                _ => Start,
            };

        public Figure Copy() => new(Start) { Segments = [.. Segments], Closed = Closed };

        public PathFigureData ToData() => new(Start, [.. Segments], Closed);

        public void Transform(Matrix3x2 matrix)
        {
            Start = Apply(Start, matrix);
            Segments = [.. Segments.Select(segment => segment switch
            {
                LineSegmentData line => (PathSegmentData)new LineSegmentData(Apply(line.To, matrix)),
                BezierSegmentData bezier => new BezierSegmentData(Apply(bezier.Control1, matrix), Apply(bezier.Control2, matrix), Apply(bezier.To, matrix)),
                _ => segment,
            })];
        }

        private static PointF Apply(PointF point, Matrix3x2 matrix)
        {
            var v = Vector2.Transform(new Vector2(point.X, point.Y), matrix);
            return new PointF(v.X, v.Y);
        }
    }
}
