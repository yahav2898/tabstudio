using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Json;
using TabStudio.Extensions.Languages;
using TabStudio.Extensions.Manifests;
using TabStudio.Extensions.Snippets;

namespace TabStudio.Extensions.Contributions;

/// <summary>
/// Everything the enabled extensions contribute, together: languages (merged by identifier, as VS Code merges them),
/// grammars, language configurations, snippets, color themes and file icon themes. Immutable; the installed
/// extensions changing makes a new set.
/// </summary>
/// <remarks>
/// Language configuration and snippet files are read the first time they are asked for, and cached. Files that cannot
/// be read are reported in <see cref="Problems"/> and contribute nothing. Every method may be called from any thread.
/// </remarks>
public sealed class ContributionSet
{
    private readonly Dictionary<string, ContributedLanguage> _languages;
    private readonly List<(ContributedLanguage Language, FileNameGlob Glob)> _patterns;
    private readonly ConcurrentDictionary<string, Regex?> _firstLinePatterns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContributedGrammar> _grammars;
    private readonly List<ContributedSnippets> _snippets;
    private readonly ConcurrentDictionary<string, LanguageConfiguration?> _configurations = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyList<SnippetDefinition>> _snippetsByLanguage = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyList<SnippetDefinition>> _snippetFiles = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _problems = new();

    private ContributionSet(IEnumerable<InstalledExtension> extensions)
    {
        var languages = new Dictionary<string, LanguageBuilder>(StringComparer.Ordinal);
        _grammars = new Dictionary<string, ContributedGrammar>(StringComparer.Ordinal);
        _snippets = [];
        var themes = new List<ContributedTheme>();
        var iconThemes = new List<ContributedIconTheme>();
        foreach (var extension in extensions)
        {
            var contributes = extension.Manifest.Contributes;
            foreach (var language in contributes.Languages)
            {
                if (!languages.TryGetValue(language.Id, out var builder))
                {
                    builder = new LanguageBuilder(language.Id);
                    languages[language.Id] = builder;
                }

                builder.Add(extension, language);
            }

            foreach (var grammar in contributes.Grammars)
            {
                if (extension.Resolve(grammar.Path) is { } path)
                {
                    _grammars[grammar.ScopeName] = new ContributedGrammar(grammar.ScopeName, grammar.Language, path, grammar.EmbeddedLanguages, grammar.InjectTo, extension.Id);
                }
            }

            foreach (var snippets in contributes.Snippets)
            {
                if (extension.Resolve(snippets.Path) is { } path)
                {
                    _snippets.Add(new ContributedSnippets(snippets.Language, path, extension.Id));
                }
            }

            foreach (var theme in contributes.Themes)
            {
                if (extension.Resolve(theme.Path) is { } path)
                {
                    themes.Add(new ContributedTheme(theme.Id, theme.Label, theme.Kind, path, extension.Folder, extension.Id));
                }
            }

            foreach (var theme in contributes.IconThemes)
            {
                if (extension.Resolve(theme.Path) is { } path)
                {
                    iconThemes.Add(new ContributedIconTheme(theme.Id, theme.Label, path, extension.Folder, extension.Id));
                }
            }
        }

        _languages = languages.ToDictionary(l => l.Key, l => l.Value.Build(), StringComparer.Ordinal);
        _patterns = [.. _languages.Values.SelectMany(l => l.FileNamePatterns.Select(p => (Language: l, Glob: FileNameGlob.TryCreate(p)))).Where(p => p.Glob is not null).Select(p => (p.Language, p.Glob!))];
        Languages = [.. _languages.Values.OrderBy(l => l.DisplayName, StringComparer.OrdinalIgnoreCase)];
        Grammars = [.. _grammars.Values];
        Themes = [.. themes.OrderBy(t => t.Label, StringComparer.OrdinalIgnoreCase)];
        IconThemes = [.. iconThemes.OrderBy(t => t.Label, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>No contributions.</summary>
    public static ContributionSet Empty { get; } = new([]);

    /// <summary>The contributed languages, by display name.</summary>
    public IReadOnlyList<ContributedLanguage> Languages { get; }

    /// <summary>The contributed grammars.</summary>
    public IReadOnlyList<ContributedGrammar> Grammars { get; }

    /// <summary>The contributed color themes, by label.</summary>
    public IReadOnlyList<ContributedTheme> Themes { get; }

    /// <summary>The contributed file icon themes, by label.</summary>
    public IReadOnlyList<ContributedIconTheme> IconThemes { get; }

    /// <summary>Files of the contributions that could not be read, as messages for the user.</summary>
    public IReadOnlyList<string> Problems => [.. _problems];

    /// <summary>Collects the contributions of the enabled extensions among <paramref name="extensions"/>.</summary>
    /// <param name="extensions">Installed extensions; disabled ones are skipped. Later ones win where two contribute the same thing.</param>
    public static ContributionSet From(IEnumerable<InstalledExtension> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        return new ContributionSet(extensions.Where(e => e.IsEnabled));
    }

    /// <summary>A language by identifier, or <see langword="null"/>.</summary>
    /// <param name="id">The identifier, such as <c>perl</c>.</param>
    public ContributedLanguage? FindLanguage(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _languages.GetValueOrDefault(id);
    }

    /// <summary>
    /// The language a file has, as VS Code decides it: by its whole name first, then by a file name pattern, then by
    /// its longest matching extension (<c>.d.ts</c> before <c>.ts</c>).
    /// </summary>
    /// <param name="path">The file's path or name.</param>
    /// <returns>The language, or <see langword="null"/> when no extension claims the file.</returns>
    public ContributedLanguage? FindLanguageForFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_languages.Count == 0)
        {
            return null;
        }

        var name = Path.GetFileName(path);
        ContributedLanguage? byName = null;
        foreach (var language in _languages.Values)
        {
            if (language.FileNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                byName = language;
            }
        }

        if (byName is not null)
        {
            return byName;
        }

        for (var i = _patterns.Count - 1; i >= 0; i--)
        {
            if (_patterns[i].Glob.IsMatch(path))
            {
                return _patterns[i].Language;
            }
        }

        ContributedLanguage? best = null;
        var bestLength = 0;
        foreach (var language in _languages.Values)
        {
            foreach (var extension in language.Extensions)
            {
                var suffix = extension.StartsWith('.') ? extension : "." + extension;
                if (suffix.Length >= bestLength && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    best = language;
                    bestLength = suffix.Length;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// The language whose <c>firstLine</c> pattern a file's first line matches, as VS Code recognizes scripts without an
    /// extension by their <c>#!</c> line, or <see langword="null"/>.
    /// </summary>
    /// <param name="firstLine">The file's first line.</param>
    public ContributedLanguage? FindLanguageForFirstLine(string firstLine)
    {
        ArgumentNullException.ThrowIfNull(firstLine);
        foreach (var language in _languages.Values)
        {
            if (language.FirstLine is { } pattern && _firstLinePatterns.GetOrAdd(pattern, p => JsRegex.TryCreate(p)) is { } regex && IsMatch(regex, firstLine))
            {
                return language;
            }
        }

        return null;
    }

    /// <summary>A grammar by scope name, or <see langword="null"/>.</summary>
    /// <param name="scopeName">The scope, such as <c>source.perl</c>.</param>
    public ContributedGrammar? FindGrammar(string scopeName)
    {
        ArgumentNullException.ThrowIfNull(scopeName);
        return _grammars.GetValueOrDefault(scopeName);
    }

    /// <summary>The grammar that colors a language, or <see langword="null"/>.</summary>
    /// <param name="languageId">The language identifier.</param>
    public ContributedGrammar? FindGrammarForLanguage(string languageId)
    {
        ArgumentNullException.ThrowIfNull(languageId);
        return Grammars.LastOrDefault(g => string.Equals(g.LanguageId, languageId, StringComparison.Ordinal));
    }

    /// <summary>The scopes of the grammars injected into a scope.</summary>
    /// <param name="scopeName">The scope grammars are injected into, such as <c>source.js</c>.</param>
    public IReadOnlyList<string> FindInjections(string scopeName)
    {
        ArgumentNullException.ThrowIfNull(scopeName);
        return [.. Grammars.Where(g => g.InjectTo.Contains(scopeName, StringComparer.Ordinal)).Select(g => g.ScopeName)];
    }

    /// <summary>A language's configuration, read the first time it is asked for, or <see langword="null"/> when it has none.</summary>
    /// <param name="languageId">The language identifier.</param>
    public LanguageConfiguration? FindConfiguration(string languageId)
    {
        ArgumentNullException.ThrowIfNull(languageId);
        return _configurations.GetOrAdd(languageId, id =>
        {
            if (FindLanguage(id)?.Configuration is not { } path)
            {
                return null;
            }

            try
            {
                return LanguageConfigurationReader.Read(Jsonc.ReadText(path));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                _problems.Enqueue($"The language configuration of {id} cannot be read: {ex.Message}");
                return null;
            }
        });
    }

    /// <summary>The snippets offered in a language, read the first time they are asked for.</summary>
    /// <param name="languageId">The language identifier.</param>
    public IReadOnlyList<SnippetDefinition> FindSnippets(string languageId)
    {
        ArgumentNullException.ThrowIfNull(languageId);
        return _snippetsByLanguage.GetOrAdd(languageId, id =>
        [
            .. _snippets
                .Where(s => s.LanguageId is null || string.Equals(s.LanguageId, id, StringComparison.Ordinal))
                .SelectMany(s => ReadSnippets(s.Path).Where(d => s.LanguageId is not null || d.Languages.Count == 0 || d.Languages.Contains(id, StringComparer.Ordinal))),
        ]);
    }

    private IReadOnlyList<SnippetDefinition> ReadSnippets(string path) => _snippetFiles.GetOrAdd(path, file =>
    {
        try
        {
            return SnippetDefinition.ReadFile(Jsonc.ReadText(file));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _problems.Enqueue($"The snippets in {Path.GetFileName(file)} cannot be read: {ex.Message}");
            return [];
        }
    });

    private static bool IsMatch(Regex regex, string text)
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

    /// <summary>Merges what several extensions contribute to one language.</summary>
    private sealed class LanguageBuilder(string id)
    {
        private readonly List<string> _aliases = [];
        private readonly List<string> _extensions = [];
        private readonly List<string> _fileNames = [];
        private readonly List<string> _patterns = [];
        private readonly List<ExtensionIdentifier> _owners = [];
        private string? _configuration;
        private string? _icon;
        private string? _firstLine;

        public void Add(InstalledExtension extension, LanguageContribution language)
        {
            _firstLine = language.FirstLine ?? _firstLine;
            _aliases.AddRange(language.Aliases.Except(_aliases, StringComparer.Ordinal));
            _extensions.AddRange(language.Extensions.Except(_extensions, StringComparer.OrdinalIgnoreCase));
            _fileNames.AddRange(language.Filenames.Except(_fileNames, StringComparer.OrdinalIgnoreCase));
            _patterns.AddRange(language.FilenamePatterns.Except(_patterns, StringComparer.Ordinal));
            _configuration = extension.Resolve(language.Configuration) ?? _configuration;
            _icon = extension.Resolve(language.IconDark ?? language.IconLight) ?? _icon;
            if (!_owners.Contains(extension.Id))
            {
                _owners.Add(extension.Id);
            }
        }

        public ContributedLanguage Build() =>
            new(id, _aliases.Count > 0 ? _aliases[0] : id, [.. _extensions], [.. _fileNames], [.. _patterns], _configuration, _icon, [.. _owners], _firstLine);
    }
}
