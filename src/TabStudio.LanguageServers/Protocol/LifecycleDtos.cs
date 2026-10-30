using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TabStudio.LanguageServers.Protocol;

/// <summary>The parameters of <c>initialize</c>.</summary>
internal sealed record InitializeParamsDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? ProcessId,
    ClientInfoDto ClientInfo,
    string Locale,
    string? RootPath,
    string? RootUri,
    JsonNode? InitializationOptions,
    JsonNode Capabilities,
    string Trace,
    IReadOnlyList<WorkspaceFolderDto> WorkspaceFolders);

/// <summary>The result of <c>initialize</c>: what the server can do.</summary>
internal sealed record InitializeResultDto(JsonElement Capabilities, ServerInfoDto? ServerInfo);

/// <summary>The parameters of <c>textDocument/didOpen</c>.</summary>
internal sealed record DidOpenTextDocumentParamsDto(TextDocumentItemDto TextDocument);

/// <summary>One change of <c>textDocument/didChange</c>: a range and its new text, or the whole text when there is no range.</summary>
internal sealed record TextDocumentContentChangeEventDto(RangeDto? Range, string Text);

/// <summary>The parameters of <c>textDocument/didChange</c>.</summary>
internal sealed record DidChangeTextDocumentParamsDto(VersionedTextDocumentIdentifierDto TextDocument, IReadOnlyList<TextDocumentContentChangeEventDto> ContentChanges);

/// <summary>The parameters of <c>textDocument/didSave</c>.</summary>
internal sealed record DidSaveTextDocumentParamsDto(TextDocumentIdentifierDto TextDocument, string? Text);

/// <summary>The parameters of <c>textDocument/didClose</c>.</summary>
internal sealed record DidCloseTextDocumentParamsDto(TextDocumentIdentifierDto TextDocument);

/// <summary>The parameters of <c>workspace/didChangeConfiguration</c>.</summary>
internal sealed record DidChangeConfigurationParamsDto(JsonNode Settings);

/// <summary>The parameters of <c>window/logMessage</c> and <c>window/showMessage</c>.</summary>
internal sealed record MessageParamsDto(int Type, string Message);

/// <summary>An action button of <c>window/showMessageRequest</c>.</summary>
internal sealed record MessageActionItemDto(string Title);

/// <summary>The parameters of <c>window/showMessageRequest</c>.</summary>
internal sealed record ShowMessageRequestParamsDto(int Type, string Message, IReadOnlyList<MessageActionItemDto>? Actions);

/// <summary>The parameters of <c>window/showDocument</c>.</summary>
internal sealed record ShowDocumentParamsDto(string Uri, bool? External, bool? TakeFocus, RangeDto? Selection);

/// <summary>The result of <c>window/showDocument</c>.</summary>
internal sealed record ShowDocumentResultDto(bool Success);

/// <summary>One section of <c>workspace/configuration</c>.</summary>
internal sealed record ConfigurationItemDto(string? ScopeUri, string? Section);

/// <summary>The parameters of <c>workspace/configuration</c>.</summary>
internal sealed record ConfigurationParamsDto(IReadOnlyList<ConfigurationItemDto> Items);

/// <summary>One registration of <c>client/registerCapability</c>.</summary>
internal sealed record RegistrationDto(string Id, string Method, JsonElement? RegisterOptions);

/// <summary>The parameters of <c>client/registerCapability</c>.</summary>
internal sealed record RegistrationParamsDto(IReadOnlyList<RegistrationDto> Registrations);

/// <summary>One registration to remove with <c>client/unregisterCapability</c>.</summary>
internal sealed record UnregistrationDto(string Id, string Method);

/// <summary>The parameters of <c>client/unregisterCapability</c>; the protocol misspells the property.</summary>
internal sealed record UnregistrationParamsDto(IReadOnlyList<UnregistrationDto> Unregisterations);

/// <summary>The parameters of <c>$/progress</c>.</summary>
internal sealed record ProgressParamsDto(JsonElement Token, JsonElement Value);

/// <summary>The parameters of <c>workspace/applyEdit</c>.</summary>
internal sealed record ApplyWorkspaceEditParamsDto(string? Label, WorkspaceEditDto Edit);

/// <summary>The result of <c>workspace/applyEdit</c>.</summary>
internal sealed record ApplyWorkspaceEditResultDto(bool Applied, string? FailureReason);

/// <summary>The parameters of <c>workspace/executeCommand</c>.</summary>
internal sealed record ExecuteCommandParamsDto(string Command, JsonElement? Arguments);
