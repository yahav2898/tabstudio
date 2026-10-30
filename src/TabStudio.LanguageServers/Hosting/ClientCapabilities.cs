using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Protocol;

namespace TabStudio.LanguageServers.Hosting;

/// <summary>
/// What TabStudio tells servers it can do in <c>initialize</c>. Only what the editor really does is announced, so a
/// server never relies on something TabStudio would ignore.
/// </summary>
internal static class ClientCapabilities
{
    /// <summary>The capabilities object.</summary>
    public static JsonObject Create() => new()
    {
        ["general"] = new JsonObject
        {
            ["positionEncodings"] = Strings("utf-16"),
        },
        ["workspace"] = new JsonObject
        {
            ["applyEdit"] = true,
            ["workspaceEdit"] = new JsonObject
            {
                ["documentChanges"] = true,
                ["resourceOperations"] = Strings("create", "rename", "delete"),
                ["failureHandling"] = "abort",
            },
            ["configuration"] = true,
            ["workspaceFolders"] = true,
            ["didChangeConfiguration"] = new JsonObject { ["dynamicRegistration"] = false },
            ["didChangeWatchedFiles"] = new JsonObject { ["dynamicRegistration"] = true, ["relativePatternSupport"] = false },
            ["symbol"] = new JsonObject { ["symbolKind"] = ValueSet(1, 26) },
            ["executeCommand"] = new JsonObject { ["dynamicRegistration"] = false },
            ["semanticTokens"] = new JsonObject { ["refreshSupport"] = true },
            ["inlayHint"] = new JsonObject { ["refreshSupport"] = true },
        },
        ["textDocument"] = new JsonObject
        {
            ["synchronization"] = new JsonObject
            {
                ["dynamicRegistration"] = false,
                ["didSave"] = true,
                ["willSave"] = false,
                ["willSaveWaitUntil"] = false,
            },
            ["completion"] = new JsonObject
            {
                ["contextSupport"] = true,
                ["completionItem"] = new JsonObject
                {
                    ["snippetSupport"] = true,
                    ["commitCharactersSupport"] = true,
                    ["documentationFormat"] = Strings("markdown", "plaintext"),
                    ["deprecatedSupport"] = true,
                    ["preselectSupport"] = true,
                    ["tagSupport"] = new JsonObject { ["valueSet"] = Numbers(LspValues.ItemTag.Deprecated) },
                    ["insertReplaceSupport"] = true,
                    ["labelDetailsSupport"] = true,
                    ["resolveSupport"] = new JsonObject { ["properties"] = Strings("documentation", "detail", "additionalTextEdits") },
                },
                ["completionItemKind"] = ValueSet(1, 25),
                ["completionList"] = new JsonObject { ["itemDefaults"] = Strings("commitCharacters", "editRange", "insertTextFormat", "data") },
            },
            ["hover"] = new JsonObject { ["contentFormat"] = Strings("markdown", "plaintext") },
            ["signatureHelp"] = new JsonObject
            {
                ["contextSupport"] = true,
                ["signatureInformation"] = new JsonObject
                {
                    ["documentationFormat"] = Strings("markdown", "plaintext"),
                    ["parameterInformation"] = new JsonObject { ["labelOffsetSupport"] = true },
                    ["activeParameterSupport"] = true,
                },
            },
            ["declaration"] = new JsonObject { ["linkSupport"] = true },
            ["definition"] = new JsonObject { ["linkSupport"] = true },
            ["typeDefinition"] = new JsonObject { ["linkSupport"] = true },
            ["implementation"] = new JsonObject { ["linkSupport"] = true },
            ["references"] = new JsonObject(),
            ["documentHighlight"] = new JsonObject(),
            ["documentSymbol"] = new JsonObject
            {
                ["hierarchicalDocumentSymbolSupport"] = true,
                ["symbolKind"] = ValueSet(1, 26),
            },
            ["codeAction"] = new JsonObject
            {
                ["codeActionLiteralSupport"] = new JsonObject
                {
                    ["codeActionKind"] = new JsonObject
                    {
                        ["valueSet"] = Strings(string.Empty, "quickfix", "refactor", "refactor.extract", "refactor.inline", "refactor.rewrite", "source", "source.organizeImports", "source.fixAll"),
                    },
                },
                ["isPreferredSupport"] = true,
                ["disabledSupport"] = true,
                ["dataSupport"] = true,
                ["resolveSupport"] = new JsonObject { ["properties"] = Strings("edit") },
            },
            ["formatting"] = new JsonObject(),
            ["onTypeFormatting"] = new JsonObject(),
            ["rename"] = new JsonObject { ["prepareSupport"] = true },
            ["publishDiagnostics"] = new JsonObject
            {
                ["tagSupport"] = new JsonObject { ["valueSet"] = Numbers(LspValues.DiagnosticTag.Unnecessary, LspValues.DiagnosticTag.Deprecated) },
                ["versionSupport"] = true,
                ["codeDescriptionSupport"] = true,
            },
            ["foldingRange"] = new JsonObject
            {
                ["lineFoldingOnly"] = true,
                ["foldingRangeKind"] = new JsonObject { ["valueSet"] = Strings("comment", "imports", "region") },
            },
            ["semanticTokens"] = new JsonObject
            {
                ["requests"] = new JsonObject { ["full"] = true, ["range"] = false },
                ["tokenTypes"] = Strings([.. LspValues.SemanticTokenTypes]),
                ["tokenModifiers"] = Strings([.. LspValues.SemanticTokenModifiers]),
                ["formats"] = Strings("relative"),
                ["overlappingTokenSupport"] = false,
                ["multilineTokenSupport"] = false,
                ["augmentsSyntaxTokens"] = true,
            },
            ["inlayHint"] = new JsonObject(),
        },
        ["window"] = new JsonObject
        {
            ["workDoneProgress"] = true,
            ["showMessage"] = new JsonObject { ["messageActionItem"] = new JsonObject { ["additionalPropertiesSupport"] = false } },
            ["showDocument"] = new JsonObject { ["support"] = true },
        },
    };

    private static JsonArray Strings(params string[] values) => new([.. values.Select(v => (JsonNode?)JsonValue.Create(v))]);

    private static JsonArray Numbers(params int[] values) => new([.. values.Select(v => (JsonNode?)JsonValue.Create(v))]);

    private static JsonObject ValueSet(int first, int last) => new() { ["valueSet"] = Numbers([.. Enumerable.Range(first, last - first + 1)]) };
}
