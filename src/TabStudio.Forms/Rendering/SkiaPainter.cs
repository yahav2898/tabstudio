using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using SkiaSharp;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// Draws the shapes of a display list with Skia, the way GDI+ draws them: pens with their caps, joins, dashes and
/// widths in world units; solid, gradient, texture, hatch and path gradient brushes; clipping to rectangles and
/// paths; antialiasing on request; and <see cref="CompositingMode.SourceCopy"/>. Windows draw through it inside
/// Avalonia's renderer, and images draw through it when an app draws on a <see cref="System.Drawing.Bitmap"/>, so a
/// shape looks the same on both. Text and images are drawn by Avalonia, around it.
/// </summary>
internal static class SkiaPainter
{
    /// <summary>Draws shapes in one state on a canvas.</summary>
    /// <param name="canvas">The canvas, with the window's client area at its current origin.</param>
    /// <param name="state">The transform, clip and modes.</param>
    /// <param name="commands">The shapes: rectangles, lines, ellipses, paths and clears.</param>
    public static void Draw(SKCanvas canvas, StateCommand state, IReadOnlyList<DrawCommand> commands)
    {
        var saved = canvas.Save();
        try
        {
            Clip(canvas, state);
            if (state.PixelCenters)
            {
                // Just under half a pixel when aliased: Skia rounds an aliased edge at exactly half a pixel up.
                var shift = state.Antialias ? 0.5f : 0.49f;
                canvas.Translate(shift, shift);
            }

            canvas.Concat(ToSkia(state.Transform));
            foreach (var command in commands)
            {
                switch (command)
                {
                    case RectangleCommand rectangle:
                        DrawRectangle(canvas, state, rectangle);
                        break;
                    case LineCommand line:
                        using (var path = new SKPath())
                        {
                            path.MoveTo(line.From.X, line.From.Y);
                            path.LineTo(line.To.X, line.To.Y);
                            Stroke(canvas, state, path, line.Pen);
                        }

                        break;
                    case EllipseCommand ellipse:
                        using (var path = new SKPath())
                        {
                            path.AddOval(ToSkia(ellipse.Bounds));
                            Fill(canvas, state, path, ellipse.Fill);
                            Stroke(canvas, state, path, ellipse.Stroke);
                        }

                        break;
                    case PathCommand pathCommand:
                        using (var path = ToSkia(pathCommand.Path))
                        {
                            Fill(canvas, state, path, pathCommand.Fill);
                            Stroke(canvas, state, path, pathCommand.Stroke);
                        }

                        break;
                    case ClearCommand clear:
                        // Clear fills the clip, whatever the transform, and replaces the pixels, alpha included.
                        canvas.Clear(new SKColor(clear.Argb));
                        break;
                }
            }
        }
        finally
        {
            canvas.RestoreToCount(saved);
        }
    }

    /// <summary>Clips a canvas to a state's rectangle and paths, which are in the canvas's current coordinates.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="state">The state.</param>
    public static void Clip(SKCanvas canvas, StateCommand state)
    {
        canvas.ClipRect(ToSkia(state.Clip));
        if (state.ClipPaths is { } paths)
        {
            foreach (var clip in paths)
            {
                using var path = ToSkia(clip);
                canvas.ClipPath(path, SKClipOperation.Intersect, antialias: false);
            }
        }
    }

    /// <summary>A Skia matrix of a transform.</summary>
    /// <param name="m">The transform.</param>
    public static SKMatrix ToSkia(Matrix3x2 m) => new(m.M11, m.M21, m.M31, m.M12, m.M22, m.M32, 0, 0, 1);

    /// <summary>A Skia rectangle of a rectangle.</summary>
    /// <param name="r">The rectangle.</param>
    public static SKRect ToSkia(RectangleF r) => new(r.Left, r.Top, r.Right, r.Bottom);

    /// <summary>A Skia path of a path's figures.</summary>
    /// <param name="data">The path.</param>
    public static SKPath ToSkia(PathData data)
    {
        var path = new SKPath { FillType = data.FillMode == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd };
        foreach (var figure in data.Figures)
        {
            path.MoveTo(figure.Start.X, figure.Start.Y);
            foreach (var segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegmentData line:
                        path.LineTo(line.To.X, line.To.Y);
                        break;
                    case BezierSegmentData bezier:
                        path.CubicTo(bezier.Control1.X, bezier.Control1.Y, bezier.Control2.X, bezier.Control2.Y, bezier.To.X, bezier.To.Y);
                        break;
                }
            }

            if (figure.Closed)
            {
                path.Close();
            }
        }

        return path;
    }

    /// <summary>A Skia color of a 0xAARRGGBB value.</summary>
    /// <param name="argb">The value.</param>
    public static SKColor ToSkia(uint argb) => new(argb);

    private static void DrawRectangle(SKCanvas canvas, StateCommand state, RectangleCommand rectangle)
    {
        using var path = new SKPath();
        var rect = ToSkia(rectangle.Rectangle);
        if (rectangle.CornerRadius > 0)
        {
            path.AddRoundRect(rect, rectangle.CornerRadius, rectangle.CornerRadius);
        }
        else
        {
            path.AddRect(rect);
        }

        Fill(canvas, state, path, rectangle.Fill);
        Stroke(canvas, state, path, rectangle.Stroke);
    }

    private static void Fill(SKCanvas canvas, StateCommand state, SKPath path, BrushSpec? brush)
    {
        if (brush is null)
        {
            return;
        }

        using var paint = NewPaint(state, SKPaintStyle.Fill);
        Paint(canvas, state, path, paint, brush);
    }

    private static void Stroke(SKCanvas canvas, StateCommand state, SKPath path, PenSpec? pen)
    {
        if (pen is null)
        {
            return;
        }

        using var paint = NewPaint(state, SKPaintStyle.Stroke);
        var scale = MathF.Sqrt(MathF.Abs(state.Transform.GetDeterminant()));
        if (scale <= 0 || float.IsNaN(scale))
        {
            return;
        }

        // GDI+ draws a pen thinner than a pixel one pixel wide.
        var width = Math.Max(pen.Width, 1 / scale);
        paint.StrokeWidth = width;
        paint.StrokeJoin = pen.Join switch
        {
            LineJoin.Bevel => SKStrokeJoin.Bevel,
            LineJoin.Round => SKStrokeJoin.Round,
            _ => SKStrokeJoin.Miter,
        };
        paint.StrokeMiter = Math.Max(1, pen.MiterLimit);
        var pattern = Dashes(pen);
        paint.StrokeCap = pattern is not null
            ? pen.DashCap == DashCap.Round ? SKStrokeCap.Round : SKStrokeCap.Butt
            : pen.Cap switch
            {
                LineCap.Round => SKStrokeCap.Round,
                LineCap.Square => SKStrokeCap.Square,
                _ => SKStrokeCap.Butt,
            };
        if (pattern is not null)
        {
            var intervals = pattern.Length % 2 == 0 ? pattern : [.. pattern, .. pattern];
            paint.PathEffect = SKPathEffect.CreateDash([.. intervals.Select(v => Math.Max(v, 0.01f) * width)], pen.DashOffset * width);
        }

        Paint(canvas, state, path, paint, pen.Brush);
    }

    /// <summary>The dash pattern of a pen, in multiples of its width, or <see langword="null"/> for a solid line.</summary>
    private static float[]? Dashes(PenSpec pen) => pen.Dash switch
    {
        DashStyle.Dash => [3, 1],
        DashStyle.Dot => [1, 1],
        DashStyle.DashDot => [3, 1, 1, 1],
        DashStyle.DashDotDot => [3, 1, 1, 1, 1, 1],
        DashStyle.Custom when pen.DashPattern is { Length: > 0 } custom => custom,
        _ => null,
    };

    private static SKPaint NewPaint(StateCommand state, SKPaintStyle style) => new()
    {
        Style = style,
        IsAntialias = state.Antialias,
        BlendMode = state.SourceCopy ? SKBlendMode.Src : SKBlendMode.SrcOver,
    };

    /// <summary>Paints a filled or stroked path with a brush.</summary>
    private static void Paint(SKCanvas canvas, StateCommand state, SKPath path, SKPaint paint, BrushSpec brush)
    {
        switch (brush)
        {
            case SolidBrushSpec solid:
                paint.Color = ToSkia(solid.Argb);
                canvas.DrawPath(path, paint);
                break;
            case PathGradientBrushSpec gradient:
                // A path gradient paints only inside its own path.
                var saved = canvas.Save();
                using (var clip = ToSkia(gradient.Path))
                {
                    clip.Transform(ToSkia(gradient.Transform));
                    canvas.ClipPath(clip, SKClipOperation.Intersect, state.Antialias);
                    if (paint.Style == SKPaintStyle.Fill)
                    {
                        canvas.ClipPath(path, SKClipOperation.Intersect, state.Antialias);
                        paint.Color = SKColors.White;
                        var (positions, colors) = PathGradientMesh(gradient);
                        canvas.DrawVertices(SKVertexMode.Triangles, positions, colors, paint);
                    }
                    else
                    {
                        paint.Shader = Shader(state, gradient);
                        canvas.DrawPath(path, paint);
                    }
                }

                canvas.RestoreToCount(saved);
                break;
            default:
                paint.Color = SKColors.Black;
                paint.Shader = Shader(state, brush);
                canvas.DrawPath(path, paint);
                break;
        }
    }

    /// <summary>The shader of a brush that is not a solid color, in world coordinates.</summary>
    private static SKShader? Shader(StateCommand state, BrushSpec brush)
    {
        switch (brush)
        {
            case LinearGradientBrushSpec linear:
            {
                var (colors, positions) = Stops(linear.Stops, reverse: false);
                return SKShader.CreateLinearGradient(
                    new SKPoint(linear.Start.X, linear.Start.Y),
                    new SKPoint(linear.End.X, linear.End.Y),
                    colors,
                    positions,
                    linear.Wrap switch
                    {
                        WrapMode.Clamp => SKShaderTileMode.Clamp,
                        WrapMode.Tile => SKShaderTileMode.Repeat,
                        _ => SKShaderTileMode.Mirror,
                    },
                    ToSkia(linear.Transform));
            }

            case PathGradientBrushSpec radial:
            {
                // Edge color at offset 0 in GDI+; Skia's radial gradients start at the center.
                var (colors, positions) = Stops(radial.Stops, reverse: true);
                var bounds = radial.Bounds;
                var rx = Math.Max(0.5f, (bounds.Width / 2) + Math.Abs(radial.Center.X - (bounds.X + (bounds.Width / 2))));
                var ry = Math.Max(0.5f, (bounds.Height / 2) + Math.Abs(radial.Center.Y - (bounds.Y + (bounds.Height / 2))));
                var local = SKMatrix.CreateScale(rx, ry).PostConcat(SKMatrix.CreateTranslation(radial.Center.X, radial.Center.Y)).PostConcat(ToSkia(radial.Transform));
                return SKShader.CreateRadialGradient(new SKPoint(0, 0), 1, colors, positions, SKShaderTileMode.Clamp, local);
            }

            case TextureBrushSpec texture:
            {
                var image = texture.Image.SkiaImage;
                if (texture.Source is { } source)
                {
                    var subset = SKRectI.Round(ToSkia(source));
                    subset.Intersect(new SKRectI(0, 0, image.Width, image.Height));
                    if (!subset.IsEmpty)
                    {
                        image = image.Subset(subset) ?? image;
                    }
                }

                var (x, y) = texture.Wrap switch
                {
                    WrapMode.Clamp => (SKShaderTileMode.Decal, SKShaderTileMode.Decal),
                    WrapMode.TileFlipX => (SKShaderTileMode.Mirror, SKShaderTileMode.Repeat),
                    WrapMode.TileFlipY => (SKShaderTileMode.Repeat, SKShaderTileMode.Mirror),
                    WrapMode.TileFlipXY => (SKShaderTileMode.Mirror, SKShaderTileMode.Mirror),
                    _ => (SKShaderTileMode.Repeat, SKShaderTileMode.Repeat),
                };
                var transform = texture.Transform;
                var pixelExact = transform.M11 == 1 && transform.M22 == 1 && transform.M12 == 0 && transform.M21 == 0
                    && state.Transform.M11 == 1 && state.Transform.M22 == 1 && state.Transform.M12 == 0 && state.Transform.M21 == 0;
                var sampling = pixelExact ? new SKSamplingOptions(SKFilterMode.Nearest) : new SKSamplingOptions(SKFilterMode.Linear);

                // With pixel centers at whole coordinates, a texel's center is too.
                var local = state.PixelCenters ? ToSkia(transform).PreConcat(SKMatrix.CreateTranslation(-0.5f, -0.5f)) : ToSkia(transform);
                return SKShader.CreateImage(image, x, y, sampling, local);
            }

            case HatchBrushSpec hatch:
            {
                // Hatch patterns are fixed to the surface's pixels, whatever the world transform.
                var pattern = HatchPatterns.Image(hatch.Style, hatch.ForeArgb, hatch.BackArgb);
                var local = Matrix3x2.Invert(state.Transform, out var inverse) ? ToSkia(inverse) : SKMatrix.Identity;
                if (state.PixelCenters)
                {
                    local = local.PreConcat(SKMatrix.CreateTranslation(-0.5f, -0.5f));
                }

                return SKShader.CreateImage(pattern, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, new SKSamplingOptions(SKFilterMode.Nearest), local);
            }

            case SolidBrushSpec solid:
                return SKShader.CreateColor(ToSkia(solid.Argb));
            default:
                return null;
        }
    }

    /// <summary>
    /// The triangles of a path gradient: for each edge of the path, a fan of bands from the edge in to the center,
    /// one per pair of neighboring colors, each colored at its corners, as GDI+ blends a path gradient.
    /// </summary>
    private static (SKPoint[] Positions, SKColor[] Colors) PathGradientMesh(PathGradientBrushSpec gradient)
    {
        var positions = new List<SKPoint>();
        var colors = new List<SKColor>();
        var transform = gradient.Transform;
        var center = System.Numerics.Vector2.Transform(new System.Numerics.Vector2(gradient.Center.X, gradient.Center.Y), transform);
        var stops = gradient.Stops.OrderBy(stop => stop.Offset).ToList();
        if (stops[0].Offset > 0)
        {
            stops.Insert(0, stops[0] with { Offset = 0 });
        }

        if (stops[^1].Offset < 1)
        {
            stops.Add(stops[^1] with { Offset = 1 });
        }

        foreach (var figure in gradient.Path.Figures)
        {
            var edge = GeometryMath.Flatten(figure).Select(p => System.Numerics.Vector2.Transform(new System.Numerics.Vector2(p.X, p.Y), transform)).ToList();
            if (edge.Count > 1 && edge[0] == edge[^1])
            {
                edge.RemoveAt(edge.Count - 1);
            }

            // Per-point surround colors apply to a polygon's own points, before flattening adds any.
            var perPoint = gradient.SurroundArgb is { } surround && surround.Count >= figure.Segments.Count && figure.Segments.All(s => s is LineSegmentData)
                ? surround
                : null;
            for (var i = 0; i < edge.Count; i++)
            {
                var a = edge[i];
                var b = edge[(i + 1) % edge.Count];
                for (var band = 0; band < stops.Count - 1; band++)
                {
                    var (outer, inner) = (stops[band], stops[band + 1]);
                    var a1 = System.Numerics.Vector2.Lerp(a, center, outer.Offset);
                    var b1 = System.Numerics.Vector2.Lerp(b, center, outer.Offset);
                    var a2 = System.Numerics.Vector2.Lerp(a, center, inner.Offset);
                    var b2 = System.Numerics.Vector2.Lerp(b, center, inner.Offset);
                    var (ca1, cb1) = band == 0 && perPoint is not null
                        ? (ToSkia(perPoint[Math.Min(i, perPoint.Count - 1)]), ToSkia(perPoint[Math.Min((i + 1) % edge.Count, perPoint.Count - 1)]))
                        : (ToSkia(outer.Argb), ToSkia(outer.Argb));
                    var c2 = ToSkia(inner.Argb);
                    Triangle(positions, colors, (a1, ca1), (b1, cb1), (b2, c2));
                    Triangle(positions, colors, (a1, ca1), (b2, c2), (a2, c2));
                }
            }
        }

        return ([.. positions], [.. colors]);
    }

    private static void Triangle(List<SKPoint> positions, List<SKColor> colors, (System.Numerics.Vector2 P, SKColor C) a, (System.Numerics.Vector2 P, SKColor C) b, (System.Numerics.Vector2 P, SKColor C) c)
    {
        positions.Add(new SKPoint(a.P.X, a.P.Y));
        positions.Add(new SKPoint(b.P.X, b.P.Y));
        positions.Add(new SKPoint(c.P.X, c.P.Y));
        colors.Add(a.C);
        colors.Add(b.C);
        colors.Add(c.C);
    }

    private static (SKColor[] Colors, float[] Positions) Stops(IReadOnlyList<GradientStopSpec> stops, bool reverse)
    {
        var count = stops.Count;
        var colors = new SKColor[count];
        var positions = new float[count];
        for (var i = 0; i < count; i++)
        {
            var stop = reverse ? stops[count - 1 - i] : stops[i];
            colors[i] = ToSkia(stop.Argb);
            positions[i] = Math.Clamp(reverse ? 1 - stop.Offset : stop.Offset, 0, 1);
        }

        return (colors, positions);
    }
}
