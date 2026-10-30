using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;

namespace TabStudio.Debugging.Tests;

/// <summary>The C# debugger's features in Visual Basic programs: breakpoints, stepping, the call stack, Locals and exceptions.</summary>
public sealed class VisualBasicDebugSessionTests
{
    private const string Calculator = """
        Module Program
            Sub Main()
                Dim calc As New Calculator()
                Dim result = calc.Add(2, 3)
                Console.WriteLine(result)
                Console.WriteLine("done")
            End Sub
        End Module

        Class Calculator
            Public Function Add(a As Integer, b As Integer) As Integer
                Dim sum = Twice(a) + b
                Return sum
            End Function

            Private Shared Function Twice(value As Integer) As Integer
                Return value * 2
            End Function
        End Class
        """;

    [Fact]
    public async Task Pauses_at_a_breakpoint_with_the_locals_in_scope_shown_as_visual_basic()
    {
        using var program = DebuggedVisualBasicProgram.Create("""
            Module Program
                Sub Main()
                    Dim name = "Ada"
                    Dim count = 3
                    Dim items As New List(Of Integer) From {1, 2, 3}
                    Dim ratio As Double = 0.5
                    Dim letter = "x"c
                    Dim flag = True
                    Dim missing As String = Nothing
                    Console.WriteLine($"{name} {count} {items.Count} {letter}")
                    Dim later = 1
                End Sub
            End Module
            """, 10);
        program.Start();

        var pause = await program.NextPauseAsync();

        Assert.Equal(PauseReason.Breakpoint, pause.Reason);
        Assert.Equal(10, pause.Location?.Line);
        Assert.Equal(DebuggedVisualBasicProgram.FilePath, pause.Location?.FilePath);
        Assert.Equal(ProjectLanguage.VisualBasic, program.Session.Language);
        Assert.Equal("\"Ada\"", DebuggedProgram.Local(pause, "name").Value);
        Assert.Equal("String", DebuggedProgram.Local(pause, "name").Type);
        Assert.Equal("3", DebuggedProgram.Local(pause, "count").Value);
        Assert.Equal("Integer", DebuggedProgram.Local(pause, "count").Type);
        Assert.Equal("Count = 3", DebuggedProgram.Local(pause, "items").Value);
        Assert.Equal("List(Of Integer)", DebuggedProgram.Local(pause, "items").Type);
        Assert.Equal(new[] { "(0)", "(1)", "(2)" }, DebuggedProgram.Local(pause, "items").Expand().Select(v => v.Name));
        Assert.Equal(new[] { "1", "2", "3" }, DebuggedProgram.Local(pause, "items").Expand().Select(v => v.Value));
        Assert.Equal("0.5", DebuggedProgram.Local(pause, "ratio").Value);
        Assert.Equal("\"x\"c", DebuggedProgram.Local(pause, "letter").Value);
        Assert.Equal("True", DebuggedProgram.Local(pause, "flag").Value);
        Assert.Equal("Boolean", DebuggedProgram.Local(pause, "flag").Type);
        Assert.Equal("Nothing", DebuggedProgram.Local(pause, "missing").Value);
        Assert.Equal("String", DebuggedProgram.Local(pause, "missing").Type);
        Assert.DoesNotContain(pause.Locals, local => local.Name == "later");

        // DataTips read Visual Basic paths: names in any case and indexes in parentheses.
        Assert.Equal("2", pause.FindValue("ITEMS(1)")?.Value);
        Assert.Equal("\"Ada\"", pause.FindValue("Name")?.Value);

        Assert.True(program.Session.Continue());
        var result = await program.FinishAsync();

        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal("Ada 3 3 x\n", program.Console.Output);
    }

    [Fact]
    public async Task Steps_into_out_of_and_over_functions()
    {
        using var program = DebuggedVisualBasicProgram.Create(Calculator, 4);
        program.Start();

        Assert.Equal(4, (await program.NextPauseAsync()).Location?.Line);

        program.Session.StepInto();
        var inAdd = await program.NextPauseAsync();
        Assert.Equal(PauseReason.Step, inAdd.Reason);
        Assert.Equal(12, inAdd.Location?.Line);
        Assert.Equal("2", DebuggedProgram.Local(inAdd, "a").Value);
        Assert.Equal("{Calculator}", DebuggedProgram.Local(inAdd, "Me").Value);
        Assert.Equal("Calculator", DebuggedProgram.Local(inAdd, "Me").Type);

        program.Session.StepInto();
        Assert.Equal(17, (await program.NextPauseAsync()).Location?.Line);

        program.Session.StepOut();
        var backInAdd = await program.NextPauseAsync();
        Assert.Equal(13, backInAdd.Location?.Line);
        Assert.Equal("7", DebuggedProgram.Local(backInAdd, "sum").Value);

        program.Session.StepOut();
        Assert.Equal(5, (await program.NextPauseAsync()).Location?.Line);

        program.Session.StepOver();
        Assert.Equal(6, (await program.NextPauseAsync()).Location?.Line);

        program.Session.Continue();
        await program.FinishAsync();
        Assert.Equal("7\ndone\n", program.Console.Output);
    }

    [Fact]
    public async Task Step_over_does_not_stop_inside_the_calls_it_runs()
    {
        using var program = DebuggedVisualBasicProgram.Create(Calculator, 4);
        program.Start();
        await program.NextPauseAsync();

        program.Session.StepOver();
        var next = await program.NextPauseAsync();

        Assert.Equal(5, next.Location?.Line);
        Assert.Equal("7", DebuggedProgram.Local(next, "result").Value);
        program.Session.Continue();
        await program.FinishAsync();
    }

    [Fact]
    public async Task Shows_the_call_stack_across_functions()
    {
        using var program = DebuggedVisualBasicProgram.Create(Calculator, 17);
        program.Start();

        var pause = await program.NextPauseAsync();

        Assert.Equal(
            new[] { "Calculator.Twice(Integer)", "Calculator.Add(Integer, Integer)", "Program.Main()" },
            pause.CallStack.Select(f => f.MethodName));
        Assert.Equal(new[] { 17, 12, 4 }, pause.CallStack.Select(f => f.Location?.Line ?? 0));
        Assert.Equal("2", DebuggedProgram.Local(pause, "value").Value);
        program.Session.Continue();
        await program.FinishAsync();
    }

    [Fact]
    public async Task Run_to_cursor_and_pause_before_the_start_stop_at_their_lines()
    {
        using var program = DebuggedVisualBasicProgram.Create(Calculator);
        program.Session.Pause();
        program.Start();

        var first = await program.NextPauseAsync();
        Assert.Equal(PauseReason.Pause, first.Reason);
        Assert.Equal(3, first.Location?.Line);
        Assert.Equal("Program.Main()", Assert.Single(first.CallStack).MethodName);

        Assert.True(program.Session.RunToLine(DebuggedVisualBasicProgram.FilePath, 6));
        var atCursor = await program.NextPauseAsync();
        Assert.Equal(PauseReason.RunToCursor, atCursor.Reason);
        Assert.Equal(6, atCursor.Location?.Line);

        program.Session.Continue();
        var result = await program.FinishAsync();
        Assert.Equal(RunOutcome.Completed, result.Outcome);
    }

    [Fact]
    public async Task Hits_a_breakpoint_in_a_loop_on_every_iteration()
    {
        using var program = DebuggedVisualBasicProgram.Create("""
            Module Program
                Sub Main()
                    Dim total = 0
                    For i = 0 To 2
                        total += i
                    Next
                    Console.WriteLine(total)
                End Sub
            End Module
            """, 5);
        program.Start();

        var seen = new List<string>();
        for (var hit = 0; hit < 3; hit++)
        {
            var pause = await program.NextPauseAsync();
            seen.Add(DebuggedProgram.Local(pause, "i").Value + "/" + DebuggedProgram.Local(pause, "total").Value);
            program.Session.Continue();
        }

        var result = await program.FinishAsync();
        Assert.Equal(new[] { "0/0", "1/0", "2/1" }, seen);
        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal("3\n", program.Console.Output);
    }

    [Fact]
    public async Task A_breakpoint_on_a_while_condition_hits_on_every_check()
    {
        using var program = DebuggedVisualBasicProgram.Create("""
            Module Program
                Sub Main()
                    Dim count = 0
                    While count < 2
                        count += 1
                    End While
                    Console.WriteLine(count)
                End Sub
            End Module
            """, 4);
        program.Start();

        var counts = new List<string>();
        for (var hit = 0; hit < 3; hit++)
        {
            var pause = await program.NextPauseAsync();
            Assert.Equal(4, pause.Location?.Line);
            counts.Add(DebuggedProgram.Local(pause, "count").Value);
            program.Session.Continue();
        }

        await program.FinishAsync();
        Assert.Equal(new[] { "0", "1", "2" }, counts);
        Assert.Equal("2\n", program.Console.Output);
    }

    [Fact]
    public async Task Breaks_on_an_unhandled_exception_at_the_line_that_threw()
    {
        using var program = DebuggedVisualBasicProgram.Create("""
            Module Program
                Sub Main()
                    Console.WriteLine("before")
                    Divide(10, 0)
                    Console.WriteLine("after")
                End Sub

                Function Divide(a As Integer, b As Integer) As Integer
                    Dim half = a \ 2
                    Return a \ b + half
                End Function
            End Module
            """);
        program.Start();

        var pause = await program.NextPauseAsync();

        Assert.Equal(PauseReason.Exception, pause.Reason);
        Assert.IsType<DivideByZeroException>(pause.Exception);
        Assert.Equal(10, pause.Location?.Line);
        Assert.Equal(new[] { "Program.Divide(Integer, Integer)", "Program.Main()" }, pause.CallStack.Select(f => f.MethodName));
        Assert.Equal(4, pause.CallStack[1].Location?.Line);
        Assert.Equal("10", DebuggedProgram.Local(pause, "a").Value);
        Assert.Equal("0", DebuggedProgram.Local(pause, "b").Value);

        program.Session.Continue();
        var result = await program.FinishAsync();
        Assert.Equal(RunOutcome.Crashed, result.Outcome);
        Assert.IsType<DivideByZeroException>(result.Exception);
        Assert.Equal("before\n", program.Console.Output);
    }

    [Fact]
    public async Task Does_not_break_on_exceptions_the_program_handles()
    {
        using var program = DebuggedVisualBasicProgram.Create("""
            Module Program
                Sub Main()
                    Try
                        Throw New InvalidOperationException("handled")
                    Catch e As InvalidOperationException
                        Console.WriteLine(e.Message)
                    End Try
                End Sub
            End Module
            """);
        program.Start();

        var result = await program.FinishAsync();

        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal("handled\n", program.Console.Output);
    }

    [Fact]
    public async Task A_conditional_breakpoint_stops_only_when_its_visual_basic_condition_holds()
    {
        using var program = DebuggedVisualBasicProgram.Create("""
            Module Program
                Sub Main()
                    Dim total = 0
                    For i = 0 To 4
                        total += i
                    Next
                    Console.WriteLine(total)
                End Sub
            End Module
            """);
        var breakpoint = program.Session.SetBreakpoint(DebuggedVisualBasicProgram.FilePath, 5, "i >= 3 AndAlso Total > 2");
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
    public async Task Debugs_lambdas_iterators_and_property_accessors()
    {
        var source = """
            Module Program
                Sub Main()
                    Dim twice As Func(Of Integer, Integer) = Function(x As Integer)
                                                                 Dim y = x * 2
                                                                 Return y
                                                             End Function
                    Console.WriteLine(twice(4))
                    Console.WriteLine(String.Join(",", Up(2)))
                    Dim holder As New Crate()
                    holder.Doubled = 6
                    Console.WriteLine(holder.Value)
                End Sub

                Iterator Function Up(count As Integer) As IEnumerable(Of Integer)
                    For i = 1 To count
                        Yield i
                    Next
                End Function
            End Module

            Class Crate
                Public Property Value As Integer

                Public Property Doubled As Integer
                    Get
                        Return Value * 2
                    End Get
                    Set(value As Integer)
                        Me.Value = value \ 2
                    End Set
                End Property
            End Class
            """;
        using var program = DebuggedVisualBasicProgram.Create(source, 5, 16, 29);
        program.Start();

        var inLambda = await program.NextPauseAsync();
        Assert.Equal(5, inLambda.Location?.Line);
        Assert.Equal("4", DebuggedProgram.Local(inLambda, "x").Value);
        Assert.Equal("8", DebuggedProgram.Local(inLambda, "y").Value);
        Assert.Equal("Program.Main.<lambda>(Integer)", inLambda.CallStack[0].MethodName);
        Assert.Equal("Program.Main()", inLambda.CallStack[1].MethodName);
        program.Session.Continue();

        var inIterator = await program.NextPauseAsync();
        Assert.Equal(16, inIterator.Location?.Line);
        Assert.Equal("1", DebuggedProgram.Local(inIterator, "i").Value);
        Assert.Equal("Program.Up(Integer)", inIterator.CallStack[0].MethodName);
        program.Session.RemoveBreakpoint(DebuggedVisualBasicProgram.FilePath, 16);
        program.Session.Continue();

        var inSetter = await program.NextPauseAsync();
        Assert.Equal(29, inSetter.Location?.Line);
        Assert.Equal("Crate.Doubled.Set(Integer)", inSetter.CallStack[0].MethodName);
        Assert.Equal("6", DebuggedProgram.Local(inSetter, "value").Value);
        Assert.Equal("Crate", DebuggedProgram.Local(inSetter, "Me").Type);
        program.Session.Continue();

        var result = await program.FinishAsync();
        Assert.True(result.Outcome == RunOutcome.Completed, program.Console.Error + result.Exception);
        Assert.Equal("8\n1,2\n3\n", program.Console.Output);
    }

    [Fact]
    public void Recording_reports_that_visual_basic_records_steps_only()
    {
        using var program = DebuggedVisualBasicProgram.Create(Calculator);

        Assert.Equal(DebugSession.VisualBasicRecordingLimitation, program.Session.RecordingLimitation);
        using var csharp = new DebuggedProgram("Console.WriteLine(1);");
        Assert.Null(csharp.Session.RecordingLimitation);
    }
}
