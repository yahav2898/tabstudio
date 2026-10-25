using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.Debugging.Instrumentation;

/// <summary>
/// Keeps interceptors working through instrumentation. A source generator's <c>[InterceptsLocation]</c> names the call
/// it replaces by a checksum of its file and a position in it, as TabStudio's XAML generator does for
/// <c>AvaloniaXamlLoader.Load(this)</c>; rewriting the file changes both. So the intercepted calls are marked before the
/// rewrite, found again after it, and the generated attributes are pointed at where they are now.
/// </summary>
internal static class InterceptorRelocation
{
    private const string AnnotationKind = "TabStudio.Debugging.Intercepted";

    /// <summary>Marks every call an interceptor of the compilation replaces, with the location data it is known by.</summary>
    /// <param name="compilation">The compilation, before instrumentation.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The compilation with the calls marked; the same one when nothing is intercepted.</returns>
    public static CSharpCompilation MarkInterceptedCalls(CSharpCompilation compilation, CancellationToken cancellationToken)
    {
        var data = compilation.SyntaxTrees
            .Where(tree => GeneratedCode.IsGenerated(tree, cancellationToken))
            .SelectMany(tree => Attributes(tree, cancellationToken))
            .Select(attribute => attribute.Data)
            .ToHashSet(StringComparer.Ordinal);
        if (data.Count == 0)
        {
            return compilation;
        }

        var result = compilation;
        foreach (var tree in compilation.SyntaxTrees.Where(t => !GeneratedCode.IsGenerated(t, cancellationToken)))
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot(cancellationToken);
            Dictionary<InvocationExpressionSyntax, string> intercepted = [];
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetInterceptableLocation(invocation, cancellationToken) is { } location && data.Contains(location.Data))
                {
                    intercepted[invocation] = location.Data;
                }
            }

            if (intercepted.Count > 0)
            {
                var marked = root.ReplaceNodes(intercepted.Keys, (original, rewritten) => rewritten.WithAdditionalAnnotations(new SyntaxAnnotation(AnnotationKind, intercepted[original])));
                result = result.ReplaceSyntaxTree(tree, tree.WithRootAndOptions(marked, tree.Options));
            }
        }

        return result;
    }

    /// <summary>Points the generated <c>[InterceptsLocation]</c> attributes at the marked calls, where the rewrite left them.</summary>
    /// <param name="compilation">The compilation, after instrumentation.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The compilation with its interceptors moved; the same one when nothing is intercepted.</returns>
    public static CSharpCompilation RelocateInterceptors(CSharpCompilation compilation, CancellationToken cancellationToken)
    {
        Dictionary<string, InterceptableLocation> moved = new(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot(cancellationToken);
            if (!root.ContainsAnnotations)
            {
                continue;
            }

            var model = compilation.GetSemanticModel(tree);
            foreach (var node in root.GetAnnotatedNodes(AnnotationKind))
            {
                if (node is InvocationExpressionSyntax invocation
                    && model.GetInterceptableLocation(invocation, cancellationToken) is { } location
                    && node.GetAnnotations(AnnotationKind).FirstOrDefault()?.Data is { } original)
                {
                    moved[original] = location;
                }
            }
        }

        if (moved.Count == 0)
        {
            return compilation;
        }

        var result = compilation;
        foreach (var tree in compilation.SyntaxTrees.Where(t => GeneratedCode.IsGenerated(t, cancellationToken)))
        {
            var attributes = Attributes(tree, cancellationToken).Where(a => moved.ContainsKey(a.Data)).ToList();
            if (attributes.Count == 0)
            {
                continue;
            }

            var root = tree.GetRoot(cancellationToken).ReplaceNodes(
                attributes.Select(a => a.Syntax),
                (original, _) =>
                {
                    var location = moved[attributes.First(a => a.Syntax == original).Data];
                    return original.WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(
                    [
                        SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(location.Version))),
                        SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(location.Data))),
                    ])));
                });
            result = result.ReplaceSyntaxTree(tree, tree.WithRootAndOptions(root, tree.Options));
        }

        return result;
    }

    /// <summary>The <c>[InterceptsLocation(version, "data")]</c> attributes of a tree.</summary>
    private static IEnumerable<(AttributeSyntax Syntax, string Data)> Attributes(SyntaxTree tree, CancellationToken cancellationToken)
    {
        if (!tree.GetText(cancellationToken).ToString().Contains("InterceptsLocation", StringComparison.Ordinal))
        {
            yield break;
        }

        foreach (var attribute in tree.GetRoot(cancellationToken).DescendantNodes().OfType<AttributeSyntax>())
        {
            var name = attribute.Name switch
            {
                QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
                SimpleNameSyntax simple => simple.Identifier.ValueText,
                _ => string.Empty,
            };
            if (name is "InterceptsLocation" or "InterceptsLocationAttribute"
                && attribute.ArgumentList?.Arguments is [_, { Expression: LiteralExpressionSyntax data }]
                && data.IsKind(SyntaxKind.StringLiteralExpression))
            {
                yield return (attribute, data.Token.ValueText);
            }
        }
    }
}
