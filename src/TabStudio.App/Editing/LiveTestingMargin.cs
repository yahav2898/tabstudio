using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using TabStudio.App.Theming;
using TabStudio.LiveTesting.Results;

namespace TabStudio.App.Editing;

/// <summary>Data for <see cref="LiveTestingMargin.MarkerTapped"/>.</summary>
/// <param name="line">The one-based line of the marker.</param>
/// <param name="marker">The marker.</param>
/// <param name="bounds">Where the marker is drawn, relative to the margin.</param>
public sealed class LiveTestMarkerTappedEventArgs(int line, LiveTestMarker marker, Rect bounds) : EventArgs
{
    /// <summary>The one-based line of the marker.</summary>
    public int Line { get; } = line;

    /// <summary>The marker.</summary>
    public LiveTestMarker Marker { get; } = marker;

    /// <summary>Where the marker is drawn, relative to the margin.</summary>
    public Rect Bounds { get; } = bounds;
}

/// <summary>
/// Live Unit Testing's margin, between the line numbers and the code, as in Visual Studio: a green check on lines
/// that only passing tests run, a red cross on lines a failing test runs, and a blue dash on lines no test runs.
/// Markers whose results are out of date are drawn hollow and faded until the tests run again.
/// </summary>
/// <remarks>
/// The whole width of a line is a tap target; tapping a marker (or long-pressing it, or clicking it) raises
/// <see cref="MarkerTapped"/>, and hovering one with a mouse shows what it means. The margin only draws the visible
/// lines, so it stays fast in long files.
/// </remarks>
public sealed class LiveTestingMargin : AbstractMargin
{
    /// <summary>The margin's width: wide enough to tap a marker with a finger beside the line numbers.</summary>
    public const double MarginWidth = 24;

    private static readonly ThemedPen CoveredLine = ThemedPen.Editor("Editor.Covered", 2.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    private static readonly ThemedPen FailingLine = ThemedPen.Editor("Editor.Failing", 2.2, lineCap: PenLineCap.Round);
    private static readonly ThemedPen StaleCoveredLine = ThemedPen.Editor("Editor.Covered", 1.3, opacity: 0.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    private static readonly ThemedPen StaleFailingLine = ThemedPen.Editor("Editor.Failing", 1.3, opacity: 0.6, lineCap: PenLineCap.Round);
    private static readonly ThemedPen StaleNotCoveredLine = ThemedPen.Editor("Editor.NotCovered", 1.1, opacity: 0.6);

    private static IBrush NotCoveredBrush => ThemeBrushes.Editor("Editor.NotCovered");

    private static IPen CoveredPen => CoveredLine.Pen;

    private static IPen FailingPen => FailingLine.Pen;

    private static IPen StaleCoveredPen => StaleCoveredLine.Pen;

    private static IPen StaleFailingPen => StaleFailingLine.Pen;

    private static IPen StaleNotCoveredPen => StaleNotCoveredLine.Pen;

    private readonly Func<LiveTestMarkers?> _markers;
    private int? _hoveredLine;

    /// <summary>Creates the margin.</summary>
    /// <param name="markers">The document's markers, or <see langword="null"/> while Live Unit Testing is off.</param>
    public LiveTestingMargin(Func<LiveTestMarkers?> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        _markers = markers;
        Cursor = new Cursor(StandardCursorType.Hand);
        AddHandler(TappedEvent, OnTapped);
        AddHandler(ContextRequestedEvent, OnContextRequested);
    }

    /// <summary>Raised when the user taps, clicks or long-presses a marker.</summary>
    public event EventHandler<LiveTestMarkerTappedEventArgs>? MarkerTapped;

    /// <summary>The marker of a line and where it is drawn, if the line is visible and has one.</summary>
    /// <param name="lineNumber">One-based line.</param>
    public (LiveTestMarker Marker, Rect Bounds)? MarkerOn(int lineNumber)
    {
        if (TextView is not { VisualLinesValid: true } textView || _markers() is not { } markers)
        {
            return null;
        }

        foreach (var line in textView.VisualLines)
        {
            if (line.FirstDocumentLine.LineNumber == lineNumber && markers.At(line.FirstDocumentLine) is { } marker)
            {
                return (marker, LineBounds(textView, line));
            }
        }

        return null;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A transparent background makes the whole margin a tap target.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (TextView is not { VisualLinesValid: true } textView || _markers() is not { Count: > 0 } markers)
        {
            return;
        }

        foreach (var line in textView.VisualLines)
        {
            if (markers.At(line.FirstDocumentLine) is { } marker)
            {
                var bounds = LineBounds(textView, line);
                DrawMarker(context, bounds.Center, marker);
            }
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(MarginWidth, 0);

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerPressed(e);

        // The text area would otherwise start a selection; the tap is handled when it completes.
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerMoved(e);
        var line = LineAt(e.GetPosition(this));
        if (line == _hoveredLine)
        {
            return;
        }

        _hoveredLine = line;
        var marker = line is { } number ? MarkerOn(number)?.Marker : null;
        ToolTip.SetTip(this, marker?.Summary);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoveredLine = null;
        ToolTip.SetTip(this, null);
    }

    private static Rect LineBounds(TextView textView, VisualLine line)
    {
        var top = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
        var bottom = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineBottom) - textView.VerticalOffset;
        return new Rect(0, top, MarginWidth, bottom - top);
    }

    private static void DrawMarker(DrawingContext context, Point center, LiveTestMarker marker)
    {
        var (x, y) = (center.X, center.Y);
        switch (marker.State)
        {
            case LineCoverageState.Covered:
                var check = new StreamGeometry();
                using (var figure = check.Open())
                {
                    figure.BeginFigure(new Point(x - 4.2, y + 0.2), isFilled: false);
                    figure.LineTo(new Point(x - 1.2, y + 3.2));
                    figure.LineTo(new Point(x + 4.4, y - 3.4));
                    figure.EndFigure(isClosed: false);
                }

                context.DrawGeometry(null, marker.IsStale ? StaleCoveredPen : CoveredPen, check);
                break;
            case LineCoverageState.Failing:
                var pen = marker.IsStale ? StaleFailingPen : FailingPen;
                context.DrawLine(pen, new Point(x - 3.6, y - 3.6), new Point(x + 3.6, y + 3.6));
                context.DrawLine(pen, new Point(x + 3.6, y - 3.6), new Point(x - 3.6, y + 3.6));
                break;
            default:
                var dash = new Rect(x - 4.5, y - 1.5, 9, 3);
                if (marker.IsStale)
                {
                    context.DrawRectangle(null, StaleNotCoveredPen, dash.Deflate(0.55), 1.2, 1.2);
                }
                else
                {
                    context.DrawRectangle(NotCoveredBrush, null, dash, 1.5, 1.5);
                }

                break;
        }
    }

    private int? LineAt(Point point)
    {
        if (TextView is not { VisualLinesValid: true } textView)
        {
            return null;
        }

        var visualTop = Math.Max(0, point.Y) + textView.VerticalOffset;
        return textView.GetVisualLineFromVisualTop(visualTop)?.FirstDocumentLine.LineNumber;
    }

    private void Raise(Point point)
    {
        if (LineAt(point) is { } line && MarkerOn(line) is { } found)
        {
            MarkerTapped?.Invoke(this, new LiveTestMarkerTappedEventArgs(line, found.Marker, found.Bounds));
        }
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        Raise(e.GetPosition(this));
        e.Handled = true;
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.TryGetPosition(this, out var point))
        {
            Raise(point);
            e.Handled = true;
        }
    }
}
