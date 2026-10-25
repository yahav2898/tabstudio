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

        var configuration = contributions.FindConfiguration(language.Id);
        var block = configuration?.BlockComment is { } comment ? (comment.Open, comment.Close) : ((string, string)?)null;
        return new FileType(language.DisplayName, Badge(language, name), BadgeKind.Script, configuration?.LineComment, block, grammar)
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
