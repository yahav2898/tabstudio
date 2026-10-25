using TabStudio.LanguageServers.Documents;

namespace TabStudio.LanguageServers.Features;

/// <summary>Text from a server, such as documentation, in Markdown or plain text.</summary>
/// <param name="Value">The text.</param>
/// <param name="IsMarkdown">Whether it is Markdown.</param>
public sealed record MarkupText(string Value, bool IsMarkdown)
{
    /// <summary>Whether there is no text to show.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
}

/// <summary>A place in a file, such as a definition or a reference.</summary>
/// <param name="FilePath">Full path of the file.</param>
/// <param name="Span">The range, as offsets into the file's current text.</param>
/// <param name="Line">One-based line of the start, as shown to the user.</param>
/// <param name="Column">One-based column of the start, as shown to the user.</param>
public sealed record LspLocation(string FilePath, TextSpan Span, int Line, int Column);

/// <summary>Replaces a range of a document with new text.</summary>
/// <param name="Span">The range, as offsets into the text the edit was made for.</param>
/// <param name="NewText">The new text.</param>
public sealed record LspTextEdit(TextSpan Span, string NewText);

/// <summary>What committing a completion item inserts in place of the word being typed.</summary>
/// <param name="Span">The range it replaces, in the text the list was computed for.</param>
/// <param name="NewText">The text to insert, with any snippet syntax expanded.</param>
/// <param name="CaretOffset">Where the caret goes, relative to the start of the inserted text.</param>
/// <param name="SelectionLength">How much text after the caret is selected, such as a snippet's first placeholder.</param>
/// <param name="IsSnippet">Whether the server sent a snippet.</param>
public sealed record LspCompletionEdit(TextSpan Span, string NewText, int CaretOffset, int SelectionLength, bool IsSnippet);

/// <summary>A completion list.</summary>
/// <param name="Items">The items, in the server's order.</param>
/// <param name="IsIncomplete">Whether typing more should ask the server again rather than only filter.</param>
public sealed record LspCompletionList(IReadOnlyList<LspCompletionItem> Items, bool IsIncomplete)
{
    /// <summary>The text the list was computed for, which the items' spans are offsets into.</summary>
    public TextSnapshot? Snapshot { get; init; }

    /// <summary>The offset the list was asked for at.</summary>
    public int Offset { get; init; }
}

/// <summary>A hover tooltip.</summary>
/// <param name="Contents">What to show.</param>
/// <param name="Span">The text it is about, when the server says.</param>
public sealed record LspHover(MarkupText Contents, TextSpan? Span);

/// <summary>A parameter of a signature.</summary>
/// <param name="LabelStart">Where its text starts in the signature's label.</param>
/// <param name="LabelLength">The length of its text.</param>
/// <param name="Documentation">Its documentation, if any.</param>
public sealed record LspParameter(int LabelStart, int LabelLength, MarkupText? Documentation);

/// <summary>One signature of the call at the caret.</summary>
/// <param name="Label">The signature, such as <c>greet(name: str) -&gt; str</c>.</param>
/// <param name="Documentation">Its documentation, if any.</param>
/// <param name="Parameters">Its parameters, in order.</param>
/// <param name="ActiveParameter">The parameter the caret is on in this signature, when it says so itself.</param>
public sealed record LspSignature(string Label, MarkupText? Documentation, IReadOnlyList<LspParameter> Parameters, int? ActiveParameter);

/// <summary>Signature help.</summary>
/// <param name="Signatures">The signatures, such as the overloads of a method.</param>
/// <param name="ActiveSignature">The one that matches best.</param>
/// <param name="ActiveParameter">The parameter the caret is on.</param>
public sealed record LspSignatureHelp(IReadOnlyList<LspSignature> Signatures, int ActiveSignature, int ActiveParameter);

/// <summary>An occurrence of the symbol at the caret.</summary>
/// <param name="Span">Where it is.</param>
/// <param name="Kind">How it uses the symbol.</param>
public readonly record struct LspHighlight(TextSpan Span, LspHighlightKind Kind);

/// <summary>A symbol declared in a document, with those declared inside it.</summary>
/// <param name="Name">The name.</param>
/// <param name="Detail">More about it, such as a signature, if any.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Span">The whole declaration.</param>
/// <param name="NameSpan">The name, where navigating to the symbol puts the caret.</param>
/// <param name="Children">The symbols declared inside it.</param>
public sealed record LspDocumentSymbol(string Name, string? Detail, LspSymbolKind Kind, TextSpan Span, TextSpan NameSpan, IReadOnlyList<LspDocumentSymbol> Children);

/// <summary>A symbol found in the workspace.</summary>
/// <param name="Name">The name.</param>
/// <param name="Kind">What it is.</param>
/// <param name="ContainerName">What it is declared in, such as its class, if any.</param>
/// <param name="Location">Where it is.</param>
public sealed record LspWorkspaceSymbol(string Name, LspSymbolKind Kind, string? ContainerName, LspLocation Location);

/// <summary>What Rename may rename at a position.</summary>
/// <param name="Span">The name under the caret.</param>
/// <param name="Placeholder">The name to offer as the starting point.</param>
public sealed record LspRenameTarget(TextSpan Span, string Placeholder);

/// <summary>A change a workspace edit makes to one file.</summary>
/// <param name="Kind">What it does.</param>
/// <param name="FilePath">Full path of the file; for a rename, its current path.</param>
/// <param name="NewFilePath">For a rename, the new path.</param>
/// <param name="Edits">For an edit, the text edits, as offsets into the file's current text, in document order.</param>
/// <param name="Overwrite">For a create or rename, whether an existing file at the target is replaced.</param>
/// <param name="IgnoreIfExists">For a create or rename, whether an existing file at the target makes it do nothing.</param>
public sealed record LspFileChange(LspFileChangeKind Kind, string FilePath, string? NewFilePath, IReadOnlyList<LspTextEdit> Edits, bool Overwrite = false, bool IgnoreIfExists = false);

/// <summary>Changes to files of the workspace, such as from a rename or a code action, in the order to apply them.</summary>
/// <param name="Changes">The changes.</param>
public sealed record LspWorkspaceEdit(IReadOnlyList<LspFileChange> Changes)
{
    /// <summary>An edit that changes nothing.</summary>
    public static LspWorkspaceEdit Empty { get; } = new([]);

    /// <summary>The number of text edits in every file.</summary>
    public int EditCount => Changes.Sum(c => c.Edits.Count);
}

/// <summary>A range of lines that can be folded.</summary>
/// <param name="StartLine">Zero-based first line, which stays visible.</param>
/// <param name="EndLine">Zero-based last line.</param>
/// <param name="Kind">The kind, such as <c>comment</c>, <c>imports</c> or <c>region</c>, if the server says.</param>
public sealed record LspFoldingRange(int StartLine, int EndLine, string? Kind);

/// <summary>Text shown in the code that is not part of it, such as a parameter name or an inferred type.</summary>
/// <param name="Offset">Where it is shown.</param>
/// <param name="Label">The text.</param>
/// <param name="Kind">What it shows.</param>
/// <param name="PaddingLeft">Whether a space goes before it.</param>
/// <param name="PaddingRight">Whether a space goes after it.</param>
/// <param name="ToolTip">More about it, if any.</param>
public sealed record LspInlayHint(int Offset, string Label, LspInlayHintKind Kind, bool PaddingLeft, bool PaddingRight, string? ToolTip);

/// <summary>A token the server classified, such as a parameter or a method name.</summary>
/// <param name="Span">Where it is.</param>
/// <param name="Type">Its type from the server's legend, such as <c>parameter</c>.</param>
/// <param name="Modifiers">Its modifiers from the legend, such as <c>readonly</c>.</param>
public sealed record LspSemanticToken(TextSpan Span, string Type, IReadOnlyList<string> Modifiers);
