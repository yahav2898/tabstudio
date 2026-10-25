using Microsoft.CodeAnalysis.Text;

namespace TabStudio.Compilation.Razor;

/// <summary>What the caret is on in a Razor file's markup.</summary>
internal enum RazorMarkupKind
{
    /// <summary>Text, or something IntelliSense does not complete.</summary>
    None,

    /// <summary>A directive or a transition after <c>@</c>, such as <c>@page</c> or <c>@if</c>, in markup.</summary>
    Directive,

    /// <summary>The name of a tag, such as <c>Counter</c> in <c>&lt;Counter</c> or <c>&lt;/Counter</c>.</summary>
    TagName,

    /// <summary>The name of an attribute of a tag, such as <c>Label</c> in <c>&lt;Counter Label="…"</c>.</summary>
    AttributeName,
}

/// <summary>
/// Where the caret is in a Razor file's markup, read from the text around it: the tag it is in and the word it is
/// on. C# in the file is not markup; it is answered by the generated C# instead.
/// </summary>
/// <param name="Kind">What the caret is on.</param>
/// <param name="WordStart">Where the word at the caret starts, the start of the text a completion replaces.</param>
/// <param name="WordLength">The length of the word at the caret, up to the caret and after it.</param>
/// <param name="TagName">The tag the caret is in, for an attribute or a tag name.</param>
internal sealed record RazorMarkupContext(RazorMarkupKind Kind, int WordStart, int WordLength, string? TagName)
{
    /// <summary>Reads the context at a position.</summary>
    /// <param name="text">The Razor file's text.</param>
    /// <param name="position">The caret.</param>
    public static RazorMarkupContext At(SourceText text, int position)
    {
        ArgumentNullException.ThrowIfNull(text);
        position = Math.Clamp(position, 0, text.Length);
        var start = position;
        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }

        var end = position;
        while (end < text.Length && IsWordChar(text[end]) && text[end] != '@')
        {
            end++;
        }

        // @page, @using, @if…: an @ at the start of a word that follows space, a line start or a tag's end.
        if (start < text.Length && text[start] == '@')
        {
            var before = start > 0 ? text[start - 1] : '\n';
            if (FindOpenTag(text, start) is { } attributeTag)
            {
                return new RazorMarkupContext(RazorMarkupKind.AttributeName, start, end - start, attributeTag.Name);
            }

            return char.IsWhiteSpace(before) || before is '>' or '}'
                ? new RazorMarkupContext(RazorMarkupKind.Directive, start, end - start, null)
                : new RazorMarkupContext(RazorMarkupKind.None, position, 0, null);
        }

        if (FindOpenTag(text, start) is not { } tag)
        {
            return new RazorMarkupContext(RazorMarkupKind.None, position, 0, null);
        }

        // The caret on the tag's name: right after < or </.
        if (start == tag.NameStart)
        {
            return new RazorMarkupContext(RazorMarkupKind.TagName, start, end - start, tag.Name);
        }

        // An attribute's name: a word after white space inside the tag, outside any quoted value.
        return start > 0 && char.IsWhiteSpace(text[start - 1])
            ? new RazorMarkupContext(RazorMarkupKind.AttributeName, start, end - start, tag.Name)
            : new RazorMarkupContext(RazorMarkupKind.None, position, 0, tag.Name);
    }

    /// <summary>
    /// The tag a position is inside, when it is between a tag's <c>&lt;</c> and its <c>&gt;</c> and not inside a
    /// quoted attribute value.
    /// </summary>
    private static OpenTag? FindOpenTag(SourceText text, int position)
    {
        // Back to the nearest < with no > after it; a quote changes nothing as long as the < is found first.
        var lineLimit = Math.Max(0, position - 2000);
        for (var i = position - 1; i >= lineLimit; i--)
        {
            var c = text[i];
            if (c == '>')
            {
                return null;
            }

            if (c != '<')
            {
                continue;
            }

            var nameStart = i + 1 < text.Length && text[i + 1] == '/' ? i + 2 : i + 1;
            var nameEnd = nameStart;
            while (nameEnd < text.Length && IsTagNameChar(text[nameEnd]))
            {
                nameEnd++;
            }

            if (nameStart < text.Length && nameEnd == nameStart && nameStart != position)
            {
                return null;
            }

            return IsInsideQuotes(text, nameEnd, position) ? null : new OpenTag(text.ToString(new TextSpan(nameStart, nameEnd - nameStart)), nameStart);
        }

        return null;
    }

    /// <summary>Whether a position is inside a quoted value, counting quotes from the tag's name.</summary>
    private static bool IsInsideQuotes(SourceText text, int from, int position)
    {
        char? quote = null;
        for (var i = from; i < position; i++)
        {
            var c = text[i];
            if (quote is null && c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == quote)
            {
                quote = null;
            }
        }

        return quote is not null;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or ':' or '@' or '.';

    private static bool IsTagNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or ':' or '.';

    /// <summary>A tag being written: its name and where the name starts.</summary>
    private sealed record OpenTag(string Name, int NameStart);
}
