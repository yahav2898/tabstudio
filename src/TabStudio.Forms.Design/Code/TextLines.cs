using Microsoft.CodeAnalysis.Text;

namespace TabStudio.Forms.Design.Code;

/// <summary>Lines of a file's text, which the designer edits whole so that the rest of the file keeps every character.</summary>
internal static class TextLines
{
    /// <summary>Where the line a position is on starts.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position.</param>
    public static int LineStart(string text, int position) =>
        position <= 0 ? 0 : text.LastIndexOf('\n', Math.Min(position, text.Length) - 1) + 1;

    /// <summary>Where the line a position is on ends, after its line break.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position.</param>
    public static int LineEnd(string text, int position)
    {
        var end = position >= text.Length ? -1 : text.IndexOf('\n', position);
        return end < 0 ? text.Length : end + 1;
    }

    /// <summary>The whole lines a span is on, line break included.</summary>
    /// <param name="text">The text.</param>
    /// <param name="span">The span.</param>
    public static (int Start, int End) LineSpan(string text, TextSpan span) => (LineStart(text, span.Start), LineEnd(text, span.End));

    /// <summary>Whether the line that starts at a position has nothing but whitespace.</summary>
    /// <param name="text">The text.</param>
    /// <param name="lineStart">Where the line starts.</param>
    public static bool IsBlankLine(string text, int lineStart)
    {
        if (lineStart < 0 || lineStart >= text.Length)
        {
            return false;
        }

        var end = LineEnd(text, lineStart);
        return text.AsSpan(lineStart, end - lineStart).IsWhiteSpace();
    }

    /// <summary>The whitespace from the start of a position's line to the position.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position.</param>
    public static string IndentOf(string text, int position)
    {
        var start = LineStart(text, position);
        return new string([.. text.AsSpan(start, position - start).ToArray().TakeWhile(char.IsWhiteSpace)]);
    }

    /// <summary>The whitespace before the last line's text of a node's leading trivia.</summary>
    /// <param name="leadingTrivia">The trivia.</param>
    public static string Indentation(string leadingTrivia)
    {
        var lastLine = leadingTrivia.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n')[^1];
        return new string([.. lastLine.TakeWhile(char.IsWhiteSpace)]);
    }

    /// <summary>The text's line ending: <c>\r\n</c> when it has one, otherwise <c>\n</c>.</summary>
    /// <param name="text">The text.</param>
    public static string NewLineOf(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
}
