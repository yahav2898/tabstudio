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
    // One coloring action per brush, so building a line allocates no closure per colored span.
    private readonly Dictionary<IBrush, Action<VisualLineElement>> _colorWith = new(ReferenceEqualityComparer.Instance);
    // Sorted and disjoint; only the first _count are in use. Edits move them where they are (see ApplyInPlace).
    private TrackedRange<IBrush>[] _ranges = [];
    private int _count;

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
        _count = _ranges.Length;
    }

    /// <summary>How many colored ranges there are now.</summary>
    internal int Count => _count;

    /// <summary>A copy of the colored ranges, for tests and measurements.</summary>
    internal TrackedRange<IBrush>[] Snapshot() => _ranges[.._count];

    /// <summary>
    /// Moves the colors through an edit, in place: typing allocates nothing here, however long the file. Call from
    /// the document's <c>Changed</c> event.
    /// </summary>
    /// <param name="change">The edit.</param>
    public void OnDocumentChanged(DocumentChangeEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        _count = TrackedRanges.ApplyInPlace(_ranges, _count, change.Offset, change.RemovalLength, change.InsertionLength);
    }

    /// <inheritdoc />
    protected override void ColorizeLine(DocumentLine line)
    {
        var ranges = new ReadOnlySpan<TrackedRange<IBrush>>(_ranges, 0, _count);
        if (ranges.Length == 0)
        {
            return;
        }

        var lineStart = line.Offset;
        var lineEnd = line.EndOffset;
        for (var i = TrackedRanges.FirstEndingAfterIn(ranges, lineStart); i < ranges.Length && ranges[i].Start < lineEnd; i++)
        {
            var start = Math.Max(ranges[i].Start, lineStart);
            var end = Math.Min(ranges[i].End, lineEnd);
            if (end > start)
            {
                ChangeLinePart(start, end, ColorWith(ranges[i].Value));
            }
        }
    }

    private Action<VisualLineElement> ColorWith(IBrush brush)
    {
        if (!_colorWith.TryGetValue(brush, out var color))
        {
            // A theme has a few dozen colors; a palette that changed many times starts over.
            if (_colorWith.Count > 512)
            {
                _colorWith.Clear();
            }

            color = element => element.TextRunProperties.SetForegroundBrush(brush);
            _colorWith[brush] = color;
        }

        return color;
    }
}
