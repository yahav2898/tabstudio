using System.Text;

namespace TabStudio.SourceControl.Merging;

/// <summary>The outcome of merging two edited versions of a text.</summary>
/// <param name="Text">The merged text; where both sides changed the same lines, it holds Git-style conflict markers.</param>
/// <param name="ConflictCount">How many places both sides changed differently.</param>
public sealed record MergeResult(string Text, int ConflictCount)
{
    /// <summary>Whether the versions merged without conflicts.</summary>
    public bool IsClean => ConflictCount == 0;
}

/// <summary>
/// A three-way line merge, as <c>git merge</c> does for text files: changes made on only one side are taken,
/// identical changes are taken once, and different changes to the same lines become a conflict.
/// </summary>
public static class TextMerge
{
    /// <summary>The line that starts a conflict, followed by the local version.</summary>
    public const string OursMarker = "<<<<<<< Yours";

    /// <summary>The line between the two versions of a conflict.</summary>
    public const string SeparatorMarker = "=======";

    /// <summary>The line that ends a conflict, after GitHub's version.</summary>
    public const string TheirsMarker = ">>>>>>> GitHub";

    /// <summary>Merges two versions that both started from a base.</summary>
    /// <param name="baseText">The common ancestor.</param>
    /// <param name="ours">The local version.</param>
    /// <param name="theirs">The version on GitHub.</param>
    public static MergeResult Merge(string baseText, string ours, string theirs)
    {
        ArgumentNullException.ThrowIfNull(baseText);
        ArgumentNullException.ThrowIfNull(ours);
        ArgumentNullException.ThrowIfNull(theirs);

        if (string.Equals(ours, theirs, StringComparison.Ordinal) || string.Equals(baseText, theirs, StringComparison.Ordinal))
        {
            return new MergeResult(ours, 0);
        }

        if (string.Equals(baseText, ours, StringComparison.Ordinal))
        {
            return new MergeResult(theirs, 0);
        }

        var baseLines = LineDiff.SplitLines(baseText);
        var ourLines = LineDiff.SplitLines(ours);
        var theirLines = LineDiff.SplitLines(theirs);
        var ourHunks = LineDiff.Compute(baseLines, ourLines);
        var theirHunks = LineDiff.Compute(baseLines, theirLines);
        var newline = DetectNewline(ours);

        var output = new StringBuilder(Math.Max(ours.Length, theirs.Length));
        var conflicts = 0;
        var basePosition = 0;
        int oi = 0, ti = 0;

        while (oi < ourHunks.Count || ti < theirHunks.Count)
        {
            // A region starts at the earliest remaining hunk and takes in every hunk, from either side, that
            // overlaps or touches it. As in Git, changes to neighboring lines count as touching.
            var takeOurs = ti >= theirHunks.Count || (oi < ourHunks.Count && ourHunks[oi].OldStart <= theirHunks[ti].OldStart);
            var regionStart = takeOurs ? ourHunks[oi].OldStart : theirHunks[ti].OldStart;
            var regionEnd = regionStart;
            int oFirst = oi, tFirst = ti;
            var grew = true;
            while (grew)
            {
                grew = false;
                while (oi < ourHunks.Count && ourHunks[oi].OldStart <= regionEnd)
                {
                    regionEnd = Math.Max(regionEnd, ourHunks[oi].OldEnd);
                    oi++;
                    grew = true;
                }

                while (ti < theirHunks.Count && theirHunks[ti].OldStart <= regionEnd)
                {
                    regionEnd = Math.Max(regionEnd, theirHunks[ti].OldEnd);
                    ti++;
                    grew = true;
                }
            }

            AppendLines(output, baseLines, basePosition, regionStart);

            var ourRegion = SideOfRegion(ourHunks, oFirst, oi, ourLines, baseLines, regionStart, regionEnd);
            var theirRegion = SideOfRegion(theirHunks, tFirst, ti, theirLines, baseLines, regionStart, regionEnd);
            if (oi == oFirst)
            {
                output.Append(theirRegion);
            }
            else if (ti == tFirst || string.Equals(ourRegion, theirRegion, StringComparison.Ordinal))
            {
                output.Append(ourRegion);
            }
            else
            {
                conflicts++;
                AppendConflict(output, ourRegion, theirRegion, newline);
            }


            basePosition = regionEnd;
        }

        AppendLines(output, baseLines, basePosition, baseLines.Count);
        return new MergeResult(output.ToString(), conflicts);
    }

    /// <summary>Whether a text still holds conflict markers written by <see cref="Merge"/>.</summary>
    /// <param name="text">The text.</param>
    public static bool HasConflictMarkers(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var line in LineDiff.SplitLines(text))
        {
            var trimmed = line.TrimEnd('\r', '\n');
            if (trimmed.StartsWith(OursMarker, StringComparison.Ordinal) || trimmed.StartsWith(TheirsMarker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The text one side has for the base lines <c>[regionStart, regionEnd)</c>.</summary>
    private static string SideOfRegion(IReadOnlyList<DiffHunk> hunks, int first, int end, IReadOnlyList<string> sideLines, IReadOnlyList<string> baseLines, int regionStart, int regionEnd)
    {
        var builder = new StringBuilder();
        var position = regionStart;
        for (var i = first; i < end; i++)
        {
            var hunk = hunks[i];
            AppendLines(builder, baseLines, position, hunk.OldStart);
            AppendLines(builder, sideLines, hunk.NewStart, hunk.NewEnd);
            position = hunk.OldEnd;
        }

        AppendLines(builder, baseLines, position, regionEnd);
        return builder.ToString();
    }

    /// <summary>
    /// Writes a conflict. Lines both versions start or end with are written once, outside the markers, as
    /// Git's "zealous" merge does, so a file added on both sides only conflicts where it really differs.
    /// </summary>
    private static void AppendConflict(StringBuilder output, string oursText, string theirsText, string newline)
    {
        var ours = LineDiff.SplitLines(oursText);
        var theirs = LineDiff.SplitLines(theirsText);
        var prefix = 0;
        while (prefix < ours.Count && prefix < theirs.Count && ours[prefix] == theirs[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < ours.Count - prefix && suffix < theirs.Count - prefix && ours[^(suffix + 1)] == theirs[^(suffix + 1)])
        {
            suffix++;
        }

        AppendLines(output, ours, 0, prefix);
        AppendMarkers(
            output,
            string.Concat(ours.Skip(prefix).Take(ours.Count - prefix - suffix)),
            string.Concat(theirs.Skip(prefix).Take(theirs.Count - prefix - suffix)),
            newline);
        AppendLines(output, ours, ours.Count - suffix, ours.Count);
    }

    private static void AppendMarkers(StringBuilder output, string ours, string theirs, string newline)
    {
        EnsureLineEnd(output, newline);
        output.Append(OursMarker).Append(newline);
        output.Append(ours);
        EnsureLineEnd(output, newline);
        output.Append(SeparatorMarker).Append(newline);
        output.Append(theirs);
        EnsureLineEnd(output, newline);
        output.Append(TheirsMarker).Append(newline);
    }

    private static void EnsureLineEnd(StringBuilder output, string newline)
    {
        if (output.Length > 0 && output[^1] != '\n')
        {
            output.Append(newline);
        }
    }

    private static void AppendLines(StringBuilder builder, IReadOnlyList<string> lines, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            builder.Append(lines[i]);
        }
    }

    private static string DetectNewline(string text)
    {
        var index = text.IndexOf('\n', StringComparison.Ordinal);
        return index > 0 && text[index - 1] == '\r' ? "\r\n" : "\n";
    }
}
