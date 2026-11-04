using System.Text;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// What InitializeComponent has besides its statements, which the designer keeps when it writes the method again:
/// the comments a statement has before it or after it on its line, and blocks of conditional compilation, such as
/// <c>#if DEBUG</c> … <c>#endif</c>, whose statements stay inside them as they were written. A region changes nothing
/// that is compiled, so the statements inside one are read as any others, and its <c>#region</c> and <c>#endregion</c>
/// lines are kept as comments are. The comments Visual Studio writes around each component's name are its own, and
/// the writer writes them again.
/// </summary>
internal static class KeptLines
{
    /// <summary>The method's body split around its statements.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="start">Where the body's first line starts.</param>
    /// <param name="end">Where the line after the body's last line starts.</param>
    /// <param name="statements">The spans of the method's statements, in order.</param>
    /// <param name="language">The file's language.</param>
    /// <param name="indent">The indentation of the statements, which the writer writes again.</param>
    /// <param name="newLine">The file's line ending.</param>
    public static BodyLayout Analyze(string text, int start, int end, IReadOnlyList<TextSpan> statements, DesignerLanguage language, string indent, string newLine)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(statements);
        var lines = new List<(int Start, int End)>();
        for (var position = start; position < end;)
        {
            var lineEnd = Math.Min(TextLines.LineEnd(text, position), end);
            lines.Add((position, lineEnd));
            position = lineEnd;
        }

        // The first line that ends after the position: a binary search, since a large form has thousands of lines and
        // each statement asks twice.
        int LineOf(int position)
        {
            var low = 0;
            var high = lines.Count - 1;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (position < lines[middle].End)
                {
                    high = middle;
                }
                else
                {
                    low = middle + 1;
                }
            }

            return high;
        }

        string Line(int i) => text[lines[i].Start..lines[i].End].TrimEnd('\r', '\n');

        // The lines each statement is on.
        var statementLines = statements.Select(s => (First: LineOf(s.Start), Last: LineOf(Math.Max(s.Start, s.End - 1)))).ToList();
        var owner = new int[lines.Count];
        Array.Fill(owner, -1);
        for (var s = 0; s < statementLines.Count; s++)
        {
            for (var i = statementLines[s].First; i <= statementLines[s].Last && i < owner.Length; i++)
            {
                owner[i] = s;
            }
        }

        var layout = new BodyLayout();

        // Blocks: from an #if to the #endif that closes it, with every statement that starts inside, which the reader
        // leaves as the block has it. A #region is not one: the lines of a region are kept as comments.
        var inBlock = new bool[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            if (owner[i] >= 0 || !Opens(Line(i)))
            {
                continue;
            }

            var depth = 0;
            var last = lines.Count - 1;
            for (var j = i; j < lines.Count; j++)
            {
                if (owner[j] >= 0)
                {
                    continue;
                }

                if (Opens(Line(j)))
                {
                    depth++;
                }
                else if (Closes(Line(j)) && --depth == 0)
                {
                    last = j;
                    break;
                }
            }

            // A statement that starts inside the block and ends after it takes the block to its end.
            for (var s = 0; s < statementLines.Count; s++)
            {
                if (statementLines[s].First >= i && statementLines[s].First <= last && statementLines[s].Last > last)
                {
                    last = statementLines[s].Last;
                }
            }

            var block = new StringBuilder();
            var first = true;
            for (var j = i; j <= last; j++)
            {
                inBlock[j] = true;
                var line = Line(j);
                var trimmed = line.StartsWith(indent, StringComparison.Ordinal) ? line[indent.Length..] : line.TrimStart();
                if (!first)
                {
                    block.Append(newLine).Append(trimmed.Length == 0 ? string.Empty : indent);
                }

                block.Append(trimmed);
                first = false;
            }

            var inside = Enumerable.Range(0, statementLines.Count).Where(s => statementLines[s].First >= i && statementLines[s].First <= last).ToList();
            layout.Blocks.Add(new KeptBlock(lines[i].Start, block.ToString(), inside));
            foreach (var s in inside)
            {
                layout.InBlocks.Add(s);
            }

            i = last;
        }

        // Comments: the lines before a statement that are not a statement, a block or the section comments the writer
        // writes again, and what follows a statement on its last line.
        var pending = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            // A comment before a block goes with the statement after it, rather than being lost.
            if (inBlock[i])
            {
                continue;
            }

            if (owner[i] < 0)
            {
                var line = Line(i).Trim();
                if (line.Length > 0)
                {
                    pending.Add(line);
                }

                continue;
            }

            var s = owner[i];
            if (statementLines[s].First != i)
            {
                continue;
            }

            var leading = WithoutSectionComments(pending, language);
            pending.Clear();
            var lastLine = statementLines[s].Last;
            var after = statements[s].End <= lines[lastLine].End ? text[statements[s].End..lines[lastLine].End].Trim() : string.Empty;
            if (leading.Count > 0 || after.Length > 0)
            {
                layout.Comments[s] = (leading, after.Length > 0 ? after : null);
            }
        }

        layout.Trailing.AddRange(WithoutSectionComments(pending, language));
        return layout;
    }

    /// <summary>
    /// Writes the kept comments back around the lines of InitializeComponent the writer wrote: each before, or after,
    /// the statement it was with; one whose statement is gone, as a property set back to its default, at the end.
    /// </summary>
    /// <param name="lines">The lines, without indentation.</param>
    /// <param name="document">The document, with its kept comments.</param>
    public static void Apply(List<string> lines, DesignDocument document)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(document);
        if (document.Comments.Count == 0)
        {
            return;
        }

        var language = document.Style.Language;
        var unused = document.Comments.ToList();
        var result = new List<string>(lines.Count + unused.Count);
        foreach (var line in lines)
        {
            var key = Key(line, language);
            var match = key.Length == 0 ? -1 : unused.FindIndex(c => c.Statement.Length > 0 && Key(c.Statement, language) == key);
            if (match < 0)
            {
                var target = Target(line, language);
                match = target is null ? -1 : unused.FindIndex(c => c.Statement.Length > 0 && Target(c.Statement, language) == target);
            }

            if (match < 0)
            {
                result.Add(line);
                continue;
            }

            var comment = unused[match];
            unused.RemoveAt(match);
            result.AddRange(comment.Leading);
            result.Add(comment.After is null ? line : line + " " + comment.After);
        }

        foreach (var comment in unused)
        {
            result.AddRange(comment.Leading);
            if (comment.After is not null)
            {
                result.Add(comment.After);
            }
        }

        BalanceRegions(result);
        lines.Clear();
        lines.AddRange(result);
    }

    /// <summary>
    /// Leaves out the <c>#region</c> and <c>#endregion</c> lines that no longer pair up. They went with the statements
    /// after them, which the writer writes in its own order, so an <c>#endregion</c> can come before its <c>#region</c>,
    /// which would not compile.
    /// </summary>
    private static void BalanceRegions(List<string> lines)
    {
        var open = new Stack<int>();
        var unpaired = new HashSet<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            switch (Directive(lines[i]))
            {
                case "region":
                    open.Push(i);
                    break;
                case "endregion" or "end region":
                    if (!open.TryPop(out _))
                    {
                        unpaired.Add(i);
                    }

                    break;
            }
        }

        unpaired.UnionWith(open);
        if (unpaired.Count == 0)
        {
            return;
        }

        var kept = lines.Where((_, i) => !unpaired.Contains(i)).ToList();
        lines.Clear();
        lines.AddRange(kept);
    }

    /// <summary>The components a block of code names, in the order it names them, of the fields given.</summary>
    /// <param name="code">The code.</param>
    /// <param name="fields">The fields' names.</param>
    public static IEnumerable<string> NamedFields(string code, IReadOnlySet<string> fields)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(fields);
        var word = new StringBuilder();
        foreach (var c in code.Append(' '))
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                word.Append(c);
                continue;
            }

            if (word.Length > 0 && fields.Contains(word.ToString()))
            {
                yield return word.ToString();
            }

            word.Clear();
        }
    }

    private static bool Opens(string line) => Directive(line) is "if";

    private static bool Closes(string line) => Directive(line) is "endif" or "end if";

    /// <summary>A directive's keyword in lower case with single spaces, such as <c>if</c> or <c>end if</c>; null for any other line.</summary>
    private static string? Directive(string line)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith('#'))
        {
            return null;
        }

        var words = trimmed[1..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return null;
        }

        var first = words[0].ToLowerInvariant();
        return first == "end" && words.Length > 1 ? "end " + words[1].ToLowerInvariant() : first;
    }

    /// <summary>
    /// The comments without the three lines Visual Studio writes before each component's section: an empty comment,
    /// the component's name, an empty comment.
    /// </summary>
    private static List<string> WithoutSectionComments(List<string> comments, DesignerLanguage language)
    {
        var result = new List<string>();
        for (var i = 0; i < comments.Count; i++)
        {
            if (i + 2 < comments.Count && IsEmptyComment(comments[i], language) && IsNameComment(comments[i + 1], language) && IsEmptyComment(comments[i + 2], language))
            {
                i += 2;
                continue;
            }

            result.Add(comments[i]);
        }

        return result;
    }

    private static string? CommentText(string line, DesignerLanguage language)
    {
        if (language == DesignerLanguage.VisualBasic)
        {
            return line.StartsWith('\'') ? line[1..] : null;
        }

        return line.StartsWith("//", StringComparison.Ordinal) && !line.StartsWith("///", StringComparison.Ordinal) ? line[2..] : null;
    }

    private static bool IsEmptyComment(string line, DesignerLanguage language) =>
        CommentText(line, language) is { } comment && string.IsNullOrWhiteSpace(comment);

    private static bool IsNameComment(string line, DesignerLanguage language) =>
        CommentText(line, language) is { } comment && comment.Trim() is { Length: > 0 } name
        && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    /// <summary>A statement as the comparison sees it: no whitespace, no <c>this.</c> or <c>Me.</c>, no namespaces of the types the designer writes.</summary>
    private static string Key(string statement, DesignerLanguage language)
    {
        var key = new string([.. statement.Where(c => !char.IsWhiteSpace(c))]).TrimEnd(';');
        var comparison = language == DesignerLanguage.VisualBasic ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var prefix in (string[])["this.", "Me.", "Global.", "System.Windows.Forms.", "System.Drawing.", "System.ComponentModel."])
        {
            key = key.Replace(prefix, string.Empty, comparison);
        }

        return language == DesignerLanguage.VisualBasic ? key.ToLowerInvariant() : key;
    }

    /// <summary>What a statement sets: its key up to <c>=</c> or <c>+=</c>, or null for a statement that sets nothing.</summary>
    private static string? Target(string statement, DesignerLanguage language)
    {
        var key = Key(statement, language);
        var equals = key.IndexOf('=', StringComparison.Ordinal);
        return equals <= 0 ? null : key[..equals].TrimEnd('+', '-');
    }
}

/// <summary>InitializeComponent's body split around its statements.</summary>
internal sealed class BodyLayout
{
    /// <summary>The blocks of conditional compilation, in order, each with the statements inside it.</summary>
    public List<KeptBlock> Blocks { get; } = [];

    /// <summary>The indexes of the statements inside a block, which the reader does not read.</summary>
    public HashSet<int> InBlocks { get; } = [];

    /// <summary>The comments before each statement and after it on its line, by the statement's index.</summary>
    public Dictionary<int, (List<string> Leading, string? After)> Comments { get; } = [];

    /// <summary>The comments after the last statement.</summary>
    public List<string> Trailing { get; } = [];
}

/// <summary>A block of conditional compilation, kept as it was written.</summary>
/// <param name="Start">Where its first line starts in the file.</param>
/// <param name="Text">Its lines, the first without indentation and the rest with the statements' indentation.</param>
/// <param name="Statements">The indexes of the statements inside it.</param>
internal sealed record KeptBlock(int Start, string Text, IReadOnlyList<int> Statements);

/// <summary>Comments the designer keeps with a statement of InitializeComponent.</summary>
/// <param name="Statement">The statement as it was written, which a rename renames.</param>
/// <param name="Leading">The comment lines before it.</param>
/// <param name="After">The comment after it on its line, or <see langword="null"/>.</param>
internal sealed record KeptComment(string Statement, IReadOnlyList<string> Leading, string? After)
{
    /// <summary>The statement as it was written, which a rename renames.</summary>
    public string Statement { get; set; } = Statement;
}
