using System.Text.Json;
using TabStudio.Extensions.Json;

namespace TabStudio.Extensions.Manifests;

/// <summary>
/// Reads an extension's <c>package.json</c> into an <see cref="ExtensionManifest"/>, localizing <c>%key%</c> strings
/// from <c>package.nls.json</c> as VS Code does, and tolerating the variations real manifests have: a string where
/// an array is expected, missing optional parts, comments and trailing commas.
/// </summary>
public static class ManifestReader
{
    /// <summary>Reads a manifest.</summary>
    /// <param name="packageJson">The text of <c>package.json</c>.</param>
    /// <param name="nlsJson">The text of <c>package.nls.json</c>, or <see langword="null"/> when the extension has none.</param>
    /// <exception cref="ExtensionPackageException">The manifest is not JSON, or lacks a name, publisher or version.</exception>
    public static ExtensionManifest Read(string packageJson, string? nlsJson = null)
    {
        ArgumentNullException.ThrowIfNull(packageJson);
        var strings = ReadLocalizedStrings(nlsJson);
        JsonDocument document;
        try
        {
            document = Jsonc.Parse(packageJson);
        }
        catch (JsonException ex)
        {
            throw new ExtensionPackageException("The extension's package.json is not valid JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ExtensionPackageException("The extension's package.json is not a JSON object.");
            }

            string? Text(JsonElement element, string name) => Localize(element.GetString(name), strings);

            var name = root.GetString("name");
            var publisher = root.GetString("publisher");
            var version = root.GetString("version");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(publisher) || string.IsNullOrWhiteSpace(version))
            {
                throw new ExtensionPackageException("The extension's package.json must have a name, a publisher and a version.");
            }

            if (!ExtensionIdentifier.TryParse($"{publisher}.{name}", out var id))
            {
                throw new ExtensionPackageException($"'{publisher}.{name}' is not a valid extension identifier.");
            }

            var displayName = Text(root, "displayName");
            return new ExtensionManifest
            {
                Id = id,
                Version = version.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName,
                Description = Text(root, "description") ?? string.Empty,
                EngineVersion = root.GetObject("engines")?.GetString("vscode"),
                Categories = root.GetStrings("categories"),
                Keywords = root.GetStrings("keywords"),
                Icon = root.GetString("icon"),
                License = root.GetString("license"),
                Homepage = WebAddress(root.GetString("homepage")),
                Repository = RepositoryAddress(root.GetElement("repository")),
                Main = NonEmpty(root.GetString("main")),
                Browser = NonEmpty(root.GetString("browser")),
                ActivationEvents = root.GetStrings("activationEvents"),
                ExtensionKind = root.GetStrings("extensionKind"),
                ExtensionDependencies = root.GetStrings("extensionDependencies"),
                ExtensionPack = root.GetStrings("extensionPack"),
                IsPreview = root.GetBoolean("preview") ?? false,
                Contributes = root.GetObject("contributes") is { } contributes ? ReadContributions(contributes, strings) : ExtensionContributions.None,
            };
        }
    }

    /// <summary>Replaces a <c>%key%</c> string with its localized text; any other string is returned as it is.</summary>
    /// <param name="value">The string from the manifest.</param>
    /// <param name="strings">The localized strings.</param>
    internal static string? Localize(string? value, IReadOnlyDictionary<string, string> strings) =>
        value is { Length: > 2 } && value[0] == '%' && value[^1] == '%' && strings.TryGetValue(value[1..^1], out var localized)
            ? localized
            : value;

    private static IReadOnlyDictionary<string, string> ReadLocalizedStrings(string? nlsJson)
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(nlsJson))
        {
            return strings;
        }

        try
        {
            using var document = Jsonc.Parse(nlsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return strings;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                // A value is a string, or { "message": "...", "comment": [...] } for translators.
                var text = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.GetString("message");
                if (text is not null)
                {
                    strings[property.Name] = text;
                }
            }
        }
        catch (JsonException)
        {
            // Damaged translations only cost the extension its display strings.
        }

        return strings;
    }

    private static ExtensionContributions ReadContributions(JsonElement contributes, IReadOnlyDictionary<string, string> strings)
    {
        var points = contributes.EnumerateObject()
            .Select(p => new ContributionPoint(p.Name, p.Value.ValueKind switch
            {
                JsonValueKind.Array => p.Value.GetArrayLength(),
                JsonValueKind.Object => p.Value.EnumerateObject().Count(),
                _ => 1,
            }))
            .ToList();

        return new ExtensionContributions(
            [.. Objects(contributes, "languages").Select(l => ReadLanguage(l, strings)).OfType<LanguageContribution>()],
            [.. Objects(contributes, "grammars").Select(ReadGrammar).OfType<GrammarContribution>()],
            [.. Objects(contributes, "snippets").Select(ReadSnippets).OfType<SnippetContribution>()],
            [.. Objects(contributes, "themes").Select(t => ReadTheme(t, strings)).OfType<ThemeContribution>()],
            [.. Objects(contributes, "iconThemes").Select(t => ReadIconTheme(t, strings)).OfType<IconThemeContribution>()],
            points);
    }

    private static IEnumerable<JsonElement> Objects(JsonElement parent, string name) =>
        parent.GetArray(name).Where(item => item.ValueKind == JsonValueKind.Object);

    private static LanguageContribution? ReadLanguage(JsonElement language, IReadOnlyDictionary<string, string> strings)
    {
        if (NonEmpty(language.GetString("id")) is not { } id)
        {
            return null;
        }

        var icon = language.GetObject("icon");
        return new LanguageContribution(
            id,
            [.. language.GetStrings("aliases").Select(a => Localize(a, strings)!).Where(a => a.Length > 0)],
            [.. language.GetStrings("extensions").Where(e => e.Length > 0)],
            [.. language.GetStrings("filenames").Where(f => f.Length > 0)],
            [.. language.GetStrings("filenamePatterns").Where(f => f.Length > 0)],
            NonEmpty(language.GetString("firstLine")),
            NonEmpty(language.GetString("configuration")),
            NonEmpty(icon?.GetString("light")),
            NonEmpty(icon?.GetString("dark")));
    }

    private static GrammarContribution? ReadGrammar(JsonElement grammar)
    {
        if (NonEmpty(grammar.GetString("scopeName")) is not { } scopeName || NonEmpty(grammar.GetString("path")) is not { } path)
        {
            return null;
        }

        return new GrammarContribution(
            NonEmpty(grammar.GetString("language")),
            scopeName,
            path,
            grammar.GetStringMap("embeddedLanguages"),
            grammar.GetStrings("injectTo"));
    }

    private static SnippetContribution? ReadSnippets(JsonElement snippets) =>
        NonEmpty(snippets.GetString("path")) is { } path
            ? new SnippetContribution(NonEmpty(snippets.GetString("language")), path)
            : null;

    private static ThemeContribution? ReadTheme(JsonElement theme, IReadOnlyDictionary<string, string> strings)
    {
        if (NonEmpty(theme.GetString("path")) is not { } path)
        {
            return null;
        }

        var label = NonEmpty(Localize(theme.GetString("label"), strings));
        var id = NonEmpty(theme.GetString("id")) ?? label ?? Path.GetFileNameWithoutExtension(path);
        var kind = theme.GetString("uiTheme") switch
        {
            "vs" => ColorThemeKind.Light,
            "hc-black" => ColorThemeKind.HighContrastDark,
            "hc-light" => ColorThemeKind.HighContrastLight,
            _ => ColorThemeKind.Dark,
        };
        return new ThemeContribution(id, label ?? id, kind, path);
    }

    private static IconThemeContribution? ReadIconTheme(JsonElement theme, IReadOnlyDictionary<string, string> strings)
    {
        if (NonEmpty(theme.GetString("path")) is not { } path)
        {
            return null;
        }

        var label = NonEmpty(Localize(theme.GetString("label"), strings));
        var id = NonEmpty(theme.GetString("id")) ?? label ?? Path.GetFileNameWithoutExtension(path);
        return new IconThemeContribution(id, label ?? id, path);
    }

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static Uri? WebAddress(string? text) =>
        Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;

    /// <summary>
    /// The repository's web address from <c>"repository": "url"</c> or <c>{ "url": "..." }</c>, turning the forms npm
    /// allows (<c>git+https://…/x.git</c>, <c>github:owner/name</c>, <c>owner/name</c>) into a page people can open.
    /// </summary>
    private static Uri? RepositoryAddress(JsonElement? repository)
    {
        var text = repository switch
        {
            { ValueKind: JsonValueKind.String } value => value.GetString(),
            { ValueKind: JsonValueKind.Object } value => value.GetString("url"),
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();
        if (text.StartsWith("github:", StringComparison.OrdinalIgnoreCase))
        {
            text = "https://github.com/" + text["github:".Length..];
        }
        else if (!text.Contains(':', StringComparison.Ordinal) && text.Count(c => c == '/') == 1)
        {
            text = "https://github.com/" + text;
        }

        if (text.StartsWith("git+", StringComparison.OrdinalIgnoreCase))
        {
            text = text[4..];
        }

        if (text.StartsWith("git://", StringComparison.OrdinalIgnoreCase))
        {
            text = "https://" + text[6..];
        }

        if (text.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^4];
        }

        return WebAddress(text);
    }
}
