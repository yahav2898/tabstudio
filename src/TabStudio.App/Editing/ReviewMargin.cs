using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using TabStudio.App.ViewModels.PullRequests;

namespace TabStudio.App.Editing;

/// <summary>
/// The review margin beside an editor's lines, as Visual Studio shows pull request comments: a speech bubble on
/// each line with review items, colored by what they are, and, where a comment can go, a plus that appears under
/// the pointer. It is wide enough to tap: a tap toggles a line's items or starts a comment on it, and a long press
/// or a right-click asks for the line's menu.
/// </summary>
public sealed class ReviewMargin : AbstractMargin
{
    /// <summary>The margin's width: a comfortable tap target.</summary>
    public const double MarginWidth = 26;

    private static readonly Geometry Bubble = Geometry.Parse("M4 5 L20 5 L20 16 L10 16 L5 20 L5 16 L4 16 Z");
    private static readonly Geometry Plus = Geometry.Parse("M12 6 L12 18 M6 12 L18 12");
    private static readonly IBrush PlusBackground = new ImmutableSolidColorBrush(Color.Parse("#0F7A70"));

    private readonly IReviewAnchorSource _source;
    private int? _hoverLine;

    /// <summary>Creates the margin.</summary>
    /// <param name="source">The document's review items by line.</param>
    public ReviewMargin(IReviewAnchorSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        Cursor = new Cursor(StandardCursorType.Hand);
        AddHandler(TappedEvent, OnTapped);
        AddHandler(ContextRequestedEvent, OnContextRequested);
    }

    /// <summary>Raised with the 1-based line when the user taps or clicks the margin.</summary>
    public event EventHandler<int>? LineTapped;

    /// <summary>Raised with the 1-based line when the user long-presses or right-clicks the margin.</summary>
    public event EventHandler<int>? LineMenuRequested;

    /// <summary>The brush of a glyph, by what the line's items are.</summary>
    /// <param name="glyph">The glyph.</param>
    public static ImmutableSolidColorBrush GlyphBrush(ReviewGlyph glyph) => glyph switch
    {
        ReviewGlyph.Pending => Brush("#E3B341"),
        ReviewGlyph.Draft => Brush("#C9B6FF"),
        ReviewGlyph.NewComment => Brush("#6FD6C4"),
        ReviewGlyph.Resolved => Brush("#6FD6A0"),
        ReviewGlyph.Outdated => Brush("#858C99"),
        _ => Brush("#6CB6FF"),
    };

    /// <summary>The 1-based line under a point of the margin, or <see langword="null"/> below the last line.</summary>
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

        var anchors = _source.Anchors.ToDictionary(a => a.Line);
        foreach (var line in textView.VisualLines)
        {
            var number = line.FirstDocumentLine.LineNumber;
            var top = line.VisualTop - textView.VerticalOffset;
            var height = Math.Min(line.Height, textView.DefaultLineHeight);
            var size = Math.Min(16, height - 2);
            var origin = new Point((Bounds.Width - size) / 2, top + ((height - size) / 2));
            if (anchors.TryGetValue(number, out var anchor))
            {
                // Filled while the line's items show, outlined while they are folded away.
                var brush = GlyphBrush(anchor.Glyph);
                DrawIcon(context, Bubble, origin, size, anchor.IsExpanded ? brush : null, brush, 1.8);
            }
            else if (number == _hoverLine && _source.CanCommentOn(number))
            {
                context.DrawRectangle(PlusBackground, null, new Rect(origin, new Size(size, size)), 4, 4);
                DrawIcon(context, Plus, origin, size, null, Brushes.White, 2.4);
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
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetHoverLine(LineAt(e.GetPosition(this)));
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHoverLine(null);
    }

    /// <inheritdoc />
    protected override void OnTextViewChanged(TextView? oldTextView, TextView? newTextView)
    {
        if (oldTextView is not null)
        {
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView is not null)
        {
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
        }

        InvalidateVisual();
    }

    private static void DrawIcon(DrawingContext context, Geometry icon, Point origin, double size, IBrush? fill, IImmutableBrush stroke, double thickness)
    {
        // The icons are drawn on a 24×24 grid, like the app's stroke icons; the pen is scaled back to its thickness.
        var scale = size / 24;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y)))
        {
            context.DrawGeometry(fill, new ImmutablePen(stroke, thickness / scale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), icon);
        }
    }

    private static ImmutableSolidColorBrush Brush(string color) => new(Color.Parse(color));

    private void SetHoverLine(int? line)
    {
        if (_hoverLine != line)
        {
            _hoverLine = line;
            InvalidateVisual();
        }
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

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
