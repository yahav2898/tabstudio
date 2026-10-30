using Microsoft.CodeAnalysis;

namespace TabStudio.TestRunner.Source;

/// <summary>
/// What source discovery needs from the syntax of one language: where classes and methods are declared, how
/// attributes are written, and what their arguments evaluate to when there is no semantic model to ask.
/// </summary>
internal abstract class SourceLanguage
{
    /// <summary>How written names compare: ordinally in C#, ignoring case in Visual Basic, as each compiler binds them.</summary>
    public abstract StringComparison NameComparison { get; }

    /// <summary>The language of a syntax tree: Visual Basic or C#.</summary>
    /// <param name="tree">The tree.</param>
    public static SourceLanguage Of(SyntaxTree tree) =>
        string.Equals(tree.Options.Language, LanguageNames.VisualBasic, StringComparison.Ordinal)
            ? VisualBasicSourceLanguage.Instance
            : CSharpSourceLanguage.Instance;

    /// <summary>The classes declared in a tree that can hold tests, nested ones included, in source order.</summary>
    /// <param name="root">The tree's root.</param>
    /// <param name="rootNamespace">
    /// The namespace the compiler puts every declaration in, which Visual Basic's <c>RootNamespace</c> sets; C#
    /// has none and ignores it.
    /// </param>
    public abstract IEnumerable<SourceTypeDeclaration> Types(SyntaxNode root, string rootNamespace);

    /// <summary>
    /// The method a node declares, such as the node a method symbol's syntax reference points to, or
    /// <see langword="null"/> when it is not an ordinary method.
    /// </summary>
    /// <param name="declaration">The node.</param>
    public abstract SourceMethodDeclaration? MethodOf(SyntaxNode declaration);

    /// <summary>The name an attribute is written with, without its namespace, such as <c>Fact</c>.</summary>
    /// <param name="attribute">An attribute node of this language.</param>
    public abstract string AttributeName(SyntaxNode attribute);

    /// <summary>
    /// The arguments of an attribute, in order: a property or field set by name (<c>Skip = "x"</c>,
    /// <c>Skip:="x"</c>) has its name, a constructor argument has none.
    /// </summary>
    /// <param name="attribute">An attribute node of this language.</param>
    public abstract IReadOnlyList<(string? Name, SyntaxNode Expression)> AttributeArguments(SyntaxNode attribute);

    /// <summary>
    /// Evaluates an argument as written: literals become their values, arrays become arrays, and everything else,
    /// such as enum members and type expressions, becomes text in the form the runner formats such values.
    /// </summary>
    /// <param name="expression">An expression node of this language.</param>
    public abstract object? Evaluate(SyntaxNode expression);

    /// <summary>Whether two written names, such as attribute or argument names, name the same thing in this language.</summary>
    /// <param name="name">A name.</param>
    /// <param name="other">Another name.</param>
    public bool NamesEqual(string name, string other) => string.Equals(name, other, NameComparison);

    /// <summary>Negates a literal's value, for arguments such as <c>-4</c>.</summary>
    /// <param name="value">The literal's value.</param>
    protected static object? Negate(object? value) => value switch
    {
        int number => -number,
        long number => -number,
        short number => -number,
        sbyte number => -number,
        float number => -number,
        double number => -number,
        decimal number => -number,
        uint number => -(long)number,
        _ => value,
    };
}
