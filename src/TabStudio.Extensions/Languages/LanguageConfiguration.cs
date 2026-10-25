using System.Text.RegularExpressions;

namespace TabStudio.Extensions.Languages;

/// <summary>A pair of brackets or delimiters, such as <c>{</c> and <c>}</c>, or <c>/*</c> and <c>*/</c>.</summary>
/// <param name="Open">The opening text.</param>
/// <param name="Close">The closing text.</param>
public sealed record BracketPair(string Open, string Close);

/// <summary>Kinds of tokens where something does not apply, such as auto-closing a quote inside a string.</summary>
[Flags]
public enum TokenContexts
{
    /// <summary>Code.</summary>
    None = 0,

    /// <summary>Inside a string.</summary>
    String = 1,

    /// <summary>Inside a comment.</summary>
    Comment = 2,

    /// <summary>Inside a regular expression literal.</summary>
    RegularExpression = 4,
}

/// <summary>A pair that is closed automatically when its opening text is typed.</summary>
/// <param name="Open">The opening text; typing its last character closes the pair.</param>
/// <param name="Close">The text inserted after the caret.</param>
/// <param name="NotIn">Where the pair is not closed, such as a quote inside a string.</param>
public sealed record AutoClosingPair(string Open, string Close, TokenContexts NotIn = TokenContexts.None);

/// <summary>What pressing Enter does to the indentation of the new line.</summary>
public enum IndentAction
{
    /// <summary>The new line keeps the indentation of the line above.</summary>
    None,

    /// <summary>The new line is indented one level more.</summary>
    Indent,

    /// <summary>
    /// The new line is indented one level more, and the text after the caret moves to a line below it at the
    /// original indentation, as between <c>{</c> and <c>}</c>.
    /// </summary>
    IndentOutdent,

    /// <summary>The new line is indented one level less.</summary>
    Outdent,
}

/// <summary>What pressing Enter does: the indentation, and text added or removed at the start of the new line.</summary>
/// <param name="Indent">The indentation change.</param>
/// <param name="AppendText">Text to add after the indentation, such as <c>// </c> to continue a comment, or <see langword="null"/>.</param>
/// <param name="RemoveText">How many characters of indentation to remove from the new line.</param>
public sealed record EnterAction(IndentAction Indent, string? AppendText = null, int RemoveText = 0);

/// <summary>A rule of <c>onEnterRules</c>: when the text around the caret matches, Enter does the rule's action.</summary>
/// <param name="BeforeText">Must match the line's text before the caret.</param>
/// <param name="AfterText">When set, must match the line's text after the caret.</param>
/// <param name="PreviousLineText">When set, must match the line above.</param>
/// <param name="Action">What Enter does.</param>
public sealed record OnEnterRule(Regex BeforeText, Regex? AfterText, Regex? PreviousLineText, EnterAction Action);

/// <summary>The <c>indentationRules</c>: patterns that say when lines are indented or outdented.</summary>
/// <param name="IncreaseIndent">A line matching it indents the lines after it.</param>
/// <param name="DecreaseIndent">A line matching it is outdented, such as one that starts with <c>}</c>.</param>
/// <param name="IndentNextLine">A line matching it indents only the next line, such as <c>if (x)</c> without braces.</param>
/// <param name="UnindentedLine">A line matching it keeps its indentation and does not affect the next line's.</param>
public sealed record IndentationRules(Regex? IncreaseIndent, Regex? DecreaseIndent, Regex? IndentNextLine, Regex? UnindentedLine);

/// <summary>The <c>folding</c> rules: indentation-based folding and region markers.</summary>
/// <param name="OffSide">
/// Whether the language uses indentation for blocks, as Python does; blank lines at the end of a block then stay
/// outside its fold.
/// </param>
/// <param name="MarkerStart">A line matching it starts a region, such as <c>#region</c>.</param>
/// <param name="MarkerEnd">A line matching it ends a region.</param>
public sealed record FoldingRules(bool OffSide, Regex? MarkerStart, Regex? MarkerEnd);

/// <summary>
/// A language's editing rules, from an extension's language configuration file: comments, brackets, auto-closing and
/// surrounding pairs, indentation, folding and what a word is.
/// </summary>
public sealed record LanguageConfiguration
{
    /// <summary>The default for <see cref="AutoCloseBefore"/>, as VS Code has it.</summary>
    public const string DefaultAutoCloseBefore = ";:.,=}])> \n\t";

    /// <summary>No rules.</summary>
    public static LanguageConfiguration Empty { get; } = new();

    /// <summary>The line comment, such as <c>#</c>, or <see langword="null"/>.</summary>
    public string? LineComment { get; init; }

    /// <summary>The block comment delimiters, such as <c>/*</c> and <c>*/</c>, or <see langword="null"/>.</summary>
    public BracketPair? BlockComment { get; init; }

    /// <summary>The brackets, which are matched and indent.</summary>
    public IReadOnlyList<BracketPair> Brackets { get; init; } = [];

    /// <summary>The pairs that are closed when their opening text is typed.</summary>
    public IReadOnlyList<AutoClosingPair> AutoClosingPairs { get; init; } = [];

    /// <summary>The pairs that surround the selection when their opening character is typed.</summary>
    public IReadOnlyList<BracketPair> SurroundingPairs { get; init; } = [];

    /// <summary>The characters after the caret that still allow a pair to be closed; a pair is never closed before other text.</summary>
    public string AutoCloseBefore { get; init; } = DefaultAutoCloseBefore;

    /// <summary>The folding rules, or <see langword="null"/>.</summary>
    public FoldingRules? Folding { get; init; }

    /// <summary>What a word is, for double-tap selection and snippet variables, or <see langword="null"/>.</summary>
    public Regex? WordPattern { get; init; }

    /// <summary>The indentation rules, or <see langword="null"/>.</summary>
    public IndentationRules? Indentation { get; init; }

    /// <summary>The rules for Enter, tried in order.</summary>
    public IReadOnlyList<OnEnterRule> OnEnterRules { get; init; } = [];
}
