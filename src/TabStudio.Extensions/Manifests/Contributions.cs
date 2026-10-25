namespace TabStudio.Extensions.Manifests;

/// <summary>A language an extension adds or adds to, from <c>contributes.languages</c>.</summary>
/// <param name="Id">The language identifier, such as <c>perl</c>. Several extensions can contribute to one language.</param>
/// <param name="Aliases">Names for the language; the first is shown to people.</param>
/// <param name="Extensions">File extensions with their dot, such as <c>.pl</c>.</param>
/// <param name="Filenames">Whole file names, such as <c>Makefile</c>.</param>
/// <param name="FilenamePatterns">Glob patterns of file names, such as <c>*.config.json</c>.</param>
/// <param name="FirstLine">A regular expression that recognizes the language by a file's first line, or <see langword="null"/>.</param>
/// <param name="Configuration">The language configuration file, relative to the extension, or <see langword="null"/>.</param>
/// <param name="IconLight">An icon for the language on light backgrounds, relative to the extension, or <see langword="null"/>.</param>
/// <param name="IconDark">An icon for the language on dark backgrounds, relative to the extension, or <see langword="null"/>.</param>
public sealed record LanguageContribution(
    string Id,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<string> Filenames,
    IReadOnlyList<string> FilenamePatterns,
    string? FirstLine,
    string? Configuration,
    string? IconLight,
    string? IconDark);

/// <summary>A TextMate grammar, from <c>contributes.grammars</c>.</summary>
/// <param name="Language">The language it colors, or <see langword="null"/> for an injection grammar.</param>
/// <param name="ScopeName">The grammar's root scope, such as <c>source.perl</c>.</param>
/// <param name="Path">The grammar file (<c>.tmLanguage.json</c> or <c>.tmLanguage</c>), relative to the extension.</param>
/// <param name="EmbeddedLanguages">Scopes of embedded code mapped to their language, such as <c>meta.embedded.block.css</c> to <c>css</c>.</param>
/// <param name="InjectTo">The scopes this grammar is injected into.</param>
public sealed record GrammarContribution(
    string? Language,
    string ScopeName,
    string Path,
    IReadOnlyDictionary<string, string> EmbeddedLanguages,
    IReadOnlyList<string> InjectTo);

/// <summary>A snippet file, from <c>contributes.snippets</c>.</summary>
/// <param name="Language">The language its snippets are offered in, or <see langword="null"/> when each snippet says.</param>
/// <param name="Path">The snippet file, relative to the extension.</param>
public sealed record SnippetContribution(string? Language, string Path);

/// <summary>How a color theme looks overall, from its <c>uiTheme</c>.</summary>
public enum ColorThemeKind
{
    /// <summary>Light text on a dark background (<c>vs-dark</c>).</summary>
    Dark,

    /// <summary>Dark text on a light background (<c>vs</c>).</summary>
    Light,

    /// <summary>High contrast on black (<c>hc-black</c>).</summary>
    HighContrastDark,

    /// <summary>High contrast on white (<c>hc-light</c>).</summary>
    HighContrastLight,
}

/// <summary>A color theme, from <c>contributes.themes</c>.</summary>
/// <param name="Id">The theme's identifier, or its label when it has none.</param>
/// <param name="Label">The name shown in the theme picker.</param>
/// <param name="Kind">Whether it is a dark, light or high contrast theme.</param>
/// <param name="Path">The theme file, relative to the extension.</param>
public sealed record ThemeContribution(string Id, string Label, ColorThemeKind Kind, string Path);

/// <summary>A file icon theme, from <c>contributes.iconThemes</c>.</summary>
/// <param name="Id">The theme's identifier.</param>
/// <param name="Label">The name shown in the icon theme picker.</param>
/// <param name="Path">The icon theme file, relative to the extension.</param>
public sealed record IconThemeContribution(string Id, string Label, string Path);

/// <summary>Everything an extension's manifest contributes, from its <c>contributes</c> object.</summary>
/// <param name="Languages">Languages.</param>
/// <param name="Grammars">TextMate grammars.</param>
/// <param name="Snippets">Snippet files.</param>
/// <param name="Themes">Color themes.</param>
/// <param name="IconThemes">File icon themes.</param>
/// <param name="Points">
/// The name of every contribution point the manifest uses, in its order, including the ones TabStudio does not
/// run, such as <c>commands</c> or <c>debuggers</c>, with how many entries each has.
/// </param>
public sealed record ExtensionContributions(
    IReadOnlyList<LanguageContribution> Languages,
    IReadOnlyList<GrammarContribution> Grammars,
    IReadOnlyList<SnippetContribution> Snippets,
    IReadOnlyList<ThemeContribution> Themes,
    IReadOnlyList<IconThemeContribution> IconThemes,
    IReadOnlyList<ContributionPoint> Points)
{
    /// <summary>A manifest that contributes nothing.</summary>
    public static ExtensionContributions None { get; } = new([], [], [], [], [], []);
}

/// <summary>A contribution point a manifest uses, such as <c>grammars</c>, and how many entries it has.</summary>
/// <param name="Name">The point's name in <c>contributes</c>.</param>
/// <param name="Count">How many entries it has: the length of an array, or the number of properties of an object.</param>
public sealed record ContributionPoint(string Name, int Count);
