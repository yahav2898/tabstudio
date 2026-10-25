using TabStudio.Compilation.Features;

namespace TabStudio.App.Services.Search;

/// <summary>A type or member found in the code.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Kind">What it is, in words, such as "Class" or "Method".</param>
/// <param name="Container">The type or namespace it is in, or <see langword="null"/>.</param>
/// <param name="Glyph">Its IntelliSense glyph.</param>
/// <param name="FilePath">The file it is declared in.</param>
/// <param name="Line">Its one-based line.</param>
/// <param name="Column">Its one-based column.</param>
public sealed record SymbolHit(string Name, string Kind, string? Container, SymbolGlyph Glyph, string FilePath, int Line, int Column);

/// <summary>Searches the code for types and members by name: Roslyn for C#, a language server for other languages.</summary>
public interface ISymbolSearch
{
    /// <summary>The symbols whose names match a query, best first.</summary>
    /// <param name="query">Part of a name.</param>
    /// <param name="limit">The most to return.</param>
    /// <param name="cancellationToken">Cancelled when the user types on.</param>
    Task<IReadOnlyList<SymbolHit>> FindAsync(string query, int limit, CancellationToken cancellationToken);
}

/// <summary>Finds types and members of the open solution, through an <see cref="ISymbolSearch"/>.</summary>
/// <param name="search">The symbol search.</param>
/// <param name="open">Opens a symbol's declaration.</param>
public sealed class SymbolSearchProvider(ISymbolSearch search, Func<SymbolHit, Task> open) : ISearchProvider
{
    /// <summary>The most symbols one search shows.</summary>
    public const int Limit = 50;

    /// <inheritdoc />
    public SearchKind Kind => SearchKind.Symbol;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (query.Length == 0)
        {
            return [];
        }

        var hits = await search.FindAsync(query, Limit, cancellationToken);
        return [.. hits.Select(hit => new SearchItem(
            SearchKind.Symbol,
            hit.Name,
            hit.Container is { Length: > 0 } container ? $"{hit.Kind} in {container}, {Path.GetFileName(hit.FilePath)}" : $"{hit.Kind}, {Path.GetFileName(hit.FilePath)}",
            () => open(hit))
        {
            Symbol = hit.Glyph,
        })];
    }
}
