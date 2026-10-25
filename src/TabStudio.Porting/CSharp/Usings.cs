using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.Porting.CSharp;

/// <summary>Keeps a file's using directives in order as namespaces come and go: System first, then the others.</summary>
internal static class Usings
{
    /// <summary>The namespaces a file imports, at its top or inside its namespace.</summary>
    /// <param name="root">The file.</param>
    public static HashSet<string> Imported(CompilationUnitSyntax root) =>
        [.. root.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax).OfType<UsingDirectiveSyntax>()
            .Where(u => u.Alias is null && u.StaticKeyword.IsKind(SyntaxKind.None) && u.Name is not null)
            .Select(u => u.Name!.ToString())];

    /// <summary>Removes some usings and adds others.</summary>
    /// <param name="root">The file.</param>
    /// <param name="remove">Which namespaces to remove.</param>
    /// <param name="add">The namespaces to add.</param>
    /// <param name="newline">The file's line ending.</param>
    public static CompilationUnitSyntax Update(CompilationUnitSyntax root, Func<string, bool> remove, IEnumerable<string> add, string newline = "\n")
    {
        var usings = root.Usings;
        var namespaceDeclaration = root.Members.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        var inNamespace = usings.Count == 0 && namespaceDeclaration is { Usings.Count: > 0 };
        if (inNamespace)
        {
            usings = namespaceDeclaration!.Usings;
        }

        var header = usings.Count > 0 ? usings[0].GetLeadingTrivia() : default;
        var kept = usings.Where(u => u.Alias is not null || u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) || u.Name is null || !remove(u.Name.ToString())).ToList();
        var names = new HashSet<string>(kept.Where(u => u.Alias is null && u.Name is not null).Select(u => u.Name!.ToString()), StringComparer.Ordinal);
        foreach (var name in add.Distinct(StringComparer.Ordinal).Where(n => !names.Contains(n)))
        {
            kept.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(name)).NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine(newline)));
            names.Add(name);
        }

        var sorted = kept
            .OrderBy(u => u.Alias is not null || !u.StaticKeyword.IsKind(SyntaxKind.None) ? 2 : u.Name?.ToString() is { } n && (n == "System" || n.StartsWith("System.", StringComparison.Ordinal)) ? 0 : 1)
            .ThenBy(u => u.Name?.ToString(), StringComparer.Ordinal)
            .Select(u => u.WithLeadingTrivia(SyntaxTriviaList.Empty))
            .ToList();
        if (sorted.Count > 0)
        {
            // The file's header comment, such as a license, stays at the top.
            sorted[0] = sorted[0].WithLeadingTrivia(header);
        }

        var list = SyntaxFactory.List(sorted);
        if (inNamespace)
        {
            return root.ReplaceNode(namespaceDeclaration!, namespaceDeclaration!.WithUsings(list));
        }

        root = root.WithUsings(list);
        if (sorted.Count == 0 && root.Members.FirstOrDefault() is { } first)
        {
            // No usings left: the file starts with its first declaration, without the blank line that followed them.
            root = root.ReplaceNode(first, first.WithLeadingTrivia(first.GetLeadingTrivia().SkipWhile(t => t.IsKind(SyntaxKind.EndOfLineTrivia) || t.IsKind(SyntaxKind.WhitespaceTrivia))));
        }

        return root;
    }
}
