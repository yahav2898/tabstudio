using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.Extensions.Snippets;

/// <summary>
/// A snippet transform, <c>/regex/format/options</c>: the regular expression is matched against a value, and each
/// match is replaced by the format, as JavaScript's <c>String.replace</c> does in VS Code.
/// </summary>
/// <param name="Pattern">The regular expression.</param>
/// <param name="Format">The replacement.</param>
/// <param name="Global">Whether every match is replaced (the <c>g</c> option) rather than the first.</param>
public sealed partial record SnippetTransform(Regex Pattern, IReadOnlyList<SnippetFormatPart> Format, bool Global)
{
    /// <summary>Transforms a value.</summary>
    /// <param name="value">The value, such as a file name or what was typed in a tab stop.</param>
    public string Apply(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var matched = false;
        string result;
        try
        {
            result = Pattern.Replace(value, match =>
            {
                matched = true;
                return Replace(match);
            }, Global ? -1 : 1);
        }
        catch (RegexMatchTimeoutException)
        {
            return value;
        }

        // Without a match, the format's "else" branches still apply.
        if (!matched && Format.Any(p => p is SnippetFormatGroup { ElseText: not null }))
        {
            result = Replace(null);
        }

        return result;
    }

    private string Replace(Match? match)
    {
        var text = new StringBuilder();
        foreach (var part in Format)
        {
            switch (part)
            {
                case SnippetFormatText literal:
                    text.Append(literal.Value);
                    break;
                case SnippetFormatGroup group:
                    var value = match is not null && group.Group < match.Groups.Count && match.Groups[group.Group].Success
                        ? match.Groups[group.Group].Value
                        : string.Empty;
                    text.Append(Resolve(group, value));
                    break;
            }
        }

        return text.ToString();
    }

    private static string Resolve(SnippetFormatGroup group, string value) => group.Modifier switch
    {
        "upcase" => value.ToUpper(CultureInfo.CurrentCulture),
        "downcase" => value.ToLower(CultureInfo.CurrentCulture),
        "capitalize" => value.Length == 0 ? value : char.ToUpper(value[0], CultureInfo.CurrentCulture) + value[1..],
        "pascalcase" => JoinWords(value, pascal: true),
        "camelcase" => JoinWords(value, pascal: false),
        _ when value.Length > 0 && group.IfText is not null => group.IfText,
        _ when value.Length == 0 && group.ElseText is not null => group.ElseText,
        _ => value,
    };

    /// <summary>Joins the words of a value (runs of letters and digits) in PascalCase or camelCase.</summary>
    private static string JoinWords(string value, bool pascal)
    {
        var words = Words().Matches(value);
        if (words.Count == 0)
        {
            return value;
        }

        var text = new StringBuilder(value.Length);
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i].Value;
            var first = i == 0 && !pascal ? char.ToLowerInvariant(word[0]) : char.ToUpperInvariant(word[0]);
            text.Append(first).Append(word, 1, word.Length - 1);
        }

        return text.ToString();
    }

    [GeneratedRegex("[a-z0-9]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Words();
}
