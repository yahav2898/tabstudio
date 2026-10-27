using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// Turns the curves of GDI+, elliptical arcs and cardinal splines, into cubic Bézier segments, as GDI+ itself does,
/// so a display list only holds lines and Béziers.
/// </summary>
internal static class GeometryMath
{
    private const double QuarterTurn = Math.PI / 2;

    /// <summary>The point at an angle on the ellipse inscribed in a rectangle.</summary>
    /// <param name="bounds">The ellipse's bounding rectangle.</param>
    /// <param name="angle">The angle in degrees, clockwise from the x-axis, measured on the ellipse.</param>
    public static PointF PointOnEllipse(RectangleF bounds, float angle) => Ellipse(bounds, ToParameter(bounds, angle * Math.PI / 180));

    /// <summary>Appends the Bézier segments of an elliptical arc, as GDI+ draws <c>DrawArc</c>.</summary>
    /// <param name="segments">Receives the segments.</param>
    /// <param name="bounds">The ellipse's bounding rectangle.</param>
    /// <param name="startAngle">The start angle in degrees, clockwise from the x-axis.</param>
    /// <param name="sweepAngle">The sweep in degrees, clockwise when positive.</param>
    public static void AppendArc(List<PathSegmentData> segments, RectangleF bounds, float startAngle, float sweepAngle)
    {
        if (sweepAngle == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var start = ToParameter(bounds, startAngle * Math.PI / 180);
        double delta;
        if (Math.Abs(sweepAngle) >= 360)
        {
            delta = Math.Sign(sweepAngle) * 2 * Math.PI;
        }
        else
        {
            var end = ToParameter(bounds, (startAngle + sweepAngle) * Math.PI / 180);
            delta = end - start;
            while (sweepAngle > 0 && delta <= 0)
            {
                delta += 2 * Math.PI;
            }

            while (sweepAngle < 0 && delta >= 0)
            {
                delta -= 2 * Math.PI;
            }
        }

        var count = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / QuarterTurn - 1e-9));
        var step = delta / count;
        var a = bounds.Width / 2.0;
        var b = bounds.Height / 2.0;
        var k = 4.0 / 3.0 * Math.Tan(step / 4);
        for (var i = 0; i < count; i++)
        {
            var t1 = start + (step * i);
            var t2 = t1 + step;
            var p0 = Ellipse(bounds, t1);
            var p3 = Ellipse(bounds, t2);
            var c1 = new PointF((float)(p0.X - (k * a * Math.Sin(t1))), (float)(p0.Y + (k * b * Math.Cos(t1))));
            var c2 = new PointF((float)(p3.X + (k * a * Math.Sin(t2))), (float)(p3.Y - (k * b * Math.Cos(t2))));
            segments.Add(new BezierSegmentData(c1, c2, p3));
        }
    }

    /// <summary>Appends the Bézier segments of a cardinal spline through points, as GDI+ draws <c>DrawCurve</c>.</summary>
    /// <param name="segments">Receives the segments, starting at the first point.</param>
    /// <param name="points">The points the curve passes through.</param>
    /// <param name="tension">How tightly the curve bends: 0 draws straight lines, 0.5 is the default.</param>
    /// <param name="closed">Whether the curve closes back to the first point.</param>
    public static void AppendCardinalSpline(List<PathSegmentData> segments, IReadOnlyList<PointF> points, float tension, bool closed)
    {
        var count = points.Count;
        if (count < 2)
        {
            return;
        }

        var last = closed ? count : count - 1;
        for (var i = 0; i < last; i++)
        {
            var p0 = points[closed ? (i - 1 + count) % count : Math.Max(i - 1, 0)];
            var p1 = points[i];
            var p2 = points[(i + 1) % count];
            var p3 = points[closed ? (i + 2) % count : Math.Min(i + 2, count - 1)];
            var c1 = new PointF(p1.X + (tension * (p2.X - p0.X) / 3), p1.Y + (tension * (p2.Y - p0.Y) / 3));
            var c2 = new PointF(p2.X - (tension * (p3.X - p1.X) / 3), p2.Y - (tension * (p3.Y - p1.Y) / 3));
            segments.Add(new BezierSegmentData(c1, c2, p2));
        }
    }

    /// <summary>The points of a path, with each Bézier segment flattened into short lines, for hit testing and bounds.</summary>
    /// <param name="figure">The figure.</param>
    public static List<PointF> Flatten(PathFigureData figure)
    {
        const int Steps = 12;
        var points = new List<PointF> { figure.Start };
        var current = figure.Start;
        foreach (var segment in figure.Segments)
        {
            switch (segment)
            {
                case LineSegmentData line:
                    points.Add(line.To);
                    current = line.To;
                    break;
                case BezierSegmentData bezier:
                    for (var i = 1; i <= Steps; i++)
                    {
                        var t = i / (float)Steps;
                        var u = 1 - t;
                        points.Add(new PointF(
                            (u * u * u * current.X) + (3 * u * u * t * bezier.Control1.X) + (3 * u * t * t * bezier.Control2.X) + (t * t * t * bezier.To.X),
                            (u * u * u * current.Y) + (3 * u * u * t * bezier.Control1.Y) + (3 * u * t * t * bezier.Control2.Y) + (t * t * t * bezier.To.Y)));
                    }

                    current = bezier.To;
                    break;
            }
        }

        return points;
    }

    /// <summary>Whether a point is inside a polygon, by the even-odd rule.</summary>
    /// <param name="polygon">The polygon's points.</param>
    /// <param name="point">The point.</param>
    public static bool Contains(IReadOnlyList<PointF> polygon, PointF point)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (a, b) = (polygon[i], polygon[j]);
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>A path with every point transformed.</summary>
    /// <param name="path">The path.</param>
    /// <param name="matrix">The transform.</param>
    public static PathData Transform(PathData path, Matrix3x2 matrix)
    {
        if (matrix.IsIdentity)
        {
            return path;
        }

        return path with
        {
            Figures = [.. path.Figures.Select(figure => new PathFigureData(
                Apply(figure.Start, matrix),
                [.. figure.Segments.Select(segment => segment switch
                {
                    LineSegmentData line => (PathSegmentData)new LineSegmentData(Apply(line.To, matrix)),
                    BezierSegmentData bezier => new BezierSegmentData(Apply(bezier.Control1, matrix), Apply(bezier.Control2, matrix), Apply(bezier.To, matrix)),
                    _ => segment,
                })],
                figure.Closed))],
        };
    }

    /// <summary>The bounds of a path's points, Bézier control points included.</summary>
    /// <param name="path">The path.</param>
    public static RectangleF Bounds(PathData path)
    {
        float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
        void Include(PointF p)
        {
            left = Math.Min(left, p.X);
            top = Math.Min(top, p.Y);
            right = Math.Max(right, p.X);
            bottom = Math.Max(bottom, p.Y);
        }

        foreach (var figure in path.Figures)
        {
            foreach (var point in Flatten(figure))
            {
                Include(point);
            }
        }

        return left > right ? RectangleF.Empty : RectangleF.FromLTRB(left, top, right, bottom);
    }

    /// <summary>Whether a point is inside a path, by its fill mode.</summary>
    /// <param name="path">The path; open figures count as closed.</param>
    /// <param name="point">The point.</param>
    public static bool Contains(PathData path, PointF point)
    {
        var winding = 0;
        var crossings = 0;
        foreach (var figure in path.Figures)
        {
            var polygon = Flatten(figure);
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var (a, b) = (polygon[j], polygon[i]);
                if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X)
                {
                    crossings++;
                    winding += b.Y > a.Y ? 1 : -1;
                }
            }
        }

        return path.FillMode == FillMode.Winding ? winding != 0 : crossings % 2 == 1;
    }

    private static PointF Apply(PointF point, Matrix3x2 matrix)
    {
        var v = Vector2.Transform(new Vector2(point.X, point.Y), matrix);
        return new PointF(v.X, v.Y);
    }

    /// <summary>The parameter of the ellipse point at a geometric angle.</summary>
    private static double ToParameter(RectangleF bounds, double angle) =>
        Math.Atan2(bounds.Width / 2.0 * Math.Sin(angle), bounds.Height / 2.0 * Math.Cos(angle));

    private static PointF Ellipse(RectangleF bounds, double t) => new(
        (float)(bounds.X + (bounds.Width / 2.0) + (bounds.Width / 2.0 * Math.Cos(t))),
        (float)(bounds.Y + (bounds.Height / 2.0) + (bounds.Height / 2.0 * Math.Sin(t))));
}
