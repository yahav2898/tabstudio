namespace TabStudio.LanguageServers.Documents;

/// <summary>A place in a text as the protocols count it: zero-based line and UTF-16 character in that line.</summary>
/// <param name="Line">Zero-based line.</param>
/// <param name="Character">Zero-based UTF-16 code unit in the line.</param>
public readonly record struct TextPosition(int Line, int Character);

/// <summary>A range of a text: zero-based character offset and length.</summary>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>Offset just past the last character.</summary>
    public int End => Start + Length;

    /// <summary>Whether an offset is in the span or just after it.</summary>
    /// <param name="offset">A zero-based offset.</param>
    public bool Touches(int offset) => offset >= Start && offset <= End;

    /// <summary>The span from one offset to another.</summary>
    /// <param name="start">The first offset.</param>
    /// <param name="end">The offset just past the end; clamped to be at least <paramref name="start"/>.</param>
    public static TextSpan FromBounds(int start, int end) => new(start, Math.Max(0, end - start));
}

/// <summary>
/// One version of a document's text, with the start of every line, so offsets and protocol positions convert both
/// ways. <c>\n</c>, <c>\r\n</c> and <c>\r</c> all end a line, as the protocols say. Immutable and thread-safe.
/// </summary>
public sealed class TextSnapshot
{
    private int[]? _lineStarts;

    /// <summary>Creates a snapshot.</summary>
    /// <param name="text">The text.</param>
    /// <param name="version">The document version it belongs to.</param>
    public TextSnapshot(string text, int version)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Version = version;
    }

    /// <summary>The text.</summary>
    public string Text { get; }

    /// <summary>The document version, which goes up with every change.</summary>
    public int Version { get; }

    /// <summary>The number of lines; an empty text has one.</summary>
    public int LineCount => LineStarts.Length;

    private int[] LineStarts => _lineStarts ??= ComputeLineStarts(Text);

    /// <summary>The offset of a position, clamped into the text and into its line.</summary>
    /// <param name="position">The position.</param>
    public int GetOffset(TextPosition position)
    {
        var starts = LineStarts;
        if (position.Line < 0)
        {
            return 0;
        }

        if (position.Line >= starts.Length)
        {
            return Text.Length;
        }

        var start = starts[position.Line];
        return Math.Clamp(start + Math.Max(0, position.Character), start, LineContentEnd(position.Line));
    }

    /// <summary>The position of an offset, clamped into the text.</summary>
    /// <param name="offset">A zero-based offset.</param>
    public TextPosition GetPosition(int offset)
    {
        var starts = LineStarts;
        offset = Math.Clamp(offset, 0, Text.Length);
        var line = Array.BinarySearch(starts, offset);
        if (line < 0)
        {
            line = ~line - 1;
        }

        return new TextPosition(line, offset - starts[line]);
    }

    /// <summary>The text of a line, without its line break.</summary>
    /// <param name="line">Zero-based line.</param>
    public string GetLineText(int line)
    {
        if (line < 0 || line >= LineStarts.Length)
        {
            return string.Empty;
        }

        var start = LineStarts[line];
        return Text[start..LineContentEnd(line)];
    }

    /// <summary>The snapshot after one change, with the next version.</summary>
    /// <param name="offset">Where the change starts.</param>
    /// <param name="removedLength">How many characters it removes.</param>
    /// <param name="insertedText">The text it inserts.</param>
    public TextSnapshot WithChange(int offset, int removedLength, string insertedText)
    {
        ArgumentNullException.ThrowIfNull(insertedText);
        offset = Math.Clamp(offset, 0, Text.Length);
        removedLength = Math.Clamp(removedLength, 0, Text.Length - offset);
        var text = string.Concat(Text.AsSpan(0, offset), insertedText, Text.AsSpan(offset + removedLength));
        return new TextSnapshot(text, Version + 1);
    }

    /// <summary>The offset just past a line's last character, before its line break.</summary>
    private int LineContentEnd(int line)
    {
        var starts = LineStarts;
        if (line + 1 >= starts.Length)
        {
            return Text.Length;
        }

        var next = starts[line + 1];
        if (next >= 2 && Text[next - 2] == '\r' && Text[next - 1] == '\n')
        {
            return next - 2;
        }

        return next - 1;
    }

    private static int[] ComputeLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                starts.Add(i + 1);
            }
            else if (character == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return [.. starts];
    }
}
