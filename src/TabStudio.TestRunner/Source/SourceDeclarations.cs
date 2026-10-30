using Microsoft.CodeAnalysis;

namespace TabStudio.TestRunner.Source;

/// <summary>A class declared in source that may hold tests, as its language's syntax describes it.</summary>
/// <param name="Node">
/// The node the semantic model declares the class with: a C# class declaration, or a Visual Basic <c>Class</c> or
/// <c>Module</c> statement, which is also what the class symbol's syntax references point to.
/// </param>
/// <param name="Identifier">The class's name, where the editor puts its marker.</param>
/// <param name="IsStatic">Whether it is a C# <c>static</c> class or a Visual Basic <c>Module</c>.</param>
/// <param name="IsAbstract">Whether it is <c>abstract</c> (<c>MustInherit</c>).</param>
/// <param name="IsGeneric">Whether it has type parameters.</param>
/// <param name="FullName">Its full name from syntax alone, as reflection writes it, such as <c>Ns.Outer+Inner</c>.</param>
/// <param name="Attributes">The attributes written on it.</param>
/// <param name="Methods">The methods it declares itself.</param>
internal sealed record SourceTypeDeclaration(
    SyntaxNode Node,
    SyntaxToken Identifier,
    bool IsStatic,
    bool IsAbstract,
    bool IsGeneric,
    string FullName,
    IReadOnlyList<SyntaxNode> Attributes,
    IReadOnlyList<SourceMethodDeclaration> Methods);

/// <summary>A method declared in source that may be a test.</summary>
/// <param name="Node">
/// The node the semantic model declares the method with: a C# method declaration, or a Visual Basic <c>Sub</c> or
/// <c>Function</c> statement.
/// </param>
/// <param name="Identifier">The method's name.</param>
/// <param name="IsStatic">Whether it is <c>static</c> (<c>Shared</c>, or declared in a Visual Basic <c>Module</c>).</param>
/// <param name="ParameterNames">Its parameter names, in order.</param>
/// <param name="Attributes">The attributes written on it.</param>
internal sealed record SourceMethodDeclaration(
    SyntaxNode Node,
    SyntaxToken Identifier,
    bool IsStatic,
    IReadOnlyList<string> ParameterNames,
    IReadOnlyList<SyntaxNode> Attributes);
