using TabStudio.App.Services.Search;

namespace TabStudio.App.Tests.Design;

/// <summary>Matching and ranking what the user types against names, as an IDE's search does.</summary>
public sealed class SearchMatcherTests
{
    [Theory]
    [InlineData("save all", "Save All", SearchMatcher.Exact)]
    [InlineData("save", "Save All", SearchMatcher.Prefix)]
    [InlineData("all", "Save All", SearchMatcher.WordPrefix)]
    [InlineData("sa", "Save All", SearchMatcher.Prefix)]
    [InlineData("gtd", "Go To Definition", SearchMatcher.WordStarts)]
    [InlineData("TeEx", "TestExplorer.cs", SearchMatcher.WordStarts)]
    [InlineData("plor", "Explorer", SearchMatcher.Substring)]
    [InlineData("gdf", "Go To Definition", SearchMatcher.Scattered)]
    public void A_match_scores_by_where_the_letters_are(string query, string text, int band)
    {
        var match = SearchMatcher.Match(query, text);

        Assert.NotNull(match);
        Assert.InRange(match.Score, band - 99, band);
    }

    [Fact]
    public void Each_word_of_the_query_must_match()
    {
        Assert.NotNull(SearchMatcher.Match("run tests", "Run All Tests"));
        Assert.Null(SearchMatcher.Match("run build", "Run All Tests"));
        Assert.Null(SearchMatcher.Match("xyz", "Save All"));
    }

    [Fact]
    public void The_matched_letters_are_the_ones_to_highlight()
    {
        Assert.Equal(new[] { 0, 5 }, SearchMatcher.Match("s a", "Save All")!.Indices.ToArray());
        Assert.Equal(new[] { 0, 3, 6 }, SearchMatcher.Match("gtd", "Go To Definition")!.Indices.ToArray());
        Assert.Equal(new[] { 2, 3, 4, 5 }, SearchMatcher.Match("plor", "Explorer")!.Indices.ToArray());
    }

    [Fact]
    public void Better_matches_rank_first_and_disabled_or_unknown_items_last()
    {
        var items = new[]
        {
            Item(SearchKind.File, "Builder.cs"),
            Item(SearchKind.Command, "Build Solution"),
            Item(SearchKind.Command, "Rebuild Solution") with { IsEnabled = false },
            Item(SearchKind.Setting, "Color theme: Dark") with { Keywords = ["build output"] },
            Item(SearchKind.File, "Greeter.cs", "src/build"),
        };

        var ranked = SearchRanker.Rank("build", items).Select(result => result.Item.Title).ToList();

        // A whole word first; a keyword or a folder ranks lower than a name; what cannot run now ranks last.
        Assert.Equal(new[] { "Build Solution", "Builder.cs", "Color theme: Dark", "Greeter.cs", "Rebuild Solution" }, ranked);
    }

    [Fact]
    public void What_was_opened_before_ranks_higher()
    {
        var history = new SearchHistory();
        var program = Item(SearchKind.File, "Program.cs");
        var projects = Item(SearchKind.File, "ProjectTemplates.cs");
        Assert.Equal("Program.cs", SearchRanker.Rank("pro", [projects, program])[0].Item.Title);

        history.Record(projects);

        Assert.Equal("ProjectTemplates.cs", SearchRanker.Rank("pro", [program, projects], history)[0].Item.Title);
        Assert.Same(projects, history.Recent[0]);
    }

    private static SearchItem Item(SearchKind kind, string title, string? detail = null) =>
        new(kind, title, detail, () => Task.CompletedTask);
}
