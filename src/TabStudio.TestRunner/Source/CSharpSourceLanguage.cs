using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.TestRunner.Naming;

namespace TabStudio.TestRunner.Source;

/// <summary>Source discovery's view of C# syntax.</summary>
internal sealed class CSharpSourceLanguage : SourceLanguage
{
    private CSharpSourceLanguage()
    {
    }

    /// <summary>The one instance.</summary>
    public static CSharpSourceLanguage Instance { get; } = new();

    /// <inheritdoc />
    public override StringComparison NameComparison => StringComparison.Ordinal;

    /// <inheritdoc />
    public override IEnumerable<SourceTypeDeclaration> Types(SyntaxNode root, string rootNamespace) =>
        root.DescendantNodes().OfType<ClassDeclarationSyntax>().Select(Describe);

    /// <inheritdoc />
    public override SourceMethodDeclaration? MethodOf(SyntaxNode declaration) =>
        declaration is MethodDeclarationSyntax method ? Describe(method) : null;

    /// <inheritdoc />
    public override string AttributeName(SyntaxNode attribute) => WrittenName(((AttributeSyntax)attribute).Name);

    /// <inheritdoc />
    public override IReadOnlyList<(string? Name, SyntaxNode Expression)> AttributeArguments(SyntaxNode attribute) =>
        ((AttributeSyntax)attribute).ArgumentList is { } arguments
            ? [.. arguments.Arguments.Select(a => (Name: a.NameEquals?.Name.Identifier.ValueText, Expression: (SyntaxNode)a.Expression))]
            : [];

    /// <inheritdoc />
    public override object? Evaluate(SyntaxNode expression) => EvaluateExpression((ExpressionSyntax)expression);

    /// <summary>The name <see cref="Type.FullName"/> would give a class, computed from its declaration's syntax.</summary>
    private static string MetadataFullName(BaseTypeDeclarationSyntax declaration)
    {
        var name = declaration.Identifier.ValueText + Arity(declaration);
        for (var parent = declaration.Parent; parent is not null; parent = parent.Parent)
        {
            switch (parent)
            {
                case BaseTypeDeclarationSyntax outer:
                    name = outer.Identifier.ValueText + Arity(outer) + "+" + name;
                    break;
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    name = namespaceDeclaration.Name.ToString() + "." + name;
                    break;
            }
        }

        return name;
    }

    private static SourceTypeDeclaration Describe(ClassDeclarationSyntax declaration) => new(
        declaration,
        declaration.Identifier,
        IsStatic: declaration.Modifiers.Any(SyntaxKind.StaticKeyword),
        IsAbstract: declaration.Modifiers.Any(SyntaxKind.AbstractKeyword),
        IsGeneric: declaration.TypeParameterList is not null,
        FullName: MetadataFullName(declaration),
        Attributes: Attributes(declaration.AttributeLists),
        Methods: [.. declaration.Members.OfType<MethodDeclarationSyntax>().Select(Describe)]);

    private static SourceMethodDeclaration Describe(MethodDeclarationSyntax method) => new(
        method,
        method.Identifier,
        IsStatic: method.Modifiers.Any(SyntaxKind.StaticKeyword),
        ParameterNames: [.. method.ParameterList.Parameters.Select(p => p.Identifier.ValueText)],
        Attributes: Attributes(method.AttributeLists));

    /// <summary>The attributes of a declaration, without those written for another target, such as <c>[return: ...]</c>.</summary>
    private static List<SyntaxNode> Attributes(SyntaxList<AttributeListSyntax> lists) =>
        [.. lists
            .Where(l => l.Target is null || l.Target.Identifier.IsKind(SyntaxKind.TypeKeyword) || l.Target.Identifier.IsKind(SyntaxKind.MethodKeyword))
            .SelectMany(l => l.Attributes)];

    private static string Arity(BaseTypeDeclarationSyntax declaration) =>
        declaration is TypeDeclarationSyntax { TypeParameterList.Parameters.Count: > 0 and var count }
            ? "`" + count.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

    private static string WrittenName(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => name.ToString(),
    };

    /// <summary>Evaluates an argument as written, when there is no semantic model to ask.</summary>
    private static object? EvaluateExpression(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal => literal.Token.Value,
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression, Operand: LiteralExpressionSyntax literal } => Negate(literal.Token.Value),
        TypeOfExpressionSyntax typeOf => new FormattedValue("typeof(" + RightmostName(typeOf.Type) + ")"),
        MemberAccessExpressionSyntax member => new FormattedValue(member.Name.Identifier.ValueText),
        ImplicitArrayCreationExpressionSyntax array => array.Initializer.Expressions.Select(EvaluateExpression).ToArray(),
        ArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions.Select(EvaluateExpression).ToArray(),
        CollectionExpressionSyntax collection => collection.Elements.OfType<ExpressionElementSyntax>().Select(e => EvaluateExpression(e.Expression)).ToArray(),
        CastExpressionSyntax cast => EvaluateExpression(cast.Expression),
        ParenthesizedExpressionSyntax parenthesized => EvaluateExpression(parenthesized.Expression),
        _ => new FormattedValue(expression.ToString()),
    };

    private static string RightmostName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        PredefinedTypeSyntax predefined => PredefinedName(predefined.Keyword.Kind()),
        _ => type.ToString(),
    };

    /// <summary>The runtime name of a C# keyword type, as <c>Type.Name</c> prints it.</summary>
    private static string PredefinedName(SyntaxKind keyword) => keyword switch
    {
        SyntaxKind.IntKeyword => "Int32",
        SyntaxKind.LongKeyword => "Int64",
        SyntaxKind.StringKeyword => "String",
        SyntaxKind.BoolKeyword => "Boolean",
        SyntaxKind.DoubleKeyword => "Double",
        SyntaxKind.FloatKeyword => "Single",
        SyntaxKind.DecimalKeyword => "Decimal",
        SyntaxKind.CharKeyword => "Char",
        SyntaxKind.ObjectKeyword => "Object",
        SyntaxKind.ByteKeyword => "Byte",
        SyntaxKind.ShortKeyword => "Int16",
        _ => keyword.ToString(),
    };
}
