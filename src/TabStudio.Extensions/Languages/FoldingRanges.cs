using System.Text.RegularExpressions;

namespace TabStudio.Extensions.Languages;

/// <summary>A range of lines that can be folded: the first line stays visible, the others are hidden.</summary>
/// <param name="StartLine">The first line, 0-based, which stays visible.</param>
/// <param name="EndLine">The last hidden line, 0-based and inclusive.</param>
/// <param name="IsRegion">Whether the range comes from region markers rather than indentation.</param>
public sealed record FoldingRange(int StartLine, int EndLine, bool IsRegion);

/// <summary>
/// Computes folding ranges as VS Code does for a language without a folding provider: by indentation, with the
/// language's region markers (<c>#region</c> … <c>#endregion</c>) on top. In off-side languages such as Python, blank
/// lines at the end of a block are left out of its fold.
/// </summary>
public static class FoldingRanges
{
    /// <summary>The most ranges computed for one document, as VS Code limits them.</summary>
    public const int MaximumRanges = 5000;

    private const int EndMarker = -2;

    /// <summary>Computes the folding ranges of a document.</summary>
    /// <param name="lines">The document's lines, without line breaks.</param>
    /// <param name="rules">The language's folding rules, or <see langword="null"/> for plain indentation folding.</param>
    /// <param name="tabSize">How many columns a tab advances to.</param>
    /// <returns>The ranges, ordered by their first line.</returns>
    public static IReadOnlyList<FoldingRange> Compute(IReadOnlyList<string> lines, FoldingRules? rules, int tabSize = 4)
    {
        ArgumentNullException.ThrowIfNull(lines);
        tabSize = Math.Max(1, tabSize);
        var offSide = rules?.OffSide ?? false;
        var ranges = new List<FoldingRange>();

        // Regions still open below the current line, innermost last; the first is a sentinel below the document.
        var open = new List<Region> { new(-1, lines.Count, lines.Count) };
        for (var line = lines.Count - 1; line >= 0; line--)
        {
            var text = lines[line];
            var indent = IndentLevel(text, tabSize);
            var previous = open[^1];
            if (indent < 0)
            {
                if (offSide)
                {
                    previous.EndAbove = line;
                }

                continue;
            }

            if (rules is not null && IsMarker(rules.MarkerStart, text))
            {
                var index = open.FindLastIndex(r => r.Indent == EndMarker);
                if (index > 0)
                {
                    open.RemoveRange(index + 1, open.Count - index - 1);
                    previous = open[index];
                    ranges.Add(new FoldingRange(line, previous.Line, IsRegion: true));
                    previous.Line = line;
                    previous.Indent = indent;
                    previous.EndAbove = line;
                    continue;
                }
            }
            else if (rules is not null && IsMarker(rules.MarkerEnd, text))
            {
                open.Add(new Region(EndMarker, line, line));
                continue;
            }

            if (previous.Indent > indent)
            {
                do
                {
                    open.RemoveAt(open.Count - 1);
                    previous = open[^1];
                }
                while (previous.Indent > indent);

                var end = previous.EndAbove - 1;
                if (end - line >= 1)
                {
                    ranges.Add(new FoldingRange(line, end, IsRegion: false));
                }
            }

            if (previous.Indent == indent)
            {
                previous.EndAbove = line;
            }
            else
            {
                open.Add(new Region(indent, line, line));
            }

            if (ranges.Count >= MaximumRanges)
            {
                break;
            }
        }

        ranges.Sort((a, b) => a.StartLine != b.StartLine ? a.StartLine.CompareTo(b.StartLine) : b.EndLine.CompareTo(a.EndLine));
        return ranges;
    }

    /// <summary>The indentation of a line in columns, or -1 for a line of only whitespace.</summary>
    /// <param name="text">The line.</param>
    /// <param name="tabSize">How many columns a tab advances to.</param>
    public static int IndentLevel(string text, int tabSize)
    {
        ArgumentNullException.ThrowIfNull(text);
        var columns = 0;
        foreach (var c in text)
        {
            if (c == ' ')
            {
                columns++;
            }
            else if (c == '\t')
            {
                columns += tabSize - (columns % tabSize);
            }
            else
            {
                return columns;
            }
        }

        return -1;
    }

    private static bool IsMarker(Regex? marker, string text)
    {
        try
        {
            return marker?.IsMatch(text) == true;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>A block still open while lines are read from the bottom up.</summary>
    private sealed class Region(int indent, int endAbove, int line)
    {
        /// <summary>The block's indentation, or <see cref="EndMarker"/> for an open region marker.</summary>
        public int Indent { get; set; } = indent;

        /// <summary>The first line below the block's last line.</summary>
        public int EndAbove { get; set; } = endAbove;

        /// <summary>The block's first line, or the end marker's line.</summary>
        public int Line { get; set; } = line;
    }
}
