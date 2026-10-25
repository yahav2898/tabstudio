using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.App.Theming;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Editing;

/// <summary>
/// Highlights every occurrence of the symbol under the caret with a subtle background, as Visual Studio does, with
/// the declaration in a slightly different color. The spans follow edits until the next request replaces them.
/// </summary>
public sealed class ReferenceHighlightRenderer : IBackgroundRenderer
{
    private static IBrush ReferenceBrush => ThemeBrushes.Editor("Editor.ReferenceHighlight");

    private static IBrush DefinitionBrush => ThemeBrushes.Editor("Editor.DefinitionHighlight");

    private TrackedRange<HighlightKind>[] _highlights = [];

    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>The highlighted spans, for tests.</summary>
    public IReadOnlyList<TrackedRange<HighlightKind>> Highlights => _highlights;

    /// <summary>Replaces the highlights.</summary>
    /// <param name="highlights">The occurrences of the symbol under the caret.</param>
    /// <param name="documentLength">The current length of the document, to drop stale spans.</param>
    public void SetHighlights(IEnumerable<DocumentHighlight> highlights, int documentLength)
    {
        ArgumentNullException.ThrowIfNull(highlights);
        _highlights = highlights
            .Where(h => h.Length > 0 && h.Start >= 0 && h.Start + h.Length <= documentLength)
            .Select(h => new TrackedRange<HighlightKind>(h.Start, h.Length, h.Kind))
            .ToArray();
    }

    /// <summary>Removes every highlight.</summary>
    public void Clear() => _highlights = [];

    /// <summary>Moves the highlights through an edit. Call from the document's <c>Changed</c> event.</summary>
    /// <param name="change">The edit.</param>
    public void OnDocumentChanged(DocumentChangeEventArgs change) => _highlights = TrackedRanges.Apply(_highlights, change);

    /// <inheritdoc />
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(drawingContext);

        var highlights = _highlights;
        if (highlights.Length == 0 || !textView.VisualLinesValid || textView.VisualLines.Count == 0)
        {
            return;
        }

        var visibleStart = textView.VisualLines[0].FirstDocumentLine.Offset;
        var visibleEnd = textView.VisualLines[^1].LastDocumentLine.EndOffset;
        var documentLength = textView.Document?.TextLength ?? 0;
        foreach (var highlight in highlights)
        {
            if (highlight.End < visibleStart || highlight.Start > visibleEnd || highlight.End > documentLength)
            {
                continue;
            }

            var segment = new TextSegment { StartOffset = highlight.Start, Length = highlight.Length };
            var brush = highlight.Value == HighlightKind.Definition ? DefinitionBrush : ReferenceBrush;
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                drawingContext.DrawRectangle(brush, null, rect, 2, 2);
            }
        }
    }
}
