using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace TabStudio.Compilation.Features;

/// <summary>Finds the symbol under the caret and converts Roslyn locations, for the navigation features.</summary>
internal static class SymbolResolver
{
    /// <summary>
    /// Returns the symbol at a position: the one declared there or the one referenced there, as Go to Definition
    /// sees it. A caret just after an identifier counts as on it.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="position">Zero-based offset of the caret.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The original definition of the symbol, or <see langword="null"/> when there is none.</returns>
    public static async Task<ISymbol?> FindSymbolAsync(Document document, int position, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, Math.Clamp(position, 0, text.Length), cancellationToken).ConfigureAwait(false);
        return symbol is null ? null : Normalize(symbol);
    }

    /// <summary>
    /// Maps a symbol to the definition that searches start from: the static method of a reduced extension method
    /// call, and the generic definition of a constructed generic.
    /// </summary>
    public static ISymbol Normalize(ISymbol symbol)
    {
        if (symbol is IMethodSymbol { ReducedFrom: { } reducedFrom })
        {
            symbol = reducedFrom;
        }

        return symbol.OriginalDefinition;
    }

    /// <summary>Converts a Roslyn location in source into a <see cref="SourceLocation"/>.</summary>
    /// <returns>The location, or <see langword="null"/> for locations in metadata or in files without a path.</returns>
    public static SourceLocation? ToSourceLocation(Location location)
    {
        if (!location.IsInSource || location.SourceTree is not { } tree || string.IsNullOrEmpty(tree.FilePath))
        {
            return null;
        }

        var span = location.SourceSpan;
        var start = location.GetLineSpan().StartLinePosition;
        return new SourceLocation(tree.FilePath, span.Start, span.Length, start.Line + 1, start.Character + 1);
    }

    /// <summary>
    /// Whether a definition that Find All References returns should be listed. Accessors and compiler-generated
    /// symbols are left out: their declarations are the property, event or type the user searched for.
    /// </summary>
    public static bool IsListedDefinition(ISymbol definition) =>
        !definition.IsImplicitlyDeclared
        && definition is not IMethodSymbol
        {
            MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise,
        };

    /// <summary>Describes where a symbol is defined.</summary>
    public static DefinitionResult ToDefinition(ISymbol symbol) => new(
        symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
        GlyphMapper.FromSymbol(symbol),
        SourceLocationsOf(symbol),
        symbol.ContainingAssembly?.Name);

    /// <summary>The source locations where a symbol is declared, in file and position order.</summary>
    public static List<SourceLocation> SourceLocationsOf(ISymbol symbol) =>
        symbol.Locations
            .Select(ToSourceLocation)
            .OfType<SourceLocation>()
            .OrderBy(l => l.FilePath, StringComparer.Ordinal)
            .ThenBy(l => l.Start)
            .ToList();
}
