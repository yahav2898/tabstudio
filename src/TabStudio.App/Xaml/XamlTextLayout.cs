namespace TabStudio.App.Xaml;

/// <summary>Lines, indentation and line endings of XAML text, so that edits look as if a person had typed them.</summary>
internal static class XamlTextLayout
{
    /// <summary>The indentation used when a file has no nested elements on lines of their own to learn it from.</summary>
    public const string DefaultIndentUnit = "    ";

    /// <summary>The offset where the line containing <paramref name="offset"/> starts.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">An offset in it.</param>
    public static int LineStart(string text, int offset) =>
        offset <= 0 ? 0 : text.LastIndexOf('\n', Math.Min(offset, text.Length) - 1) + 1;

    /// <summary>The offset where the line containing <paramref name="offset"/> ends, before its line break.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">An offset in it.</param>
    public static int LineEnd(string text, int offset)
    {
        var newline = text.IndexOf('\n', Math.Min(offset, text.Length));
        if (newline < 0)
        {
            return text.Length;
        }

        return newline > 0 && text[newline - 1] == '\r' ? newline - 1 : newline;
    }

    /// <summary>The spaces and tabs at the start of the line containing <paramref name="offset"/>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">An offset in it.</param>
    public static string Indentation(string text, int offset)
    {
        var start = LineStart(text, offset);
        var end = start;
        while (end < text.Length && text[end] is ' ' or '\t')
        {
            end++;
        }

        return text[start..end];
    }

    /// <summary>Whether only spaces and tabs come before <paramref name="offset"/> on its line.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">An offset in it.</param>
    public static bool StartsLine(string text, int offset)
    {
        var start = LineStart(text, offset);
        return text.AsSpan(start, offset - start).IsWhiteSpace();
    }

    /// <summary>The line break the text uses: <c>\r\n</c> when it has any, otherwise <c>\n</c>.</summary>
    /// <param name="text">The text.</param>
    public static string NewLine(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary>
    /// One level of indentation, learned from the first element that starts a line inside an element that starts a
    /// line: two spaces in Avalonia's templates, four in many projects, or a tab.
    /// </summary>
    /// <param name="markup">The parsed XAML.</param>
    public static string IndentUnit(XamlMarkup markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        var text = markup.Text;
        foreach (var element in markup.Elements)
        {
            if (element.Parent is not { } parent || !StartsLine(text, element.Start) || !StartsLine(text, parent.Start))
            {
                continue;
            }

            var inner = Indentation(text, element.Start);
            var outer = Indentation(text, parent.Start);
            if (inner.Length > outer.Length && inner.StartsWith(outer, StringComparison.Ordinal))
            {
                return inner[outer.Length..];
            }
        }

        return DefaultIndentUnit;
    }

    /// <summary>
    /// Lays out a XAML snippet at an indentation: its first line as is, and every further line at
    /// <paramref name="indentation"/> plus one <paramref name="unit"/> for each two spaces it starts with.
    /// </summary>
    /// <param name="snippet">The snippet, written with two spaces per level, such as a Toolbox item's XAML.</param>
    /// <param name="indentation">Where its first line goes.</param>
    /// <param name="unit">One level of indentation in the file.</param>
    /// <param name="newline">The file's line break.</param>
    public static string Indent(string snippet, string indentation, string unit, string newline)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        var lines = snippet.Trim().ReplaceLineEndings("\n").Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            var levels = (lines[i].Length - trimmed.Length) / 2;
            lines[i] = trimmed.Length == 0 ? string.Empty : indentation + string.Concat(Enumerable.Repeat(unit, levels)) + trimmed;
        }

        return string.Join(newline, lines);
    }
}
