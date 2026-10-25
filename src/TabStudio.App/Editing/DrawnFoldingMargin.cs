using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;

namespace TabStudio.App.Editing;

/// <summary>
/// The folding margin, drawn exactly as AvaloniaEdit draws it (the fold lines and a boxed minus or plus at the start of
/// each fold, highlighted with its lines under the mouse) and toggled the same way, but without a control per marker.
/// </summary>
/// <remarks>
/// <para>
/// AvaloniaEdit's margin removes every marker control and creates new ones, with their styles, each time the visible
/// lines change, which is every frame of a scroll: hundreds of style attachments and their allocations per frame. This
/// margin draws the markers itself, from the same geometry, and finds the marker under the pointer by its bounds.
/// </para>
/// <para>
/// On a touch screen a touch on the margin does not toggle at once, so a scroll can start there; a tap toggles.
/// </para>
/// </remarks>
public sealed class DrawnFoldingMargin : FoldingMargin
{
    // AvaloniaEdit's sizes: the margin is as wide as the font's size in pixels, a marker 0.7 of that.
    private const double PixelsPerPoint = 4.0 / 3.0;
    private const double MarkerSizeFactor = 0.7;

    private readonly List<Marker> _markers = [];
    private readonly List<(VisualLine VisualLine, TextLine TextLine)> _textLines = [];
    private Pen?[] _lineColors = [];
    private Pen?[] _endMarkers = [];
    private FoldingSection? _hovered;
    private (IBrush? Normal, IBrush? Selected) _pensFor;
    private Pen _linePen = new(Brushes.Gray);
    private Pen _selectedLinePen = new(Brushes.Black);
    private Pen _markerPen = new(Brushes.Gray, lineCap: PenLineCap.Square);
    private Pen _selectedMarkerPen = new(Brushes.Black, lineCap: PenLineCap.Square);

    /// <summary>Creates the margin.</summary>
    public DrawnFoldingMargin()
    {
        AddHandler(TappedEvent, OnTapped);
    }

    /// <summary>
    /// Puts this margin in place of the folding margin AvaloniaEdit installed for a folding manager.
    /// </summary>
    /// <param name="textArea">The editor's text area.</param>
    /// <param name="manager">The folding manager <see cref="FoldingManager.Install"/> returned.</param>
    /// <returns>The margin, or <see langword="null"/> when the text area has no such folding margin.</returns>
    public static DrawnFoldingMargin? ReplaceIn(TextArea textArea, FoldingManager manager)
    {
        ArgumentNullException.ThrowIfNull(textArea);
        var margins = textArea.LeftMargins;
        for (var i = 0; i < margins.Count; i++)
        {
            if (margins[i].GetType() == typeof(FoldingMargin) && ReferenceEquals(((FoldingMargin)margins[i]).FoldingManager, manager))
            {
                var margin = new DrawnFoldingMargin { FoldingManager = manager };
                margins[i] = margin;
                return margin;
            }
        }

        return null;
    }

    /// <summary>The bounds of the markers drawn last, by the line they are on, for tests.</summary>
    internal IReadOnlyList<(int Line, Rect Bounds)> MarkerBounds =>
        [.. _markers.Select(m => (m.VisualLine.FirstDocumentLine.LineNumber, m.Bounds))];

    /// <inheritdoc />
    public override void Render(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        _markers.Clear();
        if (TextView is not { VisualLinesValid: true } textView || textView.VisualLines.Count == 0 || FoldingManager is not { } manager)
        {
            return;
        }

        UpdatePens();
        var pixel = PixelSnapHelpers.GetPixelSize(this);
        CollectTextLines(textView);
        FindMarkers(textView, manager, pixel);

        var count = _textLines.Count;
        if (_lineColors.Length < count + 1)
        {
            _lineColors = new Pen?[count + 1];
            _endMarkers = new Pen?[count + 1];
        }

        Array.Clear(_lineColors);
        Array.Clear(_endMarkers);
        LinesOfFoldsFromAbove(textView, manager);
        LinesOfMarkers();
        DrawFoldLines(drawingContext, textView, pixel);
        foreach (var marker in _markers)
        {
            DrawMarker(drawingContext, marker, pixel);
        }
    }

    /// <inheritdoc />
    protected override void OnTextViewVisualLinesChanged() => InvalidateVisual();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);

        // A theme switch recolors the markers and lines without changing the visible lines.
        if (change.Property == FoldingMarkerBrushProperty || change.Property == SelectedFoldingMarkerBrushProperty
            || change.Property == FoldingMarkerBackgroundBrushProperty || change.Property == SelectedFoldingMarkerBackgroundBrushProperty)
        {
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerPressed(e);
        if (e.Handled)
        {
            return;
        }

        if (e.Pointer.Type is PointerType.Touch or PointerType.Pen)
        {
            // Nothing else acts on the press either: a drag scrolls, a tap toggles.
            e.Handled = true;
            return;
        }

        if (MarkerAt(e.GetPosition(this)) is { } marker)
        {
            marker.Section.IsFolded = !marker.Section.IsFolded;
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerMoved(e);
        var hovered = e.Pointer.Type == PointerType.Mouse ? MarkerAt(e.GetPosition(this))?.Section : null;
        if (!ReferenceEquals(hovered, _hovered))
        {
            _hovered = hovered;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hovered is not null)
        {
            _hovered = null;
            InvalidateVisual();
        }
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (e.Pointer.Type is PointerType.Touch or PointerType.Pen && MarkerAt(e.GetPosition(this)) is { } marker)
        {
            marker.Section.IsFolded = !marker.Section.IsFolded;
            e.Handled = true;
        }
    }

    private Marker? MarkerAt(Point point)
    {
        foreach (var marker in _markers)
        {
            if (marker.Bounds.Contains(point))
            {
                return marker;
            }
        }

        return null;
    }

    private void UpdatePens()
    {
        var brushes = (FoldingMarkerBrush, SelectedFoldingMarkerBrush);
        if (brushes == _pensFor)
        {
            return;
        }

        _pensFor = brushes;
        _linePen = new Pen(FoldingMarkerBrush);
        _selectedLinePen = new Pen(SelectedFoldingMarkerBrush);
        _markerPen = new Pen(FoldingMarkerBrush, lineCap: PenLineCap.Square);
        _selectedMarkerPen = new Pen(SelectedFoldingMarkerBrush, lineCap: PenLineCap.Square);
    }

    private void CollectTextLines(TextView textView)
    {
        _textLines.Clear();
        var lines = textView.VisualLines;
        for (var i = 0; i < lines.Count; i++)
        {
            var textLines = lines[i].TextLines;
            for (var j = 0; j < textLines.Count; j++)
            {
                _textLines.Add((lines[i], textLines[j]));
            }
        }
    }

    /// <summary>A marker at the start of each visible line where a fold starts, where AvaloniaEdit puts its marker controls.</summary>
    private void FindMarkers(TextView textView, FoldingManager manager, Size pixel)
    {
        var size = PixelSnapHelpers.RoundToOdd(MarkerSizeFactor * PixelsPerPoint * GetValue(TextElement.FontSizeProperty), pixel.Width);
        var lines = textView.VisualLines;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var section = manager.GetNextFolding(line.FirstDocumentLine.Offset);
            if (section is null || section.StartOffset > line.LastDocumentLine.Offset + line.LastDocumentLine.Length)
            {
                continue;
            }

            var textLine = line.GetTextLine(line.GetVisualColumn(section.StartOffset - line.FirstDocumentLine.Offset));
            var y = line.GetTextLineVisualYPosition(textLine, VisualYPosition.TextMiddle) - textView.VerticalOffset - (size / 2);
            var origin = PixelSnapHelpers.Round(new Point((Bounds.Width - size) / 2, y), pixel);
            _markers.Add(new Marker(section, line, new Rect(origin, new Size(size, size))));
        }
    }

    /// <summary>The fold lines of folds that start above the view and reach into it.</summary>
    private void LinesOfFoldsFromAbove(TextView textView, FoldingManager manager)
    {
        var lines = textView.VisualLines;
        var viewStart = lines[0].FirstDocumentLine.Offset;
        var viewEnd = lines[^1].LastDocumentLine.EndOffset;
        var maxEnd = 0;
        foreach (var section in manager.GetFoldingsContaining(viewStart))
        {
            var end = section.EndOffset;
            if (end <= viewEnd && !section.IsFolded && TextLineIndexOf(textView, end) is var index and >= 0)
            {
                _endMarkers[index] = _linePen;
            }

            if (end > maxEnd && section.StartOffset < viewStart)
            {
                maxEnd = end;
            }
        }

        if (maxEnd == 0)
        {
            return;
        }

        var last = maxEnd > viewEnd ? _textLines.Count : TextLineIndexOf(textView, maxEnd);
        for (var i = 0; i <= last && i < _textLines.Count + 1; i++)
        {
            _lineColors[i] = _linePen;
        }
    }

    /// <summary>The fold lines of folds that start in the view, highlighted for the marker under the mouse.</summary>
    private void LinesOfMarkers()
    {
        var textView = TextView;
        foreach (var marker in _markers)
        {
            var hovered = ReferenceEquals(marker.Section, _hovered);
            var endIndex = TextLineIndexOf(textView, marker.Section.EndOffset);
            if (!marker.Section.IsFolded && endIndex >= 0)
            {
                if (hovered)
                {
                    _endMarkers[endIndex] = _selectedLinePen;
                }
                else
                {
                    _endMarkers[endIndex] ??= _linePen;
                }
            }

            var startIndex = TextLineIndexOf(textView, marker.Section.StartOffset);
            if (startIndex < 0)
            {
                continue;
            }

            for (var i = startIndex + 1; i < _textLines.Count + 1 && i - 1 != endIndex; i++)
            {
                if (hovered)
                {
                    _lineColors[i] = _selectedLinePen;
                }
                else
                {
                    _lineColors[i] ??= _linePen;
                }
            }
        }
    }

    /// <summary>The vertical lines along each fold and the short horizontal line at its end, as AvaloniaEdit draws them.</summary>
    private void DrawFoldLines(DrawingContext context, TextView textView, Size pixel)
    {
        var x = PixelSnapHelpers.PixelAlign(Bounds.Width / 2, pixel.Width);
        double startY = 0;
        var pen = _lineColors[0];
        for (var i = 0; i < _textLines.Count; i++)
        {
            var (visualLine, textLine) = _textLines[i];
            if (_endMarkers[i] is { } endPen)
            {
                var y = MiddleOf(textView, visualLine, textLine, pixel.Height);
                context.DrawLine(endPen, new Point(x - (pixel.Width / 2), y), new Point(Bounds.Width, y));
            }

            if (_lineColors[i + 1] != pen)
            {
                var y = MiddleOf(textView, visualLine, textLine, pixel.Height);
                if (pen is not null)
                {
                    context.DrawLine(pen, new Point(x, startY + (pixel.Height / 2)), new Point(x, y - (pixel.Height / 2)));
                }

                pen = _lineColors[i + 1];
                startY = y;
            }
        }

        if (pen is not null)
        {
            context.DrawLine(pen, new Point(x, startY + (pixel.Height / 2)), new Point(x, Bounds.Height));
        }
    }

    /// <summary>A boxed minus, or a plus for a folded section, as AvaloniaEdit's marker control draws itself.</summary>
    private void DrawMarker(DrawingContext context, Marker marker, Size pixel)
    {
        var hovered = ReferenceEquals(marker.Section, _hovered);
        var bounds = marker.Bounds;
        var rect = new Rect(bounds.X + (pixel.Width / 2), bounds.Y + (pixel.Height / 2), bounds.Width - pixel.Width, bounds.Height - pixel.Height);
        context.FillRectangle(hovered ? SelectedFoldingMarkerBackgroundBrush : FoldingMarkerBackgroundBrush, rect);
        context.DrawRectangle(hovered ? _selectedMarkerPen : _markerPen, rect);
        var middleX = rect.X + (rect.Width / 2);
        var middleY = rect.Y + (rect.Height / 2);
        var space = PixelSnapHelpers.Round(rect.Width / 8, pixel.Width) + pixel.Width;
        context.DrawLine(_selectedMarkerPen, new Point(rect.X + space, middleY), new Point(rect.Right - space, middleY));
        if (marker.Section.IsFolded)
        {
            context.DrawLine(_selectedMarkerPen, new Point(middleX, rect.Y + space), new Point(middleX, rect.Bottom - space));
        }
    }

    private static double MiddleOf(TextView textView, VisualLine visualLine, TextLine textLine, double pixelHeight) =>
        PixelSnapHelpers.PixelAlign(visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextMiddle) - textView.VerticalOffset, pixelHeight);

    /// <summary>The index among the visible text lines of the text line that shows an offset, or -1.</summary>
    private int TextLineIndexOf(TextView textView, int offset)
    {
        if (textView.Document is not { } document)
        {
            return -1;
        }

        var visualLine = textView.GetVisualLine(document.GetLineByOffset(offset).LineNumber);
        if (visualLine is null)
        {
            return -1;
        }

        var textLine = visualLine.GetTextLine(visualLine.GetVisualColumn(offset - visualLine.FirstDocumentLine.Offset));
        for (var i = 0; i < _textLines.Count; i++)
        {
            if (ReferenceEquals(_textLines[i].TextLine, textLine))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>A fold's marker: the fold, the visual line it starts on and where the marker is drawn, relative to the margin.</summary>
    private readonly record struct Marker(FoldingSection Section, VisualLine VisualLine, Rect Bounds);
}
