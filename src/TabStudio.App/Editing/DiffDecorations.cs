using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.App.Theming;

namespace TabStudio.App.Editing;

/// <summary>The colors of an inline diff, shared by its renderers and margin.</summary>
internal static class DiffColors
{
    public static IBrush AddedBackground => ThemeBrushes.Editor("Editor.DiffAddedLine");

    public static IBrush RemovedBackground => ThemeBrushes.Editor("Editor.DiffRemovedLine");

    public static IBrush CollapsedBackground => ThemeBrushes.Editor("Editor.DiffCollapsedLine");

    public static IBrush AddedText => ThemeBrushes.Editor("Editor.DiffAddedText");

    public static IBrush RemovedText => ThemeBrushes.Editor("Editor.DiffRemovedText");

    public static IBrush LineNumber => ThemeBrushes.Editor("Editor.DiffLineNumber");

    public static IBrush CollapsedText => ThemeBrushes.Editor("Editor.DiffCollapsedText");

    /// <summary>The line a document line shows, or <see langword="null"/> past the end.</summary>
    public static DiffLine? LineAt(IReadOnlyList<DiffLine>? lines, int lineNumber) =>
        lines is not null && lineNumber >= 1 && lineNumber <= lines.Count ? lines[lineNumber - 1] : null;
}

/// <summary>
/// Paints the background of an inline diff's lines: green for added lines, red for removed ones and a band for
/// collapsed runs of unchanged lines, across the whole width of the editor.
/// </summary>
/// <param name="lines">The diff's lines; document line <c>n</c> shows line <c>n - 1</c>.</param>
public sealed class DiffBackgroundRenderer(Func<IReadOnlyList<DiffLine>?> lines) : IBackgroundRenderer
{
    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Background;

    /// <inheritdoc />
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (!textView.VisualLinesValid)
        {
            return;
        }

        var diff = lines();
        foreach (var visualLine in textView.VisualLines)
        {
            var brush = DiffColors.LineAt(diff, visualLine.FirstDocumentLine.LineNumber)?.Kind switch
            {
                DiffLineKind.Added => DiffColors.AddedBackground,
                DiffLineKind.Removed => DiffColors.RemovedBackground,
                DiffLineKind.Collapsed => DiffColors.CollapsedBackground,
                _ => null,
            };
            if (brush is not null)
            {
                var top = visualLine.VisualTop - textView.VerticalOffset;
                drawingContext.FillRectangle(brush, new Rect(0, top, textView.Bounds.Width, visualLine.Height));
            }
        }
    }
}

/// <summary>Shows the line standing for a collapsed run of unchanged lines as a muted, italic link-like note.</summary>
/// <param name="lines">The diff's lines; document line <c>n</c> shows line <c>n - 1</c>.</param>
public sealed class DiffColorizer(Func<IReadOnlyList<DiffLine>?> lines) : DocumentColorizingTransformer
{
    /// <inheritdoc />
    protected override void ColorizeLine(DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Length == 0 || DiffColors.LineAt(lines(), line.LineNumber)?.Kind != DiffLineKind.Collapsed)
        {
            return;
        }

        ChangeLinePart(line.Offset, line.EndOffset, element =>
        {
            element.TextRunProperties.SetForegroundBrush(DiffColors.CollapsedText);
            var typeface = element.TextRunProperties.Typeface;
            element.TextRunProperties.SetTypeface(new Typeface(typeface.FontFamily, FontStyle.Italic, typeface.Weight, typeface.Stretch));
        });
    }
}
