using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using TabStudio.App.Editing.Extensions;
using TabStudio.App.Theming;
using TabStudio.Extensions.Manifests;
using TabStudio.Extensions.Themes;

namespace TabStudio.App.Editing;

/// <summary>
/// TabStudio's own editor colors as a VS Code color theme, made from the palette's <c>Syntax.*</c> and <c>Editor.*</c>
/// tokens. The dark one is the editor's built-in look (and <c>TabStudioDark.tmTheme.json</c> for TextMate); the light one
/// is published as the editor's theme while the IDE is light, so C#, every TextMate grammar, the caret, the selection and
/// the margins turn light together, through the same path as an extension's theme.
/// </summary>
public static class BuiltInEditorThemes
{
    /// <summary>The key of TabStudio's light editor theme.</summary>
    public const string LightKey = "tabstudio.light";

    private static EditorColorTheme? _light;

    /// <summary>
    /// TextMate scopes and the token that colors them, as in <c>TabStudioDark.tmTheme.json</c>: the default style first,
    /// then comments, strings, keywords, types, functions, variables, markup and diffs.
    /// </summary>
    internal static IReadOnlyList<(string[] Scopes, string? Token, string? FontStyle)> ScopeRules { get; } =
    [
        Rule("Syntax.Text"),
        Rule("Syntax.Comment", "comment", "punctuation.definition.comment"),
        Rule("Syntax.String", "string", "punctuation.definition.string", "string.quoted", "string.template"),
        Rule("Syntax.StringEscape", "constant.character.escape"),
        Rule("Syntax.Number", "constant.numeric", "constant.language.numeric"),
        Rule("Syntax.Keyword", "constant.language", "constant.other.boolean"),
        Rule("Syntax.Keyword", "keyword", "storage", "storage.type", "storage.modifier", "keyword.other"),
        Rule("Syntax.ControlKeyword", "keyword.control", "keyword.control.flow", "keyword.control.import", "keyword.control.from"),
        Rule("Syntax.Text", "keyword.operator", "punctuation"),
        Rule("Syntax.Type", "entity.name.type", "entity.name.class", "entity.name.struct", "entity.name.enum", "entity.name.interface", "entity.name.namespace", "support.type", "support.class", "entity.other.inherited-class", "storage.type.class.jsdoc"),
        Rule("Syntax.Method", "entity.name.function", "support.function", "meta.function-call.generic", "entity.name.method"),
        Rule("Syntax.Variable", "variable", "variable.parameter", "variable.other", "meta.definition.variable", "support.variable"),
        Rule("Syntax.Keyword", "variable.language"),
        Rule("Syntax.Keyword", "entity.name.tag", "meta.tag.sgml.doctype"),
        Rule("Syntax.Variable", "entity.other.attribute-name"),
        Rule("Syntax.Variable", "support.type.property-name", "support.type.property-name.json", "meta.object-literal.key"),
        Rule("Syntax.Muted", "punctuation.definition.tag", "punctuation.separator.key-value"),
        Rule("Syntax.Muted", "meta.preprocessor", "entity.name.function.preprocessor", "punctuation.definition.directive"),
        StyledRule("Syntax.Keyword", "bold", "markup.heading", "entity.name.section", "markup.heading entity.name"),
        StyledRule(null, "bold", "markup.bold"),
        StyledRule(null, "italic", "markup.italic"),
        Rule("Syntax.String", "markup.inline.raw", "markup.fenced_code", "markup.raw"),
        Rule("Brush.Link", "markup.underline.link", "string.other.link"),
        Rule("Syntax.Muted", "markup.list", "punctuation.definition.list"),
        Rule("Brush.Success", "markup.inserted"),
        Rule("Brush.Error", "markup.deleted"),
        Rule("Brush.Warning", "markup.changed"),
        Rule("Brush.Error", "invalid", "invalid.illegal"),
    ];

    /// <summary>A rule that colors scopes with a token.</summary>
    private static (string[] Scopes, string? Token, string? FontStyle) Rule(string token, params string[] scopes) => (scopes, token, null);

    /// <summary>A rule that colors scopes with a token, or leaves their color, and gives them a font style.</summary>
    private static (string[] Scopes, string? Token, string? FontStyle) StyledRule(string? token, string fontStyle, params string[] scopes) => (scopes, token, fontStyle);

    /// <summary>TabStudio Light: white documents with the light <c>Syntax.*</c> colors. Call on the UI thread.</summary>
    public static EditorColorTheme Light => _light ??= Create("TabStudio Light", ColorThemeKind.Light, ThemeVariant.Light, LightKey);

    /// <summary>Makes TabStudio's editor theme for one of the palette's themes.</summary>
    /// <param name="name">The theme's name.</param>
    /// <param name="kind">Dark or light.</param>
    /// <param name="variant">The palette's theme to take the colors from.</param>
    /// <param name="key">The theme's key.</param>
    internal static EditorColorTheme Create(string name, ColorThemeKind kind, ThemeVariant variant, string key)
    {
        string Hex(string token) => ToVsCodeHex(ThemeBrushes.ColorOf(token, variant));

        var colors = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["editor.background"] = Hex("Brush.Window"),
            ["editor.foreground"] = Hex("Syntax.Text"),
            ["editor.lineHighlightBackground"] = Hex("Brush.CurrentLine"),
            ["editor.selectionBackground"] = Hex("Editor.Selection"),
            ["editorCursor.foreground"] = Hex("Editor.Caret"),
            ["editorLineNumber.foreground"] = Hex("Brush.LineNumber"),
            ["editorBracketMatch.background"] = Hex("Editor.BracketMatch"),
            ["editorBracketMatch.border"] = Hex("Editor.BracketMatchBorder"),
            ["editor.snippetTabstopHighlightBackground"] = Hex("Editor.SnippetTabStop"),
            ["editor.snippetTabstopHighlightBorder"] = Hex("Editor.SnippetTabStopBorder"),
        };
        var rules = ScopeRules
            .Select(rule => new TokenColorRule(rule.Scopes, rule.Token is { } token ? Hex(token) : null, null, rule.FontStyle))
            .ToList();
        var theme = new ColorTheme(name, kind, colors, rules, SemanticHighlighting: false, new Dictionary<string, SemanticTokenStyle>());

        // Roslyn's classifications take exactly the colors SyntaxTheme gives them, as in the dark editor.
        var brushes = new Dictionary<string, IBrush?>(StringComparer.Ordinal);
        return new EditorColorTheme(theme, key, classification =>
        {
            if (!brushes.TryGetValue(classification, out var brush))
            {
                brush = SyntaxTheme.ColorFor(classification, variant) is { } color ? new ImmutableSolidColorBrush(color) : null;
                brushes[classification] = brush;
            }

            return brush;
        });
    }

    /// <summary>A color as VS Code writes it: <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> with the alpha last.</summary>
    /// <param name="color">The color.</param>
    internal static string ToVsCodeHex(Color color) => color.A == 255
        ? string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}")
        : string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}");
}
