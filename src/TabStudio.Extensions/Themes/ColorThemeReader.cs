using System.Text.Json;
using System.Xml;
using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Json;
using TabStudio.Extensions.Manifests;

namespace TabStudio.Extensions.Themes;

/// <summary>
/// Reads a VS Code color theme: a JSON theme (with comments), following its <c>include</c> chain and a
/// <c>tokenColors</c> that names another file, or a TextMate <c>.tmTheme</c> property list.
/// </summary>
public static class ColorThemeReader
{
    private const int MaximumIncludeDepth = 10;

    /// <summary>Reads a theme file.</summary>
    /// <param name="path">The theme file.</param>
    /// <param name="kind">Whether the theme is dark, light or high contrast, from its contribution's <c>uiTheme</c>.</param>
    /// <param name="name">The name to give it, such as its label in the manifest, or <see langword="null"/> for the file's.</param>
    /// <param name="root">The extension folder; included files outside it are ignored. <see langword="null"/> allows any.</param>
    /// <exception cref="FormatException">The theme is not valid JSON or a valid property list.</exception>
    /// <exception cref="IOException">The theme cannot be read.</exception>
    public static ColorTheme Read(string path, ColorThemeKind kind, string? name = null, string? root = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var parts = new Parts();
        ReadInto(Path.GetFullPath(path), root is null ? null : Path.GetFullPath(root), parts, 0, new HashSet<string>(StringComparer.Ordinal));
        return new ColorTheme(
            name ?? parts.Name ?? Path.GetFileNameWithoutExtension(path),
            kind,
            parts.Colors,
            parts.Rules,
            parts.SemanticHighlighting ?? false,
            parts.Semantic);
    }

    private static void ReadInto(string path, string? root, Parts parts, int depth, HashSet<string> visited)
    {
        if (depth > MaximumIncludeDepth || !visited.Add(path))
        {
            return;
        }

        var text = Jsonc.ReadText(path);
        try
        {
            if (text.TrimStart().StartsWith('<'))
            {
                ReadTmTheme(text, parts);
                return;
            }

            using var document = Jsonc.Parse(text);
            var theme = document.RootElement;
            if (theme.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"'{Path.GetFileName(path)}' is not a color theme.");
            }

            if (theme.GetString("include") is { Length: > 0 } include && Resolve(path, include, root) is { } included)
            {
                ReadInto(included, root, parts, depth + 1, visited);
            }

            parts.Name = theme.GetString("name") ?? parts.Name;
            if (theme.GetObject("colors") is { } colors)
            {
                foreach (var color in colors.EnumerateObject().Where(c => c.Value.ValueKind == JsonValueKind.String))
                {
                    parts.Colors[color.Name] = color.Value.GetString()!;
                }
            }

            switch (theme.GetElement("tokenColors"))
            {
                case { ValueKind: JsonValueKind.Array } rules:
                    parts.Rules.AddRange(rules.EnumerateArray().Select(ReadRule).OfType<TokenColorRule>());
                    break;
                case { ValueKind: JsonValueKind.String } file when Resolve(path, file.GetString()!, root) is { } rulesFile:
                    ReadInto(rulesFile, root, parts, depth + 1, visited);
                    break;
            }

            parts.SemanticHighlighting = theme.GetBoolean("semanticHighlighting") ?? parts.SemanticHighlighting;
            if (theme.GetObject("semanticTokenColors") is { } semantic)
            {
                foreach (var style in semantic.EnumerateObject())
                {
                    if (ReadSemanticStyle(style.Value) is { } value)
                    {
                        parts.Semantic[style.Name] = value;
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            throw new FormatException($"'{Path.GetFileName(path)}' is not valid JSON.", ex);
        }
        catch (XmlException ex)
        {
            throw new FormatException($"'{Path.GetFileName(path)}' is not a valid TextMate theme.", ex);
        }
    }

    /// <summary>A file named by a theme, relative to it, or <see langword="null"/> when it is outside the extension or missing.</summary>
    private static string? Resolve(string from, string relative, string? root)
    {
        var folder = Path.GetDirectoryName(from)!;
        var full = root is null
            ? Path.GetFullPath(Path.Combine(folder, relative))
            : InstalledExtension.ResolveInFolder(root, Path.GetRelativePath(root, Path.GetFullPath(Path.Combine(folder, relative))));
        return full is not null && File.Exists(full) ? full : null;
    }

    private static TokenColorRule? ReadRule(JsonElement rule)
    {
        if (rule.GetObject("settings") is not { } settings)
        {
            return null;
        }

        var scopes = rule.GetElement("scope") switch
        {
            { ValueKind: JsonValueKind.String } scope => SplitScopes(scope.GetString()!),
            { ValueKind: JsonValueKind.Array } list => [.. Jsonc.Strings(list).SelectMany(SplitScopes)],
            _ => [],
        };
        return new TokenColorRule(scopes, settings.GetString("foreground"), settings.GetString("background"), settings.GetString("fontStyle"));
    }

    private static List<string> SplitScopes(string scopes) =>
        [.. scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static SemanticTokenStyle? ReadSemanticStyle(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return new SemanticTokenStyle(value.GetString());
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fontStyle = value.GetString("fontStyle");
        bool? Style(string name) => value.GetBoolean(name) ?? (fontStyle is null ? null : fontStyle.Contains(name, StringComparison.OrdinalIgnoreCase));
        return new SemanticTokenStyle(value.GetString("foreground"), Style("bold"), Style("italic"), Style("underline"), Style("strikethrough"));
    }

    /// <summary>Reads a TextMate theme: its first, scope-less settings are the editor's colors, the others are token rules.</summary>
    private static void ReadTmTheme(string xml, Parts parts)
    {
        if (PlistReader.Read(xml) is not Dictionary<string, object?> theme)
        {
            throw new FormatException("The TextMate theme has no settings.");
        }

        parts.Name = theme.GetValueOrDefault("name") as string ?? parts.Name;
        if (theme.GetValueOrDefault("settings") is not List<object?> settings)
        {
            return;
        }

        foreach (var entry in settings.OfType<Dictionary<string, object?>>())
        {
            if (entry.GetValueOrDefault("settings") is not Dictionary<string, object?> style)
            {
                continue;
            }

            string? Value(string key) => style.GetValueOrDefault(key) as string;
            if (entry.GetValueOrDefault("scope") is string scope && scope.Length > 0)
            {
                parts.Rules.Add(new TokenColorRule(SplitScopes(scope), Value("foreground"), Value("background"), Value("fontStyle")));
                continue;
            }

            parts.Rules.Add(new TokenColorRule([], Value("foreground"), Value("background"), null));
            Map("background", "editor.background");
            Map("foreground", "editor.foreground");
            Map("caret", "editorCursor.foreground");
            Map("lineHighlight", "editor.lineHighlightBackground");
            Map("selection", "editor.selectionBackground");

            void Map(string key, string color)
            {
                if (Value(key) is { Length: > 0 } value)
                {
                    parts.Colors.TryAdd(color, value);
                }
            }
        }
    }

    /// <summary>The theme as it is read, file by file along the include chain.</summary>
    private sealed class Parts
    {
        public string? Name { get; set; }

        public Dictionary<string, string> Colors { get; } = new(StringComparer.Ordinal);

        public List<TokenColorRule> Rules { get; } = [];

        public bool? SemanticHighlighting { get; set; }

        public Dictionary<string, SemanticTokenStyle> Semantic { get; } = new(StringComparer.Ordinal);
    }
}
