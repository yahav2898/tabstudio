using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace TabStudio.Forms.Rendering;

/// <summary>A brush, as a display list records it: free of Avalonia, immutable, safe to hand to another thread.</summary>
internal abstract record BrushSpec
{
    /// <summary>A representative color, for text drawn with a brush that is not solid.</summary>
    public abstract uint MainColor { get; }
}

/// <summary>A solid color.</summary>
/// <param name="Argb">The color as 0xAARRGGBB.</param>
internal sealed record SolidBrushSpec(uint Argb) : BrushSpec
{
    /// <inheritdoc />
    public override uint MainColor => Argb;
}

/// <summary>A two-color linear gradient between two points, in the coordinates the shape is drawn in.</summary>
internal sealed record LinearGradientBrushSpec(PointF Start, PointF End, uint StartArgb, uint EndArgb) : BrushSpec
{
    /// <inheritdoc />
    public override uint MainColor => StartArgb;
}

/// <summary>A pen, as a display list records it.</summary>
/// <param name="Brush">What the stroke is painted with.</param>
/// <param name="Width">The stroke width.</param>
/// <param name="Dash">The dash style.</param>
/// <param name="DashPattern">The dashes and gaps of a custom dash style, in multiples of the width.</param>
/// <param name="Cap">The line caps.</param>
/// <param name="Join">How segments are joined.</param>
internal sealed record PenSpec(BrushSpec Brush, float Width, DashStyle Dash, float[]? DashPattern, LineCap Cap, LineJoin Join);

/// <summary>Where and how text is laid out in its rectangle.</summary>
/// <param name="Horizontal">The horizontal alignment.</param>
/// <param name="Vertical">The vertical alignment.</param>
/// <param name="Wrap">Whether lines wrap at the rectangle's width.</param>
/// <param name="Ellipsis">Whether text that does not fit ends with an ellipsis.</param>
/// <param name="Clip">Whether text is clipped to the rectangle.</param>
internal readonly record struct TextOptions(StringAlignment Horizontal, StringAlignment Vertical, bool Wrap, bool Ellipsis, bool Clip)
{
    /// <summary>Left and top aligned, on one line, unclipped: text drawn at a point.</summary>
    public static TextOptions AtPoint { get; } = new(StringAlignment.Near, StringAlignment.Near, Wrap: false, Ellipsis: false, Clip: false);
}

/// <summary>One drawing operation of a display list.</summary>
internal abstract record DrawCommand;

/// <summary>
/// Sets the transform, clip and edge mode for the commands that follow. The values are absolute, so the renderer
/// replaces the previous state rather than nesting it.
/// </summary>
/// <param name="Transform">From the coordinates of the following commands to the window's client area.</param>
/// <param name="Clip">The clip rectangle, in the window's client area.</param>
/// <param name="Antialias">Whether shapes are antialiased.</param>
internal sealed record StateCommand(Matrix3x2 Transform, RectangleF Clip, bool Antialias) : DrawCommand;

/// <summary>A rectangle, filled, outlined or both, optionally with rounded corners.</summary>
internal sealed record RectangleCommand(RectangleF Rectangle, BrushSpec? Fill, PenSpec? Stroke, float CornerRadius = 0) : DrawCommand;

/// <summary>A straight line.</summary>
internal sealed record LineCommand(PointF From, PointF To, PenSpec Pen) : DrawCommand;

/// <summary>An ellipse in a bounding rectangle, filled, outlined or both.</summary>
internal sealed record EllipseCommand(RectangleF Bounds, BrushSpec? Fill, PenSpec? Stroke) : DrawCommand;

/// <summary>A path of lines and Bézier curves, filled, outlined or both.</summary>
internal sealed record PathCommand(PathData Path, BrushSpec? Fill, PenSpec? Stroke) : DrawCommand;

/// <summary>Text in a layout rectangle.</summary>
/// <param name="Text">The text, with line breaks.</param>
/// <param name="Font">The font.</param>
/// <param name="Argb">The color as 0xAARRGGBB.</param>
/// <param name="Bounds">The layout rectangle; an infinite width or height does not limit the text.</param>
/// <param name="Options">Alignment, wrapping, trimming and clipping.</param>
internal sealed record TextCommand(string Text, FontSpec Font, uint Argb, RectangleF Bounds, TextOptions Options) : DrawCommand;

/// <summary>An image, or part of one, drawn into a rectangle.</summary>
/// <param name="Image">The pixels.</param>
/// <param name="Destination">Where the image is drawn.</param>
/// <param name="Source">The part of the image drawn, in pixels, or <see langword="null"/> for all of it.</param>
/// <param name="Smooth">Whether scaling smooths pixels rather than keeping their hard edges.</param>
internal sealed record ImageCommand(ImageSnapshot Image, RectangleF Destination, RectangleF? Source, bool Smooth) : DrawCommand;

/// <summary>The figures of a path: lines and cubic Bézier curves, as GDI+ itself stores arcs and curves.</summary>
/// <param name="Figures">The figures.</param>
/// <param name="FillMode">How overlapping figures are filled.</param>
internal sealed record PathData(IReadOnlyList<PathFigureData> Figures, FillMode FillMode);

/// <summary>One figure of a path: a start point and the segments that follow it.</summary>
/// <param name="Start">The first point.</param>
/// <param name="Segments">The segments, each ending at its last point.</param>
/// <param name="Closed">Whether the figure is closed back to its start.</param>
internal sealed record PathFigureData(PointF Start, IReadOnlyList<PathSegmentData> Segments, bool Closed);

/// <summary>A segment of a path figure.</summary>
internal abstract record PathSegmentData;

/// <summary>A straight line to a point.</summary>
internal sealed record LineSegmentData(PointF To) : PathSegmentData;

/// <summary>A cubic Bézier curve to a point.</summary>
internal sealed record BezierSegmentData(PointF Control1, PointF Control2, PointF To) : PathSegmentData;
