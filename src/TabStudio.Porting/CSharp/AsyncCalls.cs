using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.Porting.CSharp;

/// <summary>
/// Avalonia's dialogs and message boxes do not block, so their calls are awaited, and the functions that make them
/// become async. A function that cannot be async (it returns a value, or is a constructor) keeps the call without
/// waiting for it, or has it commented out, with a TODO.
/// </summary>
internal static class AsyncCalls
{
    /// <summary>The kind of the annotations of awaits the port added.</summary>
    private const string AwaitKind = "TabStudio.Porting.Await";

    /// <summary>An awaited call, marked so its function becomes async.</summary>
    /// <param name="call">The call, such as <c>MessageBox.ShowAsync(…)</c>.</param>
    public static AwaitExpressionSyntax Await(ExpressionSyntax call) =>
        SyntaxFactory.AwaitExpression(
                SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithLeadingTrivia(call.GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.Space),
                call.WithoutLeadingTrivia())
            .WithAdditionalAnnotations(new SyntaxAnnotation(AwaitKind));

    /// <summary>Makes the functions of the marked awaits async.</summary>
    /// <param name="file">The file, for its notes.</param>
    /// <param name="root">Its syntax.</param>
    public static CompilationUnitSyntax MakeAsync(CodeFile file, CompilationUnitSyntax root)
    {
        var awaits = root.GetAnnotatedNodes(AwaitKind).OfType<AwaitExpressionSyntax>().ToList();
        if (awaits.Count == 0)
        {
            return root;
        }

        var asyncFunctions = new HashSet<SyntaxNode>();
        var discarded = new HashSet<SyntaxNode>();
        var commented = new HashSet<SyntaxNode>();
        foreach (var expression in awaits)
        {
            var function = SyntaxContext.Function(expression);
            if (function is not null && CanBeAsync(function))
            {
                asyncFunctions.Add(function);
            }
            else if (expression.Parent is ExpressionStatementSyntax)
            {
                discarded.Add(expression);
            }
            else if (SyntaxContext.Unit(expression) is { } unit)
            {
                commented.Add(unit);
            }
        }

        var targets = asyncFunctions.Concat(discarded).Concat(commented).ToList();
        return root.ReplaceNodes(targets, (original, rewritten) =>
        {
            if (commented.Contains(original))
            {
                return file.CommentOut(rewritten, "A dialog whose answer is used where nothing can wait", "Avalonia's dialogs do not block, and this function cannot be async; make it async, or move the question to an async function");
            }

            return discarded.Contains(original) ? Discard(file, (AwaitExpressionSyntax)rewritten) : AddAsync(rewritten);
        });
    }

    private static bool CanBeAsync(SyntaxNode function) => function switch
    {
        MethodDeclarationSyntax method => method.Modifiers.Any(SyntaxKind.AsyncKeyword) || IsVoid(method.ReturnType) || IsTaskWithoutValues(method.ReturnType, method),
        LocalFunctionStatementSyntax local => local.Modifiers.Any(SyntaxKind.AsyncKeyword) || IsVoid(local.ReturnType) || IsTaskWithoutValues(local.ReturnType, local),
        AnonymousFunctionExpressionSyntax => true,
        _ => false,
    };

    private static bool IsVoid(TypeSyntax type) => type is PredefinedTypeSyntax { Keyword.ValueText: "void" };

    private static bool IsTaskWithoutValues(TypeSyntax type, SyntaxNode function) =>
        type.ToString() is "Task" or "ValueTask" && !function.DescendantNodes().OfType<ReturnStatementSyntax>().Any(r => r.Expression is not null);

    private static SyntaxNode AddAsync(SyntaxNode function)
    {
        var keyword = SyntaxFactory.Token(SyntaxKind.AsyncKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        switch (function)
        {
            case MethodDeclarationSyntax method when !method.Modifiers.Any(SyntaxKind.AsyncKeyword):
                return method.Modifiers.Count > 0
                    ? method.AddModifiers(keyword)
                    : method.WithReturnType(method.ReturnType.WithoutLeadingTrivia()).WithModifiers(SyntaxFactory.TokenList(keyword.WithLeadingTrivia(method.ReturnType.GetLeadingTrivia())));
            case LocalFunctionStatementSyntax local when !local.Modifiers.Any(SyntaxKind.AsyncKeyword):
                return local.Modifiers.Count > 0
                    ? local.AddModifiers(keyword)
                    : local.WithReturnType(local.ReturnType.WithoutLeadingTrivia()).WithModifiers(SyntaxFactory.TokenList(keyword.WithLeadingTrivia(local.ReturnType.GetLeadingTrivia())));
            case AnonymousFunctionExpressionSyntax lambda when !lambda.Modifiers.Any(SyntaxKind.AsyncKeyword):
                var leading = lambda.GetLeadingTrivia();
                var stripped = lambda.WithoutLeadingTrivia();
                return stripped.WithModifiers(stripped.Modifiers.Insert(0, keyword.WithLeadingTrivia(leading)));
            default:
                return function;
        }
    }

    /// <summary>A call whose function cannot be async, and whose result is not used: it runs without being waited for.</summary>
    private static AssignmentExpressionSyntax Discard(CodeFile file, AwaitExpressionSyntax awaited)
    {
        var call = awaited.Expression;
        var discard = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName("_").WithLeadingTrivia(awaited.GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.EqualsToken).WithTrailingTrivia(SyntaxFactory.Space),
            call.WithoutLeadingTrivia());
        return file.Todo(discard, "A dialog that no longer waits", "Avalonia's dialogs do not block, and this function cannot be async; the code after the call runs before the dialog closes. Move that code after an await in an async function");
    }
}
