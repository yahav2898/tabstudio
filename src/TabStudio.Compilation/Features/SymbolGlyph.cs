namespace TabStudio.Compilation.Features;

/// <summary>
/// What a completion item, document symbol or Quick Info tip stands for, so the editor can show the matching icon.
/// </summary>
public enum SymbolGlyph
{
    /// <summary>Anything without a more specific icon.</summary>
    Other,

    /// <summary>A class or record class.</summary>
    Class,

    /// <summary>A struct or record struct.</summary>
    Struct,

    /// <summary>An interface.</summary>
    Interface,

    /// <summary>An enum type.</summary>
    Enum,

    /// <summary>A member of an enum.</summary>
    EnumMember,

    /// <summary>A delegate type.</summary>
    Delegate,

    /// <summary>A method, constructor, operator or local function.</summary>
    Method,

    /// <summary>An extension method.</summary>
    ExtensionMethod,

    /// <summary>A property or indexer.</summary>
    Property,

    /// <summary>A field.</summary>
    Field,

    /// <summary>An event.</summary>
    Event,

    /// <summary>A local variable or range variable.</summary>
    Local,

    /// <summary>A parameter.</summary>
    Parameter,

    /// <summary>A constant.</summary>
    Constant,

    /// <summary>A namespace.</summary>
    Namespace,

    /// <summary>A C# keyword.</summary>
    Keyword,

    /// <summary>A snippet that expands into a code template.</summary>
    Snippet,

    /// <summary>A type parameter.</summary>
    TypeParameter,

    /// <summary>A label.</summary>
    Label,
}
