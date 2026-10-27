using System.Drawing;
using System.Globalization;
using System.Text;

namespace System.Windows.Forms;

/// <summary>
/// Reads and writes rich text format for <see cref="RichTextBox"/>: the text of an RTF document, and a document of
/// plain text in one font and color. Formatting inside the text is not kept: the box shows its text in one font.
/// </summary>
internal static class RtfText
{
    /// <summary>Destinations whose text is not part of the document's text.</summary>
    private static readonly HashSet<string> SkippedDestinations = new(StringComparer.Ordinal)
    {
        "fonttbl", "colortbl", "stylesheet", "info", "pict", "object", "header", "footer", "headerl", "headerr",
        "footerl", "footerr", "footnote", "listtable", "listoverridetable", "rsidtbl", "generator", "themedata",
        "colorschememapping", "latentstyles", "datastore", "xmlnstbl", "mmathPr", "fldinst", "bkmkstart", "bkmkend",
        "filetbl", "revtbl", "pgdsctbl",
    };

    /// <summary>Whether text looks like an RTF document.</summary>
    /// <param name="text">The text.</param>
    public static bool IsRtf(string text) => text.TrimStart().StartsWith(@"{\rtf", StringComparison.Ordinal);

    /// <summary>The text of an RTF document, with paragraphs as line breaks.</summary>
    /// <param name="rtf">The document.</param>
    /// <exception cref="ArgumentException">The text is not RTF, as Windows Forms reports it.</exception>
    public static string ToText(string rtf)
    {
        ArgumentNullException.ThrowIfNull(rtf);
        if (!IsRtf(rtf))
        {
            throw new ArgumentException("File format is not valid.");
        }

        var output = new StringBuilder(rtf.Length);
        var stack = new Stack<(bool Skip, int UnicodeSkip)>();
        var skip = false;
        var unicodeSkip = 1;
        var pendingSkip = 0;
        var codePage = 1252;
        var bytes = new List<byte>();
        void FlushBytes()
        {
            if (bytes.Count > 0)
            {
                output.Append(Decode(bytes, codePage));
                bytes.Clear();
            }
        }

        for (var i = 0; i < rtf.Length; i++)
        {
            var c = rtf[i];
            switch (c)
            {
                case '{':
                    FlushBytes();
                    stack.Push((skip, unicodeSkip));
                    break;
                case '}':
                    FlushBytes();
                    (skip, unicodeSkip) = stack.Count > 0 ? stack.Pop() : (false, 1);
                    break;
                case '\\':
                    if (i + 1 >= rtf.Length)
                    {
                        break;
                    }

                    var next = rtf[i + 1];
                    if (next is '\\' or '{' or '}')
                    {
                        FlushBytes();
                        if (!skip && !Skipped(ref pendingSkip))
                        {
                            output.Append(next);
                        }

                        i++;
                        break;
                    }

                    if (next == '\'')
                    {
                        // A byte in the document's code page.
                        if (i + 3 < rtf.Length && byte.TryParse(rtf.AsSpan(i + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b) && !skip && !Skipped(ref pendingSkip))
                        {
                            bytes.Add(b);
                        }

                        i += 3;
                        break;
                    }

                    if (next == '*')
                    {
                        skip = true;
                        i++;
                        break;
                    }

                    if (!char.IsAsciiLetter(next))
                    {
                        // A control symbol: ~ is a non-breaking space, - an optional hyphen.
                        FlushBytes();
                        if (!skip && next == '~')
                        {
                            output.Append('\u00A0');
                        }

                        i++;
                        break;
                    }

                    var start = i + 1;
                    var end = start;
                    while (end < rtf.Length && char.IsAsciiLetter(rtf[end]))
                    {
                        end++;
                    }

                    var word = rtf[start..end];
                    var parameterStart = end;
                    if (end < rtf.Length && (rtf[end] == '-' || char.IsAsciiDigit(rtf[end])))
                    {
                        end++;
                        while (end < rtf.Length && char.IsAsciiDigit(rtf[end]))
                        {
                            end++;
                        }
                    }

                    int? parameter = end > parameterStart && int.TryParse(rtf.AsSpan(parameterStart, end - parameterStart), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;
                    if (end < rtf.Length && rtf[end] == ' ')
                    {
                        end++;
                    }

                    i = end - 1;
                    if (word != "u")
                    {
                        FlushBytes();
                    }

                    switch (word)
                    {
                        case "ansicpg" when parameter is { } page:
                            codePage = page;
                            break;
                        case "uc" when parameter is { } count:
                            unicodeSkip = count;
                            break;
                        case "u" when parameter is { } code:
                            FlushBytes();
                            if (!skip)
                            {
                                output.Append((char)(code < 0 ? code + 65536 : code));
                            }

                            pendingSkip = unicodeSkip;
                            break;
                        case "par" or "line" or "sect" or "page" when !skip:
                            output.Append('\n');
                            break;
                        case "tab" when !skip:
                            output.Append('\t');
                            break;
                        case "emdash" when !skip:
                            output.Append('\u2014');
                            break;
                        case "endash" when !skip:
                            output.Append('\u2013');
                            break;
                        case "bullet" when !skip:
                            output.Append('\u2022');
                            break;
                        case "lquote" when !skip:
                            output.Append('\u2018');
                            break;
                        case "rquote" when !skip:
                            output.Append('\u2019');
                            break;
                        case "ldblquote" when !skip:
                            output.Append('\u201C');
                            break;
                        case "rdblquote" when !skip:
                            output.Append('\u201D');
                            break;
                        default:
                            if (SkippedDestinations.Contains(word))
                            {
                                skip = true;
                            }

                            break;
                    }

                    break;
                case '\r' or '\n':
                    break;
                default:
                    if (!skip && !Skipped(ref pendingSkip))
                    {
                        FlushBytes();
                        output.Append(c);
                    }

                    break;
            }
        }

        FlushBytes();

        // A document ends its last paragraph with \par, which is not a line of its own.
        var text = output.ToString();
        return text.EndsWith('\n') ? text[..^1] : text;
    }

    /// <summary>An RTF document of plain text, in one font and color.</summary>
    /// <param name="text">The text; line breaks become paragraphs.</param>
    /// <param name="font">The font.</param>
    /// <param name="color">The color.</param>
    public static string FromText(string text, Font font, Color color)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        var rtf = new StringBuilder();
        rtf.Append(@"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl{\f0\fnil\fcharset0 ")
            .Append(Escape(font.Name)).Append(";}}\r\n")
            .Append(CultureInfo.InvariantCulture, $@"{{\colortbl ;\red{color.R}\green{color.G}\blue{color.B};}}")
            .Append("\r\n")
            .Append(@"\viewkind4\uc1\pard\cf1\f0")
            .Append(CultureInfo.InvariantCulture, $@"\fs{(int)Math.Round(font.SizeInPoints * 2)}");
        if (font.Bold)
        {
            rtf.Append(@"\b");
        }

        if (font.Italic)
        {
            rtf.Append(@"\i");
        }

        rtf.Append(' ');
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            rtf.Append(Escape(line)).Append("\\par\r\n");
        }

        rtf.Append('}');
        return rtf.ToString();
    }

    private static bool Skipped(ref int pending)
    {
        if (pending <= 0)
        {
            return false;
        }

        pending--;
        return true;
    }

    private static string Decode(List<byte> bytes, int codePage)
    {
        try
        {
            return CodePagesEncoding(codePage).GetString([.. bytes]);
        }
        catch (ArgumentException)
        {
            return Encoding.Latin1.GetString([.. bytes]);
        }
    }

    private static Encoding CodePagesEncoding(int codePage) => codePage switch
    {
        65001 => Encoding.UTF8,
        28591 or 1252 => Encoding.Latin1,
        _ => Encoding.GetEncoding(codePage),
    };

    private static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\' or '{' or '}':
                    builder.Append('\\').Append(c);
                    break;
                case '\t':
                    builder.Append(@"\tab ");
                    break;
                case < (char)0x80:
                    builder.Append(c);
                    break;
                default:
                    builder.Append(CultureInfo.InvariantCulture, $@"\u{(short)c}?");
                    break;
            }
        }

        return builder.ToString();
    }
}
