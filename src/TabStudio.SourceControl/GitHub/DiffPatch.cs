using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.SourceControl.GitHub;

/// <summary>How a line of a diff relates to the two versions of the file.</summary>
public enum PatchLineKind
{
    /// <summary>Unchanged, shown around a change.</summary>
    Context,

    /// <summary>Only in the new version.</summary>
    Added,

    /// <summary>Only in the old version.</summary>
    Removed,
}

/// <summary>A line of a unified diff, with its numbers in the versions it is in.</summary>
/// <param name="Kind">Added, removed or unchanged.</param>
/// <param name="OldLine">Its 1-based number in the old version, unless it was added.</param>
/// <param name="NewLine">Its 1-based number in the new version, unless it was removed.</param>
/// <param name="Text">The line, without the diff's prefix.</param>
public sealed record PatchLine(PatchLineKind Kind, int? OldLine, int? NewLine, string Text);

/// <summary>
/// A file's unified diff as GitHub gives it for a pull request: the changed lines with a few unchanged lines
/// around them. GitHub accepts review comments only on these lines, on the side they belong to.
/// </summary>
public sealed partial class DiffPatch
{
    private readonly HashSet<int> _left = [];
    private readonly HashSet<int> _right = [];

    private DiffPatch(IReadOnlyList<PatchLine> lines)
    {
        Lines = lines;
        foreach (var line in lines)
        {
            if (line.Kind != PatchLineKind.Added && line.OldLine is { } old)
            {
                _left.Add(old);
            }

            if (line.Kind != PatchLineKind.Removed && line.NewLine is { } added)
            {
                _right.Add(added);
            }
        }
    }

    /// <summary>A diff without lines, for binary files and changes too large for GitHub to show.</summary>
    public static DiffPatch Empty { get; } = new([]);

    /// <summary>The lines, in order.</summary>
    public IReadOnlyList<PatchLine> Lines { get; }

    /// <summary>Reads a unified diff.</summary>
    /// <param name="patch">The diff's hunks, each starting with an <c>@@ -a,b +c,d @@</c> header; <see langword="null"/> for none.</param>
    public static DiffPatch Parse(string? patch)
    {
        if (string.IsNullOrEmpty(patch))
        {
            return Empty;
        }

        var lines = new List<PatchLine>();
        int oldLine = 0, newLine = 0;
        var inHunk = false;
        foreach (var raw in patch.ReplaceLineEndings("\n").Split('\n'))
        {
            if (HunkHeader().Match(raw) is { Success: true } header)
            {
                oldLine = int.Parse(header.Groups["old"].Value, CultureInfo.InvariantCulture);
                newLine = int.Parse(header.Groups["new"].Value, CultureInfo.InvariantCulture);
                inHunk = true;
                continue;
            }

            if (!inHunk || raw.Length == 0 || raw[0] == '\\')
            {
                // Text before the first hunk, the end of the patch, and "\ No newline at end of file".
                continue;
            }

            var text = raw[1..];
            switch (raw[0])
            {
                case '+':
                    lines.Add(new PatchLine(PatchLineKind.Added, null, newLine++, text));
                    break;
                case '-':
                    lines.Add(new PatchLine(PatchLineKind.Removed, oldLine++, null, text));
                    break;
                default:
                    lines.Add(new PatchLine(PatchLineKind.Context, oldLine++, newLine++, text));
                    break;
            }
        }

        return new DiffPatch(lines);
    }

    /// <summary>Whether a line is part of the diff, so a review comment can go on it.</summary>
    /// <param name="side">The version the line counts in.</param>
    /// <param name="line">The 1-based line number.</param>
    public bool Contains(DiffSide side, int line) => (side == DiffSide.Left ? _left : _right).Contains(line);

    /// <summary>The line of the diff nearest to a line, for a comment that missed the diff by a little.</summary>
    /// <param name="side">The version the line counts in.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="maximumDistance">How far away the nearest line may be.</param>
    /// <returns>The line itself when it is in the diff, the nearest one within the distance, or <see langword="null"/>.</returns>
    public int? Nearest(DiffSide side, int line, int maximumDistance)
    {
        for (var distance = 0; distance <= maximumDistance; distance++)
        {
            if (Contains(side, line + distance))
            {
                return line + distance;
            }

            if (distance > 0 && Contains(side, line - distance))
            {
                return line - distance;
            }
        }

        return null;
    }

    [GeneratedRegex(@"^@@ -(?<old>\d+)(?:,\d+)? \+(?<new>\d+)(?:,\d+)? @@", RegexOptions.CultureInvariant)]
    private static partial Regex HunkHeader();
}
