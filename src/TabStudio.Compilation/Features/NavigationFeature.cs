using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace TabStudio.Compilation.Features;

/// <summary>Go to Definition (F12), Go to Implementation (Ctrl+F12) and Find All References (Shift+F12).</summary>
/// <remarks>Searches cover every loaded project, so a symbol used in one project and declared in another is found.</remarks>
public sealed class NavigationFeature
{
    private readonly RoslynWorkspaceHost _host;

    internal NavigationFeature(RoslynWorkspaceHost host) => _host = host;

    /// <summary>Finds where the symbol at a position is defined.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">Zero-based offset of the caret.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The definition, or <see langword="null"/> when there is no symbol at the position.</returns>
    public async Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (_host.GetDocument(filePath) is not { } document
            || await SymbolResolver.FindSymbolAsync(document, position, cancellationToken).ConfigureAwait(false) is not { } symbol)
        {
            return null;
        }

        // An implicit constructor has no declaration of its own; its type does.
        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor, IsImplicitlyDeclared: true } constructor)
        {
            symbol = constructor.ContainingType;
        }

        var source = await SymbolFinder.FindSourceDefinitionAsync(symbol, document.Project.Solution, cancellationToken).ConfigureAwait(false)
            ?? await FindInSkeletonProjectAsync(symbol, document.Project.Solution, cancellationToken).ConfigureAwait(false);
        return SymbolResolver.ToDefinition(source ?? symbol);
    }

    /// <summary>
    /// Finds the source of a symbol that came from a project referenced as a skeleton image (see
    /// <see cref="RoslynWorkspaceHost.UseSkeletonReferences"/>): the project is in the solution, only not
    /// referenced as source, so its declaration is looked up there by documentation id.
    /// </summary>
    private static async Task<ISymbol?> FindInSkeletonProjectAsync(ISymbol symbol, Solution solution, CancellationToken cancellationToken)
    {
        if (symbol.ContainingAssembly is not { } assembly
            || symbol.OriginalDefinition.GetDocumentationCommentId() is not { } id
            || solution.Projects.FirstOrDefault(p => p.AssemblyName == assembly.Name) is not { } project
            || await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false) is not { } compilation)
        {
            return null;
        }

        var declared = DocumentationCommentId.GetFirstSymbolForDeclarationId(id, compilation);
        return declared?.Locations.Any(l => l.IsInSource) == true ? declared : null;
    }

    /// <summary>
    /// Finds the implementations of the symbol at a position: the types that implement an interface, the classes
    /// that derive from a class, and the members that implement or override a member.
    /// </summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">Zero-based offset of the caret.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The implementations in source, or an empty list.</returns>
    public async Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (_host.GetDocument(filePath) is not { } document
            || await SymbolResolver.FindSymbolAsync(document, position, cancellationToken).ConfigureAwait(false) is not { } symbol)
        {
            return [];
        }

        var solution = document.Project.Solution;
        var found = new List<ISymbol>();
        switch (symbol)
        {
            case INamedTypeSymbol { TypeKind: TypeKind.Interface } type:
                found.AddRange(await SymbolFinder.FindImplementationsAsync(type, solution, cancellationToken: cancellationToken).ConfigureAwait(false));
                break;

            case INamedTypeSymbol { TypeKind: TypeKind.Class } type:
                found.AddRange(await SymbolFinder.FindDerivedClassesAsync(type, solution, cancellationToken: cancellationToken).ConfigureAwait(false));
                if (found.Count == 0 && !type.IsAbstract)
                {
                    found.Add(type);
                }

                break;

            case IMethodSymbol or IPropertySymbol or IEventSymbol when symbol.ContainingType?.TypeKind == TypeKind.Interface:
                found.AddRange(await SymbolFinder.FindImplementationsAsync(symbol, solution, cancellationToken: cancellationToken).ConfigureAwait(false));
                break;

            case IMethodSymbol or IPropertySymbol or IEventSymbol when symbol.IsAbstract || symbol.IsVirtual || symbol.IsOverride:
                if (!symbol.IsAbstract)
                {
                    found.Add(symbol);
                }

                found.AddRange(await SymbolFinder.FindOverridesAsync(symbol, solution, cancellationToken: cancellationToken).ConfigureAwait(false));
                break;

            default:
                found.Add(symbol);
                break;
        }

        return found
            .Distinct(SymbolEqualityComparer.Default)
            .Select(s => SymbolResolver.ToDefinition(s!))
            .Where(d => !d.IsInMetadata)
            .ToList();
    }

    /// <summary>Finds every reference to the symbol at a position, and its declarations.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">Zero-based offset of the caret.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>References and definitions in source, in file and position order.</returns>
    public async Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (_host.GetDocument(filePath) is not { } document
            || await SymbolResolver.FindSymbolAsync(document, position, cancellationToken).ConfigureAwait(false) is not { } symbol)
        {
            return [];
        }

        var referenced = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution, cancellationToken).ConfigureAwait(false);
        var items = new Dictionary<(string File, int Start), ReferenceItem>();
        foreach (var group in referenced)
        {
            if (SymbolResolver.IsListedDefinition(group.Definition))
            {
                foreach (var location in group.Definition.Locations)
                {
                    await AddAsync(items, location, isDefinition: true, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var reference in group.Locations)
            {
                if (!reference.IsImplicit)
                {
                    await AddAsync(items, reference.Location, isDefinition: false, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return items.Values
            .OrderBy(i => i.FilePath, StringComparer.Ordinal)
            .ThenBy(i => i.Start)
            .ToList();
    }

    private static async Task AddAsync(Dictionary<(string File, int Start), ReferenceItem> items, Location location, bool isDefinition, CancellationToken cancellationToken)
    {
        if (SymbolResolver.ToSourceLocation(location) is not { } source || location.SourceTree is not { } tree)
        {
            return;
        }

        var key = (source.FilePath, source.Start);
        if (items.TryGetValue(key, out var existing) && (existing.IsDefinition || !isDefinition))
        {
            return;
        }

        var text = await tree.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var line = text.Lines.GetLineFromPosition(source.Start).ToString();
        items[key] = new ReferenceItem(source.FilePath, source.Start, source.Length, source.Line, source.Column, line, isDefinition);
    }
}
