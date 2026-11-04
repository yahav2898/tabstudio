using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>Breakpoint conditions the IDE evaluates for an adapter that ignores them, as Java's debugger does in Kotlin files.</summary>
public sealed class AdapterConditionTests
{
    private static readonly Dictionary<string, string> Locals = new(StringComparer.Ordinal)
    {
        ["i"] = "2",
        ["total"] = "3.5",
        ["name"] = "\"Ada\"",
        ["done"] = "false",
        ["$count"] = "10",
    };

    [Theory]
    [InlineData("i == 2", true)]
    [InlineData("i != 2", false)]
    [InlineData("i >= 3", false)]
    [InlineData("i < 3 && total > 3", true)]
    [InlineData("i == 5 || name == \"Ada\"", true)]
    [InlineData("name == 'Ada'", true)]
    [InlineData("done", false)]
    [InlineData("$count <= 10", true)]
    [InlineData("count == 10", true)]
    [InlineData("i == total", false)]
    public void Comparisons_of_locals_and_literals_are_evaluated(string condition, bool expected)
    {
        Assert.True(AdapterCondition.TryEvaluate(condition, Locals, out var met));
        Assert.Equal(expected, met);
    }

    [Theory]
    [InlineData("missing == 1")]
    [InlineData("name > 3")]
    [InlineData("square(i) == 4")]
    [InlineData("")]
    public void What_cannot_be_evaluated_says_so_and_the_breakpoint_stops(string condition) =>
        Assert.False(AdapterCondition.TryEvaluate(condition, Locals, out _));
}
