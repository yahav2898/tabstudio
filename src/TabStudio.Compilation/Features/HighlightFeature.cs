using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace TabStudio.Compilation.Features;

/// <summary>Highlights every occurrence, in the same file, of the symbol under the caret.</summary>
public sealed class HighlightFeature
{
    private readonly RoslynWorkspaceHost _host;

    internal HighlightFeature(RoslynWorkspaceHost host) => _host = host;

    /// <summary>Returns the occurrences in a file of the symbol at a position.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">Zero-based offset of the caret.</param>
    /// <param name="cancellationToken">Cancels the search, typically because the caret moved.</param>
    /// <returns>The occurrences in document order, or an empty list when the caret is not on a symbol.</returns>
    public async Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (_host.GetDocument(filePath) is not { } document
            || await SymbolResolver.FindSymbolAsync(document, position, cancellationToken).ConfigureAwait(false) is not { } symbol
            || await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false) is not { } tree)
        {
            return [];
        }

        var referenced = await SymbolFinder.FindReferencesAsync(
            symbol,
            document.Project.Solution,
            ImmutableHashSet.Create(document),
            cancellationToken).ConfigureAwait(false);

        var highlights = new Dictionary<int, DocumentHighlight>();
        foreach (var group in referenced)
        {
            if (SymbolResolver.IsListedDefinition(group.Definition))
            {
                foreach (var location in group.Definition.Locations)
                {
                    if (location.IsInSource && location.SourceTree == tree)
                    {
                        highlights[location.SourceSpan.Start] = new DocumentHighlight(location.SourceSpan.Start, location.SourceSpan.Length, HighlightKind.Definition);
                    }
                }
            }

            foreach (var reference in group.Locations)
            {
                var span = reference.Location.SourceSpan;
                if (!reference.IsImplicit && reference.Document.Id == document.Id)
                {
                    highlights.TryAdd(span.Start, new DocumentHighlight(span.Start, span.Length, HighlightKind.Reference));
                }
            }
        }

        return highlights.Values.OrderBy(h => h.Start).ToList();
    }
}
