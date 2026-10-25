using AvaloniaEdit.Document;

namespace TabStudio.App.Editing;

/// <summary>
/// A span of a document with attached data, which follows edits made after it was computed.
/// </summary>
/// <typeparam name="T">The data attached to the span.</typeparam>
/// <param name="Start">Zero-based start offset.</param>
/// <param name="Length">Length in characters.</param>
/// <param name="Value">The attached data.</param>
public readonly record struct TrackedRange<T>(int Start, int Length, T Value)
{
    /// <summary>The offset just past the span.</summary>
    public int End => Start + Length;
}

/// <summary>Operations on lists of <see cref="TrackedRange{T}"/>.</summary>
public static class TrackedRanges
{
    /// <summary>
    /// Moves a set of ranges through one document change. Ranges before the change stay, ranges after it
    /// shift, ranges that contain the whole change grow or shrink, and ranges the change cuts through are
    /// dropped (the next analysis recomputes them).
    /// </summary>
    /// <typeparam name="T">The data attached to the ranges.</typeparam>
    /// <param name="ranges">The ranges, in any order.</param>
    /// <param name="change">The change that was just applied to the document.</param>
    /// <returns>The moved ranges, in the same order.</returns>
    public static TrackedRange<T>[] Apply<T>(IReadOnlyList<TrackedRange<T>> ranges, DocumentChangeEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(change);

        var offset = change.Offset;
        var removedEnd = offset + change.RemovalLength;
        var delta = change.InsertionLength - change.RemovalLength;
        var result = new List<TrackedRange<T>>(ranges.Count);

        foreach (var range in ranges)
        {
            if (range.End < offset || (range.End == offset && range.Length > 0))
            {
                result.Add(range);
            }
            else if (range.Start >= removedEnd)
            {
                result.Add(range with { Start = range.Start + delta });
            }
            else if (range.Start <= offset && range.End >= removedEnd)
            {
                var length = range.Length + delta;
                if (length > 0)
                {
                    result.Add(range with { Length = length });
                }
            }
        }

        return [.. result];
    }

    /// <summary>
    /// Finds the first range that ends after an offset, by binary search. The ranges must be sorted by start
    /// and must not overlap.
    /// </summary>
    /// <typeparam name="T">The data attached to the ranges.</typeparam>
    /// <param name="ranges">Sorted, disjoint ranges.</param>
    /// <param name="offset">A document offset.</param>
    /// <returns>The index of that range, or <c>ranges.Count</c> when there is none.</returns>
    public static int FirstEndingAfter<T>(IReadOnlyList<TrackedRange<T>> ranges, int offset)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        int low = 0, high = ranges.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (ranges[mid].End <= offset)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}
