namespace TabStudio.Extensions.Languages;

/// <summary>
/// Decides, as a character is typed, whether a pair is closed, a closing character typed over, or the selection
/// surrounded, following VS Code's language-defined auto-closing: a pair is closed only before whitespace or one of
/// the language's <c>autoCloseBefore</c> characters, never where its <c>notIn</c> says, and a quote is not closed right
/// after a word.
/// </summary>
public static class AutoClosingRules
{
    /// <summary>The pair to close after typing a character, or <see langword="null"/>.</summary>
    /// <param name="configuration">The language's rules.</param>
    /// <param name="textBefore">The line's text before the caret, not including the typed character.</param>
    /// <param name="typed">The typed character.</param>
    /// <param name="textAfter">The line's text after the caret.</param>
    /// <param name="context">The kind of token at the caret before typing.</param>
    public static AutoClosingPair? FindPairToClose(LanguageConfiguration configuration, string textBefore, char typed, string textAfter, TokenContexts context)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(textBefore);
        ArgumentNullException.ThrowIfNull(textAfter);

        if (textAfter.Length > 0 && !char.IsWhiteSpace(textAfter[0]) && !configuration.AutoCloseBefore.Contains(textAfter[0], StringComparison.Ordinal))
        {
            return null;
        }

        var typedText = textBefore + typed;
        AutoClosingPair? best = null;
        foreach (var pair in configuration.AutoClosingPairs)
        {
            if (pair.Open.Length == 0 || pair.Open[^1] != typed || !typedText.EndsWith(pair.Open, StringComparison.Ordinal))
            {
                continue;
            }

            if ((pair.NotIn & context) != TokenContexts.None)
            {
                continue;
            }

            // A quote typed right after a word is an apostrophe or closes a string, not the start of a new one.
            var start = typedText.Length - pair.Open.Length;
            if (pair.Open.Length == 1 && pair.Open == pair.Close && start > 0 && IsWordCharacter(typedText[start - 1]))
            {
                continue;
            }

            if (best is null || pair.Open.Length > best.Open.Length)
            {
                best = pair;
            }
        }

        return best;
    }

    /// <summary>Whether typing a character should step over the same character after the caret instead of inserting it.</summary>
    /// <param name="configuration">The language's rules.</param>
    /// <param name="typed">The typed character.</param>
    /// <param name="textAfter">The line's text after the caret.</param>
    /// <param name="closedAutomatically">Whether the character after the caret was inserted by auto-closing.</param>
    public static bool ShouldTypeOver(LanguageConfiguration configuration, char typed, string textAfter, bool closedAutomatically)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(textAfter);
        return closedAutomatically
            && textAfter.Length > 0
            && textAfter[0] == typed
            && configuration.AutoClosingPairs.Any(p => p.Close.Length > 0 && p.Close[0] == typed);
    }

    /// <summary>The pair that surrounds the selection when its opening character is typed, or <see langword="null"/>.</summary>
    /// <param name="configuration">The language's rules.</param>
    /// <param name="typed">The typed character.</param>
    public static BracketPair? FindSurroundingPair(LanguageConfiguration configuration, char typed)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.SurroundingPairs.FirstOrDefault(p => p.Open.Length == 1 && p.Open[0] == typed && p.Close.Length > 0);
    }

    /// <summary>
    /// Whether Backspace between an opening and closing character should delete both, as it does right after a pair
    /// was closed automatically.
    /// </summary>
    /// <param name="configuration">The language's rules.</param>
    /// <param name="before">The character before the caret.</param>
    /// <param name="after">The character after the caret.</param>
    public static bool ShouldDeletePair(LanguageConfiguration configuration, char before, char after)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.AutoClosingPairs.Any(p => p.Open.Length == 1 && p.Close.Length == 1 && p.Open[0] == before && p.Close[0] == after);
    }

    private static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';
}
