using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.Debugging.Instrumentation;

internal sealed partial class InstrumentingRewriter
{
    public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        var updated = (AssignmentExpressionSyntax)base.VisitAssignmentExpression(node)!;
        // Object/with initializers require member-assignment syntax at their root; the first-touch snapshot
        // captures their fields once the object exists.
        return node.Right is RefExpressionSyntax || node.Parent is InitializerExpressionSyntax ? updated : RecordWrite(node, updated, node.Left);
    }

    public override SyntaxNode? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        var updated = (PrefixUnaryExpressionSyntax)base.VisitPrefixUnaryExpression(node)!;
        return node.IsKind(SyntaxKind.PreIncrementExpression) || node.IsKind(SyntaxKind.PreDecrementExpression)
            ? RecordWrite(node, updated, node.Operand) : updated;
    }

    public override SyntaxNode? VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
    {
        var updated = (PostfixUnaryExpressionSyntax)base.VisitPostfixUnaryExpression(node)!;
        return node.IsKind(SyntaxKind.PostIncrementExpression) || node.IsKind(SyntaxKind.PostDecrementExpression)
            ? RecordWrite(node, updated, node.Operand, post: true) : updated;
    }

    public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        var updated = (VariableDeclaratorSyntax)base.VisitVariableDeclarator(node)!;
        if (Current is not { } context || updated.Initializer is null ||
            _model.GetDeclaredSymbol(node, _cancellationToken) is not ILocalSymbol { IsConst: false, RefKind: RefKind.None } local ||
            !CanRecord(local.Type) || InExpressionLambda(node))
        {
            return updated;
        }
        ExpressionSyntax value = updated.Initializer.Value;
        if (value is InitializerExpressionSyntax initializer && local.Type is IArrayTypeSymbol)
        {
            value = SyntaxFactory.ArrayCreationExpression((ArrayTypeSyntax)SyntaxFactory.ParseTypeName(local.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)), initializer);
        }
        var site = _info.AddWriteSite(context.MethodId, Locate(node.Span));
        return updated.WithInitializer(updated.Initializer.WithValue(WriteCall(context.Frame, site, local.Name, local.Type, value)));
    }

    private ExpressionSyntax RecordWrite(ExpressionSyntax original, ExpressionSyntax updated, ExpressionSyntax target, bool post = false)
    {
        if (Current is not { } context || InExpressionLambda(original) || SimplePath(target) is not { } path ||
            _model.GetSymbolInfo(target, _cancellationToken).Symbol is not (ILocalSymbol or IParameterSymbol or IFieldSymbol) ||
            _model.GetTypeInfo(original, _cancellationToken).Type is not { } type || !CanRecord(type))
        {
            return updated;
        }
        var site = _info.AddWriteSite(context.MethodId, Locate(original.Span));
        return WriteCall(context.Frame, site, path, type, updated, post ? target.WithoutTrivia() : null);
    }

    private static bool CanRecord(ITypeSymbol type) => LocalsCollector.IsBoxable(type) && !type.IsAnonymousType &&
        (type.CanBeReferencedByName || type is IArrayTypeSymbol) && type.TypeKind != TypeKind.Dynamic;

    private static bool InExpressionLambda(SyntaxNode node) => node.Ancestors()
        .FirstOrDefault(n => n is AnonymousFunctionExpressionSyntax or BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax)
        is LambdaExpressionSyntax { ExpressionBody: not null };

    // Paths must be side-effect free: reading an indexer or a property a second time would change the program.
    private string? SimplePath(ExpressionSyntax expression)
    {
        if (expression is ThisExpressionSyntax) return "this";
        if (expression is ParenthesizedExpressionSyntax parens) return SimplePath(parens.Expression);
        var symbol = _model.GetSymbolInfo(expression, _cancellationToken).Symbol;
        if (expression is IdentifierNameSyntax && symbol is ILocalSymbol or IParameterSymbol) return symbol.Name;
        if (symbol is not IFieldSymbol field) return null;
        if (field.IsStatic)
        {
            return _model.LookupSymbols(expression.SpanStart, name: field.Name)
                .Any(s => s is ILocalSymbol or IParameterSymbol || s is IFieldSymbol other && !SymbolEqualityComparer.Default.Equals(field, other))
                ? field.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + "." + field.Name
                : field.Name;
        }
        if (expression is IdentifierNameSyntax) return field.IsStatic ? field.Name : "this." + field.Name;
        return expression is MemberAccessExpressionSyntax member && SimplePath(member.Expression) is { } receiver
            ? receiver + "." + field.Name : null;
    }

    private static InvocationExpressionSyntax WriteCall(string frame, int site, string path, ITypeSymbol type, ExpressionSyntax expression, ExpressionSyntax? after = null)
    {
        var method = after is null ? "Write" : "PostWrite";
        var name = SyntaxFactory.ParseExpression("global::__TabStudioDebug." + method + "<" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">");
        var args = new List<ArgumentSyntax>
        {
            SyntaxFactory.Argument(SyntaxFactory.IdentifierName(frame)),
            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(site))),
            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(path))),
            SyntaxFactory.Argument(SyntaxFactory.ParenthesizedExpression(expression.WithoutTrivia())),
        };
        if (after is not null) args.Add(SyntaxFactory.Argument(after));
        // Preserve trivia, including line breaks inside the original expression: original PDB lines must not move.
        args[3] = SyntaxFactory.Argument(SyntaxFactory.ParenthesizedExpression(expression.WithoutLeadingTrivia().WithoutTrailingTrivia()));
        return SyntaxFactory.InvocationExpression(name, SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args)))
            .WithTriviaFrom(expression);
    }

    private string? ExceptionOperand(SyntaxNode node, bool division)
    {
        var nodes = node.DescendantNodesAndSelf(n => n == node || n is not (StatementSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax));
        var operands = division
            ? nodes.OfType<BinaryExpressionSyntax>().Where(b => b.IsKind(SyntaxKind.DivideExpression) || b.IsKind(SyntaxKind.ModuloExpression))
                .Select(b => SimplePath(b.Right)).ToArray()
            : nodes.OfType<MemberAccessExpressionSyntax>()
                .Where(m => _model.GetSymbolInfo(m.Expression, _cancellationToken).Symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol)
                .Select(m => SimplePath(m.Expression)).Distinct().ToArray();
        return operands.Length == 1 ? operands[0] : null;
    }
}
