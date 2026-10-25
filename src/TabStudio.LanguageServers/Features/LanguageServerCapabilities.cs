using System.Text.Json;

namespace TabStudio.LanguageServers.Features;

/// <summary>
/// What a running language server can do, from its answer to <c>initialize</c> and the capabilities it registered
/// later. The editor offers a feature only when the server has it.
/// </summary>
public sealed record LanguageServerCapabilities
{
    /// <summary>A server that can do nothing yet, such as one still starting.</summary>
    public static LanguageServerCapabilities None { get; } = new();

    /// <summary>Completion.</summary>
    public bool Completion { get; init; }

    /// <summary>Whether completion items can be resolved for their documentation and extra edits.</summary>
    public bool CompletionResolve { get; init; }

    /// <summary>The characters that open the completion list when typed, such as <c>.</c>.</summary>
    public IReadOnlyList<char> CompletionTriggerCharacters { get; init; } = [];

    /// <summary>The characters that commit any completion item, unless an item names its own.</summary>
    public IReadOnlyList<char> CompletionCommitCharacters { get; init; } = [];

    /// <summary>Hover, shown as Quick Info.</summary>
    public bool Hover { get; init; }

    /// <summary>Signature help, shown as Parameter Info.</summary>
    public bool SignatureHelp { get; init; }

    /// <summary>The characters that open signature help when typed, such as <c>(</c>.</summary>
    public IReadOnlyList<char> SignatureHelpTriggerCharacters { get; init; } = [];

    /// <summary>The characters that update open signature help when typed, such as <c>,</c>.</summary>
    public IReadOnlyList<char> SignatureHelpRetriggerCharacters { get; init; } = [];

    /// <summary>Go to Definition.</summary>
    public bool Definition { get; init; }

    /// <summary>Go to Declaration.</summary>
    public bool Declaration { get; init; }

    /// <summary>Go to Type Definition.</summary>
    public bool TypeDefinition { get; init; }

    /// <summary>Go to Implementation.</summary>
    public bool Implementation { get; init; }

    /// <summary>Find All References.</summary>
    public bool References { get; init; }

    /// <summary>Highlighting the occurrences of the symbol at the caret.</summary>
    public bool DocumentHighlight { get; init; }

    /// <summary>The symbols of a document, for the breadcrumb and Go to Member.</summary>
    public bool DocumentSymbol { get; init; }

    /// <summary>Searching symbols in the whole workspace.</summary>
    public bool WorkspaceSymbol { get; init; }

    /// <summary>Quick fixes and refactorings.</summary>
    public bool CodeAction { get; init; }

    /// <summary>Whether code actions can be resolved for their edits.</summary>
    public bool CodeActionResolve { get; init; }

    /// <summary>Rename.</summary>
    public bool Rename { get; init; }

    /// <summary>Whether the server says first what a rename at a position would rename.</summary>
    public bool PrepareRename { get; init; }

    /// <summary>Format Document.</summary>
    public bool Formatting { get; init; }

    /// <summary>Formatting while typing.</summary>
    public bool OnTypeFormatting { get; init; }

    /// <summary>The characters that format while typing, such as <c>}</c> and <c>;</c>.</summary>
    public IReadOnlyList<char> OnTypeFormattingTriggerCharacters { get; init; } = [];

    /// <summary>Semantic tokens of whole documents, for coloring.</summary>
    public bool SemanticTokens { get; init; }

    /// <summary>The token types of the server's semantic tokens, by index.</summary>
    public IReadOnlyList<string> SemanticTokenTypes { get; init; } = [];

    /// <summary>The token modifiers of the server's semantic tokens, by bit.</summary>
    public IReadOnlyList<string> SemanticTokenModifiers { get; init; } = [];

    /// <summary>Inlay hints.</summary>
    public bool InlayHint { get; init; }

    /// <summary>Folding ranges.</summary>
    public bool FoldingRange { get; init; }

    /// <summary>Running commands with <c>workspace/executeCommand</c>.</summary>
    public bool ExecuteCommand { get; init; }

    /// <summary>How documents are synchronized: 0 not at all, 1 full text, 2 incremental changes.</summary>
    internal int TextDocumentSync { get; init; } = 2;

    /// <summary>Whether open and close notifications are sent.</summary>
    internal bool OpenClose { get; init; } = true;

    /// <summary>Whether save notifications are sent.</summary>
    internal bool Save { get; init; }

    /// <summary>Whether save notifications include the text.</summary>
    internal bool SaveIncludesText { get; init; }

    /// <summary>Reads the capabilities a server answered <c>initialize</c> with.</summary>
    /// <param name="capabilities">The <c>capabilities</c> object.</param>
    internal static LanguageServerCapabilities FromJson(JsonElement capabilities)
    {
        if (capabilities.ValueKind != JsonValueKind.Object)
        {
            return None;
        }

        var completion = Get(capabilities, "completionProvider");
        var signatureHelp = Get(capabilities, "signatureHelpProvider");
        var codeAction = Get(capabilities, "codeActionProvider");
        var rename = Get(capabilities, "renameProvider");
        var onType = Get(capabilities, "documentOnTypeFormattingProvider");
        var semanticTokens = Get(capabilities, "semanticTokensProvider");
        var legend = Get(semanticTokens, "legend");
        var (sync, openClose, save, saveText) = ReadSync(Get(capabilities, "textDocumentSync"));

        return new LanguageServerCapabilities
        {
            Completion = IsEnabled(completion),
            CompletionResolve = GetBool(completion, "resolveProvider"),
            CompletionTriggerCharacters = GetCharacters(completion, "triggerCharacters"),
            CompletionCommitCharacters = GetCharacters(completion, "allCommitCharacters"),
            Hover = IsEnabled(Get(capabilities, "hoverProvider")),
            SignatureHelp = IsEnabled(signatureHelp),
            SignatureHelpTriggerCharacters = GetCharacters(signatureHelp, "triggerCharacters"),
            SignatureHelpRetriggerCharacters = GetCharacters(signatureHelp, "retriggerCharacters"),
            Definition = IsEnabled(Get(capabilities, "definitionProvider")),
            Declaration = IsEnabled(Get(capabilities, "declarationProvider")),
            TypeDefinition = IsEnabled(Get(capabilities, "typeDefinitionProvider")),
            Implementation = IsEnabled(Get(capabilities, "implementationProvider")),
            References = IsEnabled(Get(capabilities, "referencesProvider")),
            DocumentHighlight = IsEnabled(Get(capabilities, "documentHighlightProvider")),
            DocumentSymbol = IsEnabled(Get(capabilities, "documentSymbolProvider")),
            WorkspaceSymbol = IsEnabled(Get(capabilities, "workspaceSymbolProvider")),
            CodeAction = IsEnabled(codeAction),
            CodeActionResolve = GetBool(codeAction, "resolveProvider"),
            Rename = IsEnabled(rename),
            PrepareRename = GetBool(rename, "prepareProvider"),
            Formatting = IsEnabled(Get(capabilities, "documentFormattingProvider")),
            OnTypeFormatting = IsEnabled(onType),
            OnTypeFormattingTriggerCharacters = ReadOnTypeCharacters(onType),
            SemanticTokens = IsEnabled(semanticTokens) && IsEnabled(Get(semanticTokens, "full")),
            SemanticTokenTypes = GetStrings(legend, "tokenTypes"),
            SemanticTokenModifiers = GetStrings(legend, "tokenModifiers"),
            InlayHint = IsEnabled(Get(capabilities, "inlayHintProvider")),
            FoldingRange = IsEnabled(Get(capabilities, "foldingRangeProvider")),
            ExecuteCommand = IsEnabled(Get(capabilities, "executeCommandProvider")),
            TextDocumentSync = sync,
            OpenClose = openClose,
            Save = save,
            SaveIncludesText = saveText,
        };
    }

    /// <summary>The capabilities with a feature the server registered with <c>client/registerCapability</c>.</summary>
    /// <param name="method">The registered method, such as <c>textDocument/hover</c>.</param>
    /// <param name="options">Its registration options.</param>
    internal LanguageServerCapabilities WithRegistration(string method, JsonElement? options)
    {
        var settings = options ?? default;
        return method switch
        {
            "textDocument/completion" => this with
            {
                Completion = true,
                CompletionResolve = CompletionResolve || GetBool(settings, "resolveProvider"),
                CompletionTriggerCharacters = Union(CompletionTriggerCharacters, GetCharacters(settings, "triggerCharacters")),
                CompletionCommitCharacters = Union(CompletionCommitCharacters, GetCharacters(settings, "allCommitCharacters")),
            },
            "textDocument/hover" => this with { Hover = true },
            "textDocument/signatureHelp" => this with
            {
                SignatureHelp = true,
                SignatureHelpTriggerCharacters = Union(SignatureHelpTriggerCharacters, GetCharacters(settings, "triggerCharacters")),
                SignatureHelpRetriggerCharacters = Union(SignatureHelpRetriggerCharacters, GetCharacters(settings, "retriggerCharacters")),
            },
            "textDocument/definition" => this with { Definition = true },
            "textDocument/declaration" => this with { Declaration = true },
            "textDocument/typeDefinition" => this with { TypeDefinition = true },
            "textDocument/implementation" => this with { Implementation = true },
            "textDocument/references" => this with { References = true },
            "textDocument/documentHighlight" => this with { DocumentHighlight = true },
            "textDocument/documentSymbol" => this with { DocumentSymbol = true },
            "workspace/symbol" => this with { WorkspaceSymbol = true },
            "textDocument/codeAction" => this with { CodeAction = true, CodeActionResolve = CodeActionResolve || GetBool(settings, "resolveProvider") },
            "textDocument/rename" => this with { Rename = true, PrepareRename = PrepareRename || GetBool(settings, "prepareProvider") },
            "textDocument/formatting" => this with { Formatting = true },
            "textDocument/onTypeFormatting" => this with
            {
                OnTypeFormatting = true,
                OnTypeFormattingTriggerCharacters = Union(OnTypeFormattingTriggerCharacters, ReadOnTypeCharacters(settings)),
            },
            "textDocument/semanticTokens" => this with
            {
                SemanticTokens = true,
                SemanticTokenTypes = GetStrings(Get(settings, "legend"), "tokenTypes"),
                SemanticTokenModifiers = GetStrings(Get(settings, "legend"), "tokenModifiers"),
            },
            "textDocument/inlayHint" => this with { InlayHint = true },
            "textDocument/foldingRange" => this with { FoldingRange = true },
            "workspace/executeCommand" => this with { ExecuteCommand = true },
            _ => this,
        };
    }

    /// <summary>The capabilities without a feature the server unregistered.</summary>
    /// <param name="method">The method, such as <c>textDocument/hover</c>.</param>
    internal LanguageServerCapabilities WithoutRegistration(string method) => method switch
    {
        "textDocument/completion" => this with { Completion = false },
        "textDocument/hover" => this with { Hover = false },
        "textDocument/signatureHelp" => this with { SignatureHelp = false },
        "textDocument/definition" => this with { Definition = false },
        "textDocument/declaration" => this with { Declaration = false },
        "textDocument/typeDefinition" => this with { TypeDefinition = false },
        "textDocument/implementation" => this with { Implementation = false },
        "textDocument/references" => this with { References = false },
        "textDocument/documentHighlight" => this with { DocumentHighlight = false },
        "textDocument/documentSymbol" => this with { DocumentSymbol = false },
        "workspace/symbol" => this with { WorkspaceSymbol = false },
        "textDocument/codeAction" => this with { CodeAction = false },
        "textDocument/rename" => this with { Rename = false },
        "textDocument/formatting" => this with { Formatting = false },
        "textDocument/onTypeFormatting" => this with { OnTypeFormatting = false },
        "textDocument/semanticTokens" => this with { SemanticTokens = false },
        "textDocument/inlayHint" => this with { InlayHint = false },
        "textDocument/foldingRange" => this with { FoldingRange = false },
        _ => this,
    };

    private static JsonElement Get(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    /// <summary>A provider is on when it is <c>true</c> or an options object.</summary>
    private static bool IsEnabled(JsonElement provider) => provider.ValueKind is JsonValueKind.True or JsonValueKind.Object;

    private static bool GetBool(JsonElement element, string name) => Get(element, name).ValueKind == JsonValueKind.True;

    private static List<char> GetCharacters(JsonElement element, string name)
    {
        var characters = new List<char>();
        foreach (var text in GetStrings(element, name))
        {
            if (text.Length == 1 && !characters.Contains(text[0]))
            {
                characters.Add(text[0]);
            }
        }

        return characters;
    }

    private static List<string> GetStrings(JsonElement element, string name)
    {
        var strings = new List<string>();
        var array = Get(element, name);
        if (array.ValueKind != JsonValueKind.Array)
        {
            return strings;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } text)
            {
                strings.Add(text);
            }
        }

        return strings;
    }

    private static List<char> ReadOnTypeCharacters(JsonElement options)
    {
        var characters = GetCharacters(options, "moreTriggerCharacter");
        if (Get(options, "firstTriggerCharacter") is { ValueKind: JsonValueKind.String } first && first.GetString() is { Length: 1 } text && !characters.Contains(text[0]))
        {
            characters.Insert(0, text[0]);
        }

        return characters;
    }

    private static (int Sync, bool OpenClose, bool Save, bool SaveText) ReadSync(JsonElement sync)
    {
        if (sync.ValueKind == JsonValueKind.Number && sync.TryGetInt32(out var kind))
        {
            return (kind, kind != 0, true, false);
        }

        if (sync.ValueKind != JsonValueKind.Object)
        {
            return (0, false, false, false);
        }

        var change = Get(sync, "change") is { ValueKind: JsonValueKind.Number } number && number.TryGetInt32(out var value) ? value : 0;
        var save = Get(sync, "save");
        return (change, GetBool(sync, "openClose"), IsEnabled(save), GetBool(save, "includeText"));
    }

    private static List<char> Union(IReadOnlyList<char> first, IReadOnlyList<char> second)
    {
        var union = new List<char>(first);
        foreach (var character in second)
        {
            if (!union.Contains(character))
            {
                union.Add(character);
            }
        }

        return union;
    }
}
