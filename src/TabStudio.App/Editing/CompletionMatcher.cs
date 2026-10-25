namespace TabStudio.App.Editing;

/// <summary>How a completion item matches what the user typed, best first.</summary>
public enum CompletionMatchKind
{
    /// <summary>The item is exactly what was typed.</summary>
    Exact,

    /// <summary>The item starts with what was typed, such as <c>WriteLine</c> for <c>Wri</c>.</summary>
    Prefix,

    /// <summary>What was typed picks the humps of the name, such as <c>WriteLine</c> for <c>WL</c>.</summary>
    CamelCase,

    /// <summary>What was typed is inside the name, such as <c>WriteLine</c> for <c>Line</c>.</summary>
    Substring,
}

/// <summary>A match of the typed text against a completion item, with the matched characters to show in bold.</summary>
/// <param name="Kind">How the item matches.</param>
/// <param name="IsCaseSensitive">Whether the typed text matches with the same casing.</param>
/// <param name="MatchedIndices">The positions of the matched characters in the item, in ascending order.</param>
public sealed record CompletionMatch(CompletionMatchKind Kind, bool IsCaseSensitive, IReadOnlyList<int> MatchedIndices);

/// <summary>
/// Filters the completion list the way Visual Studio does: prefixes first, then camel-case humps
/// (<c>WL</c> finds <c>WriteLine</c>), then substrings.
/// </summary>
public static class CompletionMatcher
{
    private const int MaxCamelCasePattern = 24;

    /// <summary>Matches typed text against an item's filter text.</summary>
    /// <param name="pattern">What the user typed since the start of the word.</param>
    /// <param name="candidate">The item's filter text.</param>
    /// <returns>The match, or <see langword="null"/> when the item should be hidden.</returns>
    public static CompletionMatch? Match(string pattern, string candidate)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(candidate);

        if (pattern.Length == 0)
        {
            return new CompletionMatch(CompletionMatchKind.Prefix, true, []);
        }

        if (pattern.Length > candidate.Length)
        {
            return null;
        }

        if (candidate.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
        {
            var kind = pattern.Length == candidate.Length ? CompletionMatchKind.Exact : CompletionMatchKind.Prefix;
            var caseSensitive = candidate.StartsWith(pattern, StringComparison.Ordinal);
            return new CompletionMatch(kind, caseSensitive, Enumerable.Range(0, pattern.Length).ToArray());
        }

        if (pattern.Length <= MaxCamelCasePattern && IsSubsequence(pattern, candidate) && MatchHumps(pattern, candidate) is { } matched)
        {
            var caseSensitive = matched.Select((index, i) => candidate[index] == pattern[i]).All(same => same);
            var kind = matched[0] == 0 ? CompletionMatchKind.CamelCase : CompletionMatchKind.Substring;
            return new CompletionMatch(kind, caseSensitive, matched);
        }

        var start = pattern.Length >= 2 ? candidate.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) : -1;
        if (start >= 0)
        {
            var caseSensitive = string.CompareOrdinal(candidate, start, pattern, 0, pattern.Length) == 0;
            return new CompletionMatch(CompletionMatchKind.Substring, caseSensitive, Enumerable.Range(start, pattern.Length).ToArray());
        }

        return null;
    }

    /// <summary>Whether a character can be part of an identifier, so typing it filters the list instead of closing it.</summary>
    /// <param name="character">The character.</param>
    public static bool IsIdentifierCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';

    /// <summary>
    /// Whether typing a character commits the selected item before the character is inserted, as in Visual Studio:
    /// space, punctuation and operators.
    /// </summary>
    /// <param name="character">The typed character.</param>
    public static bool IsCommitCharacter(char character) => character switch
    {
        ' ' or '{' or '}' or '[' or ']' or '(' or ')' or '.' or ',' or ':' or ';' or '+' or '-' or '*' or '/' or '%'
            or '&' or '|' or '^' or '!' or '~' or '=' or '<' or '>' or '?' or '@' or '#' or '\'' or '"' or '\\' => true,
        _ => false,
    };

    /// <summary>
    /// The start of every hump of a name: its first character, an upper-case letter after a lower-case one or a
    /// digit, the last capital of an acronym before a lower-case letter, and a letter after an underscore.
    /// </summary>
    private static List<int> FindHumps(string name)
    {
        var humps = new List<int>();
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (!char.IsLetterOrDigit(current))
            {
                continue;
            }

            if (i == 0 || !char.IsLetterOrDigit(name[i - 1]))
            {
                humps.Add(i);
                continue;
            }

            var previous = name[i - 1];
            var startsWord = char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous));
            var endsAcronym = char.IsUpper(current) && char.IsUpper(previous) && i + 1 < name.Length && char.IsLower(name[i + 1]);
            var startsNumber = char.IsDigit(current) && !char.IsDigit(previous);
            if (startsWord || endsAcronym || startsNumber)
            {
                humps.Add(i);
            }
        }

        return humps;
    }

    /// <summary>Whether every character of the pattern occurs in the candidate, in order, ignoring case.</summary>
    private static bool IsSubsequence(string pattern, string candidate)
    {
        var next = 0;
        foreach (var character in candidate)
        {
            if (next < pattern.Length && char.ToUpperInvariant(character) == char.ToUpperInvariant(pattern[next]))
            {
                next++;
            }
        }

        return next == pattern.Length;
    }

    /// <summary>Matches the pattern against the starts of humps, each hump taking one or more pattern characters.</summary>
    private static int[]? MatchHumps(string pattern, string candidate)
    {
        var humps = FindHumps(candidate);
        var matched = new List<int>(pattern.Length);
        return MatchFrom(0, 0) ? matched.ToArray() : null;

        bool MatchFrom(int patternIndex, int humpIndex)
        {
            if (patternIndex == pattern.Length)
            {
                return true;
            }

            for (var h = humpIndex; h < humps.Count; h++)
            {
                var start = humps[h];
                var end = h + 1 < humps.Count ? humps[h + 1] : candidate.Length;
                var length = 0;
                while (patternIndex + length < pattern.Length
                    && start + length < end
                    && char.ToUpperInvariant(pattern[patternIndex + length]) == char.ToUpperInvariant(candidate[start + length]))
                {
                    length++;
                }

                for (var take = length; take >= 1; take--)
                {
                    for (var k = 0; k < take; k++)
                    {
                        matched.Add(start + k);
                    }

                    if (MatchFrom(patternIndex + take, h + 1))
                    {
                        return true;
                    }

                    matched.RemoveRange(matched.Count - take, take);
                }
            }

            return false;
        }
    }
}
