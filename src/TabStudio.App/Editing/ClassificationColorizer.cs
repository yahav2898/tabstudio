using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace TabStudio.App.Editing;

/// <summary>
/// Colors C# in the editor from Roslyn classifications. The ranges follow edits between analyses, so
/// colors stay in place while the user types and are corrected as soon as fresh classifications arrive.
/// </summary>
public sealed class ClassificationColorizer : DocumentColorizingTransformer
{
    private TrackedRange<IBrush>[] _ranges = [];

    /// <summary>
    /// The colors of an installed color theme by classification, or <see langword="null"/> for TabStudio's own
    /// (<see cref="SyntaxTheme"/>). Takes effect with the next <see cref="SetClassifications"/>.
    /// </summary>
    public Func<string, IBrush?>? Palette { get; set; }

    /// <summary>Replaces the colors with a new classification of the whole document.</summary>
    /// <param name="ranges">Classified spans in document order, with non-overlapping spans.</param>
    public void SetClassifications(IEnumerable<(int Start, int Length, string Classification)> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        _ranges = ranges
            .Select(r => (r.Start, r.Length, Brush: Palette is { } palette ? palette(r.Classification) : SyntaxTheme.For(r.Classification)))
            .Where(r => r.Brush is not null && r.Length > 0)
            .OrderBy(r => r.Start)
            .Select(r => new TrackedRange<IBrush>(r.Start, r.Length, r.Brush!))
            .ToArray();
    }

    /// <summary>Moves the colors through an edit. Call from the document's <c>Changed</c> event.</summary>
    /// <param name="change">The edit.</param>
    public void OnDocumentChanged(DocumentChangeEventArgs change) => _ranges = TrackedRanges.Apply(_ranges, change);

    /// <inheritdoc />
    protected override void ColorizeLine(DocumentLine line)
    {
        var ranges = _ranges;
        if (ranges.Length == 0)
        {
            return;
        }

        var lineStart = line.Offset;
        var lineEnd = line.EndOffset;
        for (var i = TrackedRanges.FirstEndingAfter(ranges, lineStart); i < ranges.Length && ranges[i].Start < lineEnd; i++)
        {
            var start = Math.Max(ranges[i].Start, lineStart);
            var end = Math.Min(ranges[i].End, lineEnd);
            if (end > start)
            {
                var brush = ranges[i].Value;
                ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(brush));
            }
        }
    }
}
