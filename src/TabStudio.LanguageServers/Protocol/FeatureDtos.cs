using System.Text.Json;

namespace TabStudio.LanguageServers.Protocol;

/// <summary>Why completion was requested.</summary>
internal sealed record CompletionContextDto(int TriggerKind, string? TriggerCharacter);

/// <summary>The parameters of <c>textDocument/completion</c>.</summary>
internal sealed record CompletionParamsDto(TextDocumentIdentifierDto TextDocument, PositionDto Position, CompletionContextDto? Context);

/// <summary>Extra text shown with a completion item's label.</summary>
internal sealed record CompletionItemLabelDetailsDto(string? Detail, string? Description);

/// <summary>A completion item. Its JSON is kept as well, to send back for <c>completionItem/resolve</c>.</summary>
internal sealed record CompletionItemDto(
    string Label,
    CompletionItemLabelDetailsDto? LabelDetails,
    int? Kind,
    IReadOnlyList<int>? Tags,
    string? Detail,
    JsonElement? Documentation,
    bool? Deprecated,
    bool? Preselect,
    string? SortText,
    string? FilterText,
    string? InsertText,
    int? InsertTextFormat,
    JsonElement? TextEdit,
    string? TextEditText,
    IReadOnlyList<TextEditDto>? AdditionalTextEdits,
    IReadOnlyList<string>? CommitCharacters,
    CommandDto? Command,
    JsonElement? Data);

/// <summary>Values a completion list gives every item that does not have its own.</summary>
internal sealed record CompletionItemDefaultsDto(IReadOnlyList<string>? CommitCharacters, JsonElement? EditRange, int? InsertTextFormat, JsonElement? Data);

/// <summary>A completion list; its items are read one by one, so each keeps its JSON.</summary>
internal sealed record CompletionListDto(bool IsIncomplete, CompletionItemDefaultsDto? ItemDefaults, JsonElement Items);

/// <summary>The result of <c>textDocument/hover</c>.</summary>
internal sealed record HoverDto(JsonElement Contents, RangeDto? Range);

/// <summary>Why signature help was requested.</summary>
internal sealed record SignatureHelpContextDto(int TriggerKind, string? TriggerCharacter, bool IsRetrigger);

/// <summary>The parameters of <c>textDocument/signatureHelp</c>.</summary>
internal sealed record SignatureHelpParamsDto(TextDocumentIdentifierDto TextDocument, PositionDto Position, SignatureHelpContextDto? Context);

/// <summary>A parameter of a signature: its label is a string in the signature's label or a [start, end) pair.</summary>
internal sealed record ParameterInformationDto(JsonElement Label, JsonElement? Documentation);

/// <summary>One signature of signature help.</summary>
internal sealed record SignatureInformationDto(string Label, JsonElement? Documentation, IReadOnlyList<ParameterInformationDto>? Parameters, int? ActiveParameter);

/// <summary>The result of <c>textDocument/signatureHelp</c>.</summary>
internal sealed record SignatureHelpDto(IReadOnlyList<SignatureInformationDto>? Signatures, int? ActiveSignature, int? ActiveParameter);

/// <summary>Whether Find All References includes the declaration.</summary>
internal sealed record ReferenceContextDto(bool IncludeDeclaration);

/// <summary>The parameters of <c>textDocument/references</c>.</summary>
internal sealed record ReferenceParamsDto(TextDocumentIdentifierDto TextDocument, PositionDto Position, ReferenceContextDto Context);

/// <summary>An occurrence of the symbol at the caret.</summary>
internal sealed record DocumentHighlightDto(RangeDto Range, int? Kind);

/// <summary>The parameters of requests about a whole document, such as <c>textDocument/documentSymbol</c>.</summary>
internal sealed record DocumentParamsDto(TextDocumentIdentifierDto TextDocument);

/// <summary>A symbol declared in a document, with the symbols declared inside it.</summary>
internal sealed record DocumentSymbolDto(string Name, string? Detail, int Kind, IReadOnlyList<int>? Tags, RangeDto Range, RangeDto SelectionRange, IReadOnlyList<DocumentSymbolDto>? Children);

/// <summary>A symbol in the older, flat form of <c>textDocument/documentSymbol</c> and <c>workspace/symbol</c>.</summary>
internal sealed record SymbolInformationDto(string Name, int Kind, IReadOnlyList<int>? Tags, LocationDto Location, string? ContainerName);

/// <summary>The parameters of <c>workspace/symbol</c>.</summary>
internal sealed record WorkspaceSymbolParamsDto(string Query);

/// <summary>A symbol found by <c>workspace/symbol</c>; its location may have only a URI.</summary>
internal sealed record WorkspaceSymbolDto(string Name, int Kind, IReadOnlyList<int>? Tags, string? ContainerName, JsonElement Location);

/// <summary>The parameters of <c>textDocument/rename</c>.</summary>
internal sealed record RenameParamsDto(TextDocumentIdentifierDto TextDocument, PositionDto Position, string NewName);

/// <summary>A document and, optionally, the version the edits are for.</summary>
internal sealed record OptionalVersionedTextDocumentIdentifierDto(string Uri, int? Version);

/// <summary>Edits to one document.</summary>
internal sealed record TextDocumentEditDto(OptionalVersionedTextDocumentIdentifierDto TextDocument, IReadOnlyList<TextEditDto> Edits);

/// <summary>Options of a file operation.</summary>
internal sealed record FileOperationOptionsDto(bool? Overwrite, bool? IgnoreIfExists, bool? Recursive, bool? IgnoreIfNotExists);

/// <summary>Creates a file.</summary>
internal sealed record CreateFileDto(string Kind, string Uri, FileOperationOptionsDto? Options);

/// <summary>Renames a file.</summary>
internal sealed record RenameFileDto(string Kind, string OldUri, string NewUri, FileOperationOptionsDto? Options);

/// <summary>Deletes a file.</summary>
internal sealed record DeleteFileDto(string Kind, string Uri, FileOperationOptionsDto? Options);

/// <summary>Changes to many documents: edits by URI, or document changes and file operations in order.</summary>
internal sealed record WorkspaceEditDto(IReadOnlyDictionary<string, IReadOnlyList<TextEditDto>>? Changes, JsonElement? DocumentChanges);

/// <summary>A description of a diagnostic's code, such as a link to its documentation.</summary>
internal sealed record CodeDescriptionDto(string Href);

/// <summary>An error, warning or hint the server found.</summary>
internal sealed record DiagnosticDto(
    RangeDto Range,
    int? Severity,
    JsonElement? Code,
    CodeDescriptionDto? CodeDescription,
    string? Source,
    string Message,
    IReadOnlyList<int>? Tags);

/// <summary>The parameters of <c>textDocument/publishDiagnostics</c>; diagnostics stay JSON to send back with code actions.</summary>
internal sealed record PublishDiagnosticsParamsDto(string Uri, int? Version, IReadOnlyList<JsonElement> Diagnostics);

/// <summary>What code actions are requested for.</summary>
internal sealed record CodeActionContextDto(IReadOnlyList<JsonElement> Diagnostics, IReadOnlyList<string>? Only, int? TriggerKind);

/// <summary>The parameters of <c>textDocument/codeAction</c>.</summary>
internal sealed record CodeActionParamsDto(TextDocumentIdentifierDto TextDocument, RangeDto Range, CodeActionContextDto Context);

/// <summary>Why a code action cannot be applied now.</summary>
internal sealed record CodeActionDisabledDto(string Reason);

/// <summary>A code action: an edit, a command, or both.</summary>
internal sealed record CodeActionDto(
    string Title,
    string? Kind,
    IReadOnlyList<DiagnosticDto>? Diagnostics,
    bool? IsPreferred,
    CodeActionDisabledDto? Disabled,
    WorkspaceEditDto? Edit,
    CommandDto? Command,
    JsonElement? Data);

/// <summary>How to format: the editor's indentation settings.</summary>
internal sealed record FormattingOptionsDto(int TabSize, bool InsertSpaces, bool? TrimTrailingWhitespace, bool? InsertFinalNewline);

/// <summary>The parameters of <c>textDocument/formatting</c>.</summary>
internal sealed record DocumentFormattingParamsDto(TextDocumentIdentifierDto TextDocument, FormattingOptionsDto Options);

/// <summary>The parameters of <c>textDocument/onTypeFormatting</c>.</summary>
internal sealed record DocumentOnTypeFormattingParamsDto(TextDocumentIdentifierDto TextDocument, PositionDto Position, string Ch, FormattingOptionsDto Options);

/// <summary>Semantic tokens of a whole document, five integers per token.</summary>
internal sealed record SemanticTokensDto(string? ResultId, IReadOnlyList<int> Data);

/// <summary>The parameters of <c>textDocument/inlayHint</c>.</summary>
internal sealed record InlayHintParamsDto(TextDocumentIdentifierDto TextDocument, RangeDto Range);

/// <summary>A part of an inlay hint's label.</summary>
internal sealed record InlayHintLabelPartDto(string Value, JsonElement? Tooltip);

/// <summary>An inlay hint: text shown in the code that is not part of it, such as a parameter name or an inferred type.</summary>
internal sealed record InlayHintDto(PositionDto Position, JsonElement Label, int? Kind, JsonElement? Tooltip, bool? PaddingLeft, bool? PaddingRight);

/// <summary>A range of lines that can be folded.</summary>
internal sealed record FoldingRangeDto(int StartLine, int? StartCharacter, int EndLine, int? EndCharacter, string? Kind, string? CollapsedText);
