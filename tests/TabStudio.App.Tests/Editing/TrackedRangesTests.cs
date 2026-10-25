using AvaloniaEdit.Document;
using TabStudio.App.Editing;

namespace TabStudio.App.Tests.Editing;

public sealed class TrackedRangesTests
{
    private static TrackedRange<string>[] Apply(TrackedRange<string>[] ranges, string text, Action<TextDocument> edit)
    {
        var document = new TextDocument(text);
        var result = ranges;
        document.Changed += (_, change) => result = TrackedRanges.Apply(result, change);
        edit(document);
        return result;
    }

    [Fact]
    public void Ranges_after_an_insertion_move_right()
    {
        var moved = Apply([new(10, 5, "type")], "0123456789ABCDE", d => d.Insert(0, "xx"));

        Assert.Equal(12, Assert.Single(moved).Start);
    }

    [Fact]
    public void Ranges_before_an_edit_stay()
    {
        var moved = Apply([new(0, 3, "keyword")], "var x = 1;", d => d.Insert(4, "y"));

        Assert.Equal(new TrackedRange<string>(0, 3, "keyword"), Assert.Single(moved));
    }

    [Fact]
    public void Typing_right_after_a_range_does_not_extend_it()
    {
        var moved = Apply([new(0, 3, "keyword")], "var", d => d.Insert(3, "iable"));

        Assert.Equal(3, Assert.Single(moved).Length);
    }

    [Fact]
    public void Typing_inside_a_range_grows_it()
    {
        var moved = Apply([new(0, 6, "string")], "\"abc\";", d => d.Insert(2, "zz"));

        Assert.Equal(8, Assert.Single(moved).Length);
    }

    [Fact]
    public void Ranges_cut_by_a_deletion_are_dropped()
    {
        var moved = Apply([new(2, 4, "method")], "a.Method();", d => d.Remove(0, 4));

        Assert.Empty(moved);
    }

    [Fact]
    public void Binary_search_finds_the_first_range_on_a_line()
    {
        TrackedRange<string>[] ranges = [new(0, 2, "a"), new(5, 2, "b"), new(10, 2, "c")];

        Assert.Equal(1, TrackedRanges.FirstEndingAfter(ranges, 3));
        Assert.Equal(1, TrackedRanges.FirstEndingAfter(ranges, 6));
        Assert.Equal(3, TrackedRanges.FirstEndingAfter(ranges, 12));
    }
}
