using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace TabStudio.App.Editing;

/// <summary>Colors the parts of the Output panel that are not plain program output.</summary>
public sealed class OutputColorizer : DocumentColorizingTransformer
{
    private readonly Func<IReadOnlyList<TrackedRange<IBrush>>> _segments;

    /// <summary>Creates the colorizer over the Output panel's colored segments.</summary>
    /// <param name="segments">Returns the current segments, sorted by start.</param>
    public OutputColorizer(Func<IReadOnlyList<TrackedRange<IBrush>>> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        _segments = segments;
    }

    /// <inheritdoc />
    protected override void ColorizeLine(DocumentLine line)
    {
        var segments = _segments();
        var lineStart = line.Offset;
        var lineEnd = line.EndOffset;

        for (var i = TrackedRanges.FirstEndingAfter(segments, lineStart); i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.Start >= lineEnd)
            {
                break;
            }

            var start = Math.Max(segment.Start, lineStart);
            var end = Math.Min(segment.End, lineEnd);
            if (end > start)
            {
                var brush = segment.Value;
                ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(brush));
            }
        }
    }
}
