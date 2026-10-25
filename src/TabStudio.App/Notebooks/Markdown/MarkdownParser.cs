using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.App.Notebooks.Markdown;

/// <summary>
/// Reads the Markdown that notebooks use into blocks: headings (<c>#</c> and underlined), paragraphs, bulleted,
/// numbered and task lists, fenced and indented code, quotes, rules and GitHub pipe tables.
/// </summary>
/// <remarks>
/// It follows CommonMark where notebooks need it and stays small: it is a reader for display, not a validator.
/// Text it does not understand is shown as it was written.
/// </remarks>
public static partial class MarkdownParser
{
    private const int MaxListDepth = 6;

    /// <summary>Reads Markdown text.</summary>
    /// <param name="markdown">The text of a Markdown cell or output.</param>
    /// <returns>The blocks, in order.</returns>
    public static IReadOnlyList<MarkdownBlock> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return ParseLines(markdown.ReplaceLineEndings("\n").Split('\n'));
    }

    private static List<MarkdownBlock> ParseLines(string[] lines)
    {
        var blocks = new List<MarkdownBlock>();
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
            }
            else if (Fence().Match(line) is { Success: true } fence)
            {
                i = ReadFencedCode(lines, i, fence, blocks);
            }
            else if (Heading().Match(line) is { Success: true } heading)
            {
                blocks.Add(new MarkdownHeading(heading.Groups["marks"].Length, MarkdownInlineParser.Parse(heading.Groups["text"].Value.Trim())));
                i++;
            }
            else if (Rule().IsMatch(line))
            {
                blocks.Add(new MarkdownRule());
                i++;
            }
            else if (Quote().IsMatch(line))
            {
                i = ReadQuote(lines, i, blocks);
            }
            else if (ListItem().IsMatch(line))
            {
                i = ReadList(lines, i, blocks);
            }
            else if (IsTableStart(lines, i))
            {
                i = ReadTable(lines, i, blocks);
            }
            else if (line.StartsWith("    ", StringComparison.Ordinal) || line.StartsWith('\t'))
            {
                i = ReadIndentedCode(lines, i, blocks);
            }
            else
            {
                i = ReadParagraph(lines, i, blocks);
            }
        }

        return blocks;
    }

    private static int ReadFencedCode(string[] lines, int start, Match fence, List<MarkdownBlock> blocks)
    {
        var marker = fence.Groups["fence"].Value;
        var indent = fence.Groups["indent"].Length;
        var language = fence.Groups["info"].Value;
        var code = new List<string>();
        var i = start + 1;
        for (; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length >= marker.Length && trimmed.All(c => c == marker[0]))
            {
                i++;
                break;
            }

            code.Add(RemoveIndent(lines[i], indent));
        }

        blocks.Add(new MarkdownCodeBlock(language.Length == 0 ? null : language, string.Join('\n', code)));
        return i;
    }

    private static int ReadQuote(string[] lines, int start, List<MarkdownBlock> blocks)
    {
        var quoted = new List<string>();
        var i = start;
        for (; i < lines.Length && Quote().IsMatch(lines[i]); i++)
        {
            quoted.Add(Quote().Replace(lines[i], string.Empty, 1));
        }

        blocks.Add(new MarkdownQuote(ParseLines([.. quoted])));
        return i;
    }

    private static int ReadList(string[] lines, int start, List<MarkdownBlock> blocks)
    {
        var items = new List<(string Marker, int Depth, StringBuilder Text)>();
        var indents = new Stack<int>();
        var baseIndent = IndentOf(lines[start]);
        var ordered = IsOrdered(ListItem().Match(lines[start]));
        var i = start;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                // A blank line ends the list unless more of it follows.
                var next = i + 1;
                while (next < lines.Length && string.IsNullOrWhiteSpace(lines[next]))
                {
                    next++;
                }

                if (next < lines.Length && (ListItem().IsMatch(lines[next]) || IndentOf(lines[next]) > baseIndent) && IndentOf(lines[next]) >= baseIndent)
                {
                    i = next;
                    continue;
                }

                break;
            }

            if (ListItem().Match(line) is { Success: true } item && !Rule().IsMatch(line))
            {
                var indent = IndentOf(line);

                // An outer item of the other kind, numbered or bulleted, starts a new list.
                if (indent < baseIndent || (indent == baseIndent && IsOrdered(item) != ordered))
                {
                    break;
                }

                while (indents.Count > 0 && indent < indents.Peek())
                {
                    indents.Pop();
                }

                if (indents.Count == 0 || indent > indents.Peek())
                {
                    indents.Push(indent);
                }

                var depth = Math.Min(indents.Count - 1, MaxListDepth);
                var (marker, text) = DescribeItem(item.Groups["marker"].Value, item.Groups["text"].Value, depth);
                items.Add((marker, depth, new StringBuilder(text)));
            }
            else if (items.Count > 0 && !StartsBlock(line))
            {
                // A continuation of the previous item, indented or not.
                items[^1].Text.Append(' ').Append(line.Trim());
            }
            else
            {
                break;
            }

            i++;
        }

        blocks.Add(new MarkdownList(items.Select(t => new MarkdownListItem(t.Marker, t.Depth, MarkdownInlineParser.Parse(t.Text.ToString()))).ToList()));
        return i;
    }

    private static (string Marker, string Text) DescribeItem(string marker, string text, int depth)
    {
        if (text.StartsWith("[ ] ", StringComparison.Ordinal) || text == "[ ]")
        {
            return ("☐", text.Length > 4 ? text[4..] : string.Empty);
        }

        if (text.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase) || text.Equals("[x]", StringComparison.OrdinalIgnoreCase))
        {
            return ("☑", text.Length > 4 ? text[4..] : string.Empty);
        }

        if (char.IsAsciiDigit(marker[0]))
        {
            return (marker[..^1] + ".", text);
        }

        return ((depth % 3) switch { 0 => "•", 1 => "◦", _ => "▪" }, text);
    }

    private static bool IsOrdered(Match item) => char.IsAsciiDigit(item.Groups["marker"].Value[0]);

    private static bool IsTableStart(string[] lines, int i) =>
        i + 1 < lines.Length
        && lines[i].Contains('|', StringComparison.Ordinal)
        && lines[i + 1].Contains('|', StringComparison.Ordinal)
        && TableSeparator().IsMatch(lines[i + 1]);

    private static int ReadTable(string[] lines, int start, List<MarkdownBlock> blocks)
    {
        var header = SplitRow(lines[start]);
        var rows = new List<IReadOnlyList<IReadOnlyList<MarkdownSpan>>>();
        var i = start + 2;
        for (; i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && lines[i].Contains('|', StringComparison.Ordinal); i++)
        {
            var cells = SplitRow(lines[i]);
            while (cells.Count < header.Count)
            {
                cells.Add([]);
            }

            rows.Add(cells.Take(header.Count).ToList());
        }

        blocks.Add(new MarkdownTable(header, rows));
        return i;
    }

    private static List<IReadOnlyList<MarkdownSpan>> SplitRow(string line)
    {
        var text = line.Trim();
        if (text.StartsWith('|'))
        {
            text = text[1..];
        }

        if (text.EndsWith('|') && !text.EndsWith("\\|", StringComparison.Ordinal))
        {
            text = text[..^1];
        }

        var cells = new List<IReadOnlyList<MarkdownSpan>>();
        var cell = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '|')
            {
                cell.Append("\\|");
                i++;
            }
            else if (text[i] == '|')
            {
                cells.Add(MarkdownInlineParser.Parse(cell.ToString().Trim()));
                cell.Clear();
            }
            else
            {
                cell.Append(text[i]);
            }
        }

        cells.Add(MarkdownInlineParser.Parse(cell.ToString().Trim()));
        return cells;
    }

    private static int ReadIndentedCode(string[] lines, int start, List<MarkdownBlock> blocks)
    {
        var code = new List<string>();
        var i = start;
        for (; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("    ", StringComparison.Ordinal))
            {
                code.Add(line[4..]);
            }
            else if (line.StartsWith('\t'))
            {
                code.Add(line[1..]);
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                code.Add(string.Empty);
            }
            else
            {
                break;
            }
        }

        while (code.Count > 0 && code[^1].Length == 0)
        {
            code.RemoveAt(code.Count - 1);
        }

        blocks.Add(new MarkdownCodeBlock(null, string.Join('\n', code)));
        return i;
    }

    private static int ReadParagraph(string[] lines, int start, List<MarkdownBlock> blocks)
    {
        var text = new StringBuilder();
        var i = start;
        for (; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            if (i > start)
            {
                if (Setext().Match(line) is { Success: true } underline)
                {
                    var level = underline.Groups["line"].Value[0] == '=' ? 1 : 2;
                    blocks.Add(new MarkdownHeading(level, MarkdownInlineParser.Parse(text.ToString())));
                    return i + 1;
                }

                if (StartsBlock(line))
                {
                    break;
                }

                var previous = lines[i - 1];
                if (previous.EndsWith("  ", StringComparison.Ordinal))
                {
                    text.Append('\n');
                }
                else if (previous.TrimEnd().EndsWith('\\'))
                {
                    text.Length--;
                    text.Append('\n');
                }
                else
                {
                    text.Append(' ');
                }
            }

            text.Append(line.Trim());
        }

        blocks.Add(new MarkdownParagraph(MarkdownInlineParser.Parse(text.ToString())));
        return i;
    }

    /// <summary>Whether a line starts a block that interrupts a paragraph or a list item's text.</summary>
    private static bool StartsBlock(string line) =>
        Fence().IsMatch(line) || Heading().IsMatch(line) || Rule().IsMatch(line) || Quote().IsMatch(line)
        || (ListItem().Match(line) is { Success: true } item && item.Groups["text"].Length > 0);

    private static int IndentOf(string line)
    {
        var width = 0;
        foreach (var c in line)
        {
            if (c == ' ')
            {
                width++;
            }
            else if (c == '\t')
            {
                width += 4;
            }
            else
            {
                break;
            }
        }

        return width;
    }

    private static string RemoveIndent(string line, int indent)
    {
        var remove = 0;
        while (remove < indent && remove < line.Length && line[remove] == ' ')
        {
            remove++;
        }

        return line[remove..];
    }

    [GeneratedRegex(@"^(?<indent> {0,3})(?<fence>`{3,}|~{3,})[ \t]*(?<info>[^`\s]*)")]
    private static partial Regex Fence();

    [GeneratedRegex(@"^ {0,3}(?<marks>#{1,6})(?:[ \t]+(?<text>.*?))?(?:[ \t]+#+)?[ \t]*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^ {0,3}(?:(?:\*[ \t]*){3,}|(?:-[ \t]*){3,}|(?:_[ \t]*){3,})$")]
    private static partial Regex Rule();

    [GeneratedRegex(@"^ {0,3}> ?")]
    private static partial Regex Quote();

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<marker>[-*+]|\d{1,9}[.)])(?:[ \t]+(?<text>.*))?$")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"^ {0,3}(?<line>=+|-+)[ \t]*$")]
    private static partial Regex Setext();

    [GeneratedRegex(@"^[ \t]*\|?[ \t]*:?-+:?[ \t]*(?:\|[ \t]*:?-+:?[ \t]*)*\|?[ \t]*$")]
    private static partial Regex TableSeparator();
}
