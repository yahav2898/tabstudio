using System.Text;

namespace TabStudio.Containers.Build;

/// <summary>
/// Docker's word processing for instruction arguments: variables (<c>$A</c>, <c>${A}</c>, <c>${A:-x}</c>,
/// <c>${A:+x}</c>, <c>${A-x}</c>, <c>${A+x}</c>), quotes and the escape character, and <c>name=value</c> lists.
/// </summary>
public static class BuildWords
{
    /// <summary>Expands variables in <paramref name="text"/>, keeping quotes and escapes for later word splitting.</summary>
    public static string Expand(string text, IReadOnlyDictionary<string, string> variables, char escape = '\\')
    {
        var result = new StringBuilder(text.Length);
        var single = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == escape && !single && i + 1 < text.Length)
            {
                if (text[i + 1] == '$')
                {
                    result.Append(escape).Append('$');
                }
                else
                {
                    result.Append(c).Append(text[i + 1]);
                }

                i++;
                continue;
            }

            if (c == '\'') single = !single;
            if (c != '$' || single || i + 1 >= text.Length)
            {
                result.Append(c);
                continue;
            }

            if (text[i + 1] == '{')
            {
                var close = text.IndexOf('}', i + 2);
                if (close < 0)
                {
                    result.Append(c);
                    continue;
                }

                result.Append(Substitute(text[(i + 2)..close], variables));
                i = close;
                continue;
            }

            var end = i + 1;
            while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_')) end++;
            if (end == i + 1)
            {
                result.Append(c);
                continue;
            }

            result.Append(variables.GetValueOrDefault(text[(i + 1)..end]) ?? "");
            i = end - 1;
        }

        return result.ToString();
    }

    private static string Substitute(string expression, IReadOnlyDictionary<string, string> variables)
    {
        foreach (var op in new[] { ":-", ":+", "-", "+" })
        {
            var at = expression.IndexOf(op, StringComparison.Ordinal);
            if (at <= 0) continue;
            var name = expression[..at];
            var word = expression[(at + op.Length)..];
            var exists = variables.TryGetValue(name, out var value);
            return op switch
            {
                ":-" => string.IsNullOrEmpty(value) ? Expand(word, variables) : value,
                "-" => exists ? value! : Expand(word, variables),
                ":+" => string.IsNullOrEmpty(value) ? "" : Expand(word, variables),
                _ => exists ? Expand(word, variables) : "",
            };
        }

        return variables.GetValueOrDefault(expression) ?? "";
    }

    /// <summary>Splits expanded arguments into words, removing quotes and escapes.</summary>
    public static List<string> Words(string text, char escape = '\\')
    {
        var words = new List<string>();
        var current = new StringBuilder();
        var any = false;
        char quote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == escape && quote != '\'' && i + 1 < text.Length)
            {
                current.Append(text[++i]);
                any = true;
                continue;
            }

            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                else current.Append(c);
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                any = true;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (any || current.Length > 0) words.Add(current.ToString());
                current.Clear();
                any = false;
                continue;
            }

            current.Append(c);
        }

        if (any || current.Length > 0) words.Add(current.ToString());
        return words;
    }

    /// <summary>
    /// <c>ENV</c>, <c>LABEL</c> and <c>ARG</c> pairs: <c>a=1 b="two words"</c>, or the old <c>ENV name value</c>
    /// form. A name without a value (ARG) maps to <see langword="null"/>.
    /// </summary>
    public static List<KeyValuePair<string, string?>> Pairs(string expanded, bool legacySpaceForm, char escape = '\\')
    {
        var words = Words(expanded, escape);
        var result = new List<KeyValuePair<string, string?>>();
        if (words.Count == 0) return result;
        if (legacySpaceForm && !words[0].Contains('=', StringComparison.Ordinal))
        {
            var space = expanded.TrimStart().IndexOfAny([' ', '\t']);
            var value = space < 0 ? "" : string.Join(' ', Words(expanded.TrimStart()[(space + 1)..], escape));
            result.Add(new(words[0], value));
            return result;
        }

        foreach (var word in words)
        {
            var equals = word.IndexOf('=', StringComparison.Ordinal);
            result.Add(equals < 0 ? new(word, null) : new(word[..equals], word[(equals + 1)..]));
        }

        return result;
    }
}
