namespace TabStudio.App.Services.Search;

/// <summary>A source of search results: files, commands, settings, symbols, templates or recent workspaces.</summary>
public interface ISearchProvider
{
    /// <summary>What it finds.</summary>
    SearchKind Kind { get; }

    /// <summary>
    /// The items that may match a query. Providers with a short list return all of it and let the search rank it; those
    /// that search a large index, as symbols do, return their own matches.
    /// </summary>
    /// <param name="query">What the user typed, trimmed; empty for the suggestions before typing.</param>
    /// <param name="cancellationToken">Cancelled when the user types on.</param>
    Task<IReadOnlyList<SearchItem>> SearchAsync(string query, CancellationToken cancellationToken);
}
