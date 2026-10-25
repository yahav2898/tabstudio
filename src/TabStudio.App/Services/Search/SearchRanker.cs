namespace TabStudio.App.Services.Search;

/// <summary>A search result with its score and the characters of its title that matched.</summary>
/// <param name="Item">The item.</param>
/// <param name="Score">How well it answers the query; higher first.</param>
/// <param name="Highlights">The matched characters of its title, for drawing them stronger.</param>
public sealed record RankedItem(SearchItem Item, int Score, IReadOnlyList<int> Highlights);

/// <summary>
/// Ranks what the providers found: by how well the title matches (see <see cref="SearchMatcher"/>), then its keywords
/// and its detail, raised for what the user opened before and lowered for what cannot run now.
/// </summary>
public static class SearchRanker
{
    /// <summary>How much lower a match on a keyword ranks than one on the title.</summary>
    public const int KeywordPenalty = 150;

    /// <summary>How much lower a match on the detail (a folder, a menu path) ranks than one on the title.</summary>
    public const int DetailPenalty = 250;

    /// <summary>How much lower an item ranks while it cannot be opened.</summary>
    public const int DisabledPenalty = 120;

    /// <summary>The items that match, best first.</summary>
    /// <param name="query">What the user typed.</param>
    /// <param name="items">The candidates.</param>
    /// <param name="history">What was opened before, or <see langword="null"/>.</param>
    public static IReadOnlyList<RankedItem> Rank(string query, IEnumerable<SearchItem> items, SearchHistory? history = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(items);
        var ranked = new List<RankedItem>();
        foreach (var item in items)
        {
            if (Score(query, item) is not { } match)
            {
                continue;
            }

            var score = match.Score + (history?.Boost(item) ?? 0) - (item.IsEnabled ? 0 : DisabledPenalty);
            ranked.Add(match with { Score = score });
        }

        return [.. ranked
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Item.Kind)
            .ThenBy(result => result.Item.Title.Length)
            .ThenBy(result => result.Item.Title, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static RankedItem? Score(string query, SearchItem item)
    {
        if (SearchMatcher.Match(query, item.Title) is { } title)
        {
            return new RankedItem(item, title.Score, title.Indices);
        }

        // A keyword or a detail counts only when it holds the query's letters together: letters scattered through a
        // sentence, as "greeter" through "light colors for the whole IDE, after", match nearly anything.
        var keyword = item.Keywords
            .Select(word => SearchMatcher.Match(query, word))
            .OfType<SearchMatch>()
            .Where(match => match.Score > SearchMatcher.Scattered)
            .MaxBy(match => match.Score);
        if (keyword is not null)
        {
            return new RankedItem(item, keyword.Score - KeywordPenalty, []);
        }

        return item.Detail is { } detail && SearchMatcher.Match(query, detail) is { Score: > SearchMatcher.Scattered } inDetail
            ? new RankedItem(item, inDetail.Score - DetailPenalty, [])
            : null;
    }
}
