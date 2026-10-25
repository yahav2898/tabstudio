using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Core.Diagnostics;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.Compilation.Wpf;

/// <summary>
/// Turns the C# compiler's "not found" errors about WPF API that OpenSilver does not have into errors that say so
/// (<see cref="WpfDiagnosticIds.UnsupportedApi"/>): a type such as <c>GroupBox</c>, a member such as
/// <c>Window.DialogResult</c>, a namespace such as <c>System.Windows.Xps</c>, each pointing to the Windows Bridge,
/// where the project runs on WPF itself. The names come from <see cref="WpfApiCatalog"/>.
/// </summary>
internal static class WpfUnsupportedApi
{
    /// <summary>The errors that can be about missing API: a type, a namespace, a member, a name, an override.</summary>
    private static readonly HashSet<string> NotFoundIds = new(StringComparer.Ordinal) { "CS0246", "CS0234", "CS1061", "CS0117", "CS0103", "CS0115" };

    /// <summary>The message for API WPF has and OpenSilver does not.</summary>
    /// <param name="api">The API, such as <c>GroupBox</c> or <c>Window.DialogResult</c>.</param>
    public static string Message(string api) =>
        $"{api} is in WPF but not in OpenSilver, so it cannot run in the web preview. To use it, run the project on your PC through the Windows Bridge.";

    /// <summary>The diagnostics of a file, with each one about missing WPF API said as such.</summary>
    public static IReadOnlyList<BuildDiagnostic> Explain(IReadOnlyList<BuildDiagnostic> diagnostics, SemanticModel model, CancellationToken cancellationToken)
    {
        var catalog = WpfApiCatalog.Shipped;
        if (catalog.IsEmpty || !diagnostics.Any(d => NotFoundIds.Contains(d.Id)))
        {
            return diagnostics;
        }

        return [.. diagnostics.Select(d => NotFoundIds.Contains(d.Id) && d.Location is { } location && location.FilePath == model.SyntaxTree.FilePath
            ? Rewrite(d, model, new TextSpan(location.Start, location.Length), catalog, cancellationToken)
            : d)];
    }

    /// <summary>The diagnostics of a project, with each one about missing WPF API said as such.</summary>
    public static IReadOnlyList<BuildDiagnostic> Explain(IReadOnlyList<BuildDiagnostic> diagnostics, RoslynCompilation compilation, CancellationToken cancellationToken)
    {
        var catalog = WpfApiCatalog.Shipped;
        if (catalog.IsEmpty || !diagnostics.Any(d => NotFoundIds.Contains(d.Id)))
        {
            return diagnostics;
        }

        var trees = compilation.SyntaxTrees.Where(t => t.FilePath.Length > 0).GroupBy(t => t.FilePath, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        return [.. diagnostics.Select(d => NotFoundIds.Contains(d.Id) && d.Location is { } location && trees.TryGetValue(location.FilePath, out var tree)
            ? Rewrite(d, compilation.GetSemanticModel(tree), new TextSpan(location.Start, location.Length), catalog, cancellationToken)
            : d)];
    }

    private static BuildDiagnostic Rewrite(BuildDiagnostic diagnostic, SemanticModel model, TextSpan span, WpfApiCatalog catalog, CancellationToken cancellationToken)
    {
        var root = model.SyntaxTree.GetRoot(cancellationToken);
        if (span.End > root.FullSpan.End)
        {
            return diagnostic;
        }

        var node = root.FindNode(span, getInnermostNodeForTie: true);
        var api = diagnostic.Id switch
        {
            "CS0246" => MissingType(node, model, catalog),
            "CS0234" => MissingInNamespace(node, model, catalog, cancellationToken),
            "CS1061" or "CS0117" => MissingMember(node, model, catalog, cancellationToken),
            "CS0103" => MissingName(node, model, catalog, cancellationToken),
            "CS0115" => MissingOverride(node, model, catalog, cancellationToken),
            _ => null,
        };
        return api is null ? diagnostic : diagnostic with { Id = WpfDiagnosticIds.UnsupportedApi, Severity = DiagnosticSeverityLevel.Error, Message = Message(api) };
    }

    /// <summary>A type name not found: a WPF type OpenSilver lacks in a namespace the file imports.</summary>
    private static string? MissingType(SyntaxNode node, SemanticModel model, WpfApiCatalog catalog)
    {
        if (NameOf(node) is not { } name)
        {
            return null;
        }

        var imported = ImportedNamespaces(model);
        return catalog.MissingTypesNamed(name).FirstOrDefault(t => imported.Contains(t[..t.LastIndexOf('.')])) is { } type ? WpfApiCatalog.SimpleName(type) : null;
    }

    /// <summary>A name not found in a namespace, as in <c>System.Windows.Xps</c> or <c>System.Windows.Controls.GroupBox</c>.</summary>
    private static string? MissingInNamespace(SyntaxNode node, SemanticModel model, WpfApiCatalog catalog, CancellationToken cancellationToken)
    {
        var (left, right) = node switch
        {
            QualifiedNameSyntax qualified => ((SyntaxNode?)qualified.Left, (string?)qualified.Right.Identifier.ValueText),
            MemberAccessExpressionSyntax access => (access.Expression, access.Name.Identifier.ValueText),
            SimpleNameSyntax { Parent: QualifiedNameSyntax q } simple when q.Right == simple => (q.Left, simple.Identifier.ValueText),
            SimpleNameSyntax { Parent: MemberAccessExpressionSyntax a } simple when a.Name == simple => (a.Expression, simple.Identifier.ValueText),
            _ => (null, null),
        };
        if (left is null || right is null || model.GetSymbolInfo(left, cancellationToken).Symbol is not INamespaceSymbol ns)
        {
            return null;
        }

        var full = $"{ns.ToDisplayString()}.{right}";
        return catalog.IsMissingType(full) ? right
            : catalog.IsMissingNamespace(full) ? $"The namespace {full}"
            : null;
    }

    /// <summary>A member not found on a type: a member of the WPF type it is, or derives from, that OpenSilver's lacks.</summary>
    private static string? MissingMember(SyntaxNode node, SemanticModel model, WpfApiCatalog catalog, CancellationToken cancellationToken)
    {
        var (receiver, name) = node switch
        {
            MemberAccessExpressionSyntax access => (access.Expression, access.Name.Identifier.ValueText),
            SimpleNameSyntax { Parent: MemberAccessExpressionSyntax access } simple when access.Name == simple => (access.Expression, simple.Identifier.ValueText),
            _ => (null, null),
        };
        if (receiver is null || name is null)
        {
            return null;
        }

        var type = model.GetTypeInfo(receiver, cancellationToken).Type ?? model.GetSymbolInfo(receiver, cancellationToken).Symbol as ITypeSymbol;
        return MemberAlong(type, name, catalog);
    }

    /// <summary>A name not found where it is used: a member of a WPF base class of the class it is in, or a type.</summary>
    private static string? MissingName(SyntaxNode node, SemanticModel model, WpfApiCatalog catalog, CancellationToken cancellationToken)
    {
        if (NameOf(node) is not { } name)
        {
            return null;
        }

        return MemberAlong(model.GetEnclosingSymbol(node.SpanStart, cancellationToken)?.ContainingType, name, catalog)
            ?? MissingType(node, model, catalog);
    }

    /// <summary>An override without a method to override, such as <c>OnRender</c>: a member of a WPF base class OpenSilver's lacks.</summary>
    private static string? MissingOverride(SyntaxNode node, SemanticModel model, WpfApiCatalog catalog, CancellationToken cancellationToken)
    {
        var member = node.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        var name = member switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            _ => null,
        };
        return name is null || member is null ? null : MemberAlong(model.GetDeclaredSymbol(member, cancellationToken)?.ContainingType?.BaseType, name, catalog);
    }

    /// <summary>The WPF member a type, or a type it derives from, lacks in OpenSilver, as <c>Window.DialogResult</c>.</summary>
    private static string? MemberAlong(ITypeSymbol? type, string name, WpfApiCatalog catalog)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current is INamedTypeSymbol named && catalog.FindMissingMember(MetadataName(named), name) is { } declaring)
            {
                return $"{WpfApiCatalog.SimpleName(declaring)}.{name}";
            }
        }

        return null;
    }

    private static string? NameOf(SyntaxNode node) => node switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        _ => node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().FirstOrDefault()?.Identifier.ValueText,
    };

    /// <summary>The namespaces a file imports, with the project's global usings, and those its types are declared in.</summary>
    private static HashSet<string> ImportedNamespaces(SemanticModel model)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        var root = model.SyntaxTree.GetRoot();
        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (directive.Alias is null && directive.Name is { } name)
            {
                namespaces.Add(name.ToString());
            }
        }

        foreach (var tree in model.Compilation.SyntaxTrees)
        {
            if (tree != model.SyntaxTree && tree.GetRoot() is CompilationUnitSyntax unit)
            {
                foreach (var directive in unit.Usings)
                {
                    if (directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) && directive.Alias is null && directive.Name is { } name)
                    {
                        namespaces.Add(name.ToString());
                    }
                }
            }
        }

        foreach (var declaration in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
        {
            namespaces.Add(declaration.Name.ToString());
        }

        return namespaces;
    }

    private static string MetadataName(INamedTypeSymbol type) =>
        type.ContainingNamespace is { IsGlobalNamespace: false } ns ? $"{ns.ToDisplayString()}.{type.MetadataName}" : type.MetadataName;
}
