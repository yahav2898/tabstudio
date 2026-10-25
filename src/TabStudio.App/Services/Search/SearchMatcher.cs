namespace TabStudio.App.Services.Search;

/// <summary>How well a text answers a query, and which of its characters matched, for highlighting them.</summary>
/// <param name="Score">Higher is better: an exact name beats a prefix, a prefix beats word starts, word starts beat a
/// substring, and a substring beats letters scattered through the text.</param>
/// <param name="Indices">The matched characters' positions, in order.</param>
public sealed record SearchMatch(int Score, IReadOnlyList<int> Indices);

/// <summary>
/// Matches what the user typed against names as an IDE's search does: case does not matter, each word of the query
/// must match, and "sa" finds "Save All", "gtd" "Go To Definition" and "TeEx" "TestExplorer.cs".
/// </summary>
public static class SearchMatcher
{
    /// <summary>The score of an exact match.</summary>
    public const int Exact = 1000;

    /// <summary>The score of a match at the start of the text.</summary>
    public const int Prefix = 900;

    /// <summary>The score of a match at the start of a word.</summary>
    public const int WordPrefix = 800;

    /// <summary>The score of a match made of word starts, such as an acronym.</summary>
    public const int WordStarts = 700;

    /// <summary>The score of a match inside a word.</summary>
    public const int Substring = 600;

    /// <summary>The score of letters scattered through the text, in order.</summary>
    public const int Scattered = 300;

    /// <summary>How well a text answers a query, or <see langword="null"/> when a word of the query is not in it.</summary>
    /// <param name="query">What the user typed.</param>
    /// <param name="text">A name, such as a file name or a command's title.</param>
    public static SearchMatch? Match(string query, string text)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(text);
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return new SearchMatch(0, []);
        }

        if (words.Length == 1)
        {
            return MatchWord(words[0], text);
        }

        // Every word must match; the whole query in one piece scores as such.
        if (MatchWord(query.Trim(), text) is { Score: >= Substring } whole)
        {
            return whole;
        }

        var score = 0;
        var indices = new SortedSet<int>();
        foreach (var word in words)
        {
            if (MatchWord(word, text) is not { } match)
            {
                return null;
            }

            score += match.Score;
            indices.UnionWith(match.Indices);
        }

        return new SearchMatch(score / words.Length, [.. indices]);
    }

    private static SearchMatch? MatchWord(string word, string text)
    {
        if (word.Length > text.Length)
        {
            return null;
        }

        if (text.Equals(word, StringComparison.OrdinalIgnoreCase))
        {
            return new SearchMatch(Exact, Range(0, word.Length));
        }

        if (text.StartsWith(word, StringComparison.OrdinalIgnoreCase))
        {
            return new SearchMatch(Prefix - Math.Min(99, text.Length - word.Length), Range(0, word.Length));
        }

        for (var at = 1; at <= text.Length - word.Length; at++)
        {
            if (IsWordStart(text, at) && string.Compare(text, at, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new SearchMatch(WordPrefix - Math.Min(99, at), Range(at, word.Length));
            }
        }

        var scattered = MatchScattered(word, text, preferAnchors: true) ?? MatchScattered(word, text, preferAnchors: false);
        if (scattered is { AllAnchored: true })
        {
            return new SearchMatch(WordStarts - Math.Min(99, scattered.Indices[0] + scattered.Gaps), scattered.Indices);
        }

        var index = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return new SearchMatch(Substring - Math.Min(99, index), Range(index, word.Length));
        }

        return scattered is null ? null
            : new SearchMatch(Math.Max(1, Scattered - Math.Min(250, (scattered.Gaps * 3) + scattered.Indices[0]) + (scattered.Anchored * 10)), scattered.Indices);
    }

    /// <summary>
    /// The word's letters in order through the text: with <paramref name="preferAnchors"/>, each at the start of a word
    /// or right after the previous one when it can be, anywhere after it when it cannot; otherwise each at its first
    /// place after the previous one.
    /// </summary>
    private static Scatter? MatchScattered(string word, string text, bool preferAnchors)
    {
        var indices = new int[word.Length];
        var anchored = 0;
        var gaps = 0;
        var position = 0;
        for (var i = 0; i < word.Length; i++)
        {
            var found = -1;
            var isAnchored = false;
            for (var at = position; at < text.Length; at++)
            {
                if (!SameLetter(text[at], word[i]))
                {
                    continue;
                }

                if ((i > 0 && at == indices[i - 1] + 1) || IsWordStart(text, at))
                {
                    found = at;
                    isAnchored = true;
                    break;
                }

                if (found < 0)
                {
                    found = at;
                    if (!preferAnchors)
                    {
                        break;
                    }
                }
            }

            if (found < 0)
            {
                return null;
            }

            if (i > 0)
            {
                gaps += found - indices[i - 1] - 1;
            }

            anchored += isAnchored ? 1 : 0;
            indices[i] = found;
            position = found + 1;
        }

        return new Scatter(indices, anchored, gaps, anchored == word.Length);
    }

    /// <summary>Whether a word starts at a position: after a separator, at a capital after a small letter, or at a digit after a letter.</summary>
    private static bool IsWordStart(string text, int at)
    {
        if (at == 0)
        {
            return true;
        }

        var current = text[at];
        var previous = text[at - 1];
        return (!char.IsLetterOrDigit(previous) && char.IsLetterOrDigit(current))
            || (char.IsUpper(current) && char.IsLower(previous))
            || (char.IsDigit(current) && char.IsLetter(previous));
    }

    private static bool SameLetter(char a, char b) => char.ToUpperInvariant(a) == char.ToUpperInvariant(b);

    private static int[] Range(int start, int count) => [.. Enumerable.Range(start, count)];

    private sealed record Scatter(int[] Indices, int Anchored, int Gaps, bool AllAnchored);
}
