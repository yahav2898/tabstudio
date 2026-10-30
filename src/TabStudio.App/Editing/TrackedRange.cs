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
    /// Moves sorted, disjoint ranges through one document change where they are, as <see cref="Apply{T}"/> does but
    /// without copying them: ranges before the change are not touched, the few the change reaches grow, shrink or are
    /// dropped, and the ones after it shift. A colorizer holds a range per token, about 17,000 for a 2,500-line file;
    /// copying them on every keystroke allocated 0.6 MB per edit, and over 1 GB for a Format Document.
    /// </summary>
    /// <typeparam name="T">The data attached to the ranges.</typeparam>
    /// <param name="ranges">The ranges, sorted by start, disjoint and not empty; the first <paramref name="count"/> are used.</param>
    /// <param name="count">How many of <paramref name="ranges"/> are in use.</param>
    /// <param name="offset">Where the change starts.</param>
    /// <param name="removalLength">How many characters it removed.</param>
    /// <param name="insertionLength">How many characters it inserted.</param>
    /// <returns>How many ranges are in use afterwards.</returns>
    public static int ApplyInPlace<T>(TrackedRange<T>[] ranges, int count, int offset, int removalLength, int insertionLength)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, ranges.Length);
        var removedEnd = offset + removalLength;
        var delta = insertionLength - removalLength;

        // Every range before this one ends at or before the change and stays as it is.
        var read = FirstEndingAfterIn(new ReadOnlySpan<TrackedRange<T>>(ranges, 0, count), offset);
        var write = read;

        // The ranges the change reaches: one that holds all of it grows or shrinks, one it cuts through is dropped.
        for (; read < count && ranges[read].Start < removedEnd; read++)
        {
            var range = ranges[read];
            if (range.Start <= offset && range.End >= removedEnd && range.Length + delta > 0)
            {
                ranges[write++] = range with { Length = range.Length + delta };
            }
        }

        // An insertion right where a range starts moves it, as Apply does.
        for (; read < count; read++)
        {
            var range = ranges[read];
            ranges[write++] = range with { Start = range.Start + delta };
        }

        // Dropped ranges leave stale entries at the end; clear them so their values can be collected.
        Array.Clear(ranges, write, count - write);
        return write;
    }

    /// <summary>
    /// Finds the first range that ends after an offset, by binary search. The ranges must be sorted by start
    /// and must not overlap.
    /// </summary>
    /// <typeparam name="T">The data attached to the ranges.</typeparam>
    /// <param name="ranges">Sorted, disjoint ranges.</param>
    /// <param name="offset">A document offset.</param>
    /// <returns>The index of that range, or the number of ranges when there is none.</returns>
    public static int FirstEndingAfterIn<T>(ReadOnlySpan<TrackedRange<T>> ranges, int offset)
    {
        int low = 0, high = ranges.Length;
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
