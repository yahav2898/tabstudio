using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// The caps a pen draws as shapes at the ends of open lines: triangle caps, anchors (square, round, diamond and
/// arrow), custom caps such as <see cref="AdjustableArrowCap"/>, and different caps at the two ends. The line is
/// shortened under a cap that asks for it, so it does not poke out of an arrowhead's tip.
/// </summary>
internal static class LineCaps
{
    private const int ArcSteps = 12;

    /// <summary>A shape of a cap, in world coordinates.</summary>
    /// <param name="Shape">The shape.</param>
    /// <param name="Filled">Whether it is filled with the pen's brush, or outlined with the pen.</param>
    public readonly record struct CapShape(PathData Shape, bool Filled);

    /// <summary>The path to stroke and the cap shapes to draw for a pen.</summary>
    /// <param name="path">The path, in world coordinates.</param>
    /// <param name="pen">The pen.</param>
    public static (PathData Stroke, List<CapShape> Caps) Apply(PathData path, Pen pen)
    {
        var caps = new List<CapShape>();
        if (!pen.HasShapedCaps)
        {
            return (path, caps);
        }

        var width = Math.Max(pen.Width, 1);
        var figures = new List<PathFigureData>(path.Figures.Count);
        foreach (var figure in path.Figures)
        {
            if (figure.Closed || figure.Segments.Count == 0)
            {
                figures.Add(figure);
                continue;
            }

            var current = figure;
            var (startPoint, startDirection) = StartOf(current);
            var startInset = AddCap(caps, pen.StartCap, pen.CustomStartCap, startPoint, startDirection, width);
            var (endPoint, endDirection) = EndOf(current);
            var endInset = AddCap(caps, pen.EndCap, pen.CustomEndCap, endPoint, endDirection, width);
            current = Shorten(current, startInset, endInset);
            figures.Add(current);
        }

        return (path with { Figures = figures }, caps);
    }

    private static (PointF Point, Vector2 Outward) StartOf(PathFigureData figure)
    {
        var start = figure.Start;
        var next = figure.Segments[0] switch
        {
            LineSegmentData line => line.To,
            BezierSegmentData bezier => bezier.Control1 != start ? bezier.Control1 : bezier.To,
            _ => start,
        };
        return (start, Direction(next, start));
    }

    private static (PointF Point, Vector2 Outward) EndOf(PathFigureData figure)
    {
        var last = figure.Segments[^1];
        var previous = figure.Segments.Count > 1 ? EndPoint(figure.Segments[^2]) : figure.Start;
        return last switch
        {
            LineSegmentData line => (line.To, Direction(previous, line.To)),
            BezierSegmentData bezier => (bezier.To, Direction(bezier.Control2 != bezier.To ? bezier.Control2 : previous, bezier.To)),
            _ => (previous, Vector2.UnitX),
        };
    }

    private static PointF EndPoint(PathSegmentData segment) => segment switch
    {
        LineSegmentData line => line.To,
        BezierSegmentData bezier => bezier.To,
        _ => PointF.Empty,
    };

    /// <summary>The unit vector from one point to another, or the x-axis when they coincide.</summary>
    private static Vector2 Direction(PointF from, PointF to)
    {
        var v = new Vector2(to.X - from.X, to.Y - from.Y);
        return v.LengthSquared() < 1e-12f ? Vector2.UnitX : Vector2.Normalize(v);
    }

    /// <summary>Adds the shapes of one cap, returning how much the line is shortened under it.</summary>
    private static float AddCap(List<CapShape> caps, LineCap cap, CustomLineCap? custom, PointF end, Vector2 outward, float width)
    {
        // Anchor sizes as GDI+ draws them, in multiples of the width.
        var unit = Math.Max(width, 2);
        switch (cap)
        {
            case LineCap.Square:
                caps.Add(new(Polygon(end, outward, [new(-width / 2, 0), new(width / 2, 0), new(width / 2, width / 2), new(-width / 2, width / 2)]), true));
                return 0;
            case LineCap.Round:
                caps.Add(new(Polygon(end, outward, Arc(width / 2, 0, MathF.PI)), true));
                return 0;
            case LineCap.Triangle:
                caps.Add(new(Polygon(end, outward, [new(-width / 2, 0), new(0, width / 3), new(width / 2, 0)]), true));
                return 0;
            case LineCap.SquareAnchor:
                var side = 0.75f * unit;
                caps.Add(new(Polygon(end, outward, [new(-side, -side), new(side, -side), new(side, side), new(-side, side)]), true));
                return 0;
            case LineCap.RoundAnchor:
                caps.Add(new(Polygon(end, outward, Arc(0.875f * unit, 0, 2 * MathF.PI)), true));
                return 0;
            case LineCap.DiamondAnchor:
                var reach = 0.85f * unit;
                caps.Add(new(Polygon(end, outward, [new(0, -reach), new(reach, 0), new(0, reach), new(-reach, 0)]), true));
                return 0;
            case LineCap.ArrowAnchor:
                var length = 1.75f * unit;
                caps.Add(new(Polygon(end, outward, [new(0, 0), new(-0.85f * unit, -length), new(0.85f * unit, -length)]), true));
                return length;
            case LineCap.Custom when custom is not null:
                var scale = width * custom.WidthScale;
                if (custom.FillPath is { } fill)
                {
                    caps.Add(new(Transform(fill.ToData(), end, outward, scale), true));
                }

                if (custom.StrokePath is { } stroke)
                {
                    caps.Add(new(Transform(stroke.ToData(), end, outward, scale), false));
                }

                return custom.BaseInset * scale;
            default:
                return 0;
        }
    }

    /// <summary>Points of an arc around the origin, in a cap's frame.</summary>
    private static Vector2[] Arc(float radius, float from, float sweep)
    {
        var points = new Vector2[ArcSteps + 1];
        for (var i = 0; i <= ArcSteps; i++)
        {
            var t = from + (sweep * i / ArcSteps);
            points[i] = new Vector2(radius * MathF.Cos(t), radius * MathF.Sin(t));
        }

        return points;
    }

    /// <summary>
    /// The matrix from a cap's frame to world coordinates: the origin at the line's end, +y pointing out of the
    /// line, and x across it.
    /// </summary>
    private static Matrix3x2 Frame(PointF end, Vector2 outward, float scale)
    {
        var across = new Vector2(-outward.Y, outward.X);
        return new Matrix3x2(across.X * scale, across.Y * scale, outward.X * scale, outward.Y * scale, end.X, end.Y);
    }

    private static PathData Polygon(PointF end, Vector2 outward, Vector2[] points)
    {
        var frame = Frame(end, outward, 1);
        var world = points.Select(p => Vector2.Transform(p, frame)).Select(v => new PointF(v.X, v.Y)).ToArray();
        return new PathData([new PathFigureData(world[0], [.. world.Skip(1).Select(p => (PathSegmentData)new LineSegmentData(p))], Closed: true)], FillMode.Winding);
    }

    private static PathData Transform(PathData path, PointF end, Vector2 outward, float scale) =>
        GeometryMath.Transform(path, Frame(end, outward, scale)) with { FillMode = FillMode.Winding };

    /// <summary>The figure with its first and last line segments pulled back by insets.</summary>
    private static PathFigureData Shorten(PathFigureData figure, float startInset, float endInset)
    {
        if (startInset <= 0 && endInset <= 0)
        {
            return figure;
        }

        var start = figure.Start;
        var segments = figure.Segments.ToList();
        if (startInset > 0 && segments[0] is LineSegmentData first)
        {
            start = Pull(start, first.To, startInset);
        }

        if (endInset > 0 && segments[^1] is LineSegmentData last)
        {
            var previous = segments.Count > 1 ? EndPoint(segments[^2]) : start;
            segments[^1] = new LineSegmentData(Pull(last.To, previous, endInset));
        }

        return figure with { Start = start, Segments = segments };
    }

    /// <summary>A point moved toward another by a distance, never past the middle between them.</summary>
    private static PointF Pull(PointF point, PointF toward, float distance)
    {
        var v = new Vector2(toward.X - point.X, toward.Y - point.Y);
        var length = v.Length();
        if (length < 1e-6f)
        {
            return point;
        }

        var d = Math.Min(distance, length / 2);
        return new PointF(point.X + (v.X / length * d), point.Y + (v.Y / length * d));
    }
}
