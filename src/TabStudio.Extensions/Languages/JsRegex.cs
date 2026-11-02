using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.Extensions.Languages;

/// <summary>
/// Turns the JavaScript regular expressions extensions write (language configuration rules, snippet transforms) into
/// .NET ones. The two dialects agree on almost everything; this rewrites the few constructs they read differently, so
/// a rule means in TabStudio what it means in VS Code.
/// </summary>
public static class JsRegex
{
    /// <summary>How long one match may take before it is given up, so a pathological pattern cannot freeze typing.</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    // Escapes JavaScript gives a meaning; any other escaped letter stands for the letter itself.
    private const string JavaScriptEscapes = "bBdDwWsSnrtfvcxukpP";

    /// <summary>Creates a .NET regular expression from a JavaScript pattern and flags.</summary>
    /// <param name="pattern">The pattern, without the surrounding slashes.</param>
    /// <param name="flags">JavaScript flags such as <c>i</c>, <c>m</c>, <c>s</c>, <c>u</c> and <c>g</c>, or <see langword="null"/>.</param>
    /// <returns>The expression, or <see langword="null"/> when the pattern is not valid in either dialect.</returns>
    public static Regex? TryCreate(string? pattern, string? flags = null)
    {
        if (pattern is null)
        {
            return null;
        }

        var options = RegexOptions.CultureInvariant;
        foreach (var flag in flags ?? string.Empty)
        {
            options |= flag switch
            {
                'i' => RegexOptions.IgnoreCase,
                'm' => RegexOptions.Multiline,
                's' => RegexOptions.Singleline,
                _ => RegexOptions.None,
            };
        }

        try
        {
            return new Regex(Translate(pattern, unicode: flags?.Contains('u', StringComparison.Ordinal) == true), options, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Rewrites a JavaScript pattern into the equivalent .NET pattern.</summary>
    /// <param name="pattern">The JavaScript pattern.</param>
    /// <param name="unicode">Whether the <c>u</c> flag is set, which allows <c>\u{1F600}</c>.</param>
    internal static string Translate(string pattern, bool unicode)
    {
        var result = new StringBuilder(pattern.Length + 8);
        var inClass = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                var next = pattern[i + 1];
                if (unicode && next == 'u' && i + 2 < pattern.Length && pattern[i + 2] == '{')
                {
                    var close = pattern.IndexOf('}', i + 3);
                    if (close > 0 && int.TryParse(pattern.AsSpan(i + 3, close - i - 3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint) && codePoint <= 0x10FFFF)
                    {
                        foreach (var unit in char.ConvertFromUtf32(codePoint))
                        {
                            result.Append(CultureInfo.InvariantCulture, $"\\u{(int)unit:X4}");
                        }

                        i = close;
                        continue;
                    }
                }

                // JavaScript's \d and \w are ASCII; .NET's match any script's digits and letters, so an indentation or
                // word rule matched "é:" or "٣" where VS Code does not.
                if (next is 'd' or 'w' || (!inClass && next is 'D' or 'W'))
                {
                    var set = next is 'd' or 'D' ? "0-9" : "a-zA-Z0-9_";
                    result.Append(inClass ? set : next is 'D' or 'W' ? $"[^{set}]" : $"[{set}]");
                    i++;
                    continue;
                }

                if (char.IsAsciiLetter(next) && !JavaScriptEscapes.Contains(next, StringComparison.Ordinal))
                {
                    // In JavaScript, \a is "a"; in .NET it is the bell character.
                    result.Append(next);
                }
                else
                {
                    result.Append(c).Append(next);
                }

                i++;
                continue;
            }

            if (inClass)
            {
                if (c == ']')
                {
                    inClass = false;
                }
                else if (c == '[')
                {
                    // A bracket inside a class is a plain character in JavaScript; .NET could read "-[" as class subtraction.
                    result.Append("\\[");
                    continue;
                }

                result.Append(c);
                continue;
            }

            if (c == '[')
            {
                if (string.CompareOrdinal(pattern, i, "[^]", 0, 3) == 0)
                {
                    // [^] matches any character in JavaScript.
                    result.Append("[\\s\\S]");
                    i += 2;
                    continue;
                }

                if (string.CompareOrdinal(pattern, i, "[]", 0, 2) == 0)
                {
                    // [] matches nothing in JavaScript.
                    result.Append("(?!)");
                    i += 1;
                    continue;
                }

                inClass = true;
                result.Append(c);
                if (i + 1 < pattern.Length && pattern[i + 1] == '^')
                {
                    result.Append('^');
                    i++;
                }

                continue;
            }

            result.Append(c);
        }

        return result.ToString();
    }
}
