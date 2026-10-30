using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TabStudio.Compilation.HotReload;

/// <summary>
/// The Visual Basic counterpart of <see cref="HotReloadEdits"/>: the same conservative rules, over Visual Basic's
/// syntax. Statements inside a <c>Sub</c>, <c>Function</c>, constructor, operator or property and event accessor may
/// change; anything else (declarations, signatures, fields, attributes, <c>Imports</c>, <c>Handles</c> clauses) needs a
/// restart. Lambdas, iterators and <c>Async</c> methods may change only where the shape of their syntax, their
/// captures and their inferred types stay the same.
/// </summary>
internal static class VisualBasicHotReloadEdits
{
    public static (List<SemanticEdit> Edits, string? RudeEdit) Analyze(VisualBasicCompilation before, VisualBasicCompilation after, CancellationToken token)
    {
        static IEnumerable<string> References(VisualBasicCompilation compilation) => compilation.References
            .Select(r => $"{compilation.GetAssemblyOrModuleSymbol(r)?.ToDisplayString()}|{r.Display}").Order(StringComparer.Ordinal);
        if (!References(before).SequenceEqual(References(after), StringComparer.Ordinal))
        {
            return ([], "Changing assembly references requires a restart.");
        }

        // The My template has no file; it never changes between two compilations of one project.
        var oldTrees = before.SyntaxTrees.Where(t => t.FilePath.Length > 0).ToDictionary(t => t.FilePath, StringComparer.Ordinal);
        var newTrees = after.SyntaxTrees.Where(t => t.FilePath.Length > 0).ToDictionary(t => t.FilePath, StringComparer.Ordinal);
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
                return ([], $"{Path.GetFileName(path)}: changing declarations, signatures, fields, initializers, attributes, Handles clauses or Imports requires a restart.");
            }

            var oldMethods = Methods(before, oldTree, token);
            var newMethods = Methods(after, newTree, token);
            if (!oldMethods.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(newMethods.Keys))
            {
                return ([], $"{Path.GetFileName(path)}: adding or removing a Sub, Function or accessor requires a restart.");
            }

            foreach (var (key, oldMethod) in oldMethods)
            {
                var newMethod = newMethods[key];
                if (oldMethod.Body.IsEquivalentTo(newMethod.Body))
                {
                    continue;
                }

                var oldNodes = oldMethod.Body.DescendantNodesAndSelf().ToArray();
                var newNodes = newMethod.Body.DescendantNodesAndSelf().ToArray();
                Func<SyntaxNode, SyntaxNode?>? map = null;
                if (oldNodes.Any(IsStateful) || newNodes.Any(IsStateful) || oldMethod.Symbol.IsAsync || newMethod.Symbol.IsAsync)
                {
                    if (!oldNodes.Select(n => n.RawKind).SequenceEqual(newNodes.Select(n => n.RawKind))
                        || !StateDeclarations(oldNodes).SequenceEqual(StateDeclarations(newNodes), StringComparer.Ordinal)
                        || !StateSymbols(before.GetSemanticModel(oldTree), oldNodes, token)
                            .SequenceEqual(StateSymbols(after.GetSemanticModel(newTree), newNodes, token), StringComparer.Ordinal))
                    {
                        return ([], $"{Path.GetFileName(path)}: changing the shape, captures or inferred types of a lambda, iterator or Async method requires a restart.");
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

    private static bool IsStateful(SyntaxNode node) => node is LambdaExpressionSyntax or YieldStatementSyntax or AwaitExpressionSyntax;

    private static IEnumerable<string> StateSymbols(SemanticModel model, IEnumerable<SyntaxNode> nodes, CancellationToken token)
    {
        foreach (var node in nodes.Where(n => n is ModifiedIdentifierSyntax or ParameterSyntax or LambdaExpressionSyntax or ForEachStatementSyntax or ForStatementSyntax or CatchStatementSyntax))
        {
            var symbol = node switch
            {
                LambdaExpressionSyntax => model.GetSymbolInfo(node, token).Symbol,
                ForEachStatementSyntax forEach => model.GetSymbolInfo(forEach.ControlVariable, token).Symbol,
                ForStatementSyntax @for => model.GetSymbolInfo(@for.ControlVariable, token).Symbol,
                CatchStatementSyntax { IdentifierName: { } name } => model.GetSymbolInfo(name, token).Symbol,
                _ => model.GetDeclaredSymbol(node, token),
            };
            yield return symbol switch
            {
                ILocalSymbol local => $"local:{local.Name}:{local.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}",
                IParameterSymbol parameter => $"parameter:{parameter.Name}:{parameter.RefKind}:{parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}",
                IMethodSymbol method => $"method:{method.ToDisplayString()}:{method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}",
                _ => symbol?.ToDisplayString() ?? string.Empty,
            };
        }
    }

    private static IEnumerable<string> StateDeclarations(IEnumerable<SyntaxNode> nodes) => nodes
        .Where(n => n is ModifiedIdentifierSyntax or ParameterSyntax or IdentifierNameSyntax)
        .Select(n => n switch
        {
            ModifiedIdentifierSyntax v => "var:" + v.Identifier.ValueText,
            ParameterSyntax p => p.WithDefault(null).WithoutTrivia().NormalizeWhitespace().ToFullString(),
            _ => n.ToString(),
        });

    private static Dictionary<string, (IMethodSymbol Symbol, SyntaxNode Body)> Methods(VisualBasicCompilation compilation, SyntaxTree tree, CancellationToken token)
    {
        var model = compilation.GetSemanticModel(tree);
        var result = new Dictionary<string, (IMethodSymbol, SyntaxNode)>(StringComparer.Ordinal);
        foreach (var block in tree.GetRoot(token).DescendantNodes().OfType<MethodBlockBaseSyntax>())
        {
            // Lambdas are updated through the method that contains them.
            if (model.GetDeclaredSymbol(block, token) is IMethodSymbol method)
            {
                result.TryAdd(method.GetDocumentationCommentId() ?? method.ToDisplayString(), (method, block));
            }
        }

        return result;
    }

    /// <summary>Removes every member's statements, leaving what a restart is needed for.</summary>
    private sealed class Headers : VisualBasicSyntaxRewriter
    {
        public override SyntaxNode? VisitMethodBlock(MethodBlockSyntax node) => node.WithStatements(default);

        public override SyntaxNode? VisitConstructorBlock(ConstructorBlockSyntax node) => node.WithStatements(default);

        public override SyntaxNode? VisitOperatorBlock(OperatorBlockSyntax node) => node.WithStatements(default);

        public override SyntaxNode? VisitAccessorBlock(AccessorBlockSyntax node) => node.WithStatements(default);
    }
}
