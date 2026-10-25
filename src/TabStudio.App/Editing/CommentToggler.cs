using AvaloniaEdit.Document;

namespace TabStudio.App.Editing;

/// <summary>
/// Comments or uncomments whole lines, like Visual Studio's Toggle Line Comment (Ctrl+/ or Ctrl+K, Ctrl+C).
/// </summary>
public static class CommentToggler
{
    /// <summary>
    /// Toggles comments on a range of lines. If every non-blank line is already commented, the comments are
    /// removed; otherwise every non-blank line is commented at the smallest indentation of the range.
    /// </summary>
    /// <param name="document">The document to change. The change is one undo step.</param>
    /// <param name="firstLine">The first line, 1-based.</param>
    /// <param name="lastLine">The last line, 1-based and inclusive.</param>
    /// <param name="fileType">The file type, which decides the comment syntax.</param>
    /// <returns><see langword="true"/> when the file type has comments and the document changed.</returns>
    public static bool Toggle(TextDocument document, int firstLine, int lastLine, FileType fileType)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fileType);

        var (open, close) = fileType.LineComment is { } prefix
            ? (prefix, string.Empty)
            : fileType.BlockComment ?? (string.Empty, string.Empty);
        if (open.Length == 0)
        {
            return false;
        }

        firstLine = Math.Clamp(firstLine, 1, document.LineCount);
        lastLine = Math.Clamp(lastLine, firstLine, document.LineCount);
        var lines = Enumerable.Range(firstLine, lastLine - firstLine + 1)
            .Select(document.GetLineByNumber)
            .Where(line => !string.IsNullOrWhiteSpace(document.GetText(line)))
            .ToList();
        if (lines.Count == 0)
        {
            return false;
        }

        var uncomment = lines.All(line => IsCommented(document.GetText(line), open, close));
        var indent = lines.Min(line => LeadingWhitespace(document.GetText(line)));

        document.BeginUpdate();
        try
        {
            // Work bottom-up so earlier offsets stay valid.
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                var text = document.GetText(line);
                if (uncomment)
                {
                    Uncomment(document, line, text, open, close);
                }
                else
                {
                    if (close.Length > 0)
                    {
                        document.Insert(line.EndOffset, " " + close);
                    }

                    document.Insert(line.Offset + indent, open + " ");
                }
            }
        }
        finally
        {
            document.EndUpdate();
        }

        return true;
    }

    private static bool IsCommented(string text, string open, string close)
    {
        var trimmed = text.Trim();
        return trimmed.StartsWith(open, StringComparison.Ordinal)
            && (close.Length == 0 || trimmed.EndsWith(close, StringComparison.Ordinal));
    }

    private static void Uncomment(TextDocument document, DocumentLine line, string text, string open, string close)
    {
        if (close.Length > 0)
        {
            var end = text.TrimEnd().Length;
            var closeStart = end - close.Length;
            var removeFrom = closeStart > 0 && text[closeStart - 1] == ' ' ? closeStart - 1 : closeStart;
            document.Remove(line.Offset + removeFrom, end - removeFrom);
        }

        var start = LeadingWhitespace(text);
        var length = open.Length + (start + open.Length < text.Length && text[start + open.Length] == ' ' ? 1 : 0);
        document.Remove(line.Offset + start, length);
    }

    private static int LeadingWhitespace(string text)
    {
        var count = 0;
        while (count < text.Length && char.IsWhiteSpace(text[count]))
        {
            count++;
        }

        return count;
    }
}
