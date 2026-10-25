using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Compilation.Features;

/// <summary>Rename (F2): renames a symbol and every reference to it, across all loaded projects.</summary>
public sealed class RenameFeature
{
    private readonly RoslynWorkspaceHost _host;

    internal RenameFeature(RoslynWorkspaceHost host) => _host = host;

    /// <summary>Checks whether the symbol at a position can be renamed, and where its name is.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">Zero-based offset of the caret, on or just after the identifier.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The name and span to edit, or why the symbol cannot be renamed.</returns>
    public async Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var (target, _, _) = await ResolveAsync(filePath, position, cancellationToken).ConfigureAwait(false);
        return target;
    }

    /// <summary>Computes the edits that rename the symbol at a position.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">Zero-based offset of the caret, on or just after the identifier.</param>
    /// <param name="newName">The new name. Prefix it with <c>@</c> to use a keyword, such as <c>@class</c>.</param>
    /// <param name="options">What else to rename, or <see langword="null"/> for <see cref="RenameOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels the rename.</param>
    /// <returns>The edits for every affected file, and any conflicts the new name causes.</returns>
    public async Task<RenameResult> GetRenameEditsAsync(
        string filePath,
        int position,
        string newName,
        RenameOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(newName);

        var (target, document, symbol) = await ResolveAsync(filePath, position, cancellationToken).ConfigureAwait(false);
        if (!target.CanRename || document is null || symbol is null)
        {
            return RenameResult.Failed(target.Reason ?? "This element cannot be renamed.");
        }

        if (ValidateName(newName) is { } invalid)
        {
            return RenameResult.Failed(invalid);
        }

        if (string.Equals(newName, target.CurrentName, StringComparison.Ordinal))
        {
            return new RenameResult(true, null, [], []);
        }

        options ??= RenameOptions.Default;
        var solution = document.Project.Solution;
        var renameOptions = new SymbolRenameOptions(options.RenameOverloads, options.RenameInStrings, options.RenameInComments, RenameFile: false);
        Solution renamed;
        try
        {
            renamed = await Renamer.RenameSymbolAsync(solution, symbol, renameOptions, newName, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            // Roslyn reports a rename it cannot perform this way.
            return RenameResult.Failed(ex.Message);
        }

        var edits = await SolutionEdits.GetDocumentEditsAsync(solution, renamed, cancellationToken).ConfigureAwait(false);
        var conflicts = await FindConflictsAsync(solution, renamed, cancellationToken).ConfigureAwait(false);
        return new RenameResult(true, null, edits, conflicts);
    }

    private async Task<(RenameTarget Target, Document? Document, ISymbol? Symbol)> ResolveAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        if (_host.GetDocument(filePath) is not { } document
            || await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) is not { } root)
        {
            return (RenameTarget.Unavailable("The file is not part of a loaded C# project."), null, null);
        }

        if (FindIdentifier(root, Math.Clamp(position, 0, root.FullSpan.End)) is not { } token)
        {
            return (RenameTarget.Unavailable("Place the caret on a name to rename it."), null, null);
        }

        var found = await SymbolFinder.FindSymbolAtPositionAsync(document, token.SpanStart, cancellationToken).ConfigureAwait(false);
        if (found is null)
        {
            return (RenameTarget.Unavailable("There is nothing to rename here."), null, null);
        }

        var symbol = RenamedSymbolOf(found);
        var solution = document.Project.Solution;
        if (symbol.IsImplicitlyDeclared
            || symbol is INamespaceSymbol { IsGlobalNamespace: true }
            || symbol.Locations.IsEmpty
            || symbol.Locations.Any(l => !l.IsInSource)
            || (token.ValueText == "var" && symbol is ITypeSymbol { Name: not "var" }))
        {
            return (RenameTarget.Unavailable("Only names declared in this solution can be renamed."), null, null);
        }

        if (symbol.Locations.Any(l => l.SourceTree is not { } tree || solution.GetDocument(tree) is not { } owner || IsGenerated(owner)))
        {
            return (RenameTarget.Unavailable("This name is declared in generated code."), null, null);
        }

        return (new RenameTarget(true, null, token.ValueText, token.SpanStart, token.Span.Length), document, symbol);
    }

    /// <summary>The identifier at the caret, or the one just left of it.</summary>
    private static SyntaxToken? FindIdentifier(SyntaxNode root, int position)
    {
        var token = root.FindToken(position);
        if (token.IsKind(SyntaxKind.IdentifierToken) && token.Span.IntersectsWith(position))
        {
            return token;
        }

        if (position > 0)
        {
            var left = root.FindToken(position - 1);
            if (left.IsKind(SyntaxKind.IdentifierToken) && left.Span.IntersectsWith(position))
            {
                return left;
            }
        }

        return null;
    }

    /// <summary>The symbol a rename at a name changes: renaming a constructor renames its type.</summary>
    private static ISymbol RenamedSymbolOf(ISymbol symbol)
    {
        var renamed = symbol switch
        {
            IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor } method => method.ContainingType,
            IMethodSymbol { ReducedFrom: { } reducedFrom } => reducedFrom,
            _ => symbol,
        };

        return renamed.OriginalDefinition;
    }

    private static bool IsGenerated(Document document) =>
        document.FilePath is { } path
        && (path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase));

    /// <summary>Checks that a new name is a C# identifier, returning why it is not.</summary>
    private static string? ValidateName(string name)
    {
        var escaped = name.StartsWith('@');
        var identifier = escaped ? name[1..] : name;
        if (!SyntaxFacts.IsValidIdentifier(identifier))
        {
            return $"'{name}' is not a valid C# name.";
        }

        if (!escaped && SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None)
        {
            return $"'{name}' is a C# keyword. Use '@{name}' to name a symbol after it.";
        }

        return null;
    }

    /// <summary>Finds errors in the renamed files that were not there before the rename.</summary>
    private static async Task<List<BuildDiagnostic>> FindConflictsAsync(Solution before, Solution after, CancellationToken cancellationToken)
    {
        var conflicts = new List<BuildDiagnostic>();
        foreach (var project in after.GetChanges(before).GetProjectChanges())
        {
            foreach (var id in project.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true))
            {
                if (before.GetDocument(id) is not { } oldDocument || after.GetDocument(id) is not { } newDocument)
                {
                    continue;
                }

                var remaining = (await ErrorsAsync(oldDocument, cancellationToken).ConfigureAwait(false))
                    .GroupBy(d => d.Id, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

                foreach (var error in await ErrorsAsync(newDocument, cancellationToken).ConfigureAwait(false))
                {
                    if (remaining.TryGetValue(error.Id, out var count) && count > 0)
                    {
                        remaining[error.Id] = count - 1;
                        continue;
                    }

                    conflicts.Add(DiagnosticMapper.Map(error, newDocument.Project.Name));
                }
            }
        }

        return conflicts;
    }

    private static async Task<List<Diagnostic>> ErrorsAsync(Document document, CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        return model is null
            ? []
            : model.GetDiagnostics(cancellationToken: cancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    }
}
