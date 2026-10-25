using TabStudio.Debugging.Inspection;

namespace TabStudio.Debugging.Tests;

public sealed class BreakpointConditionTests
{
    private readonly ValueInspector _inspector = new(new Evaluator(TimeSpan.FromSeconds(5)));

    [Theory]
    [InlineData("count == 3", true)]
    [InlineData("count != 3", false)]
    [InlineData("count >= 3 && name == \"Ada\"", true)]
    [InlineData("count > 5 || flag", true)]
    [InlineData("nothing == null", true)]
    [InlineData("name != null", true)]
    [InlineData("day == Friday", true)]
    [InlineData("day == DayOfWeek.Monday", false)]
    [InlineData("ratio < 1", true)]
    [InlineData("letter == 'x'", true)]
    [InlineData("flag", true)]
    [InlineData("name == \"a && b\"", false)]
    public void Evaluates_comparisons_of_variables_and_literals(string condition, bool expected)
    {
        Assert.Equal(expected, BreakpointCondition.IsMet(condition, Locals(), out _));
    }

    [Theory]
    [InlineData("missing > 1")]
    [InlineData("name")]
    [InlineData("name < 3")]
    public void A_condition_that_cannot_be_evaluated_stops_and_says_why(string condition)
    {
        Assert.True(BreakpointCondition.IsMet(condition, Locals(), out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public async Task A_conditional_breakpoint_stops_only_when_its_condition_holds()
    {
        using var program = new DebuggedProgram("""
            var total = 0;
            for (var i = 0; i < 5; i++)
            {
                total += i;
            }
            Console.WriteLine(total);
            """);
        var breakpoint = program.Session.SetBreakpoint(DebuggedProgram.FilePath, 4, "i >= 3 && total > 2");
        program.Start();

        var first = await program.NextPauseAsync();
        Assert.Equal("3", DebuggedProgram.Local(first, "i").Value);
        Assert.Same(breakpoint, first.Breakpoint);
        Assert.Null(first.ConditionError);
        program.Session.Continue();

        var second = await program.NextPauseAsync();
        Assert.Equal("4", DebuggedProgram.Local(second, "i").Value);
        program.Session.Continue();

        await program.FinishAsync();
        Assert.Equal(2, breakpoint.HitCount);
        Assert.Equal("10\n", program.Console.Output);
    }

    [Fact]
    public void A_new_condition_replaces_the_breakpoint_on_the_line()
    {
        using var program = new DebuggedProgram("var x = 1;\nConsole.WriteLine(x);");

        var plain = program.Session.SetBreakpoint(DebuggedProgram.FilePath, 2);
        var conditional = program.Session.SetBreakpoint(DebuggedProgram.FilePath, 2, "x == 1");

        Assert.NotSame(plain, conditional);
        Assert.Equal("x == 1", Assert.Single(program.Session.Breakpoints).Condition);
        Assert.Same(conditional, program.Session.SetBreakpoint(DebuggedProgram.FilePath, 2, " x == 1 "));
    }

    private DebugValue[] Locals() =>
    [
        new("count", "int", 3, _inspector),
        new("name", "string", "Ada", _inspector),
        new("nothing", "object", null, _inspector),
        new("day", "DayOfWeek", DayOfWeek.Friday, _inspector),
        new("ratio", "double", 0.5, _inspector),
        new("letter", "char", 'x', _inspector),
        new("flag", "bool", true, _inspector),
    ];
}
