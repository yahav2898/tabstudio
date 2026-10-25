using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.App.Theming;

namespace TabStudio.App.Editing;

/// <summary>
/// Highlights code for the debugger, as Visual Studio does: lines with a breakpoint in red, the statement the paused
/// program runs next in yellow, and the statement of a caller chosen in the Call Stack in green. The statement
/// highlights follow edits until the program pauses again.
/// </summary>
public sealed class DebugLineRenderer : IBackgroundRenderer
{
    private static readonly ThemedPen CurrentOutline = ThemedPen.Editor("Editor.CurrentStatementBorder", 1);
    private static readonly ThemedPen CallerOutline = ThemedPen.Editor("Editor.CallerBorder", 1);

    private static IBrush BreakpointBrush => ThemeBrushes.Editor("Editor.BreakpointLine");

    private static IBrush CurrentBrush => ThemeBrushes.Editor("Editor.CurrentStatementLine");

    private static IPen CurrentPen => CurrentOutline.Pen;

    private static IBrush CallerBrush => ThemeBrushes.Editor("Editor.CallerLine");

    private static IPen CallerPen => CallerOutline.Pen;

    private readonly Func<IEnumerable<int>> _breakpointLines;
    private TrackedRange<bool>[] _current = [];
    private TrackedRange<bool>[] _caller = [];

    /// <summary>Creates the renderer.</summary>
    /// <param name="breakpointLines">The one-based lines that have a breakpoint.</param>
    public DebugLineRenderer(Func<IEnumerable<int>> breakpointLines)
    {
        ArgumentNullException.ThrowIfNull(breakpointLines);
        _breakpointLines = breakpointLines;
    }

    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Background;

    /// <summary>Replaces the statements to highlight.</summary>
    /// <param name="current">The current statement as (start, length), or <see langword="null"/>.</param>
    /// <param name="caller">The chosen caller's statement as (start, length), or <see langword="null"/>.</param>
    /// <param name="documentLength">The document's length, to clamp spans from an older text.</param>
    public void SetStatements((int Start, int Length)? current, (int Start, int Length)? caller, int documentLength)
    {
        _current = Track(current, documentLength);
        _caller = Track(caller, documentLength);
    }

    /// <summary>Moves the highlights through an edit. Call from the document's <c>Changed</c> event.</summary>
    /// <param name="change">The edit.</param>
    public void OnDocumentChanged(DocumentChangeEventArgs change)
    {
        _current = TrackedRanges.Apply(_current, change);
        _caller = TrackedRanges.Apply(_caller, change);
    }

    /// <summary>The line where the current statement starts now, if there is one.</summary>
    /// <param name="document">The document.</param>
    public int? CurrentLine(TextDocument document) => LineOf(_current, document);

    /// <summary>The line where the chosen caller's statement starts now, if there is one.</summary>
    /// <param name="document">The document.</param>
    public int? CallerLine(TextDocument document) => LineOf(_caller, document);

    /// <summary>Where the first line of the current statement is drawn, relative to the text view, while it is in view.</summary>
    /// <param name="textView">The text view.</param>
    /// <returns>The rectangle, or <see langword="null"/> when there is no current statement or it is scrolled out of view.</returns>
    public Rect? CurrentStatementRect(TextView textView)
    {
        ArgumentNullException.ThrowIfNull(textView);
        if (_current.Length == 0 || textView.Document is not { } document || !textView.VisualLinesValid)
        {
            return null;
        }

        var rects = BackgroundGeometryBuilder.GetRectsForSegment(textView, Segment(_current[0], document)).ToList();
        if (rects.Count == 0)
        {
            return null;
        }

        var first = rects[0];
        return first.Bottom > 0 && first.Top < textView.Bounds.Height ? first : null;
    }

    /// <inheritdoc />
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (textView.Document is not { } document || !textView.VisualLinesValid || textView.VisualLines.Count == 0)
        {
            return;
        }

        var firstLine = textView.VisualLines[0].FirstDocumentLine.LineNumber;
        var lastLine = textView.VisualLines[^1].LastDocumentLine.LineNumber;

        // A statement the program is stopped at shows its own color instead of the breakpoint's, as in Visual Studio.
        var currentLine = CurrentLine(document);
        var callerLine = CallerLine(document);
        foreach (var number in _breakpointLines())
        {
            if (number < firstLine || number > lastLine || number > document.LineCount || number == currentLine || number == callerLine)
            {
                continue;
            }

            // From the first character of code to the end of the line, the way Visual Studio marks the statement.
            var line = document.GetLineByNumber(number);
            var indent = TextUtilities.GetLeadingWhitespace(document, line);
            if (line.EndOffset > indent.EndOffset)
            {
                Fill(textView, drawingContext, new TextSegment { StartOffset = indent.EndOffset, EndOffset = line.EndOffset }, BreakpointBrush, null);
            }
        }

        foreach (var caller in _caller)
        {
            Fill(textView, drawingContext, Segment(caller, document), CallerBrush, CallerPen);
        }

        foreach (var current in _current)
        {
            Fill(textView, drawingContext, Segment(current, document), CurrentBrush, CurrentPen);
        }
    }

    private static TrackedRange<bool>[] Track((int Start, int Length)? span, int documentLength)
    {
        if (span is not { } value || documentLength == 0)
        {
            return [];
        }

        var start = Math.Clamp(value.Start, 0, documentLength - 1);
        var length = Math.Clamp(value.Length, 1, documentLength - start);
        return [new TrackedRange<bool>(start, length, true)];
    }

    private static int? LineOf(TrackedRange<bool>[] ranges, TextDocument document) =>
        ranges.Length == 0 || ranges[0].Start > document.TextLength ? null : document.GetLineByOffset(ranges[0].Start).LineNumber;

    private static TextSegment Segment(TrackedRange<bool> range, TextDocument document)
    {
        var start = Math.Min(range.Start, document.TextLength);
        return new TextSegment { StartOffset = start, Length = Math.Min(range.Length, document.TextLength - start) };
    }

    private static void Fill(TextView textView, DrawingContext drawingContext, TextSegment segment, IBrush brush, IPen? pen)
    {
        if (segment.Length == 0)
        {
            return;
        }

        var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true, CornerRadius = 3 };
        builder.AddSegment(textView, segment);
        if (builder.CreateGeometry() is { } geometry)
        {
            drawingContext.DrawGeometry(brush, pen, geometry);
        }
    }
}
