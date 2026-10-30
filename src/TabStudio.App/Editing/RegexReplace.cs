using System.Text.RegularExpressions;

namespace TabStudio.App.Editing;

/// <summary>One replacement: the text at <see cref="Start"/> of <see cref="Length"/> characters becomes <see cref="Text"/>.</summary>
/// <param name="Start">Where the match starts.</param>
/// <param name="Length">How long the match is.</param>
/// <param name="Text">The replacement, with its <c>$1</c> and <c>${name}</c> groups filled in.</param>
internal readonly record struct RegexReplacement(int Start, int Length, string Text);

/// <summary>
/// Find and Replace with a regular expression, as Visual Studio does it: <c>$1</c>, <c>${name}</c> and <c>$0</c> in
/// the replacement are the match's groups. AvaloniaEdit's own Replace inserted the replacement as typed, so replacing
/// <c>(\w+)\.Value</c> with <c>$1</c> wrote "$1" over every match. A pattern that takes more than a second stops,
/// rather than freezing the editor.
/// </summary>
internal static class RegexReplace
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>The search's regular expression, as the find panel builds it, or <see langword="null"/> when it is not valid.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="matchCase">Whether case matters.</param>
    public static Regex? Create(string? pattern, bool matchCase)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return null;
        }

        try
        {
            return new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant | (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase), MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Every replacement in the text, last first, so applying them in order keeps the earlier offsets.</summary>
    /// <param name="regex">The search.</param>
    /// <param name="text">The document's text.</param>
    /// <param name="replacement">The replacement pattern.</param>
    /// <param name="wholeWords">Whether a match must be a whole word.</param>
    /// <exception cref="RegexMatchTimeoutException">The pattern took too long.</exception>
    public static List<RegexReplacement> All(Regex regex, string text, string? replacement, bool wholeWords)
    {
        ArgumentNullException.ThrowIfNull(regex);
        ArgumentNullException.ThrowIfNull(text);
        var found = new List<RegexReplacement>();
        foreach (Match match in regex.Matches(text))
        {
            if (Counts(match, text, wholeWords))
            {
                found.Add(new RegexReplacement(match.Index, match.Length, match.Result(replacement ?? string.Empty)));
            }
        }

        found.Reverse();
        return found;
    }

    /// <summary>The first replacement at or after an offset, wrapping to the start of the text.</summary>
    /// <param name="regex">The search.</param>
    /// <param name="text">The document's text.</param>
    /// <param name="offset">Where to look from.</param>
    /// <param name="replacement">The replacement pattern.</param>
    /// <param name="wholeWords">Whether a match must be a whole word.</param>
    /// <exception cref="RegexMatchTimeoutException">The pattern took too long.</exception>
    public static RegexReplacement? Next(Regex regex, string text, int offset, string? replacement, bool wholeWords)
    {
        ArgumentNullException.ThrowIfNull(regex);
        ArgumentNullException.ThrowIfNull(text);
        foreach (var start in new[] { Math.Clamp(offset, 0, text.Length), 0 })
        {
            for (var match = regex.Match(text, start); match.Success; match = match.NextMatch())
            {
                if (Counts(match, text, wholeWords))
                {
                    return new RegexReplacement(match.Index, match.Length, match.Result(replacement ?? string.Empty));
                }
            }
        }

        return null;
    }

    /// <summary>An empty match replaces nothing, and a whole-word search takes only matches between word borders.</summary>
    private static bool Counts(Match match, string text, bool wholeWords) =>
        match.Length > 0 && (!wholeWords || (IsBorder(text, match.Index) && IsBorder(text, match.Index + match.Length)));

    private static bool IsBorder(string text, int offset) =>
        offset == 0 || offset == text.Length || IsWordCharacter(text[offset - 1]) != IsWordCharacter(text[offset]);

    private static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';
}
