namespace TabStudio.App.Editing;

/// <summary>Where a line of an earlier version of a text is now.</summary>
/// <param name="Line">One-based line in the current text, or 0 when the line is gone.</param>
/// <param name="IsChanged">Whether the line's text changed since.</param>
public readonly record struct MappedLine(int Line, bool IsChanged);

/// <summary>
/// Maps the lines of an earlier version of a text onto the current one, so results computed for that version (such
/// as Live Unit Testing's markers, which come from the last build) land on the right lines while the user keeps
/// typing.
/// </summary>
/// <remarks>
/// Edits between two moments are usually in one place, so the texts are compared from both ends: lines before the
/// first difference keep their numbers, lines after the last difference move by the number of lines added or
/// removed, and lines in between changed. When as many lines are between the differences before and after, they
/// are taken to be the same lines edited; otherwise they are gone.
/// </remarks>
public static class LineMapper
{
    /// <summary>Maps every line of an earlier text.</summary>
    /// <param name="before">The lines of the earlier text.</param>
    /// <param name="after">The lines of the current text.</param>
    /// <returns>For each earlier line, in order, where it is now.</returns>
    public static MappedLine[] Map(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var prefix = 0;
        while (prefix < before.Count && prefix < after.Count && string.Equals(before[prefix], after[prefix], StringComparison.Ordinal))
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < before.Count - prefix && suffix < after.Count - prefix
            && string.Equals(before[before.Count - 1 - suffix], after[after.Count - 1 - suffix], StringComparison.Ordinal))
        {
            suffix++;
        }

        var shift = after.Count - before.Count;
        var changedBefore = before.Count - prefix - suffix;
        var changedAfter = after.Count - prefix - suffix;
        var result = new MappedLine[before.Count];
        for (var i = 0; i < before.Count; i++)
        {
            if (i < prefix)
            {
                result[i] = new MappedLine(i + 1, false);
            }
            else if (i >= before.Count - suffix)
            {
                result[i] = new MappedLine(i + 1 + shift, false);
            }
            else
            {
                result[i] = changedBefore == changedAfter ? new MappedLine(i + 1, true) : new MappedLine(0, true);
            }
        }

        return result;
    }
}
