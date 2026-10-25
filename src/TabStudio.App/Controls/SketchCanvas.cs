using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;

namespace TabStudio.App.Controls;

/// <summary>
/// A sheet of paper to draw a screen on, with a finger, a stylus or a mouse: black ink on white, one stroke per
/// touch, for Sketch to XAML.
/// </summary>
public sealed class SketchCanvas : Control
{
    private const double InkThickness = 3;

    private static readonly IPen Ink = new ImmutablePen(Brushes.Black, InkThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    private readonly List<List<Point>> _strokes = [];
    private List<Point>? _current;

    /// <summary>Raised when a stroke is finished, undone or cleared.</summary>
    public event EventHandler? StrokesChanged;

    /// <summary>Whether anything is drawn.</summary>
    public bool HasStrokes => _strokes.Count > 0;

    /// <summary>Takes away the last stroke.</summary>
    public void Undo()
    {
        if (_strokes.Count > 0)
        {
            _strokes.RemoveAt(_strokes.Count - 1);
            Changed();
        }
    }

    /// <summary>Takes away every stroke.</summary>
    public void Clear()
    {
        _strokes.Clear();
        Changed();
    }

    /// <summary>The drawing as a PNG image, at the size it is shown.</summary>
    public byte[] ToPng()
    {
        var size = new PixelSize(Math.Max(1, (int)Math.Ceiling(Bounds.Width)), Math.Max(1, (int)Math.Ceiling(Bounds.Height)));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            Draw(context, new Rect(size.ToSize(1)));
        }

        using var png = new MemoryStream();
        bitmap.Save(png);
        return png.ToArray();
    }

    /// <summary>Draws a stroke through the given points, as a finger would.</summary>
    /// <param name="points">The stroke's points, in the canvas's coordinates.</param>
    public void AddStroke(IEnumerable<Point> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _strokes.Add([.. points]);
        Changed();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) => Draw(context, new Rect(Bounds.Size));

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _current = [e.GetPosition(this)];
        _strokes.Add(_current);
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_current is null)
        {
            return;
        }

        var point = e.GetPosition(this);
        var last = _current[^1];
        if (Math.Abs(point.X - last.X) + Math.Abs(point.Y - last.Y) >= 1.5)
        {
            _current.Add(point);
            InvalidateVisual();
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndStroke();
        e.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndStroke();
    }

    private void EndStroke()
    {
        if (_current is not null)
        {
            _current = null;
            Changed();
        }
    }

    private void Changed()
    {
        InvalidateVisual();
        StrokesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Draw(DrawingContext context, Rect area)
    {
        context.FillRectangle(Brushes.White, area);
        foreach (var stroke in _strokes)
        {
            if (stroke.Count == 1)
            {
                context.DrawEllipse(Brushes.Black, null, stroke[0], InkThickness / 2, InkThickness / 2);
                continue;
            }

            var geometry = new StreamGeometry();
            using (var figure = geometry.Open())
            {
                figure.BeginFigure(stroke[0], isFilled: false);
                for (var i = 1; i < stroke.Count; i++)
                {
                    figure.LineTo(stroke[i]);
                }

                figure.EndFigure(isClosed: false);
            }

            context.DrawGeometry(null, Ink, geometry);
        }
    }
}
