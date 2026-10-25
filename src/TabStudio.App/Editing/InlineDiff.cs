using System.Globalization;
using TabStudio.SourceControl.Merging;

namespace TabStudio.App.Editing;

/// <summary>How a line of an inline diff relates to the two versions of the file.</summary>
public enum DiffLineKind
{
    /// <summary>The line is the same in both versions.</summary>
    Unchanged,

    /// <summary>The line is only in the old version.</summary>
    Removed,

    /// <summary>The line is only in the new version.</summary>
    Added,

    /// <summary>A run of unchanged lines, folded away; showing it expands them.</summary>
    Collapsed,
}

/// <summary>A line of an inline diff.</summary>
/// <param name="Kind">How the line relates to the two versions.</param>
/// <param name="Text">The line without its line ending; for a collapsed line, what it hides.</param>
/// <param name="OldNumber">The 1-based line number in the old version, for unchanged and removed lines.</param>
/// <param name="NewNumber">The 1-based line number in the new version, for unchanged and added lines.</param>
/// <param name="HiddenStart">For a collapsed line, where the lines it hides start among all the diff's lines.</param>
/// <param name="HiddenCount">For a collapsed line, how many unchanged lines it hides.</param>
public sealed record DiffLine(DiffLineKind Kind, string Text, int? OldNumber, int? NewNumber, int HiddenStart = 0, int HiddenCount = 0);

/// <summary>
/// The changes between two versions of a text, as Visual Studio's inline diff shows them: removed lines
/// followed by the lines that replace them, with a few unchanged lines of context around each change and
/// longer unchanged runs collapsed into one line that can be expanded.
/// </summary>
public sealed class InlineDiff
{
    /// <summary>How many unchanged lines are kept on each side of a change.</summary>
    public const int DefaultContext = 3;

    private readonly IReadOnlyList<DiffLine> _all;

    private InlineDiff(IReadOnlyList<DiffLine> all, IReadOnlyList<DiffLine> lines, int added, int removed, int largestLineNumber)
    {
        _all = all;
        Lines = lines;
        AddedCount = added;
        RemovedCount = removed;
        LargestLineNumber = largestLineNumber;
    }

    /// <summary>The lines shown, in order.</summary>
    public IReadOnlyList<DiffLine> Lines { get; }

    /// <summary>How many lines were added.</summary>
    public int AddedCount { get; }

    /// <summary>How many lines were removed.</summary>
    public int RemovedCount { get; }

    /// <summary>Whether the versions differ at all.</summary>
    public bool HasChanges => AddedCount + RemovedCount > 0;

    /// <summary>The largest line number of either version, for sizing a line number margin.</summary>
    public int LargestLineNumber { get; }

    /// <summary>The lines shown, one per line of text, for an editor.</summary>
    public string Text => string.Join('\n', Lines.Select(l => l.Text));

    /// <summary>Compares two versions of a text.</summary>
    /// <param name="oldText">The old version.</param>
    /// <param name="newText">The new version.</param>
    /// <param name="context">How many unchanged lines to keep around each change.</param>
    public static InlineDiff Create(string oldText, string newText, int context = DefaultContext)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        ArgumentOutOfRangeException.ThrowIfNegative(context);

        var oldLines = LineDiff.SplitLines(oldText);
        var newLines = LineDiff.SplitLines(newText);
        var all = new List<DiffLine>(Math.Max(oldLines.Count, newLines.Count));
        int oldIndex = 0, newIndex = 0, added = 0, removed = 0;

        foreach (var hunk in LineDiff.Compute(oldLines, newLines))
        {
            // Between two hunks the versions match line for line.
            for (; oldIndex < hunk.OldStart; oldIndex++, newIndex++)
            {
                all.Add(new DiffLine(DiffLineKind.Unchanged, WithoutLineEnding(newLines[newIndex]), oldIndex + 1, newIndex + 1));
            }

            for (var i = hunk.OldStart; i < hunk.OldEnd; i++)
            {
                all.Add(new DiffLine(DiffLineKind.Removed, WithoutLineEnding(oldLines[i]), i + 1, null));
            }

            for (var i = hunk.NewStart; i < hunk.NewEnd; i++)
            {
                all.Add(new DiffLine(DiffLineKind.Added, WithoutLineEnding(newLines[i]), null, i + 1));
            }

            removed += hunk.OldLength;
            added += hunk.NewLength;
            oldIndex = hunk.OldEnd;
            newIndex = hunk.NewEnd;
        }

        for (; oldIndex < oldLines.Count; oldIndex++, newIndex++)
        {
            all.Add(new DiffLine(DiffLineKind.Unchanged, WithoutLineEnding(newLines[newIndex]), oldIndex + 1, newIndex + 1));
        }

        return new InlineDiff(all, Collapse(all, context), added, removed, Math.Max(oldLines.Count, newLines.Count));
    }

    /// <summary>Shows the lines a collapsed line hides.</summary>
    /// <param name="index">The index of a collapsed line in <see cref="Lines"/>.</param>
    /// <returns>The diff with those lines shown, or this diff when the line is not collapsed.</returns>
    public InlineDiff Expand(int index)
    {
        if (index < 0 || index >= Lines.Count || Lines[index] is not { Kind: DiffLineKind.Collapsed } collapsed)
        {
            return this;
        }

        var lines = new List<DiffLine>(Lines.Count + collapsed.HiddenCount);
        lines.AddRange(Lines.Take(index));
        lines.AddRange(_all.Skip(collapsed.HiddenStart).Take(collapsed.HiddenCount));
        lines.AddRange(Lines.Skip(index + 1));
        return new InlineDiff(_all, lines, AddedCount, RemovedCount, LargestLineNumber);
    }

    /// <summary>Keeps the context around each change and folds longer unchanged runs into one line.</summary>
    private static List<DiffLine> Collapse(List<DiffLine> all, int context)
    {
        var lines = new List<DiffLine>();
        var start = 0;
        while (start < all.Count)
        {
            if (all[start].Kind != DiffLineKind.Unchanged)
            {
                lines.Add(all[start++]);
                continue;
            }

            var end = start;
            while (end < all.Count && all[end].Kind == DiffLineKind.Unchanged)
            {
                end++;
            }

            // Context is kept after the previous change and before the next one; the file's own start and end need none.
            var keepBefore = start == 0 ? 0 : context;
            var keepAfter = end == all.Count ? 0 : context;
            var hidden = end - start - keepBefore - keepAfter;
            if (hidden >= 2 || (hidden == 1 && start == 0 && end == all.Count))
            {
                lines.AddRange(all.Skip(start).Take(keepBefore));
                lines.Add(new DiffLine(DiffLineKind.Collapsed, Describe(hidden), null, null, start + keepBefore, hidden));
                lines.AddRange(all.Skip(end - keepAfter).Take(keepAfter));
            }
            else
            {
                lines.AddRange(all.Skip(start).Take(end - start));
            }

            start = end;
        }

        return lines;
    }

    private static string Describe(int hidden) => hidden == 1
        ? "… 1 unchanged line"
        : $"… {hidden.ToString(CultureInfo.CurrentCulture)} unchanged lines";

    private static string WithoutLineEnding(string line) => line.TrimEnd('\n').TrimEnd('\r');
}
