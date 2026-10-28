using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;

namespace TabStudio.Compilation.HotReload;

/// <summary>
/// Conservative rude-edit analysis. EmitDifference assumes the caller already checked whether an edit is legal;
/// it is not Roslyn's IDE rude-edit analyzer (which is not a public API).
/// </summary>
internal static class HotReloadEdits
{
    /// <summary>The edits between two compilations of the same project, or why they need a restart.</summary>
    /// <param name="before">The compilation the running program came from.</param>
    /// <param name="after">The edited compilation.</param>
    /// <param name="token">Cancels the analysis.</param>
    public static (List<SemanticEdit> Edits, string? RudeEdit) Analyze(Microsoft.CodeAnalysis.Compilation before, Microsoft.CodeAnalysis.Compilation after, CancellationToken token) => (before, after) switch
    {
        (CSharpCompilation oldCSharp, CSharpCompilation newCSharp) => AnalyzeCSharp(oldCSharp, newCSharp, token),
        (Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilation oldBasic, Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilation newBasic) => VisualBasicHotReloadEdits.Analyze(oldBasic, newBasic, token),
        _ => ([], "Changing the project's language requires a restart."),
    };

    private static (List<SemanticEdit> Edits, string? RudeEdit) AnalyzeCSharp(CSharpCompilation before, CSharpCompilation after, CancellationToken token)
    {
        static IEnumerable<string> References(CSharpCompilation compilation) => compilation.References
            .Select(r => $"{compilation.GetAssemblyOrModuleSymbol(r)?.ToDisplayString()}|{r.Display}").Order(StringComparer.Ordinal);
        if (!References(before).SequenceEqual(References(after), StringComparer.Ordinal))
        {
            return ([], "Changing assembly references requires a restart.");
        }

        var oldTrees = before.SyntaxTrees.ToDictionary(t => t.FilePath, StringComparer.Ordinal);
        var newTrees = after.SyntaxTrees.ToDictionary(t => t.FilePath, StringComparer.Ordinal);
        if (!oldTrees.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(newTrees.Keys))
        {
            return ([], "Adding, removing or renaming source files requires a restart.");
        }

        var edits = new List<SemanticEdit>();
        foreach (var (path, oldTree) in oldTrees)
        {
            var newTree = newTrees[path];
            var oldRoot = oldTree.GetRoot(token);
            var newRoot = newTree.GetRoot(token);
            if (oldRoot.IsEquivalentTo(newRoot))
            {
                continue;
            }

            if (!new Headers().Visit(oldRoot)!.IsEquivalentTo(new Headers().Visit(newRoot)))
            {
                return ([], $"{Path.GetFileName(path)}: changing declarations, signatures, fields, initializers, attributes or imports requires a restart.");
            }

            var oldMethods = Methods(before, oldTree, token);
            var newMethods = Methods(after, newTree, token);
            if (!oldMethods.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(newMethods.Keys))
            {
                return ([], $"{Path.GetFileName(path)}: adding or removing a method or accessor requires a restart.");
            }

            foreach (var (key, oldMethod) in oldMethods)
            {
                var newMethod = newMethods[key];
                // Top-level Main's signature is inferred from its statements, so the declaration comparison
                // above cannot catch void/int/Task changes. EmitDifference assumes signatures already match.
                if (key == "<top-level>" && oldMethod.Symbol.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    != newMethod.Symbol.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                {
                    return ([], $"{Path.GetFileName(path)}: changing the top-level entry point's return type requires a restart.");
                }

                var oldNodes = BodyNodes(oldMethod.Body, key).ToArray();
                var newNodes = BodyNodes(newMethod.Body, key).ToArray();
                if (key == "<top-level>"
                    ? ((CompilationUnitSyntax)oldMethod.Body).Members.OfType<GlobalStatementSyntax>()
                        .Select(n => n.WithoutTrivia().NormalizeWhitespace().ToFullString())
                        .SequenceEqual(((CompilationUnitSyntax)newMethod.Body).Members.OfType<GlobalStatementSyntax>()
                            .Select(n => n.WithoutTrivia().NormalizeWhitespace().ToFullString()), StringComparer.Ordinal)
                    : oldMethod.Body.IsEquivalentTo(newMethod.Body))
                {
                    continue;
                }

                // With closures/state machines, preserve the compiler's identity of each syntax node. If the tree
                // shape changes we cannot prove the captures/suspension points still match, so require a restart.
                Func<SyntaxNode, SyntaxNode?>? map = null;
                if (oldNodes.Any(IsStateful) || newNodes.Any(IsStateful) || oldMethod.Symbol.IsAsync || newMethod.Symbol.IsAsync)
                {
                    if (!oldNodes.Select(n => n.RawKind).SequenceEqual(newNodes.Select(n => n.RawKind))
                        || !StateDeclarations(oldNodes).SequenceEqual(StateDeclarations(newNodes), StringComparer.Ordinal)
                        || !StateSymbols(before.GetSemanticModel(oldTree), oldNodes, token)
                            .SequenceEqual(StateSymbols(after.GetSemanticModel(newTree), newNodes, token), StringComparer.Ordinal))
                    {
                        return ([], $"{Path.GetFileName(path)}: changing the shape, captures or inferred types of a lambda, local function, iterator or async method requires a restart.");
                    }

                    var pairs = newNodes.Zip(oldNodes).ToDictionary(p => p.First, p => p.Second);
                    map = node => pairs.GetValueOrDefault(node);
                }

                edits.Add(new SemanticEdit(SemanticEditKind.Update, oldMethod.Symbol, newMethod.Symbol,
                    syntaxMap: map, runtimeRudeEdit: null, instrumentation: default));
            }
        }

        return (edits, null);
    }

    private static bool IsStateful(SyntaxNode node) => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or YieldStatementSyntax;

    private static IEnumerable<SyntaxNode> BodyNodes(SyntaxNode body, string key) => key == "<top-level>"
        ? ((CompilationUnitSyntax)body).Members.OfType<GlobalStatementSyntax>().SelectMany(n => n.DescendantNodesAndSelf())
        : body.DescendantNodesAndSelf();

    private static IEnumerable<string> StateSymbols(SemanticModel model, IEnumerable<SyntaxNode> nodes, CancellationToken token)
    {
        foreach (var node in nodes.Where(n => n is VariableDeclaratorSyntax or ParameterSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax
            or ForEachStatementSyntax or CatchDeclarationSyntax or SingleVariableDesignationSyntax))
        {
            // Syntax alone misses inferred changes (var x = 1 -> 1L, or Task.Run(() => 1 -> 1L)), which change
            // hoisted field types or generated lambda signatures even without a local variable declaration.
            var symbol = node is AnonymousFunctionExpressionSyntax
                ? model.GetSymbolInfo(node, token).Symbol
                : model.GetDeclaredSymbol(node, token);
            yield return symbol switch
            {
                ILocalSymbol local => $"local:{local.Name}:{local.RefKind}:{local.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}",
                IParameterSymbol parameter => $"parameter:{parameter.Name}:{parameter.RefKind}:{parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}",
                IMethodSymbol method => $"method:{method.ToDisplayString()}:{method.RefKind}:{method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}",
                _ => symbol?.ToDisplayString() ?? string.Empty,
            };
        }
    }

    private static IEnumerable<string> StateDeclarations(IEnumerable<SyntaxNode> nodes) => nodes
        .Where(n => n is VariableDeclaratorSyntax or ParameterSyntax or LocalFunctionStatementSyntax or IdentifierNameSyntax)
        .Select(n => n switch
        {
            VariableDeclaratorSyntax v => "var:" + v.Identifier.ValueText,
            ParameterSyntax p => p.WithDefault(null).WithoutTrivia().NormalizeWhitespace().ToFullString(),
            LocalFunctionStatementSyntax f => f.WithBody(null).WithExpressionBody(null).WithoutTrivia().NormalizeWhitespace().ToFullString(),
            _ => n.ToString(), // Capturing a different variable is a rude edit even if the tree shape is identical.
        });

    private static Dictionary<string, (IMethodSymbol Symbol, SyntaxNode Body)> Methods(CSharpCompilation compilation, SyntaxTree tree, CancellationToken token)
    {
        var model = compilation.GetSemanticModel(tree);
        var result = new Dictionary<string, (IMethodSymbol, SyntaxNode)>(StringComparer.Ordinal);
        var root = tree.GetRoot(token);
        foreach (var node in root.DescendantNodes())
        {
            // Local functions and lambdas are updated through their enclosing method.
            var body = node switch
            {
                BaseMethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody,
                AccessorDeclarationSyntax a => (SyntaxNode?)a.Body ?? a.ExpressionBody,
                PropertyDeclarationSyntax p => p.ExpressionBody,
                IndexerDeclarationSyntax i => i.ExpressionBody,
                _ => null,
            };
            if (body is null)
            {
                continue;
            }

            var symbol = model.GetDeclaredSymbol(node, token);
            var method = symbol as IMethodSymbol ?? (symbol as IPropertySymbol)?.GetMethod;
            if (method is not null)
            {
                result.Add(method.GetDocumentationCommentId() ?? method.ToDisplayString(), (method, body));
            }
        }

        if (root is CompilationUnitSyntax unit && unit.Members.OfType<GlobalStatementSyntax>().Any()
            && compilation.GetEntryPoint(token) is { } entry)
        {
            result.Add("<top-level>", (entry, unit));
        }

        return result;
    }

    private sealed class Headers : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node) => base.VisitCompilationUnit(
            node.WithMembers(SyntaxFactory.List(node.Members.Where(m => m is not GlobalStatementSyntax))));

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) => node.WithBody(null).WithExpressionBody(null).WithSemicolonToken(default);
        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node) => node.WithBody(null).WithExpressionBody(null).WithSemicolonToken(default);
        public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node) => node.WithBody(null).WithExpressionBody(null).WithSemicolonToken(default);
        public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node) => node.WithBody(null).WithExpressionBody(null).WithSemicolonToken(default);
        public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node) => node.WithBody(null).WithExpressionBody(null).WithSemicolonToken(default);
        public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node) => node.WithBody(null).WithExpressionBody(null).WithSemicolonToken(default);
        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node) => base.VisitPropertyDeclaration(node.WithExpressionBody(null));
        public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node) => base.VisitIndexerDeclaration(node.WithExpressionBody(null));
    }
}
