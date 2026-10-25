using TabStudio.Extensions.Manifests;

namespace TabStudio.Extensions.Contributions;

/// <summary>A language as all enabled extensions contribute it together.</summary>
/// <param name="Id">The language identifier, such as <c>perl</c>.</param>
/// <param name="DisplayName">The name shown to people: the first alias, or the identifier.</param>
/// <param name="Extensions">File extensions with their dot, such as <c>.pl</c>.</param>
/// <param name="FileNames">Whole file names.</param>
/// <param name="FileNamePatterns">Glob patterns of file names.</param>
/// <param name="Configuration">The full path of the language configuration file, or <see langword="null"/>.</param>
/// <param name="Icon">The full path of the language's icon for dark backgrounds, or <see langword="null"/>.</param>
/// <param name="Owners">The extensions that contribute to it.</param>
public sealed record ContributedLanguage(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<string> FileNames,
    IReadOnlyList<string> FileNamePatterns,
    string? Configuration,
    string? Icon,
    IReadOnlyList<ExtensionIdentifier> Owners);

/// <summary>A TextMate grammar of an enabled extension.</summary>
/// <param name="ScopeName">The grammar's root scope, such as <c>source.perl</c>.</param>
/// <param name="LanguageId">The language it colors, or <see langword="null"/> for an injection grammar.</param>
/// <param name="Path">The full path of the grammar file.</param>
/// <param name="EmbeddedLanguages">Scopes of embedded code mapped to their language.</param>
/// <param name="InjectTo">The scopes this grammar is injected into.</param>
/// <param name="Owner">The extension that contributes it.</param>
public sealed record ContributedGrammar(
    string ScopeName,
    string? LanguageId,
    string Path,
    IReadOnlyDictionary<string, string> EmbeddedLanguages,
    IReadOnlyList<string> InjectTo,
    ExtensionIdentifier Owner);

/// <summary>A color theme of an enabled extension.</summary>
/// <param name="Id">The theme's identifier, unique across extensions as <c>publisher.name/id</c> in <see cref="Key"/>.</param>
/// <param name="Label">The name shown in the theme picker.</param>
/// <param name="Kind">Whether it is dark, light or high contrast.</param>
/// <param name="Path">The full path of the theme file.</param>
/// <param name="ExtensionFolder">The extension's folder, which the theme's includes stay within.</param>
/// <param name="Owner">The extension that contributes it.</param>
public sealed record ContributedTheme(string Id, string Label, ColorThemeKind Kind, string Path, string ExtensionFolder, ExtensionIdentifier Owner)
{
    /// <summary>Identifies the theme across extensions, for remembering the user's choice.</summary>
    public string Key => $"{Owner}/{Id}";
}

/// <summary>A file icon theme of an enabled extension.</summary>
/// <param name="Id">The theme's identifier.</param>
/// <param name="Label">The name shown in the icon theme picker.</param>
/// <param name="Path">The full path of the icon theme file.</param>
/// <param name="ExtensionFolder">The extension's folder, which its icons stay within.</param>
/// <param name="Owner">The extension that contributes it.</param>
public sealed record ContributedIconTheme(string Id, string Label, string Path, string ExtensionFolder, ExtensionIdentifier Owner)
{
    /// <summary>Identifies the theme across extensions, for remembering the user's choice.</summary>
    public string Key => $"{Owner}/{Id}";
}

/// <summary>A snippet file of an enabled extension.</summary>
/// <param name="LanguageId">The language its snippets are for, or <see langword="null"/> when each snippet says.</param>
/// <param name="Path">The full path of the snippet file.</param>
/// <param name="Owner">The extension that contributes it.</param>
public sealed record ContributedSnippets(string? LanguageId, string Path, ExtensionIdentifier Owner);
