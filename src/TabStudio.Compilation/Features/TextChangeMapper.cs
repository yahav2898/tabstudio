namespace TabStudio.Compilation.Features;

/// <summary>
/// Moves edits computed against an older text of a document onto its current text.
/// </summary>
/// <remarks>
/// A completion list is computed once, and the user keeps typing to filter it. When an item is committed, Roslyn
/// computes the change against the text the list was made for, so the edits must follow what was typed since.
/// The texts are assumed to differ in one region, which is what typing at the caret produces. An edit that touches
/// that region grows to cover it, so the typed filter text is replaced too.
/// </remarks>
internal static class TextChangeMapper
{
    /// <summary>Maps edits made for <paramref name="oldText"/> onto <paramref name="newText"/>.</summary>
    /// <param name="oldText">The text the edits were computed for.</param>
    /// <param name="newText">The current text.</param>
    /// <param name="edits">Non-overlapping edits for the old text, in document order.</param>
    /// <returns>The edits for the current text, or <see langword="null"/> when they cannot be mapped without overlapping.</returns>
    public static List<TextEdit>? Map(string oldText, string newText, IReadOnlyList<TextEdit> edits)
    {
        if (string.Equals(oldText, newText, StringComparison.Ordinal))
        {
            return [.. edits];
        }

        var (start, oldEnd, newEnd) = Diff(oldText, newText);
        var delta = newEnd - oldEnd;
        var mapped = new List<TextEdit>(edits.Count);
        foreach (var edit in edits)
        {
            var end = edit.Start + edit.Length;
            if (end < start)
            {
                mapped.Add(edit);
            }
            else if (edit.Start > oldEnd)
            {
                mapped.Add(edit with { Start = edit.Start + delta });
            }
            else
            {
                var mappedStart = Math.Min(edit.Start, start);
                var mappedEnd = Math.Max(end, oldEnd) + delta;
                mapped.Add(new TextEdit(mappedStart, mappedEnd - mappedStart, edit.NewText));
            }
        }

        for (var i = 1; i < mapped.Count; i++)
        {
            if (mapped[i].Start < mapped[i - 1].Start + mapped[i - 1].Length)
            {
                return null;
            }
        }

        return mapped;
    }

    /// <summary>Maps a position in <paramref name="oldText"/> to the same place in <paramref name="newText"/>.</summary>
    /// <returns>The position; one inside the changed region moves to the region's end.</returns>
    public static int MapPosition(string oldText, string newText, int position)
    {
        var (start, oldEnd, newEnd) = Diff(oldText, newText);
        if (position <= start)
        {
            return position;
        }

        return position >= oldEnd ? position + (newEnd - oldEnd) : newEnd;
    }

    /// <summary>Applies non-overlapping edits, given in document order, to a text.</summary>
    public static string Apply(string text, IReadOnlyList<TextEdit> edits)
    {
        var result = text;
        for (var i = edits.Count - 1; i >= 0; i--)
        {
            var edit = edits[i];
            result = string.Concat(result.AsSpan(0, edit.Start), edit.NewText, result.AsSpan(edit.Start + edit.Length));
        }

        return result;
    }

    /// <summary>Finds the one region in which two texts differ.</summary>
    /// <returns>The region's start, its end in the old text and its end in the new text.</returns>
    private static (int Start, int OldEnd, int NewEnd) Diff(string oldText, string newText)
    {
        var shorter = Math.Min(oldText.Length, newText.Length);
        var prefix = 0;
        while (prefix < shorter && oldText[prefix] == newText[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < shorter - prefix && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
        {
            suffix++;
        }

        return (prefix, oldText.Length - suffix, newText.Length - suffix);
    }
}
