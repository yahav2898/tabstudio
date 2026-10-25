using TabStudio.App.Editing;

namespace TabStudio.App.Tests.Editing;

public sealed class CompletionMatcherTests
{
    [Fact]
    public void A_prefix_matches_and_marks_the_typed_letters()
    {
        var match = CompletionMatcher.Match("Wri", "WriteLine");

        Assert.NotNull(match);
        Assert.Equal(CompletionMatchKind.Prefix, match.Kind);
        Assert.True(match.IsCaseSensitive);
        Assert.Equal(new[] { 0, 1, 2 }, match.MatchedIndices);
    }

    [Fact]
    public void A_prefix_in_other_casing_still_matches_but_ranks_lower()
    {
        var match = CompletionMatcher.Match("wri", "WriteLine");

        Assert.NotNull(match);
        Assert.Equal(CompletionMatchKind.Prefix, match.Kind);
        Assert.False(match.IsCaseSensitive);
    }

    [Fact]
    public void The_whole_name_is_an_exact_match()
    {
        Assert.Equal(CompletionMatchKind.Exact, CompletionMatcher.Match("Write", "Write")?.Kind);
    }

    [Fact]
    public void Capitals_pick_the_humps_of_a_name()
    {
        var match = CompletionMatcher.Match("WL", "WriteLine");

        Assert.NotNull(match);
        Assert.Equal(CompletionMatchKind.CamelCase, match.Kind);
        Assert.Equal(new[] { 0, 5 }, match.MatchedIndices);
    }

    [Fact]
    public void Humps_can_take_several_letters_each()
    {
        var match = CompletionMatcher.Match("StrBu", "StringBuilder");

        Assert.NotNull(match);
        Assert.Equal(CompletionMatchKind.CamelCase, match.Kind);
        Assert.Equal(new[] { 0, 1, 2, 6, 7 }, match.MatchedIndices);
    }

    [Fact]
    public void Text_inside_a_name_is_a_substring_match()
    {
        var match = CompletionMatcher.Match("Line", "WriteLine");

        Assert.NotNull(match);
        Assert.Equal(CompletionMatchKind.Substring, match.Kind);
        Assert.Equal(new[] { 5, 6, 7, 8 }, match.MatchedIndices);
    }

    [Fact]
    public void Unrelated_text_does_not_match()
    {
        Assert.Null(CompletionMatcher.Match("Xyz", "WriteLine"));
        Assert.Null(CompletionMatcher.Match("WriteLines", "WriteLine"));
    }

    [Fact]
    public void Nothing_typed_matches_everything()
    {
        var match = CompletionMatcher.Match(string.Empty, "Console");

        Assert.NotNull(match);
        Assert.Empty(match.MatchedIndices);
    }

    [Theory]
    [InlineData('(', true)]
    [InlineData('.', true)]
    [InlineData(' ', true)]
    [InlineData(';', true)]
    [InlineData('a', false)]
    [InlineData('_', false)]
    public void Punctuation_and_space_commit_but_letters_filter(char typed, bool commits)
    {
        Assert.Equal(commits, CompletionMatcher.IsCommitCharacter(typed));
        Assert.Equal(!commits, CompletionMatcher.IsIdentifierCharacter(typed));
    }
}
