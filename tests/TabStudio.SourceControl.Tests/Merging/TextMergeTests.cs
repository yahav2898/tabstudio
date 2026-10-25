using TabStudio.SourceControl.Merging;

namespace TabStudio.SourceControl.Tests.Merging;

public sealed class TextMergeTests
{
    private const string Base = "one\ntwo\nthree\nfour\nfive\nsix\n";

    [Fact]
    public void Changes_to_different_parts_of_a_file_are_combined()
    {
        var result = TextMerge.Merge(Base, "ONE\ntwo\nthree\nfour\nfive\nsix\n", "one\ntwo\nthree\nfour\nfive\nSIX\n");

        Assert.True(result.IsClean);
        Assert.Equal("ONE\ntwo\nthree\nfour\nfive\nSIX\n", result.Text);
    }

    [Fact]
    public void Insertions_and_deletions_on_both_sides_are_combined()
    {
        var result = TextMerge.Merge(Base, "zero\none\ntwo\nthree\nfour\nfive\nsix\n", "one\ntwo\nthree\nfive\nsix\nseven\n");

        Assert.True(result.IsClean);
        Assert.Equal("zero\none\ntwo\nthree\nfive\nsix\nseven\n", result.Text);
    }

    [Fact]
    public void The_same_change_on_both_sides_is_taken_once()
    {
        var both = "one\ntwo\n3\nfour\nfive\nsix\n";
        var result = TextMerge.Merge(Base, both, both.Replace("six", "SIX", StringComparison.Ordinal));

        Assert.True(result.IsClean);
        Assert.Equal("one\ntwo\n3\nfour\nfive\nSIX\n", result.Text);
    }

    [Fact]
    public void Different_changes_to_the_same_lines_become_a_conflict_with_markers()
    {
        var result = TextMerge.Merge(Base, "one\ntwo\nmine\nfour\nfive\nsix\n", "one\ntwo\ntheirs\nfour\nfive\nsix\n");

        Assert.Equal(1, result.ConflictCount);
        Assert.Equal(
            "one\ntwo\n<<<<<<< Yours\nmine\n=======\ntheirs\n>>>>>>> GitHub\nfour\nfive\nsix\n",
            result.Text);
        Assert.True(TextMerge.HasConflictMarkers(result.Text));
    }

    [Fact]
    public void Changes_to_neighboring_lines_conflict_as_in_git()
    {
        var result = TextMerge.Merge(Base, "one\ntwo\nTHREE\nfour\nfive\nsix\n", "one\ntwo\nthree\nFOUR\nfive\nsix\n");

        Assert.Equal(1, result.ConflictCount);
        Assert.Contains("<<<<<<< Yours\nTHREE\nfour\n=======\nthree\nFOUR\n>>>>>>> GitHub\n", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Several_conflicts_are_counted_and_the_rest_merges()
    {
        var result = TextMerge.Merge(Base, "A\ntwo\nthree\nfour\nE\nsix\n", "B\ntwo\nthree\nTHREE-AND-A-HALF\nfour\nF\nsix\n");

        Assert.Equal(2, result.ConflictCount);
        Assert.Contains("THREE-AND-A-HALF\n", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void One_sided_changes_take_that_side()
    {
        Assert.Equal("x\n", TextMerge.Merge(Base, Base, "x\n").Text);
        Assert.Equal("y\n", TextMerge.Merge(Base, "y\n", Base).Text);
    }

    [Fact]
    public void A_conflict_without_a_final_newline_still_gets_markers_on_their_own_lines()
    {
        var result = TextMerge.Merge("a\nb", "a\nmine", "a\ntheirs");

        Assert.Equal("a\n<<<<<<< Yours\nmine\n=======\ntheirs\n>>>>>>> GitHub\n", result.Text);
    }

    [Fact]
    public void Markers_use_the_files_line_endings()
    {
        var result = TextMerge.Merge("a\r\nb\r\n", "a\r\nmine\r\n", "a\r\ntheirs\r\n");

        Assert.Equal("a\r\n<<<<<<< Yours\r\nmine\r\n=======\r\ntheirs\r\n>>>>>>> GitHub\r\n", result.Text);
    }

    [Fact]
    public void Files_added_on_both_sides_merge_against_an_empty_base()
    {
        var result = TextMerge.Merge(string.Empty, "same start\nmine\n", "same start\ntheirs\n");

        Assert.Equal(1, result.ConflictCount);
        Assert.StartsWith("same start\n<<<<<<< Yours\n", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_without_markers_is_not_reported_as_conflicted() =>
        Assert.False(TextMerge.HasConflictMarkers("a <<<<<<< b\n=======\n"));
}
