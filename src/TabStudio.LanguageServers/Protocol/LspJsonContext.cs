using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.LanguageServers.Protocol;

/// <summary>
/// Source-generated JSON for the Language Server Protocol, so nothing depends on reflection and the client works
/// with trimming on Android. Properties are camelCase, and properties without a value are left out.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(PositionDto))]
[JsonSerializable(typeof(RangeDto))]
[JsonSerializable(typeof(LocationDto))]
[JsonSerializable(typeof(LocationLinkDto))]
[JsonSerializable(typeof(TextDocumentIdentifierDto))]
[JsonSerializable(typeof(TextDocumentPositionParamsDto))]
[JsonSerializable(typeof(TextEditDto))]
[JsonSerializable(typeof(IReadOnlyList<TextEditDto>))]
[JsonSerializable(typeof(InsertReplaceEditDto))]
[JsonSerializable(typeof(MarkupContentDto))]
[JsonSerializable(typeof(CommandDto))]
[JsonSerializable(typeof(InitializeParamsDto))]
[JsonSerializable(typeof(InitializeResultDto))]
[JsonSerializable(typeof(DidOpenTextDocumentParamsDto))]
[JsonSerializable(typeof(DidChangeTextDocumentParamsDto))]
[JsonSerializable(typeof(DidSaveTextDocumentParamsDto))]
[JsonSerializable(typeof(DidCloseTextDocumentParamsDto))]
[JsonSerializable(typeof(DidChangeConfigurationParamsDto))]
[JsonSerializable(typeof(MessageParamsDto))]
[JsonSerializable(typeof(MessageActionItemDto))]
[JsonSerializable(typeof(ShowMessageRequestParamsDto))]
[JsonSerializable(typeof(ShowDocumentParamsDto))]
[JsonSerializable(typeof(ShowDocumentResultDto))]
[JsonSerializable(typeof(ConfigurationParamsDto))]
[JsonSerializable(typeof(RegistrationParamsDto))]
[JsonSerializable(typeof(UnregistrationParamsDto))]
[JsonSerializable(typeof(ProgressParamsDto))]
[JsonSerializable(typeof(ApplyWorkspaceEditParamsDto))]
[JsonSerializable(typeof(ApplyWorkspaceEditResultDto))]
[JsonSerializable(typeof(ExecuteCommandParamsDto))]
[JsonSerializable(typeof(CompletionParamsDto))]
[JsonSerializable(typeof(CompletionItemDto))]
[JsonSerializable(typeof(CompletionListDto))]
[JsonSerializable(typeof(HoverDto))]
[JsonSerializable(typeof(SignatureHelpParamsDto))]
[JsonSerializable(typeof(SignatureHelpDto))]
[JsonSerializable(typeof(ReferenceParamsDto))]
[JsonSerializable(typeof(DocumentHighlightDto))]
[JsonSerializable(typeof(DocumentParamsDto))]
[JsonSerializable(typeof(DocumentSymbolDto))]
[JsonSerializable(typeof(SymbolInformationDto))]
[JsonSerializable(typeof(WorkspaceSymbolParamsDto))]
[JsonSerializable(typeof(WorkspaceSymbolDto))]
[JsonSerializable(typeof(RenameParamsDto))]
[JsonSerializable(typeof(TextDocumentEditDto))]
[JsonSerializable(typeof(CreateFileDto))]
[JsonSerializable(typeof(RenameFileDto))]
[JsonSerializable(typeof(DeleteFileDto))]
[JsonSerializable(typeof(WorkspaceEditDto))]
[JsonSerializable(typeof(DiagnosticDto))]
[JsonSerializable(typeof(PublishDiagnosticsParamsDto))]
[JsonSerializable(typeof(CodeActionParamsDto))]
[JsonSerializable(typeof(CodeActionDto))]
[JsonSerializable(typeof(DocumentFormattingParamsDto))]
[JsonSerializable(typeof(DocumentOnTypeFormattingParamsDto))]
[JsonSerializable(typeof(SemanticTokensDto))]
[JsonSerializable(typeof(InlayHintParamsDto))]
[JsonSerializable(typeof(InlayHintDto))]
[JsonSerializable(typeof(InlayHintLabelPartDto))]
[JsonSerializable(typeof(FoldingRangeDto))]
internal sealed partial class LspJsonContext : JsonSerializerContext
{
}
