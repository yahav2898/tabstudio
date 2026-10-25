using System.Text.RegularExpressions;

namespace TabStudio.Extensions.Languages;

/// <summary>
/// Decides what Enter does in a language, as VS Code's language-defined auto-indent does: first the language's
/// <c>onEnterRules</c>, then its brackets (between <c>{</c> and <c>}</c> the closing bracket moves to its own line), then
/// its indentation rules.
/// </summary>
public static class EnterRules
{
    /// <summary>Decides the action for Enter.</summary>
    /// <param name="configuration">The language's rules.</param>
    /// <param name="beforeText">The line's text before the caret.</param>
    /// <param name="afterText">The line's text after the caret.</param>
    /// <param name="previousLineText">The text of the line above the caret's line, or an empty string.</param>
    /// <returns>The action, or <see langword="null"/> when no rule applies and the new line keeps the indentation.</returns>
    public static EnterAction? Decide(LanguageConfiguration configuration, string beforeText, string afterText, string previousLineText)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(beforeText);
        ArgumentNullException.ThrowIfNull(afterText);
        ArgumentNullException.ThrowIfNull(previousLineText);

        foreach (var rule in configuration.OnEnterRules)
        {
            if (Matches(rule.BeforeText, beforeText)
                && (rule.AfterText is null || Matches(rule.AfterText, afterText))
                && (rule.PreviousLineText is null || Matches(rule.PreviousLineText, previousLineText)))
            {
                return rule.Action;
            }
        }

        var beforeTrimmed = beforeText.TrimEnd();
        var afterTrimmed = afterText.TrimStart();
        if (beforeTrimmed.Length > 0)
        {
            foreach (var bracket in configuration.Brackets)
            {
                if (EndsWithOpen(beforeTrimmed, bracket.Open))
                {
                    return afterTrimmed.StartsWith(bracket.Close, StringComparison.Ordinal) && bracket.Close.Length > 0
                        ? new EnterAction(IndentAction.IndentOutdent)
                        : new EnterAction(IndentAction.Indent);
                }
            }
        }

        if (configuration.Indentation is not { } rules)
        {
            return null;
        }

        var increases = (rules.IncreaseIndent is { } increase && Matches(increase, beforeText))
            || (rules.IndentNextLine is { } next && Matches(next, beforeText));
        if (increases)
        {
            return rules.DecreaseIndent is { } decrease && afterTrimmed.Length > 0 && Matches(decrease, afterTrimmed)
                ? new EnterAction(IndentAction.IndentOutdent)
                : new EnterAction(IndentAction.Indent);
        }

        // The line after "if (x)" without braces was indented once; the one after it goes back.
        if (rules.IndentNextLine is { } indentNext && previousLineText.Length > 0 && Matches(indentNext, previousLineText)
            && (rules.IncreaseIndent is null || !Matches(rules.IncreaseIndent, previousLineText)))
        {
            return new EnterAction(IndentAction.Outdent);
        }

        return null;
    }

    /// <summary>
    /// Whether a line that was just typed should be outdented, because it now matches the language's decrease pattern,
    /// such as a line that starts with <c>}</c>.
    /// </summary>
    /// <param name="configuration">The language's rules.</param>
    /// <param name="lineText">The whole line.</param>
    public static bool ShouldOutdent(LanguageConfiguration configuration, string lineText)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(lineText);
        if (configuration.Indentation?.DecreaseIndent is { } decrease)
        {
            return Matches(decrease, lineText);
        }

        // Without indentation rules, a line that starts with a closing bracket is outdented.
        var trimmed = lineText.TrimStart();
        return configuration.Brackets.Any(b => b.Close.Length > 0 && trimmed.StartsWith(b.Close, StringComparison.Ordinal) && trimmed.Length == b.Close.Length);
    }

    /// <summary>Whether a line increases the indentation of the lines after it.</summary>
    /// <param name="configuration">The language's rules.</param>
    /// <param name="lineText">The whole line.</param>
    public static bool IncreasesIndent(LanguageConfiguration configuration, string lineText)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(lineText);
        if (configuration.Indentation?.IncreaseIndent is { } increase)
        {
            return Matches(increase, lineText);
        }

        var trimmed = lineText.TrimEnd();
        return configuration.Brackets.Any(b => EndsWithOpen(trimmed, b.Open));
    }

    /// <summary>Whether text ends with an opening bracket; a bracket that is a word, such as <c>begin</c>, must be a whole word.</summary>
    private static bool EndsWithOpen(string text, string open)
    {
        if (open.Length == 0 || !text.EndsWith(open, StringComparison.Ordinal))
        {
            return false;
        }

        var before = text.Length - open.Length - 1;
        return !char.IsLetterOrDigit(open[0]) || before < 0 || !char.IsLetterOrDigit(text[before]);
    }

    private static bool Matches(Regex regex, string text)
    {
        try
        {
            return regex.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
