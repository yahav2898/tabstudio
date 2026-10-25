using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.App.Editing.Extensions;
using TabStudio.Extensions.Snippets;

namespace TabStudio.App.Editing.Snippets;

/// <summary>
/// Shows the tab stops of the snippet being filled in, as VS Code does: every tab stop has a faint background, and the
/// ones Tab is on now are outlined. An empty tab stop is drawn as a thin bar.
/// </summary>
public sealed class SnippetTabStopRenderer : IBackgroundRenderer
{
    private static readonly IBrush DefaultFill = new ImmutableSolidColorBrush(Color.Parse("#337C7C7C"));
    private static readonly IPen DefaultBorder = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#B37C9CC4")), 1);

    private IBrush _fill = DefaultFill;
    private IPen _border = DefaultBorder;
    private IReadOnlyList<SnippetRange> _ranges = [];
    private int _current = -1;

    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>Shows a snippet's tab stops, with the current one outlined, or nothing.</summary>
    /// <param name="ranges">The tab stops.</param>
    /// <param name="current">The current tab stop's number.</param>
    public void Show(IReadOnlyList<SnippetRange> ranges, int current)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        _ranges = ranges;
        _current = current;
    }

    /// <summary>Stops showing tab stops.</summary>
    public void Clear() => _ranges = [];

    /// <summary>Uses a color theme's snippet colors, or TabStudio's own.</summary>
    /// <param name="theme">The theme, or <see langword="null"/>.</param>
    public void SetTheme(EditorColorTheme? theme)
    {
        _fill = ThemeColors.Brush(theme?.Source.Color("editor.snippetTabstopHighlightBackground")) ?? DefaultFill;
        _border = ThemeColors.Brush(theme?.Source.Color("editor.snippetTabstopHighlightBorder")) is IImmutableBrush border
            ? new ImmutablePen(border, 1)
            : DefaultBorder;
    }

    /// <inheritdoc />
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (_ranges.Count == 0 || !textView.VisualLinesValid || textView.Document is not { } document)
        {
            return;
        }

        foreach (var range in _ranges)
        {
            if (range.Start < 0 || range.Start + range.Length > document.TextLength)
            {
                continue;
            }

            var pen = range.Index == _current ? _border : null;
            if (range.Length == 0)
            {
                drawingContext.DrawRectangle(_fill, pen, Bar(textView, document, range.Start), 1, 1);
                continue;
            }

            var segment = new TextSegment { StartOffset = range.Start, Length = range.Length };
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                drawingContext.DrawRectangle(_fill, pen, rect, 2, 2);
            }
        }
    }

    /// <summary>A thin bar where an empty tab stop is, in the text view's coordinates.</summary>
    private static Avalonia.Rect Bar(TextView textView, TextDocument document, int offset)
    {
        var position = new TextViewPosition(document.GetLocation(offset));
        var top = textView.GetVisualPosition(position, VisualYPosition.LineTop) - textView.ScrollOffset;
        var bottom = textView.GetVisualPosition(position, VisualYPosition.LineBottom) - textView.ScrollOffset;
        return new Avalonia.Rect(top.X - 1, top.Y, 2, Math.Max(1, bottom.Y - top.Y));
    }
}
