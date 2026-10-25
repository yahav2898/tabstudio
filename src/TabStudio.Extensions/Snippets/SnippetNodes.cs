namespace TabStudio.Extensions.Snippets;

/// <summary>A part of a parsed snippet body.</summary>
public abstract record SnippetNode;

/// <summary>Literal text.</summary>
/// <param name="Value">The text, with escapes already resolved.</param>
public sealed record SnippetText(string Value) : SnippetNode;

/// <summary>
/// A tab stop: <c>$1</c>, a placeholder <c>${1:default}</c>, a choice <c>${1|one,two|}</c>, or a transformed copy of
/// another tab stop <c>${1/(.*)/${1:/upcase}/}</c>. Tab stops with the same index are linked: typing in one changes all.
/// </summary>
/// <param name="Index">The tab stop's number; 0 is where the caret ends up.</param>
/// <param name="Placeholder">The default content, which may contain other tab stops; empty for a plain tab stop.</param>
/// <param name="Choices">The options of a choice, or <see langword="null"/>.</param>
/// <param name="Transform">The transform of a transformed copy, or <see langword="null"/>.</param>
public sealed record SnippetTabStop(int Index, IReadOnlyList<SnippetNode> Placeholder, IReadOnlyList<string>? Choices = null, SnippetTransform? Transform = null) : SnippetNode;

/// <summary>A variable: <c>$TM_FILENAME</c>, <c>${TM_FILENAME:default}</c> or <c>${TM_FILENAME/(.*)/${1:/upcase}/}</c>.</summary>
/// <param name="Name">The variable's name.</param>
/// <param name="Default">What is inserted when the variable has no value; may contain tab stops.</param>
/// <param name="Transform">How the value is transformed, or <see langword="null"/>.</param>
public sealed record SnippetVariable(string Name, IReadOnlyList<SnippetNode> Default, SnippetTransform? Transform = null) : SnippetNode;

/// <summary>A part of a transform's format string.</summary>
public abstract record SnippetFormatPart;

/// <summary>Literal text in a format string.</summary>
/// <param name="Value">The text.</param>
public sealed record SnippetFormatText(string Value) : SnippetFormatPart;

/// <summary>
/// A reference to a capture group in a format string: <c>$1</c>, <c>${1:/upcase}</c>, <c>${1:+if}</c>, <c>${1:?if:else}</c>
/// or <c>${1:-else}</c>.
/// </summary>
/// <param name="Group">The capture group's number.</param>
/// <param name="Modifier">One of <c>upcase</c>, <c>downcase</c>, <c>capitalize</c>, <c>camelcase</c> and <c>pascalcase</c>, or <see langword="null"/>.</param>
/// <param name="IfText">Inserted instead of the group when it matched, or <see langword="null"/>.</param>
/// <param name="ElseText">Inserted when the group did not match, or <see langword="null"/>.</param>
public sealed record SnippetFormatGroup(int Group, string? Modifier = null, string? IfText = null, string? ElseText = null) : SnippetFormatPart;
