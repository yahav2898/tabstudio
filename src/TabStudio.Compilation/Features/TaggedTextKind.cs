namespace TabStudio.Compilation.Features;

/// <summary>What a <see cref="TaggedRun"/> of a tooltip or signature is, so it can be colored like code.</summary>
public enum TaggedTextKind
{
    /// <summary>Plain prose, such as documentation.</summary>
    Text,

    /// <summary>Spaces between parts of a signature.</summary>
    Space,

    /// <summary>A line break. Its text is <c>"\n"</c>.</summary>
    LineBreak,

    /// <summary>A C# keyword, such as <c>public</c> or <c>int</c>.</summary>
    Keyword,

    /// <summary>Punctuation, such as parentheses and commas.</summary>
    Punctuation,

    /// <summary>An operator.</summary>
    Operator,

    /// <summary>A string or character literal.</summary>
    StringLiteral,

    /// <summary>A numeric literal.</summary>
    NumericLiteral,

    /// <summary>The name of a class.</summary>
    Class,

    /// <summary>The name of a record class.</summary>
    Record,

    /// <summary>The name of a struct.</summary>
    Struct,

    /// <summary>The name of a record struct.</summary>
    RecordStruct,

    /// <summary>The name of an interface.</summary>
    Interface,

    /// <summary>The name of an enum.</summary>
    Enum,

    /// <summary>The name of an enum member.</summary>
    EnumMember,

    /// <summary>The name of a delegate type.</summary>
    Delegate,

    /// <summary>The name of a type parameter.</summary>
    TypeParameter,

    /// <summary>The name of a namespace.</summary>
    Namespace,

    /// <summary>The name of a method.</summary>
    Method,

    /// <summary>The name of an extension method.</summary>
    ExtensionMethod,

    /// <summary>The name of a property.</summary>
    Property,

    /// <summary>The name of a field.</summary>
    Field,

    /// <summary>The name of an event.</summary>
    Event,

    /// <summary>The name of a constant.</summary>
    Constant,

    /// <summary>The name of a local variable.</summary>
    Local,

    /// <summary>The name of a parameter.</summary>
    Parameter,

    /// <summary>The name of a label.</summary>
    Label,

    /// <summary>The name of a query range variable.</summary>
    RangeVariable,

    /// <summary>The name of a <c>using</c> alias.</summary>
    Alias,

    /// <summary>The name of a type that could not be resolved.</summary>
    ErrorType,

    /// <summary>The name of an assembly.</summary>
    Assembly,

    /// <summary>The name of a module.</summary>
    Module,

    /// <summary>The placeholder Roslyn shows for an anonymous type, such as <c>'a</c>.</summary>
    AnonymousTypeIndicator,
}
