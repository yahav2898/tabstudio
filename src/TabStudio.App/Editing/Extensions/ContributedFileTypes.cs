using TabStudio.Extensions.Contributions;

namespace TabStudio.App.Editing.Extensions;

/// <summary>
/// The file types of languages installed extensions contribute, as <see cref="FileTypes.Contributed"/> asks for them: a
/// file an extension claims gets the extension's language, grammar, comments and icon. A file TabStudio already knows
/// keeps its name and badge, and takes the extension's grammar.
/// </summary>
internal static class ContributedFileTypes
{
    /// <summary>The type of a file an installed extension claims, or <see langword="null"/>.</summary>
    /// <param name="path">The file's path.</param>
    /// <param name="builtIn">TabStudio's own type for the file, or <see langword="null"/>.</param>
    public static FileType? For(string path, FileType? builtIn)
    {
        var contributions = EditorExtensions.Contributions;
        if (contributions.FindLanguageForFile(path) is not { } language)
        {
            return null;
        }

        var name = Path.GetFileName(path);
        var grammar = contributions.FindGrammarForLanguage(language.Id) is not null ? name : null;
        if (builtIn is not null)
        {
            return builtIn with { HighlightingExtension = grammar ?? builtIn.HighlightingExtension, LanguageId = language.Id, IconPath = language.Icon };
        }

        return Create(contributions, language, name, grammar);
    }

    /// <summary>
    /// The type of a file that no name claims, by its first line, as VS Code recognizes scripts such as
    /// <c>#!/usr/bin/env perl</c>; <see langword="null"/> when no installed language's <c>firstLine</c> matches.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <param name="firstLine">The file's first line.</param>
    public static FileType? ForFirstLine(string path, string firstLine)
    {
        var contributions = EditorExtensions.Contributions;
        if (contributions.FindLanguageForFirstLine(firstLine) is not { } language)
        {
            return null;
        }

        // The file's name does not lead to the grammar, so the type names the grammar's scope itself.
        var grammar = contributions.FindGrammarForLanguage(language.Id) is { } found ? ScopePrefix + found.ScopeName : null;
        return Create(contributions, language, Path.GetFileName(path), grammar);
    }

    /// <summary>The start of a <see cref="FileType.HighlightingExtension"/> that names a grammar's scope rather than a file.</summary>
    internal const string ScopePrefix = "scope:";

    private static FileType Create(ContributionSet contributions, ContributedLanguage language, string fileName, string? grammar)
    {
        var configuration = contributions.FindConfiguration(language.Id);
        var block = configuration?.BlockComment is { } comment ? (comment.Open, comment.Close) : ((string, string)?)null;
        return new FileType(language.DisplayName, Badge(language, fileName), BadgeKind.Script, configuration?.LineComment, block, grammar)
        {
            LanguageId = language.Id,
            IconPath = language.Icon,
        };
    }

    /// <summary>
    /// The badge for a file of a contributed language: its extension in capitals when it is short, such as <c>PL</c>,
    /// otherwise the start of the language's name, as for a file recognized by its whole name.
    /// </summary>
    internal static string Badge(ContributedLanguage language, string fileName)
    {
        var extension = language.Extensions
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .Where(e => fileName.EndsWith(e, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Length)
            .FirstOrDefault();
        var suffix = extension is not null && extension.Length > 1 ? extension[(extension.LastIndexOf('.') + 1)..] : string.Empty;
        var text = suffix.Length is > 0 and <= 4 && suffix.All(char.IsLetterOrDigit)
            ? suffix
            : new string([.. language.DisplayName.Where(char.IsLetterOrDigit).Take(3)]);
        text = text.ToUpperInvariant();
        return text.Length == 0 ? "TXT" : text;
    }
}
