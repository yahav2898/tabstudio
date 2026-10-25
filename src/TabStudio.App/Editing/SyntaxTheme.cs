using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TabStudio.App.Editing;

/// <summary>
/// The colors of code in the editor, keyed by Roslyn classification name. The same palette drives the
/// TextMate theme for other languages (see <c>TabStudioDark.tmTheme.json</c>), so every file looks alike.
/// </summary>
public static class SyntaxTheme
{
    /// <summary>Plain code text.</summary>
    public static IBrush Text { get; } = Brush("#D7DAE0");

    /// <summary>Keywords such as <c>public</c> and <c>class</c>.</summary>
    public static IBrush Keyword { get; } = Brush("#7FB2FF");

    /// <summary>Control flow keywords such as <c>if</c>, <c>return</c> and <c>await</c>.</summary>
    public static IBrush ControlKeyword { get; } = Brush("#D3A6F2");

    /// <summary>Type names.</summary>
    public static IBrush Type { get; } = Brush("#5FD0BA");

    /// <summary>Method names.</summary>
    public static IBrush Method { get; } = Brush("#E2D58E");

    /// <summary>Locals, parameters, fields and properties.</summary>
    public static IBrush Variable { get; } = Brush("#A9D4F5");

    /// <summary>String and character literals.</summary>
    public static IBrush String { get; } = Brush("#E6A26B");

    /// <summary>Escape sequences inside strings.</summary>
    public static IBrush StringEscape { get; } = Brush("#F0C674");

    /// <summary>Numbers.</summary>
    public static IBrush Number { get; } = Brush("#B5CEA8");

    /// <summary>Comments.</summary>
    public static IBrush Comment { get; } = Brush("#8CA77A");

    /// <summary>Preprocessor directives and punctuation that is visually quieter.</summary>
    public static IBrush Muted { get; } = Brush("#9AA1AE");

    /// <summary>Code excluded by <c>#if</c>.</summary>
    public static IBrush Excluded { get; } = Brush("#6E7685");

    /// <summary>The brush for a Roslyn classification, or <see langword="null"/> to keep the default text color.</summary>
    /// <param name="classification">A name from <c>Microsoft.CodeAnalysis.Classification.ClassificationTypeNames</c>.</param>
    public static IBrush? For(string classification) => classification switch
    {
        "keyword" => Keyword,
        "keyword - control" => ControlKeyword,
        "class name" or "record class name" or "struct name" or "record struct name" or "interface name"
            or "enum name" or "delegate name" or "type parameter name" or "module name" => Type,
        "method name" or "extension method name" => Method,
        "local name" or "parameter name" or "field name" or "property name" or "event name"
            or "constant name" or "enum member name" or "label name" => Variable,
        "string" or "string - verbatim" => String,
        "string - escape character" => StringEscape,
        "number" => Number,
        "comment" => Comment,
        "excluded code" => Excluded,
        "preprocessor keyword" or "preprocessor text" => Muted,
        _ when classification.StartsWith("xml doc comment", StringComparison.Ordinal) => Comment,
        _ when classification.StartsWith("regex", StringComparison.Ordinal) => String,
        _ when classification.StartsWith("json", StringComparison.Ordinal) => String,
        _ => null,
    };

    private static ImmutableSolidColorBrush Brush(string color) => new(Color.Parse(color));
}
