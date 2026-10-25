using TabStudio.SourceControl.Merging;

namespace TabStudio.SourceControl.Tests.Merging;

public sealed class LineDiffTests
{
    [Fact]
    public void Splitting_keeps_line_endings_so_text_can_be_rebuilt()
    {
        var lines = LineDiff.SplitLines("a\r\nb\nc");

        Assert.Equal(new[] { "a\r\n", "b\n", "c" }, lines);
        Assert.Equal("a\r\nb\nc", string.Concat(lines));
        Assert.Empty(LineDiff.SplitLines(string.Empty));
    }

    [Fact]
    public void Identical_texts_have_no_hunks() =>
        Assert.Empty(LineDiff.Compute(Lines("a b c"), Lines("a b c")));

    [Fact]
    public void A_changed_line_is_one_hunk()
    {
        var hunks = LineDiff.Compute(Lines("a b c d"), Lines("a B c d"));

        Assert.Equal(new[] { new DiffHunk(1, 1, 1, 1) }, hunks);
    }

    [Fact]
    public void Insertions_and_deletions_are_found()
    {
        var hunks = LineDiff.Compute(Lines("a b c d e"), Lines("a x b d e f"));

        Assert.Equal(new[] { new DiffHunk(1, 0, 1, 1), new DiffHunk(2, 1, 3, 0), new DiffHunk(5, 0, 5, 1) }, hunks);
    }

    [Fact]
    public void Everything_replaced_when_nothing_matches()
    {
        Assert.Equal(new[] { new DiffHunk(0, 2, 0, 3) }, LineDiff.Compute(Lines("a b"), Lines("x y z")));
        Assert.Equal(new[] { new DiffHunk(0, 0, 0, 2) }, LineDiff.Compute(Lines(string.Empty), Lines("x y")));
        Assert.Equal(new[] { new DiffHunk(0, 2, 0, 0) }, LineDiff.Compute(Lines("x y"), Lines(string.Empty)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Applying_the_hunks_rebuilds_the_new_text_with_a_minimal_edit_count(int seed)
    {
        var random = new Random(seed);
        for (var round = 0; round < 200; round++)
        {
            var oldLines = RandomLines(random);
            var newLines = Mutate(oldLines, random);

            var hunks = LineDiff.Compute(oldLines, newLines);

            Assert.Equal(newLines, Apply(oldLines, newLines, hunks));
            var edits = hunks.Sum(h => h.OldLength + h.NewLength);
            Assert.Equal(oldLines.Count + newLines.Count - (2 * LongestCommonSubsequence(oldLines, newLines)), edits);
        }
    }

    [Fact]
    public void Over_the_edit_limit_the_texts_are_treated_as_completely_different()
    {
        var oldLines = Enumerable.Range(0, 50).Select(i => $"old {i}\n").ToList();
        var newLines = Enumerable.Range(0, 50).Select(i => i % 2 == 0 ? $"old {i}\n" : $"new {i}\n").ToList();

        var hunks = LineDiff.Compute(oldLines, newLines, maximumEdits: 4);

        Assert.Single(hunks);
    }

    private static List<string> Lines(string words) =>
        words.Length == 0 ? [] : [.. words.Split(' ').Select(w => w + "\n")];

    private static List<string> RandomLines(Random random) =>
        [.. Enumerable.Range(0, random.Next(0, 25)).Select(_ => $"{(char)('a' + random.Next(0, 5))}\n")];

    private static List<string> Mutate(List<string> lines, Random random)
    {
        var result = new List<string>(lines);
        for (var i = random.Next(0, 6); i > 0; i--)
        {
            switch (random.Next(3))
            {
                case 0:
                    result.Insert(random.Next(result.Count + 1), $"{(char)('a' + random.Next(0, 7))}\n");
                    break;
                case 1 when result.Count > 0:
                    result.RemoveAt(random.Next(result.Count));
                    break;
                case 2 when result.Count > 0:
                    result[random.Next(result.Count)] = $"{(char)('a' + random.Next(0, 7))}\n";
                    break;
            }
        }

        return result;
    }

    private static List<string> Apply(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines, IReadOnlyList<DiffHunk> hunks)
    {
        var result = new List<string>();
        var position = 0;
        foreach (var hunk in hunks)
        {
            Assert.True(hunk.OldStart >= position, "Hunks are in order and do not overlap.");
            result.AddRange(oldLines.Skip(position).Take(hunk.OldStart - position));
            result.AddRange(newLines.Skip(hunk.NewStart).Take(hunk.NewLength));
            position = hunk.OldEnd;
        }

        result.AddRange(oldLines.Skip(position));
        return result;
    }

    private static int LongestCommonSubsequence(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var table = new int[a.Count + 1, b.Count + 1];
        for (var i = a.Count - 1; i >= 0; i--)
        {
            for (var j = b.Count - 1; j >= 0; j--)
            {
                table[i, j] = a[i] == b[j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table[0, 0];
    }
}
