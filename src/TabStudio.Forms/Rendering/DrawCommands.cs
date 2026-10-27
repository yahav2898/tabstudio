using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace TabStudio.Forms.Rendering;

/// <summary>A brush, as a display list records it: free of Avalonia, immutable, safe to hand to another thread.</summary>
internal abstract record BrushSpec
{
    /// <summary>A representative color, for places that can only draw one color.</summary>
    public abstract uint MainColor { get; }
}

/// <summary>A solid color.</summary>
/// <param name="Argb">The color as 0xAARRGGBB.</param>
internal sealed record SolidBrushSpec(uint Argb) : BrushSpec
{
    /// <inheritdoc />
    public override uint MainColor => Argb;
}

/// <summary>A color at a position of a gradient.</summary>
/// <param name="Offset">Where the color is, from 0 at the start to 1 at the end.</param>
/// <param name="Argb">The color as 0xAARRGGBB.</param>
internal readonly record struct GradientStopSpec(float Offset, uint Argb);

/// <summary>A linear gradient between two points, in the coordinates the shape is drawn in.</summary>
/// <param name="Start">Where the first color is.</param>
/// <param name="End">Where the last color is.</param>
/// <param name="Stops">The colors, at least two, in order of their offsets.</param>
/// <param name="Wrap">How the gradient repeats beyond its two points.</param>
/// <param name="Transform">The brush's own transformation, applied before the surface's.</param>
internal sealed record LinearGradientBrushSpec(PointF Start, PointF End, IReadOnlyList<GradientStopSpec> Stops, WrapMode Wrap, Matrix3x2 Transform) : BrushSpec
{
    /// <summary>A two-color gradient that tiles, as GDI+ draws a new <see cref="System.Drawing.Drawing2D.LinearGradientBrush"/>.</summary>
    public LinearGradientBrushSpec(PointF start, PointF end, uint startArgb, uint endArgb)
        : this(start, end, [new GradientStopSpec(0, startArgb), new GradientStopSpec(1, endArgb)], WrapMode.Tile, Matrix3x2.Identity)
    {
    }

    /// <summary>The first color.</summary>
    public uint StartArgb => Stops[0].Argb;

    /// <summary>The last color.</summary>
    public uint EndArgb => Stops[^1].Argb;

    /// <inheritdoc />
    public override uint MainColor => StartArgb;
}

/// <summary>An image that tiles, or is drawn once, as <see cref="System.Drawing.TextureBrush"/> paints.</summary>
/// <param name="Image">The pixels.</param>
/// <param name="Source">The part of the image used, in pixels, or <see langword="null"/> for all of it.</param>
/// <param name="Wrap">How the image repeats; <see cref="WrapMode.Clamp"/> paints it once.</param>
/// <param name="Transform">The brush's own transformation, applied before the surface's.</param>
internal sealed record TextureBrushSpec(ImageSnapshot Image, RectangleF? Source, WrapMode Wrap, Matrix3x2 Transform) : BrushSpec
{
    /// <inheritdoc />
    public override uint MainColor => 0xFF808080;
}

/// <summary>An 8 by 8 pattern of two colors, fixed to the surface's pixels, as <see cref="HatchBrush"/> paints.</summary>
/// <param name="Style">The pattern.</param>
/// <param name="ForeArgb">The color of the lines.</param>
/// <param name="BackArgb">The color between them.</param>
internal sealed record HatchBrushSpec(HatchStyle Style, uint ForeArgb, uint BackArgb) : BrushSpec
{
    /// <inheritdoc />
    public override uint MainColor => ForeArgb;
}

/// <summary>
/// A gradient from a center color out to the edge of a path, as <see cref="PathGradientBrush"/> paints: it paints
/// only inside its path, and is drawn as a radial gradient filling the path's bounds.
/// </summary>
/// <param name="Bounds">The bounds of the brush's path.</param>
/// <param name="Path">The brush's path: nothing outside it is painted.</param>
/// <param name="Center">Where the center color is.</param>
/// <param name="Stops">The colors from the edge (offset 0) to the center (offset 1).</param>
/// <param name="Transform">The brush's own transformation, applied before the surface's.</param>
internal sealed record PathGradientBrushSpec(RectangleF Bounds, PathData Path, PointF Center, IReadOnlyList<GradientStopSpec> Stops, Matrix3x2 Transform) : BrushSpec
{
    /// <inheritdoc />
    public override uint MainColor => Stops[^1].Argb;
}

/// <summary>A pen, as a display list records it.</summary>
/// <param name="Brush">What the stroke is painted with.</param>
/// <param name="Width">The stroke width.</param>
/// <param name="Dash">The dash style.</param>
/// <param name="DashPattern">The dashes and gaps of a custom dash style, in multiples of the width.</param>
/// <param name="Cap">The line caps at both ends: flat, square or round. Other caps are drawn as shapes.</param>
/// <param name="Join">How segments are joined.</param>
/// <param name="DashOffset">How far into the dash pattern the line starts, in multiples of the width.</param>
/// <param name="DashCap">The caps of each dash.</param>
/// <param name="MiterLimit">How far a mitered join may reach, in multiples of the width.</param>
internal sealed record PenSpec(
    BrushSpec Brush,
    float Width,
    DashStyle Dash,
    float[]? DashPattern,
    LineCap Cap,
    LineJoin Join,
    float DashOffset = 0,
    DashCap DashCap = DashCap.Flat,
    float MiterLimit = 10);

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
internal sealed record StateCommand(Matrix3x2 Transform, RectangleF Clip, bool Antialias) : DrawCommand
{
    /// <summary>Shapes the drawing is further clipped to, in the window's client area, or <see langword="null"/>.</summary>
    public IReadOnlyList<PathData>? ClipPaths { get; init; }

    /// <summary>Whether shapes and images replace the pixels under them, as <see cref="CompositingMode.SourceCopy"/> asks.</summary>
    public bool SourceCopy { get; init; }
}

/// <summary>A rectangle, filled, outlined or both, optionally with rounded corners.</summary>
internal sealed record RectangleCommand(RectangleF Rectangle, BrushSpec? Fill, PenSpec? Stroke, float CornerRadius = 0) : DrawCommand;

/// <summary>A straight line.</summary>
internal sealed record LineCommand(PointF From, PointF To, PenSpec Pen) : DrawCommand;

/// <summary>An ellipse in a bounding rectangle, filled, outlined or both.</summary>
internal sealed record EllipseCommand(RectangleF Bounds, BrushSpec? Fill, PenSpec? Stroke) : DrawCommand;

/// <summary>A path of lines and Bézier curves, filled, outlined or both.</summary>
internal sealed record PathCommand(PathData Path, BrushSpec? Fill, PenSpec? Stroke) : DrawCommand;

/// <summary>
/// Sets every pixel in the clip to a color, transparency included, as <see cref="Graphics.Clear"/> does on an image.
/// </summary>
/// <param name="Argb">The color as 0xAARRGGBB.</param>
internal sealed record ClearCommand(uint Argb) : DrawCommand;

/// <summary>Text in a layout rectangle.</summary>
/// <param name="Text">The text, with line breaks.</param>
/// <param name="Font">The font.</param>
/// <param name="Argb">The color as 0xAARRGGBB.</param>
/// <param name="Bounds">The layout rectangle; an infinite width or height does not limit the text.</param>
/// <param name="Options">Alignment, wrapping, trimming and clipping.</param>
internal sealed record TextCommand(string Text, FontSpec Font, uint Argb, RectangleF Bounds, TextOptions Options) : DrawCommand
{
    /// <summary>The brush the text is painted with when it is not a solid color, or <see langword="null"/>.</summary>
    public BrushSpec? Brush { get; init; }
}

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
