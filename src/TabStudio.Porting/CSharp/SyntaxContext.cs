using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.Porting.CSharp;

/// <summary>Where a name stands in C# syntax, without a compilation: as a type, or as the target of a static member.</summary>
internal static class SyntaxContext
{
    /// <summary>Whether a simple name is used as a type, or as the type of a static member access such as <c>Brushes.Red</c>.</summary>
    /// <param name="name">The name.</param>
    public static bool IsTypeOrStatic(SimpleNameSyntax name)
    {
        SyntaxNode node = name;
        var parent = node.Parent;
        while (parent is NullableTypeSyntax or ArrayTypeSyntax or RefTypeSyntax or PointerTypeSyntax or ScopedTypeSyntax)
        {
            node = parent;
            parent = node.Parent;
        }

        return parent switch
        {
            QualifiedNameSyntax qualified => qualified.Left == node && IsTypeName(qualified),
            AliasQualifiedNameSyntax => false,
            MemberAccessExpressionSyntax access => access.Expression == node,
            VariableDeclarationSyntax declaration => declaration.Type == node,
            ParameterSyntax parameter => parameter.Type == node,
            PropertyDeclarationSyntax property => property.Type == node,
            IndexerDeclarationSyntax indexer => indexer.Type == node,
            EventDeclarationSyntax eventDeclaration => eventDeclaration.Type == node,
            MethodDeclarationSyntax method => method.ReturnType == node,
            LocalFunctionStatementSyntax function => function.ReturnType == node,
            DelegateDeclarationSyntax @delegate => @delegate.ReturnType == node,
            OperatorDeclarationSyntax @operator => @operator.ReturnType == node,
            ConversionOperatorDeclarationSyntax conversion => conversion.Type == node,
            SimpleBaseTypeSyntax or TypeArgumentListSyntax or TypeOfExpressionSyntax or DefaultExpressionSyntax or TypeConstraintSyntax
                or TupleElementSyntax or TypePatternSyntax or SizeOfExpressionSyntax => true,
            CastExpressionSyntax cast => cast.Type == node,
            ObjectCreationExpressionSyntax creation => creation.Type == node,
            BinaryExpressionSyntax binary => binary.Right == node && binary.Kind() is SyntaxKind.AsExpression or SyntaxKind.IsExpression,
            DeclarationPatternSyntax pattern => pattern.Type == node,
            RecursivePatternSyntax recursive => recursive.Type == node,
            ForEachStatementSyntax forEach => forEach.Type == node,
            DeclarationExpressionSyntax expression => expression.Type == node,
            CatchDeclarationSyntax @catch => @catch.Type == node,
            ArrayCreationExpressionSyntax => true,
            _ => false,
        };
    }

    /// <summary>Whether a qualified name names a type or a namespace member, not a using directive's namespace.</summary>
    private static bool IsTypeName(QualifiedNameSyntax name) =>
        !name.AncestorsAndSelf().Any(n => n is UsingDirectiveSyntax or BaseNamespaceDeclarationSyntax);

    /// <summary>The innermost function a node is in: a method, a local function, a lambda, an accessor or a constructor.</summary>
    /// <param name="node">The node.</param>
    public static SyntaxNode? Function(SyntaxNode node) =>
        node.Ancestors().FirstOrDefault(a => a is MethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax
            or AccessorDeclarationSyntax or ConstructorDeclarationSyntax or DestructorDeclarationSyntax or OperatorDeclarationSyntax
            or ConversionOperatorDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax or FieldDeclarationSyntax);

    /// <summary>
    /// The statement that holds a node directly in a block (so it can be commented out alone), or the member it is in
    /// when it is not inside a block.
    /// </summary>
    /// <param name="node">The node.</param>
    public static SyntaxNode? Unit(SyntaxNode node)
    {
        foreach (var ancestor in node.AncestorsAndSelf())
        {
            switch (ancestor)
            {
                case StatementSyntax statement when statement.Parent is BlockSyntax or SwitchSectionSyntax or GlobalStatementSyntax:
                    return statement;
                case MemberDeclarationSyntax member when member is not (BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax):
                    return member;
                case ArrowExpressionClauseSyntax arrow when arrow.Parent is MemberDeclarationSyntax owner:
                    return owner;
            }
        }

        return null;
    }

    /// <summary>The names a statement or member declares: locals, fields, properties, methods, events.</summary>
    /// <param name="unit">The statement or member.</param>
    public static IEnumerable<string> DeclaredNames(SyntaxNode unit) => unit switch
    {
        LocalDeclarationStatementSyntax local => local.Declaration.Variables.Select(v => v.Identifier.ValueText),
        FieldDeclarationSyntax field => field.Declaration.Variables.Select(v => v.Identifier.ValueText),
        EventFieldDeclarationSyntax eventField => eventField.Declaration.Variables.Select(v => v.Identifier.ValueText),
        PropertyDeclarationSyntax property => [property.Identifier.ValueText],
        MethodDeclarationSyntax method => [method.Identifier.ValueText],
        EventDeclarationSyntax eventDeclaration => [eventDeclaration.Identifier.ValueText],
        LocalFunctionStatementSyntax function => [function.Identifier.ValueText],
        ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { Left: DeclarationExpressionSyntax declaration } } =>
            declaration.Designation.DescendantNodesAndSelf().OfType<SingleVariableDesignationSyntax>().Select(d => d.Identifier.ValueText),
        _ => [],
    };

    /// <summary>The <c>object sender</c> parameter of an event handler as <c>object? sender</c>, as Avalonia's events declare it.</summary>
    /// <param name="parameter">The parameter.</param>
    public static ParameterSyntax NullableSender(ParameterSyntax parameter) =>
        parameter.Type is PredefinedTypeSyntax { Keyword.ValueText: "object" } type
            ? parameter.WithType(SyntaxFactory.NullableType(type.WithoutTrailingTrivia()).WithTrailingTrivia(type.GetTrailingTrivia()))
            : parameter;
}
