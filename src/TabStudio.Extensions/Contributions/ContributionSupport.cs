using TabStudio.Extensions.Manifests;

namespace TabStudio.Extensions.Contributions;

/// <summary>Whether TabStudio runs a contribution.</summary>
public enum ContributionSupportLevel
{
    /// <summary>TabStudio runs it itself: languages, grammars, language configuration, snippets and themes.</summary>
    Supported,

    /// <summary>It works through the extension's JavaScript, which needs an extension host.</summary>
    NeedsCodeHost,

    /// <summary>TabStudio has nothing that uses it.</summary>
    NotSupported,
}

/// <summary>One thing an extension contributes, and whether it works in TabStudio.</summary>
/// <param name="Point">The contribution point, such as <c>grammars</c> or <c>commands</c>.</param>
/// <param name="Title">What it is, such as "Language: Perl".</param>
/// <param name="Detail">More about it, such as the file extensions of a language.</param>
/// <param name="Support">Whether TabStudio runs it.</param>
public sealed record ContributionStatus(string Point, string Title, string Detail, ContributionSupportLevel Support);

/// <summary>
/// Describes what an extension contributes, honestly: the declarative contributions TabStudio runs, its code and the
/// contributions that need it, and those TabStudio has no use for.
/// </summary>
public static class ContributionSupport
{
    private static readonly HashSet<string> Declarative = new(StringComparer.Ordinal) { "languages", "grammars", "snippets", "themes", "iconThemes" };

    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["commands"] = "Commands",
        ["configuration"] = "Settings",
        ["configurationDefaults"] = "Default settings",
        ["keybindings"] = "Keyboard shortcuts",
        ["menus"] = "Menu items",
        ["submenus"] = "Submenus",
        ["debuggers"] = "Debuggers",
        ["breakpoints"] = "Breakpoints",
        ["views"] = "Views",
        ["viewsContainers"] = "View containers",
        ["viewsWelcome"] = "Welcome content",
        ["taskDefinitions"] = "Task types",
        ["problemMatchers"] = "Problem matchers",
        ["problemPatterns"] = "Problem patterns",
        ["jsonValidation"] = "JSON schemas",
        ["customEditors"] = "Custom editors",
        ["notebooks"] = "Notebook types",
        ["notebookRenderer"] = "Notebook renderers",
        ["walkthroughs"] = "Walkthroughs",
        ["semanticTokenTypes"] = "Semantic token types",
        ["semanticTokenModifiers"] = "Semantic token modifiers",
        ["semanticTokenScopes"] = "Semantic token scopes",
        ["colors"] = "Theme colors",
        ["localizations"] = "Display language",
        ["productIconThemes"] = "Product icon theme",
        ["terminal"] = "Terminal profiles",
        ["authentication"] = "Sign-in providers",
        ["typescriptServerPlugins"] = "TypeScript server plugins",
        ["resourceLabelFormatters"] = "Resource labels",
        ["icons"] = "Icons",
        ["codeActions"] = "Code actions",
        ["documentation"] = "Documentation",
        ["html"] = "HTML data",
        ["css"] = "CSS data",
        ["chatParticipants"] = "Chat participants",
        ["languageModelTools"] = "Language model tools",
        ["mcpServerDefinitionProviders"] = "MCP servers",
    };

    /// <summary>Describes an extension's contributions.</summary>
    /// <param name="manifest">The extension's manifest.</param>
    /// <param name="codeHost">The extension host, or <see langword="null"/> when TabStudio has none.</param>
    public static IReadOnlyList<ContributionStatus> Describe(ExtensionManifest manifest, IExtensionCodeHost? codeHost = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var contributes = manifest.Contributes;
        var statuses = new List<ContributionStatus>();
        var languageNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var language in contributes.Languages.Where(l => l.Aliases.Count > 0))
        {
            languageNames.TryAdd(language.Id, language.Aliases[0]);
        }

        string LanguageName(string? id) => id is null ? "every language" : languageNames.GetValueOrDefault(id) ?? id;

        foreach (var language in contributes.Languages)
        {
            var files = language.Extensions.Concat(language.Filenames).ToList();
            var detail = files.Count == 0 ? language.Id : string.Join(", ", files.Take(6)) + (files.Count > 6 ? ", …" : string.Empty);
            if (language.Configuration is not null)
            {
                detail += " · comments, brackets, indentation and folding";
            }

            statuses.Add(new ContributionStatus("languages", $"Language: {LanguageName(language.Id)}", detail, ContributionSupportLevel.Supported));
        }

        foreach (var grammar in contributes.Grammars)
        {
            var title = grammar.Language is not null
                ? $"Syntax coloring for {LanguageName(grammar.Language)}"
                : grammar.InjectTo.Count > 0 ? $"Syntax coloring added to {string.Join(", ", grammar.InjectTo)}" : "Syntax coloring";
            statuses.Add(new ContributionStatus("grammars", title, grammar.ScopeName, ContributionSupportLevel.Supported));
        }

        foreach (var snippets in contributes.Snippets)
        {
            statuses.Add(new ContributionStatus("snippets", $"Snippets for {LanguageName(snippets.Language)}", Path.GetFileName(snippets.Path), ContributionSupportLevel.Supported));
        }

        foreach (var theme in contributes.Themes)
        {
            var kind = theme.Kind switch
            {
                ColorThemeKind.Light => "Light editor colors",
                ColorThemeKind.HighContrastDark or ColorThemeKind.HighContrastLight => "High contrast editor colors",
                _ => "Dark editor colors",
            };
            statuses.Add(new ContributionStatus("themes", $"Color theme: {theme.Label}", kind, ContributionSupportLevel.Supported));
        }

        foreach (var theme in contributes.IconThemes)
        {
            statuses.Add(new ContributionStatus("iconThemes", $"File icon theme: {theme.Label}", "Icons in the Solution Explorer and on tabs", ContributionSupportLevel.Supported));
        }

        var runsCode = manifest.HasCode && codeHost?.CanRun(manifest) == true;
        if (manifest.HasCode)
        {
            var entry = manifest.Main ?? manifest.Browser!;
            var events = manifest.ActivationEvents.Count == 0 ? string.Empty : " · starts on " + string.Join(", ", manifest.ActivationEvents.Take(3)) + (manifest.ActivationEvents.Count > 3 ? ", …" : string.Empty);
            statuses.Add(new ContributionStatus(
                "main",
                "Extension code",
                entry + events,
                runsCode ? ContributionSupportLevel.Supported : ContributionSupportLevel.NeedsCodeHost));
        }

        foreach (var point in contributes.Points.Where(p => !Declarative.Contains(p.Name)))
        {
            var title = Names.GetValueOrDefault(point.Name) ?? point.Name;
            var detail = point.Count == 1 ? "1 entry" : $"{point.Count} entries";
            var support = manifest.HasCode
                ? runsCode ? ContributionSupportLevel.Supported : ContributionSupportLevel.NeedsCodeHost
                : ContributionSupportLevel.NotSupported;
            statuses.Add(new ContributionStatus(point.Name, title, detail, support));
        }

        return statuses;
    }
}
