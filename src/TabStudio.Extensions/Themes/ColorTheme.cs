using TabStudio.Extensions.Manifests;

namespace TabStudio.Extensions.Themes;

/// <summary>A TextMate token color rule: the scopes it applies to and the style it gives them.</summary>
/// <param name="Scopes">Scope selectors such as <c>comment</c> or <c>source.python string</c>; empty for the theme's default style.</param>
/// <param name="Foreground">The text color, such as <c>#6A9955</c>, or <see langword="null"/>.</param>
/// <param name="Background">The background color, or <see langword="null"/>.</param>
/// <param name="FontStyle">Space-separated <c>italic</c>, <c>bold</c>, <c>underline</c> and <c>strikethrough</c>, or <see langword="null"/>.</param>
public sealed record TokenColorRule(IReadOnlyList<string> Scopes, string? Foreground, string? Background, string? FontStyle);

/// <summary>A style for semantic tokens, from a theme's <c>semanticTokenColors</c>.</summary>
/// <param name="Foreground">The text color, or <see langword="null"/>.</param>
/// <param name="Bold">Whether the text is bold, or <see langword="null"/> to leave it.</param>
/// <param name="Italic">Whether the text is italic, or <see langword="null"/> to leave it.</param>
/// <param name="Underline">Whether the text is underlined, or <see langword="null"/> to leave it.</param>
/// <param name="Strikethrough">Whether the text is struck through, or <see langword="null"/> to leave it.</param>
public sealed record SemanticTokenStyle(string? Foreground, bool? Bold = null, bool? Italic = null, bool? Underline = null, bool? Strikethrough = null);

/// <summary>
/// A VS Code color theme, with its <c>include</c> chain already merged: workbench and editor colors, TextMate token
/// colors, and semantic token colors.
/// </summary>
/// <param name="Name">The theme's name.</param>
/// <param name="Kind">Whether it is dark, light or high contrast.</param>
/// <param name="Colors">Colors by key, such as <c>editor.background</c>.</param>
/// <param name="TokenColors">The TextMate rules, the included theme's first, so later rules win.</param>
/// <param name="SemanticHighlighting">Whether semantic token colors apply.</param>
/// <param name="SemanticTokenColors">Styles by semantic token selector, such as <c>variable.readonly</c> or <c>*.deprecated</c>.</param>
public sealed record ColorTheme(
    string Name,
    ColorThemeKind Kind,
    IReadOnlyDictionary<string, string> Colors,
    IReadOnlyList<TokenColorRule> TokenColors,
    bool SemanticHighlighting,
    IReadOnlyDictionary<string, SemanticTokenStyle> SemanticTokenColors)
{
    /// <summary>Whether the theme has dark backgrounds.</summary>
    public bool IsDark => Kind is ColorThemeKind.Dark or ColorThemeKind.HighContrastDark;

    /// <summary>A color by key, or <see langword="null"/> when the theme does not set it.</summary>
    /// <param name="key">The key, such as <c>editor.background</c>.</param>
    public string? Color(string key) => Colors.TryGetValue(key, out var color) && color.Length > 0 ? color : null;

    /// <summary>The default text color: <c>editor.foreground</c>, or the token rules' default style.</summary>
    public string? DefaultForeground =>
        Color("editor.foreground") ?? TokenColors.LastOrDefault(r => r.Scopes.Count == 0 && r.Foreground is not null)?.Foreground;

    /// <summary>The editor's background: <c>editor.background</c>, or the token rules' default style.</summary>
    public string? DefaultBackground =>
        Color("editor.background") ?? TokenColors.LastOrDefault(r => r.Scopes.Count == 0 && r.Background is not null)?.Background;
}
