using TabStudio.App.Editing;

namespace TabStudio.App.Tests.Editing;

public sealed class InlineDiffTests
{
    private static readonly string Twenty = string.Concat(Enumerable.Range(1, 20).Select(i => $"line {i}\n"));

    [Fact]
    public void Shows_removed_then_added_lines_with_both_line_numbers()
    {
        var diff = InlineDiff.Create("a\nb\nc\n", "a\nB\nc\nd\n");

        Assert.Equal(2, diff.AddedCount);
        Assert.Equal(1, diff.RemovedCount);
        Assert.Equal(
            [
                new DiffLine(DiffLineKind.Unchanged, "a", 1, 1),
                new DiffLine(DiffLineKind.Removed, "b", 2, null),
                new DiffLine(DiffLineKind.Added, "B", null, 2),
                new DiffLine(DiffLineKind.Unchanged, "c", 3, 3),
                new DiffLine(DiffLineKind.Added, "d", null, 4),
            ],
            diff.Lines);
        Assert.Equal("a\nb\nB\nc\nd", diff.Text);
    }

    [Fact]
    public void Collapses_unchanged_runs_beyond_three_lines_of_context()
    {
        var changed = Twenty.Replace("line 2\n", "line two\n", StringComparison.Ordinal).Replace("line 18\n", "line eighteen\n", StringComparison.Ordinal);

        var diff = InlineDiff.Create(Twenty, changed);

        var kinds = diff.Lines.Select(l => l.Kind).ToList();
        Assert.Equal(DiffLineKind.Unchanged, kinds[0]);
        Assert.Equal([DiffLineKind.Removed, DiffLineKind.Added], kinds.Skip(1).Take(2));
        var collapsed = Assert.Single(diff.Lines, l => l.Kind == DiffLineKind.Collapsed);
        Assert.Equal(9, collapsed.HiddenCount);
        Assert.Equal("… 9 unchanged lines", collapsed.Text);

        // Three lines of context on each side of the fold: lines 3-5, then 15-17.
        var index = diff.Lines.ToList().IndexOf(collapsed);
        Assert.Equal(5, diff.Lines[index - 1].NewNumber);
        Assert.Equal(15, diff.Lines[index + 1].NewNumber);
    }

    [Fact]
    public void Expanding_a_collapsed_line_shows_what_it_hid()
    {
        var changed = Twenty.Replace("line 1\n", "line one\n", StringComparison.Ordinal);
        var diff = InlineDiff.Create(Twenty, changed);
        var index = diff.Lines.ToList().FindIndex(l => l.Kind == DiffLineKind.Collapsed);

        var expanded = diff.Expand(index);

        Assert.DoesNotContain(expanded.Lines, l => l.Kind == DiffLineKind.Collapsed);
        Assert.Equal(Enumerable.Range(1, 20), expanded.Lines.Where(l => l.NewNumber is not null).Select(l => l.NewNumber!.Value));
        Assert.Same(expanded, expanded.Expand(0));
    }

    [Fact]
    public void A_new_file_is_all_added_and_a_deleted_one_all_removed()
    {
        var added = InlineDiff.Create(string.Empty, "x\ny\n");
        var removed = InlineDiff.Create("x\ny\n", string.Empty);

        Assert.All(added.Lines, l => Assert.Equal(DiffLineKind.Added, l.Kind));
        Assert.Equal(2, added.AddedCount);
        Assert.All(removed.Lines, l => Assert.Equal(DiffLineKind.Removed, l.Kind));
        Assert.Equal(2, removed.RemovedCount);
    }

    [Fact]
    public void Identical_texts_collapse_into_one_line()
    {
        var diff = InlineDiff.Create(Twenty, Twenty);

        Assert.False(diff.HasChanges);
        var only = Assert.Single(diff.Lines);
        Assert.Equal(DiffLineKind.Collapsed, only.Kind);
        Assert.Equal(20, only.HiddenCount);
    }

    [Fact]
    public void Windows_line_endings_are_not_shown()
    {
        var diff = InlineDiff.Create("a\r\nb\r\n", "a\r\nc\r\n");

        Assert.Equal(["a", "b", "c"], diff.Lines.Select(l => l.Text));
    }
}
