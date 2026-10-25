using System.Net;
using System.Text;

namespace TabStudio.App.Notebooks.Markdown;

/// <summary>
/// Reads the inline syntax of Markdown text: emphasis, code, strikethrough, links, autolinks, escapes and
/// HTML entities. Inline HTML tags are dropped, except <c>&lt;br&gt;</c>, which becomes a line break.
/// </summary>
public static class MarkdownInlineParser
{
    private const string EscapableCharacters = "\\`*_{}[]()#+-.!|~<>\"'";

    private static readonly char[] TagNameTerminators = [' ', '/', '\t', '\n'];

    /// <summary>Turns Markdown text into styled runs.</summary>
    /// <param name="text">The text of one paragraph, heading, list item or table cell.</param>
    /// <returns>The runs in order; adjacent runs with the same style are merged.</returns>
    public static IReadOnlyList<MarkdownSpan> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var spans = new List<MarkdownSpan>();
        ParseInto(text, MarkdownStyles.None, null, spans);
        return Merge(spans);
    }

    /// <summary>The text of runs without their styles, for tests and accessibility.</summary>
    /// <param name="spans">The runs.</param>
    public static string ToPlainText(IEnumerable<MarkdownSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        return string.Concat(spans.Select(s => s.Text));
    }

    private static void ParseInto(string text, MarkdownStyles style, string? link, List<MarkdownSpan> spans)
    {
        var buffer = new StringBuilder();

        void Flush()
        {
            if (buffer.Length > 0)
            {
                spans.Add(new MarkdownSpan(buffer.ToString(), style, link));
                buffer.Clear();
            }
        }

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length && EscapableCharacters.Contains(text[i + 1], StringComparison.Ordinal))
            {
                buffer.Append(text[i + 1]);
                i += 2;
            }
            else if (c == '`' && TryReadCode(text, i, out var code, out var afterCode))
            {
                Flush();
                spans.Add(new MarkdownSpan(code, style | MarkdownStyles.Code, link));
                i = afterCode;
            }
            else if (c == '!' && i + 1 < text.Length && text[i + 1] == '[' && TryReadLink(text, i + 1, out var alt, out _, out var afterImage))
            {
                // Images cannot be shown inline; their description stands in for them.
                Flush();
                spans.Add(new MarkdownSpan(alt.Length > 0 ? alt : "image", style | MarkdownStyles.Italic, link));
                i = afterImage;
            }
            else if (c == '[' && link is null && TryReadLink(text, i, out var label, out var destination, out var afterLink))
            {
                Flush();
                ParseInto(label, style, destination, spans);
                i = afterLink;
            }
            else if (c == '<' && TryReadAngle(text, i, out var angle, out var afterAngle))
            {
                Flush();
                if (angle.Length > 0)
                {
                    spans.Add(angle == "\n" ? new MarkdownSpan("\n", style, link) : new MarkdownSpan(angle, style, link ?? angle));
                }

                i = afterAngle;
            }
            else if ((c is '*' or '_') && TryReadEmphasis(text, i, out var inner, out var strength, out var afterEmphasis))
            {
                Flush();
                var emphasis = strength switch
                {
                    1 => MarkdownStyles.Italic,
                    2 => MarkdownStyles.Bold,
                    _ => MarkdownStyles.Bold | MarkdownStyles.Italic,
                };
                ParseInto(inner, style | emphasis, link, spans);
                i = afterEmphasis;
            }
            else if (c == '~' && TryReadStrikethrough(text, i, out var struck, out var afterStrike))
            {
                Flush();
                ParseInto(struck, style | MarkdownStyles.Strikethrough, link, spans);
                i = afterStrike;
            }
            else if (c == 'h' && link is null && TryReadBareUrl(text, i, out var url))
            {
                Flush();
                spans.Add(new MarkdownSpan(url, style, url));
                i += url.Length;
            }
            else if (c == '&' && TryReadEntity(text, i, out var decoded, out var afterEntity))
            {
                buffer.Append(decoded);
                i = afterEntity;
            }
            else
            {
                buffer.Append(c);
                i++;
            }
        }

        Flush();
    }

    private static bool TryReadCode(string text, int start, out string code, out int after)
    {
        var ticks = CountRun(text, start, '`');
        var search = start + ticks;
        while (search < text.Length)
        {
            var close = text.IndexOf('`', search);
            if (close < 0)
            {
                break;
            }

            var closing = CountRun(text, close, '`');
            if (closing == ticks)
            {
                code = text[(start + ticks)..close];
                if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.Trim().Length > 0)
                {
                    code = code[1..^1];
                }

                after = close + closing;
                return true;
            }

            search = close + closing;
        }

        code = string.Empty;
        after = start;
        return false;
    }

    private static bool TryReadLink(string text, int start, out string label, out string destination, out int after)
    {
        label = string.Empty;
        destination = string.Empty;
        after = start;

        var depth = 0;
        var close = -1;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
            }
            else if (text[i] == '[')
            {
                depth++;
            }
            else if (text[i] == ']' && --depth == 0)
            {
                close = i;
                break;
            }
        }

        if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
        {
            return false;
        }

        var end = text.IndexOf(')', close + 2);
        if (end < 0)
        {
            return false;
        }

        // [text](destination "title"): the title is not shown.
        var target = text[(close + 2)..end].Trim();
        var space = target.IndexOf(' ', StringComparison.Ordinal);
        if (space > 0)
        {
            target = target[..space];
        }

        label = text[(start + 1)..close];
        destination = target.Trim('<', '>');
        after = end + 1;
        return true;
    }

    private static bool TryReadAngle(string text, int start, out string value, out int after)
    {
        value = string.Empty;
        after = start;
        var end = text.IndexOf('>', start + 1);
        if (end < 0)
        {
            return false;
        }

        var inside = text[(start + 1)..end];
        if (inside.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || inside.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = inside;
            after = end + 1;
            return true;
        }

        // An HTML tag: <br> breaks the line, any other tag is dropped and its text kept.
        var tag = inside.TrimStart('/');
        var nameEnd = tag.IndexOfAny(TagNameTerminators);
        var name = nameEnd < 0 ? tag : tag[..nameEnd];
        if (name.Length == 0 || !char.IsAsciiLetter(name[0]) || !name.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        value = name.Equals("br", StringComparison.OrdinalIgnoreCase) ? "\n" : string.Empty;
        after = end + 1;
        return true;
    }

    private static bool TryReadEmphasis(string text, int start, out string inner, out int strength, out int after)
    {
        var marker = text[start];
        var run = CountRun(text, start, marker);
        strength = Math.Min(run, 3);
        inner = string.Empty;
        after = start;

        var contentStart = start + strength;
        if (contentStart >= text.Length || char.IsWhiteSpace(text[contentStart]))
        {
            return false;
        }

        // snake_case_names are not emphasis.
        if (marker == '_' && start > 0 && char.IsLetterOrDigit(text[start - 1]))
        {
            return false;
        }

        var delimiter = new string(marker, strength);
        var search = contentStart;
        while (search < text.Length)
        {
            var close = text.IndexOf(delimiter, search, StringComparison.Ordinal);
            if (close < 0)
            {
                return false;
            }

            var followedBy = close + strength < text.Length ? text[close + strength] : ' ';
            var closesHere = close > contentStart
                && !char.IsWhiteSpace(text[close - 1])
                && followedBy != marker
                && (marker != '_' || !char.IsLetterOrDigit(followedBy));
            if (closesHere)
            {
                inner = text[contentStart..close];
                after = close + strength;
                return true;
            }

            search = close + CountRun(text, close, marker);
        }

        return false;
    }

    private static bool TryReadStrikethrough(string text, int start, out string inner, out int after)
    {
        inner = string.Empty;
        after = start;
        if (CountRun(text, start, '~') != 2)
        {
            return false;
        }

        var close = text.IndexOf("~~", start + 2, StringComparison.Ordinal);
        if (close <= start + 2)
        {
            return false;
        }

        inner = text[(start + 2)..close];
        after = close + 2;
        return true;
    }

    private static bool TryReadBareUrl(string text, int start, out string url)
    {
        url = string.Empty;
        var startsUrl = text.AsSpan(start).StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || text.AsSpan(start).StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        if (!startsUrl || (start > 0 && !char.IsWhiteSpace(text[start - 1]) && text[start - 1] != '('))
        {
            return false;
        }

        var end = start;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not ('<' or '>' or '"'))
        {
            end++;
        }

        // Trailing punctuation belongs to the sentence, not the address.
        while (end > start && text[end - 1] is '.' or ',' or ')' or ';' or ':' or '!' or '?')
        {
            end--;
        }

        url = text[start..end];
        return url.Length > "https://".Length;
    }

    private static bool TryReadEntity(string text, int start, out string decoded, out int after)
    {
        decoded = string.Empty;
        after = start;
        var end = text.IndexOf(';', start);
        if (end < 0 || end - start > 10)
        {
            return false;
        }

        var entity = text[start..(end + 1)];
        var value = WebUtility.HtmlDecode(entity);
        if (value == entity)
        {
            return false;
        }

        decoded = value;
        after = end + 1;
        return true;
    }

    private static int CountRun(string text, int start, char c)
    {
        var end = start;
        while (end < text.Length && text[end] == c)
        {
            end++;
        }

        return end - start;
    }

    private static List<MarkdownSpan> Merge(List<MarkdownSpan> spans)
    {
        var merged = new List<MarkdownSpan>(spans.Count);
        foreach (var span in spans)
        {
            if (merged.Count > 0 && merged[^1] is var last && last.Style == span.Style && last.Destination == span.Destination)
            {
                merged[^1] = last with { Text = last.Text + span.Text };
            }
            else
            {
                merged.Add(span);
            }
        }

        return merged;
    }
}
