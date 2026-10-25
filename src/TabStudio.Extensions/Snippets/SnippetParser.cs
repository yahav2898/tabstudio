using System.Text;
using TabStudio.Extensions.Languages;

namespace TabStudio.Extensions.Snippets;

/// <summary>
/// Parses VS Code's snippet syntax: tab stops (<c>$1</c>, <c>${1}</c>), placeholders (<c>${1:text}</c>, nested), choices
/// (<c>${1|a,b|}</c>), variables (<c>$NAME</c>, <c>${NAME:default}</c>) and transforms (<c>${1/regex/format/flags}</c>), with
/// <c>\$</c>, <c>\}</c> and <c>\\</c> escapes. Like VS Code, it never fails: a construct that does not parse is text.
/// </summary>
public static class SnippetParser
{
    private static readonly string[] Modifiers = ["upcase", "downcase", "capitalize", "camelcase", "pascalcase"];

    /// <summary>Parses a snippet body.</summary>
    /// <param name="body">The body, with its lines joined by <c>\n</c>.</param>
    public static IReadOnlyList<SnippetNode> Parse(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new Parser(body).ParseNodes(insideBraces: false);
    }

    private sealed class Parser(string source)
    {
        private int _pos;

        private bool AtEnd => _pos >= source.Length;

        private char Current => source[_pos];

        /// <summary>Parses until the end, or until an unescaped <c>}</c> (not consumed) inside braces.</summary>
        public List<SnippetNode> ParseNodes(bool insideBraces)
        {
            var nodes = new List<SnippetNode>();
            var text = new StringBuilder();
            while (!AtEnd)
            {
                var c = Current;
                if (c == '\\' && _pos + 1 < source.Length && source[_pos + 1] is '$' or '}' or '\\')
                {
                    text.Append(source[_pos + 1]);
                    _pos += 2;
                    continue;
                }

                if (c == '}' && insideBraces)
                {
                    break;
                }

                if (c == '$')
                {
                    var start = _pos;
                    if (TryParseDollar(out var node))
                    {
                        Flush(text, nodes);
                        nodes.Add(node);
                        continue;
                    }

                    _pos = start + 1;
                    text.Append('$');
                    continue;
                }

                text.Append(c);
                _pos++;
            }

            Flush(text, nodes);
            return nodes;
        }

        private static void Flush(StringBuilder text, List<SnippetNode> nodes)
        {
            if (text.Length > 0)
            {
                nodes.Add(new SnippetText(text.ToString()));
                text.Clear();
            }
        }

        private bool Accept(char c)
        {
            if (!AtEnd && Current == c)
            {
                _pos++;
                return true;
            }

            return false;
        }

        private bool TryReadInt(out int value)
        {
            var start = _pos;
            while (!AtEnd && char.IsAsciiDigit(Current))
            {
                _pos++;
            }

            if (_pos == start || !int.TryParse(source.AsSpan(start, _pos - start), out value))
            {
                _pos = start;
                value = 0;
                return false;
            }

            return true;
        }

        private bool TryReadName(out string name)
        {
            var start = _pos;
            if (!AtEnd && (char.IsAsciiLetter(Current) || Current == '_'))
            {
                _pos++;
                while (!AtEnd && (char.IsAsciiLetterOrDigit(Current) || Current == '_'))
                {
                    _pos++;
                }
            }

            name = source[start.._pos];
            return name.Length > 0;
        }

        /// <summary>Parses what starts at a <c>$</c>; on failure the position is left anywhere and the caller restores it.</summary>
        private bool TryParseDollar(out SnippetNode node)
        {
            node = null!;
            _pos++;
            if (TryReadInt(out var index))
            {
                node = new SnippetTabStop(index, []);
                return true;
            }

            if (TryReadName(out var name))
            {
                node = new SnippetVariable(name, []);
                return true;
            }

            if (!Accept('{'))
            {
                return false;
            }

            if (TryReadInt(out index))
            {
                if (Accept('}'))
                {
                    node = new SnippetTabStop(index, []);
                    return true;
                }

                if (Accept(':'))
                {
                    var children = ParseNodes(insideBraces: true);
                    if (Accept('}'))
                    {
                        node = new SnippetTabStop(index, children);
                        return true;
                    }

                    return false;
                }

                if (Accept('|'))
                {
                    if (TryParseChoices(out var choices))
                    {
                        node = new SnippetTabStop(index, [], choices);
                        return true;
                    }

                    return false;
                }

                if (Accept('/') && TryParseTransform(out var transform))
                {
                    node = new SnippetTabStop(index, [], Transform: transform);
                    return true;
                }

                return false;
            }

            if (TryReadName(out name))
            {
                if (Accept('}'))
                {
                    node = new SnippetVariable(name, []);
                    return true;
                }

                if (Accept(':'))
                {
                    var children = ParseNodes(insideBraces: true);
                    if (Accept('}'))
                    {
                        node = new SnippetVariable(name, children);
                        return true;
                    }

                    return false;
                }

                if (Accept('/') && TryParseTransform(out var transform))
                {
                    node = new SnippetVariable(name, [], transform);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Parses <c>a,b,c|}</c> after <c>${1|</c>.</summary>
        private bool TryParseChoices(out List<string> choices)
        {
            choices = [];
            var option = new StringBuilder();
            while (!AtEnd)
            {
                var c = Current;
                if (c == '\\' && _pos + 1 < source.Length && source[_pos + 1] is ',' or '|' or '\\')
                {
                    option.Append(source[_pos + 1]);
                    _pos += 2;
                    continue;
                }

                if (c is ',' or '|')
                {
                    if (option.Length == 0)
                    {
                        return false;
                    }

                    choices.Add(option.ToString());
                    option.Clear();
                    _pos++;
                    if (c == '|')
                    {
                        return Accept('}');
                    }

                    continue;
                }

                option.Append(c);
                _pos++;
            }

            return false;
        }

        /// <summary>Parses <c>regex/format/options}</c> after the first <c>/</c>.</summary>
        private bool TryParseTransform(out SnippetTransform transform)
        {
            transform = null!;
            var regex = new StringBuilder();
            while (true)
            {
                if (AtEnd)
                {
                    return false;
                }

                if (Accept('/'))
                {
                    break;
                }

                if (Accept('\\'))
                {
                    regex.Append(Accept('/') ? '/' : '\\');
                    continue;
                }

                regex.Append(Current);
                _pos++;
            }

            var format = new List<SnippetFormatPart>();
            var literal = new StringBuilder();
            while (true)
            {
                if (AtEnd)
                {
                    return false;
                }

                if (Accept('/'))
                {
                    break;
                }

                if (Accept('\\'))
                {
                    literal.Append(Accept('\\') ? '\\' : Accept('/') ? '/' : '\\');
                    continue;
                }

                if (Current == '$')
                {
                    var start = _pos;
                    if (TryParseFormatGroup(out var group))
                    {
                        FlushFormat(literal, format);
                        format.Add(group);
                        continue;
                    }

                    _pos = start + 1;
                    literal.Append('$');
                    continue;
                }

                literal.Append(Current);
                _pos++;
            }

            FlushFormat(literal, format);
            var options = new StringBuilder();
            while (!Accept('}'))
            {
                if (AtEnd)
                {
                    return false;
                }

                options.Append(Current);
                _pos++;
            }

            var flags = options.ToString();
            if (JsRegex.TryCreate(regex.ToString(), flags) is not { } pattern)
            {
                return false;
            }

            transform = new SnippetTransform(pattern, format, flags.Contains('g', StringComparison.Ordinal));
            return true;
        }

        private static void FlushFormat(StringBuilder literal, List<SnippetFormatPart> format)
        {
            if (literal.Length > 0)
            {
                format.Add(new SnippetFormatText(literal.ToString()));
                literal.Clear();
            }
        }

        /// <summary>Parses <c>$1</c>, <c>${1}</c>, <c>${1:/upcase}</c>, <c>${1:+if}</c>, <c>${1:?if:else}</c>, <c>${1:-else}</c> or <c>${1:else}</c>.</summary>
        private bool TryParseFormatGroup(out SnippetFormatGroup group)
        {
            group = null!;
            _pos++;
            if (TryReadInt(out var index))
            {
                group = new SnippetFormatGroup(index);
                return true;
            }

            if (!Accept('{') || !TryReadInt(out index))
            {
                return false;
            }

            if (Accept('}'))
            {
                group = new SnippetFormatGroup(index);
                return true;
            }

            if (!Accept(':'))
            {
                return false;
            }

            if (Accept('/'))
            {
                if (TryReadName(out var modifier) && Modifiers.Contains(modifier, StringComparer.Ordinal) && Accept('}'))
                {
                    group = new SnippetFormatGroup(index, modifier);
                    return true;
                }

                return false;
            }

            if (Accept('+'))
            {
                if (TryReadUntil('}', out var ifText))
                {
                    group = new SnippetFormatGroup(index, IfText: ifText);
                    return true;
                }

                return false;
            }

            if (Accept('?'))
            {
                if (TryReadUntil(':', out var ifText) && TryReadUntil('}', out var elseText))
                {
                    group = new SnippetFormatGroup(index, IfText: ifText, ElseText: elseText);
                    return true;
                }

                return false;
            }

            Accept('-');
            if (TryReadUntil('}', out var otherwise))
            {
                group = new SnippetFormatGroup(index, ElseText: otherwise);
                return true;
            }

            return false;
        }

        /// <summary>Reads text up to an unescaped character, which is consumed.</summary>
        private bool TryReadUntil(char end, out string text)
        {
            var value = new StringBuilder();
            while (!AtEnd)
            {
                if (Current == '\\' && _pos + 1 < source.Length)
                {
                    value.Append(source[_pos + 1]);
                    _pos += 2;
                    continue;
                }

                if (Accept(end))
                {
                    text = value.ToString();
                    return true;
                }

                value.Append(Current);
                _pos++;
            }

            text = string.Empty;
            return false;
        }
    }
}
