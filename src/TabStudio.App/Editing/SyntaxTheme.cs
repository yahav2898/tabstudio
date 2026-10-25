using Avalonia.Media;
using Avalonia.Styling;
using TabStudio.App.Theming;

namespace TabStudio.App.Editing;

/// <summary>
/// TabStudio's colors of code, keyed by Roslyn classification name: the <c>Syntax.*</c> tokens of Colors.axaml, in the
/// dark or light theme. The same palette drives the TextMate theme for other languages (<c>TabStudioDark.tmTheme.json</c>
/// and <see cref="BuiltInEditorThemes.Light"/>), so every file looks alike.
/// </summary>
public static class SyntaxTheme
{
    /// <summary>Plain code text.</summary>
    public static IBrush Text => ThemeBrushes.Get("Syntax.Text");

    /// <summary>Keywords such as <c>public</c> and <c>class</c>.</summary>
    public static IBrush Keyword => ThemeBrushes.Get("Syntax.Keyword");

    /// <summary>Control flow keywords such as <c>if</c>, <c>return</c> and <c>await</c>.</summary>
    public static IBrush ControlKeyword => ThemeBrushes.Get("Syntax.ControlKeyword");

    /// <summary>Type names.</summary>
    public static IBrush Type => ThemeBrushes.Get("Syntax.Type");

    /// <summary>Method names.</summary>
    public static IBrush Method => ThemeBrushes.Get("Syntax.Method");

    /// <summary>Locals, parameters, fields and properties.</summary>
    public static IBrush Variable => ThemeBrushes.Get("Syntax.Variable");

    /// <summary>String and character literals.</summary>
    public static IBrush StringLiteral => ThemeBrushes.Get("Syntax.String");

    /// <summary>Escape sequences inside strings.</summary>
    public static IBrush StringEscape => ThemeBrushes.Get("Syntax.StringEscape");

    /// <summary>Numbers.</summary>
    public static IBrush Number => ThemeBrushes.Get("Syntax.Number");

    /// <summary>Comments.</summary>
    public static IBrush Comment => ThemeBrushes.Get("Syntax.Comment");

    /// <summary>Preprocessor directives and punctuation that is visually quieter.</summary>
    public static IBrush Muted => ThemeBrushes.Get("Syntax.Muted");

    /// <summary>Code excluded by <c>#if</c>.</summary>
    public static IBrush Excluded => ThemeBrushes.Get("Syntax.Excluded");

    /// <summary>
    /// The brush for a Roslyn classification in the app's theme, or <see langword="null"/> to keep the default text
    /// color. The brush changes color with the theme.
    /// </summary>
    /// <param name="classification">A name from <c>Microsoft.CodeAnalysis.Classification.ClassificationTypeNames</c>.</param>
    public static IBrush? For(string classification) => TokenFor(classification) is { } key ? ThemeBrushes.Get(key) : null;

    /// <summary>The color of a Roslyn classification in a given theme, or <see langword="null"/> for the default text color.</summary>
    /// <param name="classification">A classification name.</param>
    /// <param name="variant">The theme.</param>
    public static Color? ColorFor(string classification, ThemeVariant variant) =>
        TokenFor(classification) is { } key ? ThemeBrushes.ColorOf(key, variant) : null;

    /// <summary>The palette token of a Roslyn classification, or <see langword="null"/> for the default text color.</summary>
    /// <param name="classification">A classification name.</param>
    public static string? TokenFor(string classification)
    {
        ArgumentNullException.ThrowIfNull(classification);
        return classification switch
        {
            "keyword" => "Syntax.Keyword",
            "keyword - control" => "Syntax.ControlKeyword",
            "class name" or "record class name" or "struct name" or "record struct name" or "interface name"
                or "enum name" or "delegate name" or "type parameter name" or "module name" => "Syntax.Type",
            "method name" or "extension method name" => "Syntax.Method",
            "local name" or "parameter name" or "field name" or "property name" or "event name"
                or "constant name" or "enum member name" or "label name" => "Syntax.Variable",
            "string" or "string - verbatim" => "Syntax.String",
            "string - escape character" => "Syntax.StringEscape",
            "number" => "Syntax.Number",
            "comment" => "Syntax.Comment",
            "excluded code" => "Syntax.Excluded",
            "preprocessor keyword" or "preprocessor text" => "Syntax.Muted",
            _ when classification.StartsWith("xml doc comment", StringComparison.Ordinal) => "Syntax.Comment",
            _ when classification.StartsWith("regex", StringComparison.Ordinal) => "Syntax.String",
            _ when classification.StartsWith("json", StringComparison.Ordinal) => "Syntax.String",
            _ => null,
        };
    }
}
