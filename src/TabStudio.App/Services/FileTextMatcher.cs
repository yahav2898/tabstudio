using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using TabStudio.Compilation;

namespace TabStudio.App.Services;

/// <summary>
/// Finds a <see cref="FindInFilesQuery"/> in text, and works out its replacements: plain text or a regular expression,
/// with or without letter case, whole words or not. A regular expression that takes longer than
/// <see cref="MatchTimeout"/> on one text stops with <see cref="RegexMatchTimeoutException"/>, rather than searching on
/// for ever. Safe to use from any thread.
/// </summary>
internal sealed class FileTextMatcher
{
    /// <summary>How long a regular expression may take to find one match.</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private readonly Regex? _regex;
    private readonly StringComparison _comparison;

    private FileTextMatcher(FindInFilesQuery query, Regex? regex)
    {
        Query = query;
        _regex = regex;
        _comparison = query.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    }

    /// <summary>What is searched for.</summary>
    public FindInFilesQuery Query { get; }

    /// <summary>Makes the matcher of a query, or says why its regular expression is not valid.</summary>
    /// <param name="query">The query. Its text must not be empty.</param>
    /// <param name="matcher">The matcher, when the query is valid.</param>
    /// <param name="error">Why it is not, in a sentence, such as "Not a valid regular expression: …".</param>
    /// <returns>Whether the query is valid.</returns>
    public static bool TryCreate(FindInFilesQuery query, [NotNullWhen(true)] out FileTextMatcher? matcher, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrEmpty(query.Text);
        matcher = null;
        error = null;
        if (!query.UseRegularExpressions)
        {
            matcher = new FileTextMatcher(query, regex: null);
            return true;
        }

        var options = RegexOptions.Multiline | RegexOptions.CultureInvariant | (query.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        try
        {
            matcher = new FileTextMatcher(query, new Regex(query.Text, options, MatchTimeout));
            return true;
        }
        catch (RegexParseException ex)
        {
            error = $"Not a valid regular expression: {Describe(ex.Error)} at character {ex.Offset}.";
            return false;
        }
        catch (ArgumentException ex)
        {
            error = "Not a valid regular expression: " + ex.Message;
            return false;
        }
    }

    /// <summary>The matches in a text, in order: where each starts and how long it is.</summary>
    /// <param name="text">The text.</param>
    /// <param name="limit">The most matches to return.</param>
    /// <exception cref="RegexMatchTimeoutException">The regular expression took too long.</exception>
    public List<(int Start, int Length)> Find(string text, int limit = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(text);
        var found = new List<(int Start, int Length)>();
        foreach (var (start, length, _) in Enumerate(text))
        {
            if (found.Count >= limit)
            {
                break;
            }

            found.Add((start, length));
        }

        return found;
    }

    /// <summary>
    /// The edits that replace every match in a text, in order. With a regular expression, <c>$1</c>, <c>${name}</c> and
    /// <c>$0</c> in the replacement are the match's groups, and <c>$$</c> is a dollar sign; otherwise the replacement is
    /// taken as it is.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="replacement">What replaces each match.</param>
    /// <exception cref="RegexMatchTimeoutException">The regular expression took too long.</exception>
    public List<TextEdit> Replacements(string text, string replacement)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(replacement);
        var edits = new List<TextEdit>();
        foreach (var (start, length, match) in Enumerate(text))
        {
            edits.Add(new TextEdit(start, length, match is null ? replacement : match.Result(replacement)));
        }

        return edits;
    }

    /// <summary>Applies edits in document order to a text, in one pass however many there are.</summary>
    /// <param name="text">The text the edits were worked out for.</param>
    /// <param name="edits">Non-overlapping edits, in order.</param>
    /// <returns>The edited text.</returns>
    public static string Apply(string text, IReadOnlyList<TextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(edits);
        var builder = new StringBuilder(text.Length);
        var copied = 0;
        foreach (var edit in edits)
        {
            if (edit.Start < copied || edit.Length < 0 || edit.Start + edit.Length > text.Length)
            {
                continue;
            }

            builder.Append(text, copied, edit.Start - copied).Append(edit.NewText);
            copied = edit.Start + edit.Length;
        }

        return builder.Append(text, copied, text.Length - copied).ToString();
    }

    /// <summary>Every match, in order; a regular expression's empty matches are left out, as they replace nothing.</summary>
    private IEnumerable<(int Start, int Length, Match? Match)> Enumerate(string text)
    {
        if (_regex is { } regex)
        {
            for (var match = regex.Match(text); match.Success; match = match.NextMatch())
            {
                if (match.Length > 0 && IsWhole(text, match.Index, match.Length))
                {
                    yield return (match.Index, match.Length, match);
                }
            }

            yield break;
        }

        var needle = Query.Text;
        var index = text.IndexOf(needle, _comparison);
        while (index >= 0)
        {
            int next;
            if (IsWhole(text, index, needle.Length))
            {
                yield return (index, needle.Length, null);
                next = index + needle.Length;
            }
            else
            {
                // "cat" in "concat cat": the first is part of a word, so the search goes on from the next character.
                next = index + 1;
            }

            index = next <= text.Length ? text.IndexOf(needle, next, _comparison) : -1;
        }
    }

    /// <summary>Whether a match counts: always, unless whole words are asked for and a word runs on past either end.</summary>
    private bool IsWhole(string text, int start, int length) =>
        !Query.MatchWholeWord || (!JoinsWords(text, start) && !JoinsWords(text, start + length));

    /// <summary>Whether a word character on each side of an offset makes it the middle of a word.</summary>
    private static bool JoinsWords(string text, int offset) =>
        offset > 0 && offset < text.Length && IsWordCharacter(text[offset - 1]) && IsWordCharacter(text[offset]);

    private static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>A parse error's name in words: <c>InsufficientClosingParentheses</c> is "insufficient closing parentheses".</summary>
    private static string Describe(RegexParseError error)
    {
        var name = error.ToString();
        var words = new StringBuilder(name.Length + 8);
        foreach (var c in name)
        {
            if (char.IsUpper(c) && words.Length > 0)
            {
                words.Append(' ');
            }

            words.Append(char.ToLowerInvariant(c));
        }

        return words.ToString();
    }
}
