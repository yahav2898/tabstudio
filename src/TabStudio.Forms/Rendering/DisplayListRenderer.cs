using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaSize = Avalonia.Size;
using FillMode = System.Drawing.Drawing2D.FillMode;
using RectangleF = System.Drawing.RectangleF;
using StringAlignment = System.Drawing.StringAlignment;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// Draws display lists: shapes with <see cref="SkiaPainter"/>, in Avalonia custom draw operations, and text and
/// images with Avalonia. Windows are drawn on the UI thread; a <see cref="System.Drawing.Bitmap"/> is drawn on its
/// app's Forms thread, which may create no Avalonia objects, so that path uses immutable brushes only.
/// </summary>
internal sealed class DisplayListRenderer
{
    private const int CacheLimit = 256;

    private readonly Dictionary<uint, ImmutableSolidColorBrush> _brushes = [];

    /// <summary>Draws a display list of a window, on the UI thread.</summary>
    /// <param name="context">The drawing context, with the window's client area at its origin.</param>
    /// <param name="list">The display list.</param>
    public void Render(DrawingContext context, DisplayList list) => Render(context, list, onImage: false);

    /// <summary>Draws a display list.</summary>
    /// <param name="context">The drawing context, with the client area at its origin.</param>
    /// <param name="list">The display list.</param>
    /// <param name="onImage">
    /// Whether the context draws on an image, immediately and off the UI thread: clipping to paths then goes through
    /// the canvas, and a failure is thrown rather than logged.
    /// </param>
    public void Render(DrawingContext context, DisplayList list, bool onImage)
    {
        var walk = new Walk(this, context, onImage);
        try
        {
            foreach (var command in list.Commands)
            {
                walk.Add(command);
            }

            walk.End();
        }
        finally
        {
            walk.PopAvalonia();
        }

        if (walk.Error is { } error)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    /// <summary>The size of laid-out text, for tests.</summary>
    internal static AvaloniaSize Measure(TextCommand text) => TextMetrics.Measure(text.Text, text.Font);

    /// <summary>An immutable Avalonia brush of a brush spec, which any thread may create and use.</summary>
    /// <param name="spec">The brush.</param>
    internal static IImmutableBrush ToImmutableBrush(BrushSpec spec) => spec switch
    {
        SolidBrushSpec solid => new ImmutableSolidColorBrush(Color.FromUInt32(solid.Argb)),
        LinearGradientBrushSpec gradient => new ImmutableLinearGradientBrush(
            [.. gradient.Stops.Select(stop => new ImmutableGradientStop(stop.Offset, Color.FromUInt32(stop.Argb)))],
            transform: gradient.Transform.IsIdentity ? null : new ImmutableTransform(ToMatrix(gradient.Transform)),
            transformOrigin: RelativePoint.TopLeft,
            spreadMethod: gradient.Wrap switch
            {
                System.Drawing.Drawing2D.WrapMode.Clamp => GradientSpreadMethod.Pad,
                System.Drawing.Drawing2D.WrapMode.Tile => GradientSpreadMethod.Repeat,
                _ => GradientSpreadMethod.Reflect,
            },
            startPoint: new RelativePoint(gradient.Start.X, gradient.Start.Y, RelativeUnit.Absolute),
            endPoint: new RelativePoint(gradient.End.X, gradient.End.Y, RelativeUnit.Absolute)),
        _ => new ImmutableSolidColorBrush(Color.FromUInt32(spec.MainColor)),
    };

    private static Matrix ToMatrix(System.Numerics.Matrix3x2 m) => new(m.M11, m.M12, m.M21, m.M22, m.M31, m.M32);

    private static Rect ToRect(RectangleF rect) => new(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));

    private static void DrawImage(DrawingContext context, ImageCommand image)
    {
        var bitmap = image.Image.Bitmap;
        var source = image.Source is { } part ? ToRect(part) : new Rect(0, 0, image.Image.Width, image.Image.Height);

        // Avalonia measures a bitmap's source rectangle in device-independent pixels at its own DPI; the images
        // here are at 96 DPI, so pixels and units are the same.
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = image.Smooth ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None }))
        {
            context.DrawImage(bitmap, source, ToRect(image.Destination));
        }
    }

    private void DrawText(DrawingContext context, TextCommand text)
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
        IBrush brush = text.Brush is { } spec and not SolidBrushSpec ? ToImmutableBrush(spec) : ToSolidBrush(text.Argb);
        using var layout = TextMetrics.CreateLayout(
            text.Text,
            text.Font,
            brush,
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

    private ImmutableSolidColorBrush ToSolidBrush(uint argb)
    {
        lock (_brushes)
        {
            if (!_brushes.TryGetValue(argb, out var brush))
            {
                if (_brushes.Count >= CacheLimit)
                {
                    _brushes.Clear();
                }

                brush = new ImmutableSolidColorBrush(Color.FromUInt32(argb));
                _brushes[argb] = brush;
            }

            return brush;
        }
    }

    /// <summary>One pass over a display list: shapes are batched per state, text and images drawn in between.</summary>
    private sealed class Walk(DisplayListRenderer renderer, DrawingContext context, bool onImage)
    {
        private readonly List<DrawCommand> _shapes = [];
        private readonly Stack<IDisposable> _pushed = new();
        private StateCommand? _state;

        public Exception? Error { get; private set; }

        public void Add(DrawCommand command)
        {
            switch (command)
            {
                case StateCommand state:
                    FlushShapes();
                    PopAvalonia();
                    _state = state;
                    break;
                case TextCommand text:
                    FlushShapes();
                    PushAvalonia();
                    renderer.DrawText(context, text);
                    break;
                case ImageCommand image:
                    FlushShapes();
                    PushAvalonia();
                    DrawImage(context, image);
                    break;
                default:
                    if (_state is not null)
                    {
                        PopAvalonia();
                        _shapes.Add(command);
                    }

                    break;
            }
        }

        public void End() => FlushShapes();

        public void PopAvalonia()
        {
            while (_pushed.Count > 0)
            {
                _pushed.Pop().Dispose();
            }
        }

        private void FlushShapes()
        {
            if (_shapes.Count == 0 || _state is not { } state)
            {
                return;
            }

            context.Custom(new ShapeBatch(state, [.. _shapes], this));
            _shapes.Clear();
        }

        private void PushAvalonia()
        {
            if (_pushed.Count > 0 || _state is not { } state)
            {
                return;
            }

            _pushed.Push(context.PushClip(ToRect(state.Clip)));
            if (state.ClipPaths is { Count: > 0 } paths)
            {
                if (onImage)
                {
                    // No Avalonia geometry may be made off the UI thread: clip the canvas itself, until popped.
                    context.Custom(new CanvasClip(state, restore: false, this));
                    _pushed.Push(new Restore(context, state, this));
                }
                else
                {
                    foreach (var path in paths)
                    {
                        _pushed.Push(context.PushGeometryClip(ToGeometry(path)));
                    }
                }
            }

            var t = state.Transform;
            _pushed.Push(context.PushTransform(new Matrix(t.M11, t.M12, t.M21, t.M22, t.M31, t.M32)));
            _pushed.Push(context.PushRenderOptions(new RenderOptions
            {
                EdgeMode = state.Antialias ? EdgeMode.Antialias : EdgeMode.Aliased,
                BitmapBlendingMode = state.SourceCopy ? BitmapBlendingMode.Source : BitmapBlendingMode.SourceOver,
            }));
        }

        private static StreamGeometry ToGeometry(PathData path)
        {
            var geometry = new StreamGeometry();
            using var sink = geometry.Open();
            sink.SetFillRule(path.FillMode == FillMode.Winding ? FillRule.NonZero : FillRule.EvenOdd);
            foreach (var figure in path.Figures)
            {
                sink.BeginFigure(new AvaloniaPoint(figure.Start.X, figure.Start.Y), isFilled: true);
                foreach (var segment in figure.Segments)
                {
                    switch (segment)
                    {
                        case LineSegmentData line:
                            sink.LineTo(new AvaloniaPoint(line.To.X, line.To.Y));
                            break;
                        case BezierSegmentData bezier:
                            sink.CubicBezierTo(
                                new AvaloniaPoint(bezier.Control1.X, bezier.Control1.Y),
                                new AvaloniaPoint(bezier.Control2.X, bezier.Control2.Y),
                                new AvaloniaPoint(bezier.To.X, bezier.To.Y));
                            break;
                    }
                }

                sink.EndFigure(true);
            }

            return geometry;
        }

        /// <summary>Records a failure of a draw operation; an image's drawing throws it at the end.</summary>
        public void Fail(Exception error)
        {
            if (onImage)
            {
                Error ??= error;
            }
        }

        /// <summary>Leases Skia's canvas from a custom draw operation's context.</summary>
        public static ISkiaSharpApiLease? Lease(ImmediateDrawingContext context) =>
            context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();

        private sealed class Restore(DrawingContext context, StateCommand state, Walk walk) : IDisposable
        {
            public void Dispose() => context.Custom(new CanvasClip(state, restore: true, walk));
        }
    }

    /// <summary>Shapes drawn by Skia in one state.</summary>
    private sealed class ShapeBatch(StateCommand state, DrawCommand[] commands, Walk walk) : ICustomDrawOperation
    {
        public Rect Bounds => ToRect(state.Clip);

        public bool HitTest(AvaloniaPoint p) => false;

        public void Render(ImmediateDrawingContext context)
        {
            try
            {
                using var lease = Walk.Lease(context)
                    ?? throw new InvalidOperationException("Drawing shapes needs Avalonia's Skia renderer.");
                SkiaPainter.Draw(lease.SkCanvas, state, commands);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                walk.Fail(error);
                throw;
            }
        }

        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

        public void Dispose()
        {
            // Nothing to release: the commands are immutable records.
        }
    }

    /// <summary>Clips the canvas to a state's paths, or restores it, around text and images drawn on an image.</summary>
    private sealed class CanvasClip(StateCommand state, bool restore, Walk walk) : ICustomDrawOperation
    {
        public Rect Bounds => ToRect(state.Clip);

        public bool HitTest(AvaloniaPoint p) => false;

        public void Render(ImmediateDrawingContext context)
        {
            try
            {
                using var lease = Walk.Lease(context)
                    ?? throw new InvalidOperationException("Clipping to a path needs Avalonia's Skia renderer.");
                var canvas = lease.SkCanvas;
                if (restore)
                {
                    canvas.Restore();
                    return;
                }

                canvas.Save();
                if (state.ClipPaths is { } paths)
                {
                    foreach (var path in paths)
                    {
                        using var skia = SkiaPainter.ToSkia(path);
                        canvas.ClipPath(skia, SkiaSharp.SKClipOperation.Intersect, antialias: false);
                    }
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                walk.Fail(error);
                throw;
            }
        }

        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

        public void Dispose()
        {
            // Nothing to release.
        }
    }
}
