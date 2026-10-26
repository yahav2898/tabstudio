using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.Porting.CSharp;

/// <summary>A reason code cannot be ported: what it uses, and what to do instead.</summary>
/// <param name="What">What the code uses, such as <c>CollectionViewSource</c>.</param>
/// <param name="Why">Why Avalonia has nothing that maps exactly, and what to do instead.</param>
internal sealed record Unported(string What, string Why);

/// <summary>
/// Code that uses what Avalonia does not have is commented out under a TODO, so the project still builds: the
/// statement (or member) that uses it, and then whatever uses what that code declared, such as a field.
/// </summary>
internal static class UnsupportedCode
{
    /// <summary>Marks the code to comment out.</summary>
    /// <param name="file">The file.</param>
    /// <param name="root">Its syntax.</param>
    /// <param name="find">Whether a node uses something Avalonia does not have, and why.</param>
    /// <returns>The syntax with the code marked, and a throw after each commented-out return.</returns>
    public static CompilationUnitSyntax CommentOut(CodeFile file, CompilationUnitSyntax root, Func<SyntaxNode, Unported?> find)
    {
        var units = new Dictionary<SyntaxNode, Unported>();
        foreach (var node in root.DescendantNodes())
        {
            if (find(node) is { } reason && SyntaxContext.Unit(node) is { } unit && !IsCovered(units, unit))
            {
                units[unit] = reason;
            }
        }

        // What the commented code declared is gone too, so the code that uses it goes with it.
        var cascaded = new HashSet<SyntaxNode>();
        bool changed;
        do
        {
            changed = false;
            var declared = new List<(string Name, SyntaxNode Scope, Unported Reason)>();
            foreach (var (unit, reason) in units)
            {
                if (!cascaded.Add(unit))
                {
                    continue;
                }

                var scope = unit is StatementSyntax ? SyntaxContext.Function(unit) ?? unit.Parent! : unit.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault() ?? (SyntaxNode)root;
                foreach (var name in SyntaxContext.DeclaredNames(unit))
                {
                    declared.Add((name, scope, reason));
                }
            }

            foreach (var (name, scope, reason) in declared)
            {
                foreach (var use in scope.DescendantNodes().OfType<IdentifierNameSyntax>().Where(i => i.Identifier.ValueText == name))
                {
                    if (SyntaxContext.Unit(use) is { } unit && !IsCovered(units, unit))
                    {
                        units[unit] = new Unported($"Uses {name}", $"{name} is commented out above: {reason.What}");
                        changed = true;
                    }
                }
            }
        }
        while (changed);

        if (units.Count == 0)
        {
            return root;
        }

        // Outermost units only: a member that is commented out takes its statements with it.
        var outermost = units.Keys.Where(u => !u.Ancestors().Any(units.ContainsKey)).ToList();
        root = root.ReplaceNodes(outermost, (original, rewritten) => file.CommentOut(rewritten, units[original].What, units[original].Why));

        // A function that returned a value must still return or throw.
        var blocks = root.GetAnnotatedNodes(CodeFile.CommentOutKind).OfType<ReturnStatementSyntax>()
            .Where(r => r.Expression is not null && r.Parent is BlockSyntax)
            .Select(r => (BlockSyntax)r.Parent!)
            .Distinct()
            .ToList();
        return root.ReplaceNodes(blocks, (_, block) => block.WithStatements(SyntaxFactory.List(block.Statements.SelectMany(statement =>
            statement is ReturnStatementSyntax { Expression: not null } && statement.HasAnnotations(CodeFile.CommentOutKind)
                ? [statement, Throw(statement, file.Newline)]
                : new[] { statement }))));
    }

    /// <summary>A throw in place of a return that is commented out, so the function still compiles.</summary>
    private static StatementSyntax Throw(StatementSyntax statement, string newline)
    {
        var indent = statement.GetLeadingTrivia().Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).ToList();
        return SyntaxFactory.ParseStatement("throw new NotSupportedException(\"Not ported to Avalonia yet: see the TODO(port) above.\");")
            .WithLeadingTrivia(indent.Count > 0 ? indent[^1] : SyntaxFactory.Whitespace(string.Empty))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine(newline));
    }

    private static bool IsCovered(Dictionary<SyntaxNode, Unported> units, SyntaxNode unit) =>
        unit.AncestorsAndSelf().Any(units.ContainsKey);
}
