using System.Text;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TabStudio.Extensions.Themes;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Themes;

namespace TabStudio.App.Editing.Extensions;

/// <summary>
/// A VS Code color theme as the editor uses it: its token colors for TextMate highlighting, its semantic and token
/// colors for C#'s Roslyn classifications, and the editor's own colors (background, text, current line, selection,
/// caret and line numbers). TabStudio's own light colors are one too (<see cref="BuiltInEditorThemes.Light"/>). The IDE
/// around the editor follows the app's theme.
/// </summary>
public sealed class EditorColorTheme
{
    // Roslyn classifications as VS Code's semantic token types, with the TextMate scopes VS Code falls back to.
    private static readonly Dictionary<string, (string? Type, string[] Modifiers, string[] Scopes)> Classifications = new(StringComparer.Ordinal)
    {
        ["keyword"] = (null, [], ["keyword", "storage.type"]),
        ["keyword - control"] = (null, [], ["keyword.control"]),
        ["class name"] = ("class", [], ["entity.name.type.class", "entity.name.type", "support.class"]),
        ["record class name"] = ("class", [], ["entity.name.type.class", "entity.name.type", "support.class"]),
        ["struct name"] = ("struct", [], ["entity.name.type.struct", "entity.name.type"]),
        ["record struct name"] = ("struct", [], ["entity.name.type.struct", "entity.name.type"]),
        ["interface name"] = ("interface", [], ["entity.name.type.interface", "entity.name.type"]),
        ["enum name"] = ("enum", [], ["entity.name.type.enum", "entity.name.type"]),
        ["delegate name"] = ("type", [], ["entity.name.type.delegate", "entity.name.type"]),
        ["type parameter name"] = ("typeParameter", [], ["entity.name.type.parameter", "entity.name.type"]),
        ["module name"] = ("namespace", [], ["entity.name.type.module", "entity.name.namespace"]),
        ["namespace name"] = ("namespace", [], ["entity.name.namespace"]),
        ["method name"] = ("method", [], ["entity.name.function.member", "entity.name.function"]),
        ["extension method name"] = ("method", ["static"], ["entity.name.function.member", "entity.name.function"]),
        ["local name"] = ("variable", [], ["variable.other.readwrite", "variable"]),
        ["parameter name"] = ("parameter", [], ["variable.parameter", "variable"]),
        ["field name"] = ("property", [], ["variable.other.property", "variable"]),
        ["property name"] = ("property", [], ["variable.other.property", "variable"]),
        ["event name"] = ("event", [], ["variable.other.event", "variable.other.property", "variable"]),
        ["constant name"] = ("variable", ["readonly"], ["variable.other.constant", "variable"]),
        ["enum member name"] = ("enumMember", [], ["variable.other.enummember", "constant.other.enum", "variable"]),
        ["label name"] = ("label", [], ["entity.name.label"]),
        ["string"] = ("string", [], ["string"]),
        ["string - verbatim"] = ("string", [], ["string"]),
        ["string - escape character"] = (null, [], ["constant.character.escape"]),
        ["number"] = ("number", [], ["constant.numeric"]),
        ["comment"] = ("comment", [], ["comment"]),
        ["excluded code"] = (null, [], ["comment"]),
        ["preprocessor keyword"] = (null, [], ["keyword.preprocessor", "meta.preprocessor"]),
        ["preprocessor text"] = (null, [], ["meta.preprocessor"]),
        ["operator"] = ("operator", [], ["keyword.operator"]),
        ["operator - overloaded"] = ("operator", [], ["keyword.operator"]),
    };

    // VS Code's own colors for a theme that leaves them out (its Dark+ and Light+ defaults), not TabStudio's palette:
    // an extension's theme looks as it does in VS Code.
    private static readonly Color VsCodeDarkBackground = Color.FromRgb(0x1E, 0x1E, 0x1E);
    private static readonly Color VsCodeDarkForeground = Color.FromRgb(0xD4, 0xD4, 0xD4);
    private static readonly Color VsCodeDarkSelection = Color.FromRgb(0x26, 0x4F, 0x78);
    private static readonly Color VsCodeLightBackground = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static readonly Color VsCodeLightForeground = Color.FromRgb(0x1F, 0x1F, 0x1F);
    private static readonly Color VsCodeLightSelection = Color.FromRgb(0xAD, 0xD6, 0xFF);

    private readonly Theme _textMate;
    private readonly Dictionary<string, IBrush?> _classifications = new(StringComparer.Ordinal);
    private readonly Func<string, IBrush?>? _classify;

    /// <summary>Prepares a theme for the editor.</summary>
    /// <param name="theme">The theme, as read from its extension.</param>
    /// <param name="key">Identifies the theme across extensions, for remembering the user's choice.</param>
    /// <param name="classify">
    /// The colors of Roslyn's classifications, for a theme that knows them, such as TabStudio's own; <see langword="null"/>
    /// derives them from the theme's semantic and TextMate colors.
    /// </param>
    public EditorColorTheme(ColorTheme theme, string key, Func<string, IBrush?>? classify = null)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentException.ThrowIfNullOrEmpty(key);
        Source = theme;
        Key = key;
        _classify = classify;
        RawTheme = ToRawTheme(theme);
        _textMate = Theme.CreateFromRawTheme(RawTheme, null);

        var dark = theme.IsDark;
        var background = ParseColor(theme.DefaultBackground) ?? (dark ? VsCodeDarkBackground : VsCodeLightBackground);
        var foreground = ParseColor(theme.DefaultForeground) ?? (dark ? VsCodeDarkForeground : VsCodeLightForeground);
        var edge = dark ? VsCodeLightBackground : Color.FromRgb(0, 0, 0);
        Background = Solid(background);
        Foreground = Solid(foreground);
        CurrentLine = Solid(ParseColor(theme.Color("editor.lineHighlightBackground")) is { A: > 0 } line
            ? line
            : ThemeColors.Mix(background, edge, 0.05));
        Selection = Solid(ParseColor(theme.Color("editor.selectionBackground")) ?? (dark ? VsCodeDarkSelection : VsCodeLightSelection));
        Caret = Solid(ParseColor(theme.Color("editorCursor.foreground")) ?? foreground);
        LineNumbers = Solid(ParseColor(theme.Color("editorLineNumber.foreground")) ?? ThemeColors.Mix(background, foreground, 0.45));
    }

    /// <summary>Identifies the theme across extensions.</summary>
    public string Key { get; }

    /// <summary>The theme's name.</summary>
    public string Name => Source.Name;

    /// <summary>The theme as its extension wrote it.</summary>
    public ColorTheme Source { get; }

    /// <summary>The theme for TextMate highlighting.</summary>
    public IRawTheme RawTheme { get; }

    /// <summary>The editor's background.</summary>
    public IBrush Background { get; }

    /// <summary>The default text color.</summary>
    public IBrush Foreground { get; }

    /// <summary>The background of the caret's line.</summary>
    public IBrush CurrentLine { get; }

    /// <summary>The selection.</summary>
    public IBrush Selection { get; }

    /// <summary>The caret.</summary>
    public IBrush Caret { get; }

    /// <summary>The line numbers.</summary>
    public IBrush LineNumbers { get; }

    /// <summary>
    /// The color of a Roslyn classification: the theme's semantic token color for it when the theme has semantic
    /// highlighting, otherwise its TextMate color for the scopes VS Code uses for that kind of token, or
    /// <see langword="null"/> for the default text color.
    /// </summary>
    /// <param name="classification">A Roslyn classification name, such as <c>class name</c>.</param>
    public IBrush? ClassificationBrush(string classification)
    {
        ArgumentNullException.ThrowIfNull(classification);
        lock (_classifications)
        {
            if (!_classifications.TryGetValue(classification, out var brush))
            {
                brush = _classify is { } classify ? classify(classification) : Resolve(classification);
                _classifications[classification] = brush;
            }

            return brush;
        }
    }

    /// <summary>The theme's TextMate color for a scope, or <see langword="null"/> when no rule colors it.</summary>
    /// <param name="scope">A scope such as <c>keyword.control.perl</c>.</param>
    public string? ScopeColor(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        foreach (var rule in _textMate.Match([scope]))
        {
            if (rule.foreground > 0)
            {
                return _textMate.GetColor(rule.foreground);
            }
        }

        return null;
    }

    private IBrush? Resolve(string classification)
    {
        var key = classification.StartsWith("xml doc comment", StringComparison.Ordinal) ? "comment"
            : classification.StartsWith("regex", StringComparison.Ordinal) || classification.StartsWith("json", StringComparison.Ordinal) ? "string"
            : classification;
        if (!Classifications.TryGetValue(key, out var mapping))
        {
            return null;
        }

        if (mapping.Type is { } type && SemanticTokenStyles.Resolve(Source, type, mapping.Modifiers, "csharp")?.Foreground is { } semantic
            && ThemeColors.Brush(semantic) is { } semanticBrush)
        {
            return semanticBrush;
        }

        foreach (var scope in mapping.Scopes)
        {
            if (ScopeColor(scope) is { } color && ThemeColors.Brush(color) is { } brush)
            {
                return brush;
            }
        }

        return null;
    }

    private static Color? ParseColor(string? text) => ThemeColors.TryParse(text, out var color) ? color : null;

    private static ImmutableSolidColorBrush Solid(Color color) => new(color);

    /// <summary>Writes the theme as a VS Code JSON theme and reads it with TextMateSharp, which resolves its scopes and font styles.</summary>
    private static IRawTheme ToRawTheme(ColorTheme theme)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("name", theme.Name);
            json.WriteStartObject("colors");
            foreach (var (key, value) in theme.Colors)
            {
                json.WriteString(key, value);
            }

            json.WriteEndObject();
            json.WriteStartArray("tokenColors");
            foreach (var rule in theme.TokenColors)
            {
                json.WriteStartObject();
                if (rule.Scopes.Count > 0)
                {
                    json.WriteStartArray("scope");
                    foreach (var scope in rule.Scopes)
                    {
                        json.WriteStringValue(scope);
                    }

                    json.WriteEndArray();
                }

                json.WriteStartObject("settings");
                WriteIfSet(json, "foreground", rule.Foreground);
                WriteIfSet(json, "background", rule.Background);
                WriteIfSet(json, "fontStyle", rule.FontStyle);
                json.WriteEndObject();
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8);
        return ThemeReader.ReadThemeSync(reader);
    }

    private static void WriteIfSet(Utf8JsonWriter json, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            json.WriteString(name, value);
        }
    }
}
