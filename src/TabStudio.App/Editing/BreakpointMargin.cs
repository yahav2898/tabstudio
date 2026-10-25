using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Editing;

/// <summary>A breakpoint glyph to draw on a line.</summary>
/// <param name="Line">One-based line.</param>
/// <param name="Glyph">How it is drawn.</param>
public readonly record struct BreakpointMark(int Line, BreakpointGlyph Glyph);

/// <summary>
/// The glyph margin left of the line numbers, as in Visual Studio: red breakpoint dots (hollow when a debug session
/// finds no code for them), the yellow arrow of the statement the paused program runs next, and the green arrow of a
/// caller chosen in the Call Stack. It is wide enough to tap; a tap toggles the line's breakpoint, and a long press
/// or a right-click asks for the line's breakpoint menu.
/// </summary>
public sealed class BreakpointMargin : AbstractMargin
{
    /// <summary>The margin's width: a comfortable tap target beside the line numbers.</summary>
    public const double MarginWidth = 30;

    private static readonly IBrush BreakpointBrush = new ImmutableSolidColorBrush(Color.Parse("#E5534B"));
    private static readonly IPen BreakpointPen = new ImmutablePen(BreakpointBrush, 1.6);
    private static readonly IBrush ConditionBrush = new ImmutableSolidColorBrush(Colors.White);
    private static readonly IBrush CurrentBrush = new ImmutableSolidColorBrush(Color.Parse("#F2CC4E"));
    private static readonly IBrush CallerBrush = new ImmutableSolidColorBrush(Color.Parse("#6FD6A0"));
    private static readonly IPen ArrowOutline = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#1B1D23")), 1);

    private readonly Func<IReadOnlyList<BreakpointMark>> _breakpoints;
    private readonly Func<int?> _currentLine;
    private readonly Func<int?> _callerLine;

    /// <summary>Creates the margin.</summary>
    /// <param name="breakpoints">The breakpoints to draw.</param>
    /// <param name="currentLine">The line of the current statement, if it is in this document.</param>
    /// <param name="callerLine">The line of the caller chosen in the Call Stack, if it is in this document.</param>
    public BreakpointMargin(Func<IReadOnlyList<BreakpointMark>> breakpoints, Func<int?> currentLine, Func<int?> callerLine)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);
        ArgumentNullException.ThrowIfNull(currentLine);
        ArgumentNullException.ThrowIfNull(callerLine);
        _breakpoints = breakpoints;
        _currentLine = currentLine;
        _callerLine = callerLine;
        Cursor = new Cursor(StandardCursorType.Hand);
        AddHandler(TappedEvent, OnTapped);

        // A quick second tap is a double tap, which toggles the breakpoint back, as a second click does in Visual Studio.
        AddHandler(DoubleTappedEvent, OnTapped);

        // Controls turn a long press on touch, and a right-click, into a context request.
        AddHandler(ContextRequestedEvent, OnContextRequested);
    }

    /// <summary>Raised with the one-based line when the user taps or clicks the margin.</summary>
    public event EventHandler<int>? LineTapped;

    /// <summary>Raised with the one-based line when the user long-presses or right-clicks the margin.</summary>
    public event EventHandler<int>? LineMenuRequested;

    /// <summary>The line under a point of the margin, or <see langword="null"/> below the last line.</summary>
    /// <param name="point">A point relative to the margin.</param>
    public int? LineAt(Point point)
    {
        if (TextView is not { VisualLinesValid: true } textView)
        {
            return null;
        }

        var visualTop = Math.Max(0, point.Y) + textView.VerticalOffset;
        return textView.GetVisualLineFromVisualTop(visualTop)?.FirstDocumentLine.LineNumber;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A transparent background makes the whole margin a tap target.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (TextView is not { VisualLinesValid: true } textView)
        {
            return;
        }

        var marks = new Dictionary<int, BreakpointGlyph>();
        foreach (var mark in _breakpoints())
        {
            marks.TryAdd(mark.Line, mark.Glyph);
        }

        var current = _currentLine();
        var caller = _callerLine();
        foreach (var line in textView.VisualLines)
        {
            var number = line.FirstDocumentLine.LineNumber;
            var top = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
            var bottom = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineBottom) - textView.VerticalOffset;
            var center = new Point(Bounds.Width / 2, (top + bottom) / 2);
            var radius = Math.Min(7, ((bottom - top) / 2) - 2);

            if (marks.TryGetValue(number, out var glyph))
            {
                DrawBreakpoint(context, center, radius, glyph);
            }

            if (number == caller)
            {
                DrawArrow(context, center, radius, CallerBrush);
            }

            if (number == current)
            {
                DrawArrow(context, center, radius, CurrentBrush);
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

    private static void DrawBreakpoint(DrawingContext context, Point center, double radius, BreakpointGlyph glyph)
    {
        if (glyph == BreakpointGlyph.Unbound)
        {
            context.DrawEllipse(null, BreakpointPen, center, radius - 0.8, radius - 0.8);
            return;
        }

        context.DrawEllipse(BreakpointBrush, null, center, radius, radius);
        if (glyph == BreakpointGlyph.Conditional)
        {
            // A white bar across the dot, as Visual Studio marks breakpoints with conditions.
            var half = radius * 0.55;
            context.FillRectangle(ConditionBrush, new Rect(center.X - half, center.Y - 1, half * 2, 2));
        }
    }

    private static void DrawArrow(DrawingContext context, Point center, double radius, IBrush brush)
    {
        var size = radius + 3;
        var left = center.X - size;
        var right = center.X + size;
        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(new Point(left, center.Y - (size * 0.38)), isFilled: true);
            figure.LineTo(new Point(center.X, center.Y - (size * 0.38)));
            figure.LineTo(new Point(center.X, center.Y - (size * 0.85)));
            figure.LineTo(new Point(right, center.Y));
            figure.LineTo(new Point(center.X, center.Y + (size * 0.85)));
            figure.LineTo(new Point(center.X, center.Y + (size * 0.38)));
            figure.LineTo(new Point(left, center.Y + (size * 0.38)));
            figure.EndFigure(isClosed: true);
        }

        context.DrawGeometry(brush, ArrowOutline, geometry);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (LineAt(e.GetPosition(this)) is { } line)
        {
            LineTapped?.Invoke(this, line);
            e.Handled = true;
        }
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.TryGetPosition(this, out var point) && LineAt(point) is { } line)
        {
            LineMenuRequested?.Invoke(this, line);
            e.Handled = true;
        }
    }
}
