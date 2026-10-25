using System.Text.Json;

namespace TabStudio.LanguageServers.Protocol;

// The basic shapes of the Language Server Protocol 3.17, as TabStudio reads and writes them. Property names are
// converted to camelCase by LspJsonContext. Fields the protocol defines as a union of shapes are JsonElements,
// read by LspUnions.

/// <summary>A position: zero-based line and UTF-16 character offset in that line.</summary>
internal sealed record PositionDto(int Line, int Character);

/// <summary>A range from a start position to an end position, exclusive.</summary>
internal sealed record RangeDto(PositionDto Start, PositionDto End);

/// <summary>A range in a document.</summary>
internal sealed record LocationDto(string Uri, RangeDto Range);

/// <summary>A link to a range in a document, with the range to select there.</summary>
internal sealed record LocationLinkDto(RangeDto? OriginSelectionRange, string TargetUri, RangeDto TargetRange, RangeDto? TargetSelectionRange);

/// <summary>Names a document.</summary>
internal sealed record TextDocumentIdentifierDto(string Uri);

/// <summary>Names a version of a document.</summary>
internal sealed record VersionedTextDocumentIdentifierDto(string Uri, int Version);

/// <summary>A document as it is opened: its language, version and text.</summary>
internal sealed record TextDocumentItemDto(string Uri, string LanguageId, int Version, string Text);

/// <summary>A position in a document, the parameters of most requests.</summary>
internal sealed record TextDocumentPositionParamsDto(TextDocumentIdentifierDto TextDocument, PositionDto Position);

/// <summary>Replaces a range with new text.</summary>
internal sealed record TextEditDto(RangeDto Range, string NewText);

/// <summary>A completion edit that can insert or replace the word at the caret.</summary>
internal sealed record InsertReplaceEditDto(string NewText, RangeDto Insert, RangeDto Replace);

/// <summary>Text in plain text or Markdown.</summary>
internal sealed record MarkupContentDto(string Kind, string Value);

/// <summary>A command the server can run when asked with <c>workspace/executeCommand</c>.</summary>
internal sealed record CommandDto(string Title, string Command, JsonElement? Arguments);

/// <summary>A folder of the workspace.</summary>
internal sealed record WorkspaceFolderDto(string Uri, string Name);

/// <summary>Names the server and its version.</summary>
internal sealed record ServerInfoDto(string Name, string? Version);

/// <summary>Names the client and its version.</summary>
internal sealed record ClientInfoDto(string Name, string? Version);
