using System.Globalization;
using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;

namespace TabStudio.App.Editing;

/// <summary>
/// The editor's line numbers, drawn exactly as AvaloniaEdit draws them but without formatting every visible number
/// again on every frame: each number's shaped text is kept while it is near the view, so a scroll only formats the
/// numbers that come into view, and the margin's width is measured again only when the digits or the font change.
/// </summary>
/// <remarks>
/// <para>
/// AvaloniaEdit's margin measures itself and creates a text layout for every visible line number each time the visible
/// lines change, which is every frame of a scroll: dozens of text shapings and their allocations per frame.
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
    private (int Digits, Typeface Typeface, double Size)? _measuredFor;
    private Size _measured;
    private (IBrush? Foreground, Typeface Typeface, double Size) _drawnWith;

    /// <summary>Creates the margin.</summary>
    public CachedLineNumberMargin()
    {
        AddHandler(TappedEvent, OnTapped);
    }

    /// <summary>The number of line numbers whose text is kept, for tests.</summary>
    internal int CachedNumbers => _numbers.Count;

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

        // Invisible, but it makes every point of the margin hit it, not only the digits.
        drawingContext.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (TextView is not { VisualLinesValid: true } textView || textView.VisualLines.Count == 0)
        {
            return;
        }

        var foreground = GetValue(TextElement.ForegroundProperty);
        var drawWith = (foreground, Typeface, EmSize);
        if (drawWith != _drawnWith)
        {
            _numbers.Clear();
            _drawnWith = drawWith;
        }

        var width = Bounds.Width;
        var lines = textView.VisualLines;
        foreach (var line in lines)
        {
            var number = line.FirstDocumentLine.LineNumber;
            if (!_numbers.TryGetValue(number, out var text))
            {
                text = new FormattedText(number.ToString(CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface, EmSize, foreground);
                _numbers[number] = text;
            }

            var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop);
            drawingContext.DrawText(text, new Point(width - text.Width, y - textView.VerticalOffset));
        }

        Trim(lines[0].FirstDocumentLine.LineNumber, lines[^1].FirstDocumentLine.LineNumber, lines.Count);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
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
}
