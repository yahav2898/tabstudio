namespace TabStudio.LanguageServers.Features;

/// <summary>What a completion item stands for, as the protocol numbers it.</summary>
public enum LspCompletionItemKind
{
    /// <summary>The server did not say.</summary>
    Unknown = 0,

    /// <summary>Plain text.</summary>
    Text = 1,

    /// <summary>A method.</summary>
    Method = 2,

    /// <summary>A function.</summary>
    Function = 3,

    /// <summary>A constructor.</summary>
    Constructor = 4,

    /// <summary>A field.</summary>
    Field = 5,

    /// <summary>A variable.</summary>
    Variable = 6,

    /// <summary>A class.</summary>
    Class = 7,

    /// <summary>An interface.</summary>
    Interface = 8,

    /// <summary>A module.</summary>
    Module = 9,

    /// <summary>A property.</summary>
    Property = 10,

    /// <summary>A unit, such as <c>px</c>.</summary>
    Unit = 11,

    /// <summary>A value.</summary>
    Value = 12,

    /// <summary>An enumeration.</summary>
    Enum = 13,

    /// <summary>A keyword.</summary>
    Keyword = 14,

    /// <summary>A snippet.</summary>
    Snippet = 15,

    /// <summary>A color.</summary>
    Color = 16,

    /// <summary>A file.</summary>
    File = 17,

    /// <summary>A reference.</summary>
    Reference = 18,

    /// <summary>A folder.</summary>
    Folder = 19,

    /// <summary>A member of an enumeration.</summary>
    EnumMember = 20,

    /// <summary>A constant.</summary>
    Constant = 21,

    /// <summary>A struct.</summary>
    Struct = 22,

    /// <summary>An event.</summary>
    Event = 23,

    /// <summary>An operator.</summary>
    Operator = 24,

    /// <summary>A type parameter.</summary>
    TypeParameter = 25,
}

/// <summary>What a symbol is, as the protocol numbers it.</summary>
public enum LspSymbolKind
{
    /// <summary>The server did not say.</summary>
    Unknown = 0,

    /// <summary>A file.</summary>
    File = 1,

    /// <summary>A module.</summary>
    Module = 2,

    /// <summary>A namespace.</summary>
    Namespace = 3,

    /// <summary>A package.</summary>
    Package = 4,

    /// <summary>A class.</summary>
    Class = 5,

    /// <summary>A method.</summary>
    Method = 6,

    /// <summary>A property.</summary>
    Property = 7,

    /// <summary>A field.</summary>
    Field = 8,

    /// <summary>A constructor.</summary>
    Constructor = 9,

    /// <summary>An enumeration.</summary>
    Enum = 10,

    /// <summary>An interface.</summary>
    Interface = 11,

    /// <summary>A function.</summary>
    Function = 12,

    /// <summary>A variable.</summary>
    Variable = 13,

    /// <summary>A constant.</summary>
    Constant = 14,

    /// <summary>A string value.</summary>
    StringValue = 15,

    /// <summary>A number.</summary>
    Number = 16,

    /// <summary>A Boolean.</summary>
    Boolean = 17,

    /// <summary>An array.</summary>
    Array = 18,

    /// <summary>An object value.</summary>
    ObjectValue = 19,

    /// <summary>A key.</summary>
    Key = 20,

    /// <summary>Null.</summary>
    Null = 21,

    /// <summary>A member of an enumeration.</summary>
    EnumMember = 22,

    /// <summary>A struct.</summary>
    Struct = 23,

    /// <summary>An event.</summary>
    Event = 24,

    /// <summary>An operator.</summary>
    Operator = 25,

    /// <summary>A type parameter.</summary>
    TypeParameter = 26,
}

/// <summary>How serious a diagnostic is.</summary>
public enum LspDiagnosticSeverity
{
    /// <summary>An error; also what a diagnostic without a severity is.</summary>
    Error,

    /// <summary>A warning.</summary>
    Warning,

    /// <summary>Information.</summary>
    Information,

    /// <summary>A hint, usually shown faintly.</summary>
    Hint,
}

/// <summary>How an occurrence of a symbol uses it.</summary>
public enum LspHighlightKind
{
    /// <summary>A textual occurrence.</summary>
    Text,

    /// <summary>The symbol is read.</summary>
    Read,

    /// <summary>The symbol is written, such as assigned or declared.</summary>
    Write,
}

/// <summary>What an inlay hint shows.</summary>
public enum LspInlayHintKind
{
    /// <summary>Something else.</summary>
    Other,

    /// <summary>An inferred type, such as <c>: int</c>.</summary>
    Type,

    /// <summary>A parameter name, such as <c>count:</c>.</summary>
    Parameter,
}

/// <summary>What a workspace edit does to a file.</summary>
public enum LspFileChangeKind
{
    /// <summary>Changes the file's text.</summary>
    Edit,

    /// <summary>Creates the file.</summary>
    Create,

    /// <summary>Renames or moves the file.</summary>
    Rename,

    /// <summary>Deletes the file.</summary>
    Delete,
}

/// <summary>What made the editor ask for completion.</summary>
public enum LspCompletionTriggerKind
{
    /// <summary>The user asked for it.</summary>
    Invoked,

    /// <summary>The user typed one of the server's trigger characters.</summary>
    TriggerCharacter,
}
