namespace TabStudio.LanguageServers.Protocol;

/// <summary>The numbers the Language Server Protocol uses for its enumerations, by enumeration.</summary>
internal static class LspValues
{
    /// <summary><c>TextDocumentSyncKind</c>.</summary>
    public static class TextDocumentSyncKind
    {
        public const int None = 0;
        public const int Full = 1;
        public const int Incremental = 2;
    }

    /// <summary><c>CompletionTriggerKind</c>.</summary>
    public static class CompletionTriggerKind
    {
        public const int Invoked = 1;
        public const int TriggerCharacter = 2;
        public const int TriggerForIncompleteCompletions = 3;
    }

    /// <summary><c>SignatureHelpTriggerKind</c>.</summary>
    public static class SignatureHelpTriggerKind
    {
        public const int Invoked = 1;
        public const int TriggerCharacter = 2;
        public const int ContentChange = 3;
    }

    /// <summary><c>InsertTextFormat</c>.</summary>
    public static class InsertTextFormat
    {
        public const int PlainText = 1;
        public const int Snippet = 2;
    }

    /// <summary><c>DiagnosticSeverity</c>.</summary>
    public static class DiagnosticSeverity
    {
        public const int Error = 1;
        public const int Warning = 2;
        public const int Information = 3;
        public const int Hint = 4;
    }

    /// <summary><c>DiagnosticTag</c>.</summary>
    public static class DiagnosticTag
    {
        public const int Unnecessary = 1;
        public const int Deprecated = 2;
    }

    /// <summary><c>CompletionItemTag</c> and <c>SymbolTag</c>, which share their only value.</summary>
    public static class ItemTag
    {
        public const int Deprecated = 1;
    }

    /// <summary><c>DocumentHighlightKind</c>.</summary>
    public static class DocumentHighlightKind
    {
        public const int Text = 1;
        public const int Read = 2;
        public const int Write = 3;
    }

    /// <summary><c>MessageType</c>.</summary>
    public static class MessageType
    {
        public const int Error = 1;
        public const int Warning = 2;
        public const int Info = 3;
        public const int Log = 4;
        public const int Debug = 5;
    }

    /// <summary><c>InlayHintKind</c>.</summary>
    public static class InlayHintKind
    {
        public const int Type = 1;
        public const int Parameter = 2;
    }

    /// <summary><c>CodeActionTriggerKind</c>.</summary>
    public static class CodeActionTriggerKind
    {
        public const int Invoked = 1;
        public const int Automatic = 2;
    }

    /// <summary>The semantic token types the protocol defines, which TabStudio announces in this order.</summary>
    public static readonly IReadOnlyList<string> SemanticTokenTypes =
    [
        "namespace", "type", "class", "enum", "interface", "struct", "typeParameter", "parameter", "variable", "property",
        "enumMember", "event", "function", "method", "macro", "keyword", "modifier", "comment", "string", "number",
        "regexp", "operator", "decorator", "label",
    ];

    /// <summary>The semantic token modifiers the protocol defines, which TabStudio announces in this order.</summary>
    public static readonly IReadOnlyList<string> SemanticTokenModifiers =
    [
        "declaration", "definition", "readonly", "static", "deprecated", "abstract", "async", "modification",
        "documentation", "defaultLibrary",
    ];
}
