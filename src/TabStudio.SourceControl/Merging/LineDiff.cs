namespace TabStudio.SourceControl.Merging;

/// <summary>A run of lines that differs between two versions of a text.</summary>
/// <param name="OldStart">The first changed line in the old version, zero-based.</param>
/// <param name="OldLength">How many lines of the old version are replaced.</param>
/// <param name="NewStart">The first line in the new version that replaces them.</param>
/// <param name="NewLength">How many lines replace them.</param>
public readonly record struct DiffHunk(int OldStart, int OldLength, int NewStart, int NewLength)
{
    /// <summary>The line after the last replaced line of the old version.</summary>
    public int OldEnd => OldStart + OldLength;

    /// <summary>The line after the last inserted line of the new version.</summary>
    public int NewEnd => NewStart + NewLength;
}

/// <summary>
/// Line-by-line differences between two texts, with Eugene Myers' O(ND) algorithm: the shortest edit script,
/// the same one <c>git diff</c> starts from.
/// </summary>
public static class LineDiff
{
    /// <summary>Splits a text into lines, keeping each line's ending so the text can be put back together exactly.</summary>
    /// <param name="text">The text.</param>
    public static IReadOnlyList<string> SplitLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines.Add(text[start..(i + 1)]);
                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }

    /// <summary>Finds the hunks that turn the old lines into the new ones.</summary>
    /// <param name="oldLines">The old version.</param>
    /// <param name="newLines">The new version.</param>
    /// <param name="maximumEdits">
    /// The most edits to search for. Above it the texts are treated as completely different, which keeps
    /// unrelated large files from taking long.
    /// </param>
    /// <returns>The hunks in order, or a single hunk replacing everything when the limit is reached.</returns>
    public static IReadOnlyList<DiffHunk> Compute(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines, int maximumEdits = 1000)
    {
        ArgumentNullException.ThrowIfNull(oldLines);
        ArgumentNullException.ThrowIfNull(newLines);

        // Lines shared at the start and the end are never part of a change; trimming them keeps the search small.
        var prefix = 0;
        while (prefix < oldLines.Count && prefix < newLines.Count && string.Equals(oldLines[prefix], newLines[prefix], StringComparison.Ordinal))
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < oldLines.Count - prefix && suffix < newLines.Count - prefix
            && string.Equals(oldLines[oldLines.Count - 1 - suffix], newLines[newLines.Count - 1 - suffix], StringComparison.Ordinal))
        {
            suffix++;
        }

        var n = oldLines.Count - prefix - suffix;
        var m = newLines.Count - prefix - suffix;
        if (n == 0 && m == 0)
        {
            return [];
        }

        if (n == 0 || m == 0)
        {
            return [new DiffHunk(prefix, n, prefix, m)];
        }

        var path = ShortestPath(oldLines, newLines, prefix, n, m, maximumEdits);
        return path is null ? [new DiffHunk(prefix, n, prefix, m)] : ToHunks(path, prefix, n, m);
    }

    /// <summary>
    /// Runs the forward Myers search, saving the part of each frontier the backtrack needs, then walks back
    /// from the end to collect the matched line pairs.
    /// </summary>
    /// <returns>The matched line pairs in order, relative to the trimmed ranges; or null over the edit limit.</returns>
    private static List<(int X, int Y)>? ShortestPath(IReadOnlyList<string> a, IReadOnlyList<string> b, int offset, int n, int m, int maximumEdits)
    {
        var max = Math.Min(n + m, maximumEdits);
        var center = max + 1;
        var v = new int[(2 * max) + 3];

        // trace[d] holds the frontier before step d, for diagonals -d-1 .. d+1: 2d + 3 values, not the whole array.
        var trace = new List<int[]>();

        for (var d = 0; d <= max; d++)
        {
            trace.Add(v.AsSpan(center - d - 1, (2 * d) + 3).ToArray());
            for (var k = -d; k <= d; k += 2)
            {
                var x = k == -d || (k != d && v[center + k - 1] < v[center + k + 1])
                    ? v[center + k + 1]
                    : v[center + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && string.Equals(a[offset + x], b[offset + y], StringComparison.Ordinal))
                {
                    x++;
                    y++;
                }

                v[center + k] = x;
                if (x >= n && y >= m)
                {
                    return Backtrack(trace, n, m);
                }
            }
        }

        return null;
    }

    private static List<(int X, int Y)> Backtrack(List<int[]> trace, int n, int m)
    {
        var matches = new List<(int X, int Y)>();
        var x = n;
        var y = m;
        for (var d = trace.Count - 1; d >= 0; d--)
        {
            var frontier = trace[d];
            int At(int k) => frontier[k + d + 1];

            var k = x - y;
            var previousK = k == -d || (k != d && At(k - 1) < At(k + 1)) ? k + 1 : k - 1;
            var previousX = At(previousK);
            var previousY = previousX - previousK;
            while (x > previousX && y > previousY)
            {
                x--;
                y--;
                matches.Add((x, y));
            }

            x = previousX;
            y = previousY;
        }

        matches.Reverse();
        return matches;
    }

    /// <summary>Turns the matched pairs into hunks: whatever lies between two matches is a change.</summary>
    private static List<DiffHunk> ToHunks(List<(int X, int Y)> matches, int offset, int n, int m)
    {
        var hunks = new List<DiffHunk>();
        int x = 0, y = 0;
        foreach (var (mx, my) in matches.Append((n, m)))
        {
            if (mx > x || my > y)
            {
                hunks.Add(new DiffHunk(offset + x, mx - x, offset + y, my - y));
            }

            x = mx + 1;
            y = my + 1;
        }

        return hunks;
    }
}
