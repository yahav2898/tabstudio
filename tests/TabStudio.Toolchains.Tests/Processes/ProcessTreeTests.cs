using TabStudio.Toolchains.Processes.Unix;

namespace TabStudio.Toolchains.Tests.Processes;

/// <summary>Reading process parents from <c>/proc</c>.</summary>
public sealed class ProcessTreeTests
{
    [Theory]
    [InlineData("1234 (python3) S 1200 1234 1234 0 -1 4194560", 1200)]
    [InlineData("77 (my (odd) name) R 5 77 77 0", 5)]
    [InlineData("88 (a b) Z 1 88", 1)]
    public void Reads_the_parent_after_the_command_name(string stat, int parent)
    {
        Assert.Equal(parent, ProcessTree.ParseParent(stat));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12 (truncated")]
    [InlineData("12 (x)")]
    public void Ignores_lines_that_are_not_stat_lines(string stat)
    {
        Assert.Null(ProcessTree.ParseParent(stat));
    }

    [Fact]
    public void Finds_no_descendants_of_a_process_that_has_none()
    {
        Assert.Empty(ProcessTree.Descendants(int.MaxValue));
    }
}
