using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.Extensions.Languages;

namespace TabStudio.App.Editing.Extensions;

/// <summary>
/// Outlines the bracket next to the caret and the one that matches it, as VS Code does for the brackets a language
/// configuration declares. The color theme's <c>editorBracketMatch</c> colors are used when it has them.
/// </summary>
public sealed class BracketHighlightRenderer : IBackgroundRenderer
{
    private static readonly IBrush DefaultFill = new ImmutableSolidColorBrush(Color.Parse("#1A0064FF"));
    private static readonly IPen DefaultBorder = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#8A8F99")), 1);

    private IBrush _fill = DefaultFill;
    private IPen _border = DefaultBorder;

    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>The highlighted pair, or <see langword="null"/>.</summary>
    public BracketMatch? Match { get; private set; }

    /// <summary>Highlights a pair, or nothing.</summary>
    /// <param name="match">The pair, or <see langword="null"/>.</param>
    /// <returns>Whether the highlight changed, so the view needs drawing again.</returns>
    public bool SetMatch(BracketMatch? match)
    {
        if (match == Match)
        {
            return false;
        }

        Match = match;
        return true;
    }

    /// <summary>Uses a color theme's bracket colors, or TabStudio's own.</summary>
    /// <param name="theme">The theme, or <see langword="null"/>.</param>
    public void SetTheme(EditorColorTheme? theme)
    {
        _fill = ThemeColors.Brush(theme?.Source.Color("editorBracketMatch.background")) ?? DefaultFill;
        _border = ThemeColors.Brush(theme?.Source.Color("editorBracketMatch.border")) is IImmutableBrush border
            ? new ImmutablePen(border, 1)
            : DefaultBorder;
    }

    /// <inheritdoc />
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (Match is not { } match || !textView.VisualLinesValid || textView.Document is not { } document)
        {
            return;
        }

        Draw(textView, drawingContext, document, match.OpenStart, match.OpenLength);
        Draw(textView, drawingContext, document, match.CloseStart, match.CloseLength);
    }

    private void Draw(TextView textView, DrawingContext drawingContext, TextDocument document, int start, int length)
    {
        if (start < 0 || start + length > document.TextLength)
        {
            return;
        }

        var segment = new TextSegment { StartOffset = start, Length = length };
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            drawingContext.DrawRectangle(_fill, _border, rect.Deflate(0.5), 2, 2);
        }
    }
}
