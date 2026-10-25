using TabStudio.Compilation.Running;

namespace TabStudio.Debugging.Tests;

public sealed class DebugSessionTests
{
    private const string Calculator = """
        var calculator = new Calculator();
        var result = calculator.Add(2, 3);
        Console.WriteLine(result);
        Console.WriteLine("done");

        class Calculator
        {
            public int Add(int a, int b)
            {
                var sum = Twice(a) + b;
                return sum;
            }

            private static int Twice(int value)
            {
                return value * 2;
            }
        }
        """;

    [Fact]
    public async Task Pauses_at_a_breakpoint_with_the_locals_in_scope()
    {
        using var program = new DebuggedProgram("""
            var name = "Ada";
            var count = 3;
            var items = new List<int> { 1, 2, 3 };
            double ratio = 0.5;
            char letter = 'x';
            Console.WriteLine($"{name} {count} {items.Count} {letter}");
            var later = 1;
            """, 6);
        program.Start();

        var pause = await program.NextPauseAsync();

        Assert.Equal(PauseReason.Breakpoint, pause.Reason);
        Assert.Equal(6, pause.Location?.Line);
        Assert.Equal(6, pause.Breakpoint?.Location?.Line);
        Assert.Equal(DebugSessionState.Paused, program.Session.State);
        Assert.Equal("\"Ada\"", DebuggedProgram.Local(pause, "name").Value);
        Assert.Equal("string", DebuggedProgram.Local(pause, "name").Type);
        Assert.Equal("3", DebuggedProgram.Local(pause, "count").Value);
        Assert.Equal("int", DebuggedProgram.Local(pause, "count").Type);
        Assert.Equal("Count = 3", DebuggedProgram.Local(pause, "items").Value);
        Assert.Equal("List<int>", DebuggedProgram.Local(pause, "items").Type);
        Assert.Equal(new[] { "1", "2", "3" }, DebuggedProgram.Local(pause, "items").Expand().Select(v => v.Value));
        Assert.Equal("0.5", DebuggedProgram.Local(pause, "ratio").Value);
        Assert.Equal("'x'", DebuggedProgram.Local(pause, "letter").Value);
        Assert.DoesNotContain(pause.Locals, local => local.Name == "later");

        Assert.True(program.Session.Continue());
        var result = await program.FinishAsync();

        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal("Ada 3 3 x\n", program.Console.Output);
        Assert.Equal(DebugSessionState.Exited, program.Session.State);
    }

    [Fact]
    public async Task Steps_into_out_of_and_over_methods()
    {
        using var program = new DebuggedProgram(Calculator, 2);
        program.Start();

        Assert.Equal(2, (await program.NextPauseAsync()).Location?.Line);

        program.Session.StepInto();
        var inAdd = await program.NextPauseAsync();
        Assert.Equal(PauseReason.Step, inAdd.Reason);
        Assert.Equal(10, inAdd.Location?.Line);
        Assert.Equal("2", DebuggedProgram.Local(inAdd, "a").Value);
        Assert.Equal("{Calculator}", DebuggedProgram.Local(inAdd, "this").Value);

        program.Session.StepInto();
        Assert.Equal(16, (await program.NextPauseAsync()).Location?.Line);

        program.Session.StepOut();
        var backInAdd = await program.NextPauseAsync();
        Assert.Equal(11, backInAdd.Location?.Line);
        Assert.Equal("7", DebuggedProgram.Local(backInAdd, "sum").Value);

        program.Session.StepOut();
        Assert.Equal(3, (await program.NextPauseAsync()).Location?.Line);

        program.Session.StepOver();
        Assert.Equal(4, (await program.NextPauseAsync()).Location?.Line);

        program.Session.Continue();
        await program.FinishAsync();
        Assert.Equal("7\ndone\n", program.Console.Output);
    }

    [Fact]
    public async Task Step_over_does_not_stop_inside_the_calls_it_runs()
    {
        using var program = new DebuggedProgram(Calculator, 2);
        program.Start();
        await program.NextPauseAsync();

        program.Session.StepOver();
        var next = await program.NextPauseAsync();

        Assert.Equal(3, next.Location?.Line);
        Assert.Equal("7", DebuggedProgram.Local(next, "result").Value);
        program.Session.Continue();
        await program.FinishAsync();
    }

    [Fact]
    public async Task Run_to_cursor_pauses_at_the_line_once()
    {
        using var program = new DebuggedProgram(Calculator);
        Assert.True(program.Session.RunToLine(DebuggedProgram.FilePath, 3));
        program.Start();

        var atStart = await program.NextPauseAsync();
        Assert.Equal(PauseReason.RunToCursor, atStart.Reason);
        Assert.Equal(3, atStart.Location?.Line);

        Assert.False(program.Session.RunToLine(DebuggedProgram.FilePath, 400));
        Assert.True(program.Session.RunToLine(DebuggedProgram.FilePath, 4));
        var next = await program.NextPauseAsync();
        Assert.Equal(PauseReason.RunToCursor, next.Reason);
        Assert.Equal(4, next.Location?.Line);

        program.Session.Continue();
        var result = await program.FinishAsync();
        Assert.Equal(RunOutcome.Completed, result.Outcome);
    }

    [Fact]
    public async Task Pausing_before_the_start_stops_at_the_first_statement()
    {
        using var program = new DebuggedProgram(Calculator);
        program.Session.Pause();
        program.Start();

        var pause = await program.NextPauseAsync();

        Assert.Equal(PauseReason.Pause, pause.Reason);
        Assert.Equal(1, pause.Location?.Line);
        Assert.Equal("<top-level statements>", Assert.Single(pause.CallStack).MethodName);
        program.Session.Continue();
        await program.FinishAsync();
    }

    [Fact]
    public async Task Shows_the_call_stack_across_methods()
    {
        using var program = new DebuggedProgram(Calculator, 16);
        program.Start();

        var pause = await program.NextPauseAsync();

        Assert.Equal(
            new[] { "Calculator.Twice(int)", "Calculator.Add(int, int)", "<top-level statements>" },
            pause.CallStack.Select(f => f.MethodName));
        Assert.Equal(new[] { 16, 10, 2 }, pause.CallStack.Select(f => f.Location?.Line ?? 0));
        Assert.Equal("2", DebuggedProgram.Local(pause, "value").Value);
        program.Session.Continue();
        await program.FinishAsync();
    }

    [Fact]
    public async Task Hits_a_breakpoint_in_a_loop_on_every_iteration()
    {
        using var program = new DebuggedProgram("""
            var total = 0;
            for (var i = 0; i < 3; i++)
            {
                total += i;
            }
            Console.WriteLine(total);
            """, 4);
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
        using var program = new DebuggedProgram("""
            var count = 0;
            while (count < 3)
            {
                count++;
            }
            Console.WriteLine(count);
            """, 2);
        program.Start();

        var seen = new List<string>();
        for (var hit = 0; hit < 4; hit++)
        {
            var pause = await program.NextPauseAsync();
            Assert.Equal(2, pause.Location?.Line);
            seen.Add(DebuggedProgram.Local(pause, "count").Value);
            program.Session.Continue();
        }

        await program.FinishAsync();
        Assert.Equal(new[] { "0", "1", "2", "3" }, seen);
        Assert.Equal("3\n", program.Console.Output);
    }

    [Fact]
    public async Task Stepping_over_the_end_of_a_loop_body_returns_to_its_condition()
    {
        using var program = new DebuggedProgram("""
            var total = 0;
            for (var i = 0; i < 2; i++)
            {
                total += i;
            }
            Console.WriteLine(total);
            """, 4);
        program.Start();

        var lines = new List<int>();
        var pause = await program.NextPauseAsync();
        lines.Add(pause.Location?.Line ?? 0);
        for (var step = 0; step < 4; step++)
        {
            program.Session.StepOver();
            pause = await program.NextPauseAsync();
            lines.Add(pause.Location?.Line ?? 0);
        }

        Assert.Equal(new[] { 4, 2, 4, 2, 6 }, lines);
        Assert.Equal("1", DebuggedProgram.Local(pause, "total").Value);
        Assert.DoesNotContain(pause.Locals, local => local.Name == "i");
        program.Session.Continue();
        await program.FinishAsync();
        Assert.Equal("1\n", program.Console.Output);
    }

    [Fact]
    public async Task Debugs_an_async_main_across_awaits()
    {
        using var program = new DebuggedProgram("""
            Console.WriteLine("start");
            await Task.Delay(10);
            var value = await ComputeAsync(20);
            Console.WriteLine(value);

            static async Task<int> ComputeAsync(int input)
            {
                await Task.Yield();
                return input + 1;
            }
            """, 2, 9);
        program.Start();

        Assert.Equal(2, (await program.NextPauseAsync()).Location?.Line);

        program.Session.StepOver();
        Assert.Equal(3, (await program.NextPauseAsync()).Location?.Line);

        program.Session.Continue();
        var inside = await program.NextPauseAsync();
        Assert.Equal(9, inside.Location?.Line);
        Assert.Equal("20", DebuggedProgram.Local(inside, "input").Value);
        Assert.Equal(new[] { "ComputeAsync(int)", "<top-level statements>" }, inside.CallStack.Select(f => f.MethodName));
        Assert.Equal(3, inside.CallStack[1].Location?.Line);

        program.Session.Continue();
        await program.FinishAsync();
        Assert.Equal("start\n21\n", program.Console.Output);
    }

    [Fact]
    public async Task Breaks_on_an_unhandled_exception_at_the_line_that_threw()
    {
        using var program = new DebuggedProgram("""
            Console.WriteLine("before");
            Divide(10, 0);
            Console.WriteLine("after");

            static int Divide(int a, int b)
            {
                var half = a / 2;
                return a / b + half;
            }
            """);
        program.Start();

        var pause = await program.NextPauseAsync();

        Assert.Equal(PauseReason.Exception, pause.Reason);
        Assert.IsType<DivideByZeroException>(pause.Exception);
        Assert.Equal(8, pause.Location?.Line);
        Assert.Equal(new[] { "Divide(int, int)", "<top-level statements>" }, pause.CallStack.Select(f => f.MethodName));
        Assert.Equal(2, pause.CallStack[1].Location?.Line);
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
        using var program = new DebuggedProgram("""
            try
            {
                throw new InvalidOperationException("handled");
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine(e.Message);
            }
            """);
        program.Start();

        var result = await program.FinishAsync();

        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal("handled\n", program.Console.Output);
    }

    [Fact]
    public async Task Pause_breaks_into_a_running_loop_and_stop_ends_the_session()
    {
        using var program = new DebuggedProgram("""
            var count = 0;
            Console.WriteLine("looping");
            while (true)
            {
                count++;
            }
            """);
        var exited = new TaskCompletionSource<RunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        program.Session.Exited += (_, e) => exited.TrySetResult(e.Result);
        program.Start();
        await program.WaitForOutputAsync("looping");

        program.Session.Pause();
        var pause = await program.NextPauseAsync();
        Assert.Equal(PauseReason.Pause, pause.Reason);
        Assert.Contains(pause.Location?.Line ?? 0, new[] { 3, 5 });

        program.Session.Stop();
        var result = await program.FinishAsync();

        Assert.Equal(RunOutcome.Stopped, result.Outcome);
        Assert.Equal(RunOutcome.Stopped, (await exited.Task.WaitAsync(DebuggedProgram.Timeout)).Outcome);
        Assert.Equal(DebugSessionState.Exited, program.Session.State);
        Assert.False(program.Session.Continue());
    }

    [Fact]
    public void Breakpoints_bind_to_the_next_statement_or_stay_unbound()
    {
        using var program = new DebuggedProgram("""
            var total = 0;

            for (var i = 0; i < 3; i++)
            {
                total += i;
            }
            """);

        var onBlankLine = program.Session.SetBreakpoint(DebuggedProgram.FilePath, 2);
        var onBrace = program.Session.SetBreakpoint(DebuggedProgram.FilePath, 4);
        var pastTheEnd = program.Session.SetBreakpoint(DebuggedProgram.FilePath, 40);

        Assert.True(onBlankLine.IsBound);
        Assert.Equal(3, onBlankLine.Location?.Line);
        Assert.Equal(5, onBrace.Location?.Line);
        Assert.False(pastTheEnd.IsBound);
        Assert.Same(onBrace, program.Session.SetBreakpoint(DebuggedProgram.FilePath, 4));
        Assert.True(program.Session.RemoveBreakpoint(DebuggedProgram.FilePath, 4));
        Assert.Equal(2, program.Session.Breakpoints.Count);
    }

    [Fact]
    public async Task A_breakpoint_set_while_running_is_hit()
    {
        using var input = new ConsoleInput();
        using var program = new DebuggedProgram("""
            var line = Console.ReadLine();
            Console.WriteLine(line);
            """);
        input.WaitingForInput += (_, _) =>
        {
            program.Session.SetBreakpoint(DebuggedProgram.FilePath, 2);
            input.SubmitLine("typed");
        };
        program.Start(input);

        var pause = await program.NextPauseAsync();

        Assert.Equal(2, pause.Location?.Line);
        Assert.Equal("\"typed\"", DebuggedProgram.Local(pause, "line").Value);
        program.Session.Continue();
        await program.FinishAsync();
    }
}
