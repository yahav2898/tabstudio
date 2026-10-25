using System.Globalization;
using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;

namespace TabStudio.App.Editing;

/// <summary>
/// The editor's line numbers, drawn exactly as AvaloniaEdit draws them but not again on every frame of a scroll: each
/// number is shaped and drawn once while it is in view, at its line's place in the document, and the numbers as a whole
/// move by the scroll offset, so a frame of a scroll draws only the numbers that came into view. The margin's width is
/// measured again only when the digits or the font change.
/// </summary>
/// <remarks>
/// <para>
/// AvaloniaEdit's margin measures itself, creates a text layout for every visible line number and draws them all again
/// each time the visible lines change, which is every frame of a scroll: dozens of text shapings and drawings, and
/// their allocations, per frame.
/// </para>
/// <para>
/// On a touch screen a touch on the numbers does not select at once, so a scroll can start there; a tap selects the
/// line, as a click does. The whole width of a line's row is the line's target, for a finger as for the mouse, rather
/// than only the digits AvaloniaEdit's margin can be hit on.
/// </para>
/// </remarks>
public sealed class CachedLineNumberMargin : LineNumberMargin
{
    private readonly Dictionary<int, FormattedText> _numbers = [];
    private readonly List<int> _stale = [];
    private readonly Dictionary<int, NumberDrawing> _drawings = [];
    private readonly List<int> _gone = [];
    private readonly NumbersLayer _layer = new();
    private (int Digits, Typeface Typeface, double Size)? _measuredFor;
    private Size _measured;
    private (IBrush? Foreground, Typeface Typeface, double Size) _drawnWith;
    private TextView? _scrolledView;
    private int _generation;

    /// <summary>Creates the margin.</summary>
    public CachedLineNumberMargin()
    {
        AddHandler(TappedEvent, OnTapped);
        VisualChildren.Add(_layer);
    }

    /// <summary>The number of line numbers whose text is kept, for tests.</summary>
    internal int CachedNumbers => _numbers.Count;

    /// <summary>The number of line numbers drawn now, for tests.</summary>
    internal int DrawnNumbers => _drawings.Count;

    /// <summary>
    /// Puts this margin in place of the editor's own line numbers, with the same color binding. Does nothing when the
    /// editor shows no line numbers or already uses this margin.
    /// </summary>
    /// <param name="editor">The editor.</param>
    public static void ReplaceIn(TextEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var margins = editor.TextArea.LeftMargins;
        for (var i = 0; i < margins.Count; i++)
        {
            if (margins[i].GetType() == typeof(LineNumberMargin))
            {
                var margin = new CachedLineNumberMargin();
                margin.Bind(TextElement.ForegroundProperty, editor.GetBindingObservable(TextEditor.LineNumbersForegroundProperty));
                margins[i] = margin;
                return;
            }
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        // Invisible, but it makes every point of the margin hit it, not only the digits. The digits are the layer's.
        drawingContext.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _layer.Measure(availableSize);

        // The width only depends on the number of digits and the font, not on which lines are visible.
        var measureFor = (MaxLineNumberLength, this.CreateTypeface(), GetValue(TextElement.FontSizeProperty));
        if (_measuredFor != measureFor)
        {
            _measured = base.MeasureOverride(availableSize);
            _measuredFor = measureFor;
        }

        return _measured;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _layer.Arrange(new Rect(finalSize));
        var drawWith = (GetValue(TextElement.ForegroundProperty), Typeface, EmSize);
        if (drawWith != _drawnWith)
        {
            // Another color or font: every number is shaped and drawn again.
            _drawnWith = drawWith;
            _numbers.Clear();
            foreach (var drawing in _drawings.Values)
            {
                drawing.InvalidateVisual();
            }
        }

        if (TextView is not { VisualLinesValid: true } textView)
        {
            return finalSize;
        }

        _layer.ScrollTo(textView.VerticalOffset);
        foreach (var drawing in _drawings.Values)
        {
            // In document coordinates: a number keeps its place, and so its drawing, while the view scrolls.
            var line = drawing.Line;
            if (!line.IsDisposed && line.TextLines.Count > 0)
            {
                var top = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop);
                drawing.Arrange(new Rect(0, top, finalSize.Width, line.Height));
            }
        }

        return finalSize;
    }

    /// <summary>Keeps a drawing for each visible number: new ones get one, numbers that left the view lose theirs.</summary>
    protected override void OnTextViewVisualLinesChanged()
    {
        if (TextView is not { VisualLinesValid: true } textView || textView.VisualLines.Count == 0)
        {
            return;
        }

        // A number keeps its drawing while it stays in view, even when its line is built again.
        var lines = textView.VisualLines;
        var generation = ++_generation;
        foreach (var line in lines)
        {
            var number = line.FirstDocumentLine.LineNumber;
            if (_drawings.TryGetValue(number, out var drawing))
            {
                drawing.Line = line;
            }
            else
            {
                drawing = new NumberDrawing(this, number, line);
                _drawings.Add(number, drawing);
                _layer.Add(drawing);
            }

            drawing.Generation = generation;
        }

        // Numbers that scrolled out of view, or into a fold.
        _gone.Clear();
        foreach (var (number, drawing) in _drawings)
        {
            if (drawing.Generation != generation)
            {
                _gone.Add(number);
            }
        }

        foreach (var number in _gone)
        {
            if (_drawings.Remove(number, out var drawing))
            {
                _layer.Remove(drawing);
            }
        }

        var first = lines[0].FirstDocumentLine.LineNumber;
        var last = lines[^1].FirstDocumentLine.LineNumber;
        Trim(first, last, lines.Count);
        InvalidateArrange();
    }

    /// <inheritdoc />
    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (_scrolledView is not null)
        {
            _scrolledView.ScrollOffsetChanged -= OnScrollOffsetChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);
        _scrolledView = newTextView;
        if (_scrolledView is not null)
        {
            _scrolledView.ScrollOffsetChanged += OnScrollOffsetChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == TextElement.ForegroundProperty)
        {
            InvalidateArrange();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Pointer.Type is PointerType.Touch or PointerType.Pen)
        {
            // Nothing else acts on the press either; the tap selects the line.
            e.Handled = true;
            return;
        }

        base.OnPointerPressed(e);
    }

    /// <summary>A number's shaped text, as AvaloniaEdit's margin shapes it, kept while the number is near the view.</summary>
    private FormattedText Shaped(int number)
    {
        if (!_numbers.TryGetValue(number, out var text))
        {
            var culture = CultureInfo.CurrentCulture;
            text = new FormattedText(number.ToString(culture), culture, FlowDirection.LeftToRight, Typeface, EmSize, GetValue(TextElement.ForegroundProperty));
            _numbers[number] = text;
        }

        return text;
    }

    private void OnScrollOffsetChanged(object? sender, EventArgs e) => InvalidateArrange();

    /// <summary>Forgets the numbers far from the view, so the cache stays the size of a few screens.</summary>
    private void Trim(int first, int last, int visible)
    {
        if (_numbers.Count <= (visible * 4) + 64)
        {
            return;
        }

        _stale.Clear();
        foreach (var number in _numbers.Keys)
        {
            if (number < first - visible || number > last + visible)
            {
                _stale.Add(number);
            }
        }

        foreach (var number in _stale)
        {
            _numbers.Remove(number);
        }
    }

    /// <summary>Selects the tapped line, caret at its end, as a click on its number does.</summary>
    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (e.Pointer.Type is not (PointerType.Touch or PointerType.Pen) || TextView is not { VisualLinesValid: true } textView
            || TextArea is not { } textArea || textArea.Document is not { } document)
        {
            return;
        }

        var y = Math.Clamp(e.GetPosition(textView).Y, 0, textView.Bounds.Height) + textView.VerticalOffset;
        if (textView.GetVisualLineFromVisualTop(y) is not { } line)
        {
            return;
        }

        var textLine = line.GetTextLineByVisualYPosition(y);
        var startColumn = line.GetTextLineVisualStartColumn(textLine);
        var start = line.GetRelativeOffset(startColumn) + line.FirstDocumentLine.Offset;
        var end = line.GetRelativeOffset(startColumn + textLine.Length) + line.FirstDocumentLine.Offset;
        if (end == line.LastDocumentLine.EndOffset)
        {
            end += line.LastDocumentLine.DelimiterLength;
        }

        textArea.Focus();
        end = Math.Min(end, document.TextLength);
        textArea.Caret.Offset = end;
        textArea.Selection = Selection.Create(textArea, start, end);
        e.Handled = true;
    }

    /// <summary>The numbers in document coordinates, moved as a whole by the scroll offset.</summary>
    private sealed class NumbersLayer : Layoutable
    {
        private readonly TranslateTransform _scroll = new();

        public NumbersLayer()
        {
            RenderTransform = _scroll;
        }

        public void Add(NumberDrawing drawing) => VisualChildren.Add(drawing);

        public void Remove(NumberDrawing drawing) => VisualChildren.Remove(drawing);

        public void ScrollTo(double verticalOffset)
        {
            if (_scroll.Y != -verticalOffset)
            {
                _scroll.Y = -verticalOffset;
            }
        }

        protected override Size MeasureOverride(Size availableSize) => default;

        protected override Size ArrangeOverride(Size finalSize) => finalSize;
    }

    /// <summary>One line's number, drawn right-aligned in the margin as AvaloniaEdit's margin draws it.</summary>
    private sealed class NumberDrawing : Layoutable
    {
        private readonly CachedLineNumberMargin _margin;
        private readonly int _number;

        public NumberDrawing(CachedLineNumberMargin margin, int number, VisualLine line)
        {
            _margin = margin;
            _number = number;
            Line = line;

            // Numbers sit at their lines' fractional positions, as the text does; rounding would shift them.
            UseLayoutRounding = false;
        }

        /// <summary>The visual line the number belongs to now; a line built again is a new one.</summary>
        public VisualLine Line { get; set; }

        /// <summary>The last update of the margin that found the number in view.</summary>
        public int Generation { get; set; }

        public override void Render(DrawingContext context)
        {
            var text = _margin.Shaped(_number);
            context.DrawText(text, new Point(Bounds.Width - text.Width, 0));
        }
    }
}
