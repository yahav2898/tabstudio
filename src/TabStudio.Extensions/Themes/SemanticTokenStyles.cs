namespace TabStudio.Extensions.Themes;

/// <summary>
/// Finds a theme's style for a semantic token, as VS Code matches <c>semanticTokenColors</c> selectors
/// (<c>type</c>, <c>type.modifier</c>, <c>*.modifier</c>, <c>type:language</c>): the most specific matching selector wins.
/// </summary>
public static class SemanticTokenStyles
{
    /// <summary>
    /// The style for a token, or <see langword="null"/> when no selector of the theme matches it. As in VS Code, each part
    /// of the style (color, bold, italic, …) comes from the most specific selector that sets it.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="type">The token type, such as <c>class</c> or <c>variable</c>.</param>
    /// <param name="modifiers">The token's modifiers, such as <c>readonly</c> or <c>static</c>.</param>
    /// <param name="language">The document's language identifier, such as <c>csharp</c>, or <see langword="null"/>.</param>
    public static SemanticTokenStyle? Resolve(ColorTheme theme, string type, IReadOnlyCollection<string> modifiers, string? language)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(modifiers);
        if (!theme.SemanticHighlighting)
        {
            return null;
        }

        var matched = false;
        (string? Value, int Score) foreground = (null, -1);
        (bool? Value, int Score) bold = (null, -1), italic = (null, -1), underline = (null, -1), strikethrough = (null, -1);
        foreach (var (selector, style) in theme.SemanticTokenColors)
        {
            var score = Score(selector, type, modifiers, language);
            if (score < 0)
            {
                continue;
            }

            matched = true;
            Take(ref foreground, style.Foreground, score);
            Take(ref bold, style.Bold, score);
            Take(ref italic, style.Italic, score);
            Take(ref underline, style.Underline, score);
            Take(ref strikethrough, style.Strikethrough, score);
        }

        return matched ? new SemanticTokenStyle(foreground.Value, bold.Value, italic.Value, underline.Value, strikethrough.Value) : null;

        static void Take<T>(ref (T Value, int Score) current, T value, int score)
        {
            if (value is not null && score >= current.Score)
            {
                current = (value, score);
            }
        }
    }

    /// <summary>How specifically a selector matches a token, or -1 when it does not match.</summary>
    internal static int Score(string selector, string type, IReadOnlyCollection<string> modifiers, string? language)
    {
        var text = selector.Trim();
        string? selectorLanguage = null;
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            selectorLanguage = text[(colon + 1)..];
            text = text[..colon];
        }

        var parts = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return -1;
        }

        var score = 0;
        if (parts[0] != "*")
        {
            if (!string.Equals(parts[0], type, StringComparison.Ordinal))
            {
                return -1;
            }

            score += 100;
        }

        foreach (var modifier in parts.Skip(1))
        {
            if (!modifiers.Contains(modifier, StringComparer.Ordinal))
            {
                return -1;
            }

            score += 100;
        }

        if (selectorLanguage is not null)
        {
            if (!string.Equals(selectorLanguage, language, StringComparison.Ordinal))
            {
                return -1;
            }

            score += 10;
        }

        return score;
    }
}
