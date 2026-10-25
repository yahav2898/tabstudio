using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaSize = Avalonia.Size;
using DashStyle = System.Drawing.Drawing2D.DashStyle;
using DrawingPoint = System.Drawing.PointF;
using FillMode = System.Drawing.Drawing2D.FillMode;
using LineCap = System.Drawing.Drawing2D.LineCap;
using LineJoin = System.Drawing.Drawing2D.LineJoin;
using RectangleF = System.Drawing.RectangleF;
using StringAlignment = System.Drawing.StringAlignment;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// Draws display lists with Avalonia, on the UI thread. Brushes and pens of solid colors are cached, since the same
/// few colors are drawn again and again.
/// </summary>
internal sealed class DisplayListRenderer
{
    private const int CacheLimit = 256;

    private readonly Dictionary<uint, ImmutableSolidColorBrush> _brushes = [];
    private readonly Dictionary<PenSpec, ImmutablePen> _pens = [];

    /// <summary>Draws a display list.</summary>
    /// <param name="context">The drawing context, with the window's client area at its origin.</param>
    /// <param name="list">The display list.</param>
    public void Render(DrawingContext context, DisplayList list)
    {
        var pushed = new Stack<DrawingContext.PushedState>();
        try
        {
            foreach (var command in list.Commands)
            {
                switch (command)
                {
                    case StateCommand state:
                        while (pushed.Count > 0)
                        {
                            pushed.Pop().Dispose();
                        }

                        pushed.Push(context.PushClip(ToRect(state.Clip)));
                        pushed.Push(context.PushTransform(new Matrix(state.Transform.M11, state.Transform.M12, state.Transform.M21, state.Transform.M22, state.Transform.M31, state.Transform.M32)));
                        pushed.Push(context.PushRenderOptions(new RenderOptions { EdgeMode = state.Antialias ? EdgeMode.Antialias : EdgeMode.Aliased }));
                        break;
                    case RectangleCommand rectangle:
                        context.DrawRectangle(ToBrush(rectangle.Fill), ToPen(rectangle.Stroke), ToRect(rectangle.Rectangle), rectangle.CornerRadius, rectangle.CornerRadius);
                        break;
                    case LineCommand line:
                        if (ToPen(line.Pen) is { } linePen)
                        {
                            context.DrawLine(linePen, ToPoint(line.From), ToPoint(line.To));
                        }

                        break;
                    case EllipseCommand ellipse:
                        context.DrawEllipse(ToBrush(ellipse.Fill), ToPen(ellipse.Stroke), ToRect(ellipse.Bounds));
                        break;
                    case PathCommand path:
                        DrawPath(context, path);
                        break;
                    case TextCommand text:
                        DrawText(context, text);
                        break;
                    case ImageCommand image:
                        DrawImage(context, image);
                        break;
                }
            }
        }
        finally
        {
            while (pushed.Count > 0)
            {
                pushed.Pop().Dispose();
            }
        }
    }

    private static Rect ToRect(RectangleF rect) => new(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));

    private static AvaloniaPoint ToPoint(DrawingPoint point) => new(point.X, point.Y);

    private static Color ToColor(uint argb) => Color.FromUInt32(argb);

    private static void DrawImage(DrawingContext context, ImageCommand image)
    {
        var bitmap = image.Image.Bitmap;
        var source = image.Source is { } part ? ToRect(part) : new Rect(0, 0, image.Image.Width, image.Image.Height);
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = image.Smooth ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None }))
        {
            // Avalonia measures a bitmap's source rectangle in device-independent pixels at its own DPI; the images
            // here are at 96 DPI, so pixels and units are the same.
            context.DrawImage(bitmap, source, ToRect(image.Destination));
        }
    }

    private static void DrawText(DrawingContext context, TextCommand text)
    {
        var bounds = text.Bounds;
        var options = text.Options;
        var width = float.IsPositiveInfinity(bounds.Width) ? double.PositiveInfinity : bounds.Width;
        var height = float.IsPositiveInfinity(bounds.Height) ? double.PositiveInfinity : bounds.Height;
        var alignment = double.IsPositiveInfinity(width)
            ? TextAlignment.Left
            : options.Horizontal switch
            {
                StringAlignment.Center => TextAlignment.Center,
                StringAlignment.Far => TextAlignment.Right,
                _ => TextAlignment.Left,
            };
        using var layout = TextMetrics.CreateLayout(
            text.Text,
            text.Font,
            new ImmutableSolidColorBrush(ToColor(text.Argb)),
            width,
            options.Clip ? height : double.PositiveInfinity,
            alignment,
            options.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            options.Ellipsis);

        var top = bounds.Y + (double.IsPositiveInfinity(height)
            ? 0
            : options.Vertical switch
            {
                StringAlignment.Center => (height - layout.Height) / 2,
                StringAlignment.Far => height - layout.Height,
                _ => 0,
            });

        if (options.Clip && !double.IsPositiveInfinity(width) && !double.IsPositiveInfinity(height))
        {
            using (context.PushClip(new Rect(bounds.X, bounds.Y, width, height)))
            {
                layout.Draw(context, new AvaloniaPoint(bounds.X, top));
            }
        }
        else
        {
            layout.Draw(context, new AvaloniaPoint(bounds.X, top));
        }
    }

    private void DrawPath(DrawingContext context, PathCommand path)
    {
        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            sink.SetFillRule(path.Path.FillMode == FillMode.Winding ? FillRule.NonZero : FillRule.EvenOdd);
            foreach (var figure in path.Path.Figures)
            {
                sink.BeginFigure(ToPoint(figure.Start), isFilled: path.Fill is not null);
                foreach (var segment in figure.Segments)
                {
                    switch (segment)
                    {
                        case LineSegmentData line:
                            sink.LineTo(ToPoint(line.To));
                            break;
                        case BezierSegmentData bezier:
                            sink.CubicBezierTo(ToPoint(bezier.Control1), ToPoint(bezier.Control2), ToPoint(bezier.To));
                            break;
                    }
                }

                sink.EndFigure(figure.Closed);
            }
        }

        context.DrawGeometry(ToBrush(path.Fill), ToPen(path.Stroke), geometry);
    }

    private IBrush? ToBrush(BrushSpec? spec) => spec switch
    {
        SolidBrushSpec solid => ToSolidBrush(solid.Argb),
        LinearGradientBrushSpec gradient => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(gradient.Start.X, gradient.Start.Y, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(gradient.End.X, gradient.End.Y, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(ToColor(gradient.StartArgb), 0),
                new GradientStop(ToColor(gradient.EndArgb), 1),
            },
        },
        _ => null,
    };

    private ImmutableSolidColorBrush ToSolidBrush(uint argb)
    {
        if (!_brushes.TryGetValue(argb, out var brush))
        {
            if (_brushes.Count >= CacheLimit)
            {
                _brushes.Clear();
            }

            brush = new ImmutableSolidColorBrush(ToColor(argb));
            _brushes[argb] = brush;
        }

        return brush;
    }

    private ImmutablePen? ToPen(PenSpec? spec)
    {
        if (spec is null)
        {
            return null;
        }

        if (spec.Brush is SolidBrushSpec && _pens.TryGetValue(spec, out var cached))
        {
            return cached;
        }

        var width = spec.Width <= 0 ? 1 : spec.Width;
        var dash = spec.Dash switch
        {
            DashStyle.Dash => new ImmutableDashStyle([3, 1], 0),
            DashStyle.Dot => new ImmutableDashStyle([1, 1], 0),
            DashStyle.DashDot => new ImmutableDashStyle([3, 1, 1, 1], 0),
            DashStyle.DashDotDot => new ImmutableDashStyle([3, 1, 1, 1, 1, 1], 0),
            DashStyle.Custom when spec.DashPattern is { Length: > 0 } pattern => new ImmutableDashStyle([.. pattern.Select(v => (double)v)], 0),
            _ => null,
        };
        var cap = spec.Cap switch
        {
            LineCap.Round or LineCap.RoundAnchor => PenLineCap.Round,
            LineCap.Square or LineCap.SquareAnchor => PenLineCap.Square,
            _ => PenLineCap.Flat,
        };
        var join = spec.Join switch
        {
            LineJoin.Bevel => PenLineJoin.Bevel,
            LineJoin.Round => PenLineJoin.Round,
            _ => PenLineJoin.Miter,
        };
        var brush = ToBrush(spec.Brush) switch
        {
            IImmutableBrush immutable => immutable,
            LinearGradientBrush gradient => gradient.ToImmutable(),
            _ => null,
        };
        var pen = new ImmutablePen(brush, width, dash, cap, join);
        if (spec.Brush is SolidBrushSpec)
        {
            if (_pens.Count >= CacheLimit)
            {
                _pens.Clear();
            }

            _pens[spec] = pen;
        }

        return pen;
    }

    /// <summary>The size of laid-out text, for tests.</summary>
    internal static AvaloniaSize Measure(TextCommand text) => TextMetrics.Measure(text.Text, text.Font);
}
