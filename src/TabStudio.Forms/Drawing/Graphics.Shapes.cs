using System.Drawing.Drawing2D;
using TabStudio.Forms.Rendering;

namespace System.Drawing;

/// <summary>Lines, rectangles, ellipses, arcs, polygons, curves and paths.</summary>
public sealed partial class Graphics
{
    /// <summary>Fills a rectangle.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="rect">The rectangle.</param>
    public void FillRectangle(Brush brush, RectangleF rect)
    {
        ArgumentNullException.ThrowIfNull(brush);
        if (rect.Width > 0 && rect.Height > 0)
        {
            Record(new RectangleCommand(rect, brush.ToSpec(), null));
        }
    }

    /// <summary>Fills a rectangle.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="rect">The rectangle.</param>
    public void FillRectangle(Brush brush, Rectangle rect) => FillRectangle(brush, (RectangleF)rect);

    /// <summary>Fills a rectangle.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void FillRectangle(Brush brush, int x, int y, int width, int height) => FillRectangle(brush, new RectangleF(x, y, width, height));

    /// <summary>Fills a rectangle.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void FillRectangle(Brush brush, float x, float y, float width, float height) => FillRectangle(brush, new RectangleF(x, y, width, height));

    /// <summary>Fills rectangles.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="rects">The rectangles.</param>
    public void FillRectangles(Brush brush, RectangleF[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
        {
            FillRectangle(brush, rect);
        }
    }

    /// <summary>Fills rectangles.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="rects">The rectangles.</param>
    public void FillRectangles(Brush brush, Rectangle[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
        {
            FillRectangle(brush, rect);
        }
    }

    /// <summary>Draws the outline of a rectangle, covering its right and bottom edges as GDI+ does.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="rect">The rectangle.</param>
    public void DrawRectangle(Pen pen, RectangleF rect)
    {
        ArgumentNullException.ThrowIfNull(pen);
        if (pen.Alignment == PenAlignment.Inset && pen.Width > 1)
        {
            // An inset pen draws inside the rectangle: its outer edge is the rectangle's edge.
            var half = pen.Width / 2;
            Record(new RectangleCommand(new RectangleF(rect.X + half, rect.Y + half, Math.Max(0, rect.Width - pen.Width), Math.Max(0, rect.Height - pen.Width)), null, pen.ToSpec()));
            return;
        }

        var offset = StrokeOffset(pen);
        Record(new RectangleCommand(new RectangleF(rect.X + offset, rect.Y + offset, rect.Width, rect.Height), null, pen.ToSpec()));
    }

    /// <summary>Draws the outline of a rectangle.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="rect">The rectangle.</param>
    public void DrawRectangle(Pen pen, Rectangle rect) => DrawRectangle(pen, (RectangleF)rect);

    /// <summary>Draws the outline of a rectangle.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void DrawRectangle(Pen pen, int x, int y, int width, int height) => DrawRectangle(pen, new RectangleF(x, y, width, height));

    /// <summary>Draws the outline of a rectangle.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void DrawRectangle(Pen pen, float x, float y, float width, float height) => DrawRectangle(pen, new RectangleF(x, y, width, height));

    /// <summary>Draws the outlines of rectangles.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="rects">The rectangles.</param>
    public void DrawRectangles(Pen pen, RectangleF[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
        {
            DrawRectangle(pen, rect);
        }
    }

    /// <summary>Draws the outlines of rectangles.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="rects">The rectangles.</param>
    public void DrawRectangles(Pen pen, Rectangle[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
        {
            DrawRectangle(pen, rect);
        }
    }

    /// <summary>Draws a line.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="pt1">The start.</param>
    /// <param name="pt2">The end.</param>
    public void DrawLine(Pen pen, PointF pt1, PointF pt2)
    {
        ArgumentNullException.ThrowIfNull(pen);
        var offset = StrokeOffset(pen);
        var from = new PointF(pt1.X + offset, pt1.Y + offset);
        var to = new PointF(pt2.X + offset, pt2.Y + offset);
        if (pen.HasShapedCaps)
        {
            Stroke(Polyline([from, to], closed: false), pen);
        }
        else
        {
            Record(new LineCommand(from, to, pen.ToSpec()));
        }
    }

    /// <summary>Draws a line.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="pt1">The start.</param>
    /// <param name="pt2">The end.</param>
    public void DrawLine(Pen pen, Point pt1, Point pt2) => DrawLine(pen, (PointF)pt1, pt2);

    /// <summary>Draws a line.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="x1">The start's x.</param>
    /// <param name="y1">The start's y.</param>
    /// <param name="x2">The end's x.</param>
    /// <param name="y2">The end's y.</param>
    public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) => DrawLine(pen, new PointF(x1, y1), new PointF(x2, y2));

    /// <summary>Draws a line.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="x1">The start's x.</param>
    /// <param name="y1">The start's y.</param>
    /// <param name="x2">The end's x.</param>
    /// <param name="y2">The end's y.</param>
    public void DrawLine(Pen pen, float x1, float y1, float x2, float y2) => DrawLine(pen, new PointF(x1, y1), new PointF(x2, y2));

    /// <summary>Draws connected lines through points.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The points.</param>
    public void DrawLines(Pen pen, PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length >= 2)
        {
            Stroke(Polyline(Offset(points, StrokeOffset(pen)), closed: false), pen);
        }
    }

    /// <summary>Draws connected lines through points.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The points.</param>
    public void DrawLines(Pen pen, Point[] points) => DrawLines(pen, ToFloat(points));

    /// <summary>Draws the outline of an ellipse.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="rect">The bounding rectangle.</param>
    public void DrawEllipse(Pen pen, RectangleF rect)
    {
        ArgumentNullException.ThrowIfNull(pen);
        var offset = StrokeOffset(pen);
        Record(new EllipseCommand(new RectangleF(rect.X + offset, rect.Y + offset, rect.Width, rect.Height), null, pen.ToSpec()));
    }

    /// <summary>Draws the outline of an ellipse.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="rect">The bounding rectangle.</param>
    public void DrawEllipse(Pen pen, Rectangle rect) => DrawEllipse(pen, (RectangleF)rect);

    /// <summary>Draws the outline of an ellipse.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void DrawEllipse(Pen pen, int x, int y, int width, int height) => DrawEllipse(pen, new RectangleF(x, y, width, height));

    /// <summary>Draws the outline of an ellipse.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void DrawEllipse(Pen pen, float x, float y, float width, float height) => DrawEllipse(pen, new RectangleF(x, y, width, height));

    /// <summary>Fills an ellipse.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="rect">The bounding rectangle.</param>
    public void FillEllipse(Brush brush, RectangleF rect)
    {
        ArgumentNullException.ThrowIfNull(brush);
        Record(new EllipseCommand(rect, brush.ToSpec(), null));
    }

    /// <summary>Fills an ellipse.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="rect">The bounding rectangle.</param>
    public void FillEllipse(Brush brush, Rectangle rect) => FillEllipse(brush, (RectangleF)rect);

    /// <summary>Fills an ellipse.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void FillEllipse(Brush brush, int x, int y, int width, int height) => FillEllipse(brush, new RectangleF(x, y, width, height));

    /// <summary>Fills an ellipse.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void FillEllipse(Brush brush, float x, float y, float width, float height) => FillEllipse(brush, new RectangleF(x, y, width, height));

    /// <summary>Draws the outline of a polygon.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The corners.</param>
    public void DrawPolygon(Pen pen, PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length >= 2)
        {
            Record(new PathCommand(Polyline(Offset(points, StrokeOffset(pen)), closed: true), null, pen.ToSpec()));
        }
    }

    /// <summary>Draws the outline of a polygon.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The corners.</param>
    public void DrawPolygon(Pen pen, Point[] points) => DrawPolygon(pen, ToFloat(points));

    /// <summary>Fills a polygon.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="points">The corners.</param>
    public void FillPolygon(Brush brush, PointF[] points) => FillPolygon(brush, points, FillMode.Alternate);

    /// <summary>Fills a polygon.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="points">The corners.</param>
    /// <param name="fillMode">How overlapping parts are filled.</param>
    public void FillPolygon(Brush brush, PointF[] points, FillMode fillMode)
    {
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length >= 3)
        {
            var path = Polyline(points, closed: true);
            Record(new PathCommand(path with { FillMode = fillMode }, brush.ToSpec(), null));
        }
    }

    /// <summary>Fills a polygon.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="points">The corners.</param>
    public void FillPolygon(Brush brush, Point[] points) => FillPolygon(brush, ToFloat(points), FillMode.Alternate);

    /// <summary>Fills a polygon.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="points">The corners.</param>
    /// <param name="fillMode">How overlapping parts are filled.</param>
    public void FillPolygon(Brush brush, Point[] points, FillMode fillMode) => FillPolygon(brush, ToFloat(points), fillMode);

    /// <summary>Draws an elliptical arc.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="rect">The ellipse's bounding rectangle.</param>
    /// <param name="startAngle">The start angle in degrees, clockwise from the x-axis.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void DrawArc(Pen pen, RectangleF rect, float startAngle, float sweepAngle)
    {
        ArgumentNullException.ThrowIfNull(pen);
        using var path = new GraphicsPath();
        var offset = StrokeOffset(pen);
        path.AddArc(new RectangleF(rect.X + offset, rect.Y + offset, rect.Width, rect.Height), startAngle, sweepAngle);
        Stroke(path.ToData(), pen);
    }

    /// <summary>Draws an elliptical arc.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="rect">The ellipse's bounding rectangle.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void DrawArc(Pen pen, Rectangle rect, float startAngle, float sweepAngle) => DrawArc(pen, (RectangleF)rect, startAngle, sweepAngle);

    /// <summary>Draws an elliptical arc.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="x">The bounding rectangle's left.</param>
    /// <param name="y">The bounding rectangle's top.</param>
    /// <param name="width">The bounding rectangle's width.</param>
    /// <param name="height">The bounding rectangle's height.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void DrawArc(Pen pen, int x, int y, int width, int height, int startAngle, int sweepAngle) =>
        DrawArc(pen, new RectangleF(x, y, width, height), startAngle, sweepAngle);

    /// <summary>Draws an elliptical arc.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="x">The bounding rectangle's left.</param>
    /// <param name="y">The bounding rectangle's top.</param>
    /// <param name="width">The bounding rectangle's width.</param>
    /// <param name="height">The bounding rectangle's height.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void DrawArc(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle) =>
        DrawArc(pen, new RectangleF(x, y, width, height), startAngle, sweepAngle);

    /// <summary>Draws the outline of a pie slice.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="rect">The ellipse's bounding rectangle.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void DrawPie(Pen pen, RectangleF rect, float startAngle, float sweepAngle)
    {
        ArgumentNullException.ThrowIfNull(pen);
        Record(new PathCommand(Pie(rect, startAngle, sweepAngle), null, pen.ToSpec()));
    }

    /// <summary>Draws the outline of a pie slice.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="rect">The ellipse's bounding rectangle.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void DrawPie(Pen pen, Rectangle rect, float startAngle, float sweepAngle) => DrawPie(pen, (RectangleF)rect, startAngle, sweepAngle);

    /// <summary>Draws the outline of a pie slice.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="x">The bounding rectangle's left.</param>
    /// <param name="y">The bounding rectangle's top.</param>
    /// <param name="width">The bounding rectangle's width.</param>
    /// <param name="height">The bounding rectangle's height.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void DrawPie(Pen pen, int x, int y, int width, int height, int startAngle, int sweepAngle) =>
        DrawPie(pen, new RectangleF(x, y, width, height), startAngle, sweepAngle);

    /// <summary>Draws the outline of a pie slice.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="x">The bounding rectangle's left.</param>
    /// <param name="y">The bounding rectangle's top.</param>
    /// <param name="width">The bounding rectangle's width.</param>
    /// <param name="height">The bounding rectangle's height.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void DrawPie(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle) =>
        DrawPie(pen, new RectangleF(x, y, width, height), startAngle, sweepAngle);

    /// <summary>Fills a pie slice.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="rect">The ellipse's bounding rectangle.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void FillPie(Brush brush, Rectangle rect, float startAngle, float sweepAngle)
    {
        ArgumentNullException.ThrowIfNull(brush);
        Record(new PathCommand(Pie(rect, startAngle, sweepAngle), brush.ToSpec(), null));
    }

    /// <summary>Fills a pie slice.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="x">The bounding rectangle's left.</param>
    /// <param name="y">The bounding rectangle's top.</param>
    /// <param name="width">The bounding rectangle's width.</param>
    /// <param name="height">The bounding rectangle's height.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void FillPie(Brush brush, int x, int y, int width, int height, int startAngle, int sweepAngle)
    {
        ArgumentNullException.ThrowIfNull(brush);
        Record(new PathCommand(Pie(new RectangleF(x, y, width, height), startAngle, sweepAngle), brush.ToSpec(), null));
    }

    /// <summary>Fills a pie slice.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="x">The bounding rectangle's left.</param>
    /// <param name="y">The bounding rectangle's top.</param>
    /// <param name="width">The bounding rectangle's width.</param>
    /// <param name="height">The bounding rectangle's height.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="sweepAngle">The sweep in degrees.</param>
    public void FillPie(Brush brush, float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        ArgumentNullException.ThrowIfNull(brush);
        Record(new PathCommand(Pie(new RectangleF(x, y, width, height), startAngle, sweepAngle), brush.ToSpec(), null));
    }

    /// <summary>Draws a cubic Bézier curve.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="pt1">The start.</param>
    /// <param name="pt2">The first control point.</param>
    /// <param name="pt3">The second control point.</param>
    /// <param name="pt4">The end.</param>
    public void DrawBezier(Pen pen, PointF pt1, PointF pt2, PointF pt3, PointF pt4)
    {
        ArgumentNullException.ThrowIfNull(pen);
        Stroke(new PathData([new PathFigureData(pt1, [new BezierSegmentData(pt2, pt3, pt4)], false)], FillMode.Alternate), pen);
    }

    /// <summary>Draws a cubic Bézier curve.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="pt1">The start.</param>
    /// <param name="pt2">The first control point.</param>
    /// <param name="pt3">The second control point.</param>
    /// <param name="pt4">The end.</param>
    public void DrawBezier(Pen pen, Point pt1, Point pt2, Point pt3, Point pt4) => DrawBezier(pen, (PointF)pt1, pt2, pt3, pt4);

    /// <summary>Draws a cubic Bézier curve.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="x1">The start's x.</param>
    /// <param name="y1">The start's y.</param>
    /// <param name="x2">The first control point's x.</param>
    /// <param name="y2">The first control point's y.</param>
    /// <param name="x3">The second control point's x.</param>
    /// <param name="y3">The second control point's y.</param>
    /// <param name="x4">The end's x.</param>
    /// <param name="y4">The end's y.</param>
    public void DrawBezier(Pen pen, float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4) =>
        DrawBezier(pen, new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3), new PointF(x4, y4));

    /// <summary>Draws connected cubic Bézier curves: a start point, then three points per curve.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The points.</param>
    public void DrawBeziers(Pen pen, PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(pen);
        using var path = new GraphicsPath();
        path.AddBeziers(points);
        Stroke(path.ToData(), pen);
    }

    /// <summary>Draws connected cubic Bézier curves.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The points.</param>
    public void DrawBeziers(Pen pen, Point[] points) => DrawBeziers(pen, ToFloat(points));

    /// <summary>Draws a cardinal spline through points.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The points.</param>
    public void DrawCurve(Pen pen, PointF[] points) => DrawCurve(pen, points, 0.5f);

    /// <summary>Draws a cardinal spline through points.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The points.</param>
    /// <param name="tension">How tightly the curve bends.</param>
    public void DrawCurve(Pen pen, PointF[] points, float tension)
    {
        ArgumentNullException.ThrowIfNull(pen);
        using var path = new GraphicsPath();
        path.AddCurve(points, tension);
        Stroke(path.ToData(), pen);
    }

    /// <summary>Draws a cardinal spline through points.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The points.</param>
    public void DrawCurve(Pen pen, Point[] points) => DrawCurve(pen, ToFloat(points), 0.5f);

    /// <summary>Draws a cardinal spline through points.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The points.</param>
    /// <param name="tension">How tightly the curve bends.</param>
    public void DrawCurve(Pen pen, Point[] points, float tension) => DrawCurve(pen, ToFloat(points), tension);

    /// <summary>Draws a closed cardinal spline through points.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The points.</param>
    public void DrawClosedCurve(Pen pen, PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(pen);
        using var path = new GraphicsPath();
        path.AddClosedCurve(points);
        Record(new PathCommand(path.ToData(), null, pen.ToSpec()));
    }

    /// <summary>Draws a closed cardinal spline through points.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="points">The points.</param>
    public void DrawClosedCurve(Pen pen, Point[] points) => DrawClosedCurve(pen, ToFloat(points));

    /// <summary>Fills a closed cardinal spline through points.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="points">The points.</param>
    public void FillClosedCurve(Brush brush, PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(brush);
        using var path = new GraphicsPath();
        path.AddClosedCurve(points);
        Record(new PathCommand(path.ToData(), brush.ToSpec(), null));
    }

    /// <summary>Fills a closed cardinal spline through points.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="points">The points.</param>
    public void FillClosedCurve(Brush brush, Point[] points) => FillClosedCurve(brush, ToFloat(points));

    /// <summary>Draws the outline of a path.</summary>
    /// <param name="pen">The pen.</param>
    /// <param name="path">The path.</param>
    public void DrawPath(Pen pen, GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(path);
        Stroke(path.ToData(), pen);
    }

    /// <summary>Fills the interior of a path.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="path">The path.</param>
    public void FillPath(Brush brush, GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(path);
        Record(new PathCommand(path.ToData(), brush.ToSpec(), null));
    }

    /// <summary>Fills a region.</summary>
    /// <param name="brush">The brush.</param>
    /// <param name="region">The region.</param>
    public void FillRegion(Brush brush, Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (region.Path is null && region.Exclusions.Count == 0)
        {
            FillRectangle(brush, region.Bounds);
            return;
        }

        var state = Save();
        SetClip(region, CombineMode.Intersect);
        FillRectangle(brush, region.Bounds);
        Restore(state);
    }

    /// <summary>Strokes a path with a pen, with the shapes of its caps.</summary>
    private void Stroke(PathData data, Pen pen)
    {
        var (stroke, caps) = LineCaps.Apply(data, pen);
        var spec = pen.ToSpec();
        Record(new PathCommand(stroke, null, spec));
        foreach (var cap in caps)
        {
            Record(cap.Filled
                ? new PathCommand(cap.Shape, spec.Brush, null)
                : new PathCommand(cap.Shape, null, spec with { Dash = DashStyle.Solid, Cap = LineCap.Flat }));
        }
    }

    private static PointF[] ToFloat(Point[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        return [.. points.Select(p => (PointF)p)];
    }

    private static PointF[] Offset(PointF[] points, float offset) =>
        offset == 0 ? points : [.. points.Select(p => new PointF(p.X + offset, p.Y + offset))];

    private static PathData Polyline(PointF[] points, bool closed)
    {
        var segments = new List<PathSegmentData>(points.Length - 1);
        for (var i = 1; i < points.Length; i++)
        {
            segments.Add(new LineSegmentData(points[i]));
        }

        return new PathData([new PathFigureData(points[0], segments, closed)], FillMode.Alternate);
    }

    private static PathData Pie(RectangleF rect, float startAngle, float sweepAngle)
    {
        using var path = new GraphicsPath();
        path.AddPie(Rectangle.Round(rect), startAngle, sweepAngle);
        return path.ToData();
    }
}
