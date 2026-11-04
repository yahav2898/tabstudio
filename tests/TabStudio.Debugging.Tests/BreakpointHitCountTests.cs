namespace TabStudio.Debugging.Tests;

/// <summary>Hit counts on C# breakpoints, as Visual Studio's breakpoint settings offer them: =, &gt;= and a multiple of.</summary>
public sealed class BreakpointHitCountTests
{
    private const string Loop = """
        var total = 0;
        for (var i = 0; i < 6; i++)
        {
            total += i;
        }
        Console.WriteLine(total);
        """;

    [Theory]
    [InlineData("5", BreakpointHitKind.Equal, 5)]
    [InlineData("= 5", BreakpointHitKind.Equal, 5)]
    [InlineData("==5", BreakpointHitKind.Equal, 5)]
    [InlineData(" >= 10 ", BreakpointHitKind.AtLeast, 10)]
    [InlineData("% 2", BreakpointHitKind.MultipleOf, 2)]
    [InlineData("multiple of 3", BreakpointHitKind.MultipleOf, 3)]
    public void Hit_counts_read_as_Visual_Studio_writes_them(string text, BreakpointHitKind kind, int count)
    {
        Assert.True(BreakpointHitCondition.TryParse(text, out var condition));
        Assert.Equal(new BreakpointHitCondition(kind, count), condition);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("> 5")]
    [InlineData("five")]
    [InlineData("%")]
    public void What_is_not_a_hit_count_is_refused(string text)
    {
        Assert.False(BreakpointHitCondition.TryParse(text, out var condition));
        Assert.Null(condition);
    }

    [Fact]
    public void A_blank_hit_count_is_none()
    {
        Assert.True(BreakpointHitCondition.TryParse("  ", out var condition));
        Assert.Null(condition);
    }

    [Theory]
    [InlineData("= 3", new[] { 2 })]
    [InlineData(">= 5", new[] { 4, 5 })]
    [InlineData("% 2", new[] { 1, 3, 5 })]
    public async Task A_breakpoint_with_a_hit_count_stops_only_on_those_hits(string hitCount, int[] stopsAt)
    {
        using var program = new DebuggedProgram(Loop);
        var breakpoint = program.Session.SetBreakpoint(DebuggedProgram.FilePath, 4, hitCondition: BreakpointHitCondition.Parse(hitCount));
        program.Start();

        foreach (var expected in stopsAt)
        {
            var pause = await program.NextPauseAsync();
            Assert.Equal(PauseReason.Breakpoint, pause.Reason);
            Assert.Same(breakpoint, pause.Breakpoint);
            Assert.Equal(expected.ToString(System.Globalization.CultureInfo.InvariantCulture), DebuggedProgram.Local(pause, "i").Value);
            program.Session.Continue();
        }

        await program.FinishAsync();
        Assert.Equal(6, breakpoint.HitCount);
        Assert.Equal("15\n", program.Console.Output);
    }

    [Fact]
    public async Task A_hit_count_counts_only_the_hits_where_the_condition_holds()
    {
        using var program = new DebuggedProgram(Loop);
        var breakpoint = program.Session.SetBreakpoint(DebuggedProgram.FilePath, 4, "i >= 2", BreakpointHitCondition.Parse("= 2"));
        program.Start();

        var pause = await program.NextPauseAsync();
        Assert.Equal("3", DebuggedProgram.Local(pause, "i").Value);
        program.Session.Continue();

        await program.FinishAsync();
        Assert.Equal(4, breakpoint.HitCount);
    }

    [Fact]
    public void A_new_hit_count_replaces_the_breakpoint_on_the_line_and_the_same_one_keeps_it()
    {
        using var program = new DebuggedProgram("var x = 1;\nConsole.WriteLine(x);");

        var plain = program.Session.SetBreakpoint(DebuggedProgram.FilePath, 2);
        var counted = program.Session.SetBreakpoint(DebuggedProgram.FilePath, 2, hitCondition: BreakpointHitCondition.Parse(">= 2"));

        Assert.NotSame(plain, counted);
        Assert.Equal(new BreakpointHitCondition(BreakpointHitKind.AtLeast, 2), Assert.Single(program.Session.Breakpoints).HitCondition);
        Assert.Same(counted, program.Session.SetBreakpoint(DebuggedProgram.FilePath, 2, hitCondition: BreakpointHitCondition.Parse(">=2")));
    }
}
