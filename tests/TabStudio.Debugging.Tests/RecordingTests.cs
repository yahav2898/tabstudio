using TabStudio.Compilation.Running;
using TabStudio.Debugging.Recording;
using Xunit.Abstractions;

namespace TabStudio.Debugging.Tests;

public sealed class RecordingTests(ITestOutputHelper output)
{
    private static DebugSession Recorded(string source, int capacity = 1024 * 1024) =>
        new(DebuggedProgram.Compile(source), new RecordingOptions { CapacityBytes = capacity });

    private static async Task RunAsync(DebugSession session, RunOutcome expected = RunOutcome.Completed)
    {
        session.BreakOnUnhandledExceptions = false;
        var result = await session.RunAsync([], new CapturingConsole()).WaitAsync(DebuggedProgram.Timeout);
        Assert.True(result.Outcome == expected, $"Expected {expected}, got {result.Outcome}: {result.Exception}");
    }

    private static IEnumerable<RecordedStep> Steps(DebugSession session) =>
        Enumerable.Range(0, session.Recording!.Count).Select(i => session.Recording.GetStep(i)!);

    [Fact]
    public async Task Recording_is_opt_in_and_a_disposed_recording_remains_navigable()
    {
        using var ordinary = new DebugSession(DebuggedProgram.Compile("int x = 1; x++;"));
        Assert.Null(ordinary.Recording);
        await RunAsync(ordinary);
        Assert.Null(ordinary.StepBack());

        var recorded = Recorded("int x = 1; x++; x = 3;");
        await RunAsync(recorded);
        recorded.Dispose();
        Assert.True(recorded.StepBack()!.IsHistorical);
        Assert.Equal("2", recorded.HistoricalPause!.FindValue("x")!.Value);
    }

    [Fact]
    public async Task Loop_records_every_sequence_point_and_same_value_writes_with_original_lines()
    {
        const string source = """
            int total = 0;
            for (int i = 0; i < 3; i++)
            {
                total += i;
            }
            total = 3;
            Console.WriteLine(total);
            """;
        using var session = Recorded(source);
        await RunAsync(session);
        var steps = Steps(session).ToArray();
        var writes = steps.Where(s => s.WrittenPath == "total").ToArray();
        Assert.Equal(new[] { "0", "0", "1", "3", "3" }, writes.Select(w => w.Frames[0].Locals.Single(l => l.Name == "total").Value));
        Assert.Equal(new[] { 1, 4, 4, 4, 6 }, writes.Select(s => s.Location!.Line));
        Assert.All(steps.Where(s => s.WrittenPath == "i"), s => Assert.Equal(2, s.Location!.Line));
        Assert.All(steps, s => Assert.True(s.ThreadId > 0));
        Assert.All(steps, s => Assert.All(s.Frames[0].Locals, v => Assert.True(v.IsHistorical)));
        var lastWrite = session.JumpToLastWrite("total")!;
        Assert.Equal(6, lastWrite.Location!.Line);
        Assert.Equal("3", lastWrite.FindValue("total")!.Value);
        Assert.Equal(4, session.JumpToLastWrite("total")!.Location!.Line);
    }

    [Fact]
    public async Task Shallow_objects_have_stable_identity_and_primitive_fields_without_getters_or_ToString()
    {
        using var session = Recorded("""
            var box = new Box();
            var alias = box;
            box.Number = 2;
            box.Number++;
            Console.WriteLine(box.Number);
            class Box
            {
                public int Number = 1;
                public string Text = "saved";
                public int Evil => throw new Exception("must not run");
                public override string ToString() => throw new Exception("must not run");
            }
            """);
        await RunAsync(session);
        var first = Steps(session).First(s => s.WrittenPath == "alias");
        var box = first.Frames[0].Locals.Single(l => l.Name == "box");
        Assert.Equal(box.Value, first.Frames[0].Locals.Single(l => l.Name == "alias").Value);
        Assert.Equal("1", box.Expand().Single(v => v.Name == "Number").Value);
        Assert.DoesNotContain(box.Expand(), v => v.Name == "Evil");
        var write = Steps(session).Last(s => s.WrittenPath == "box.Number");
        Assert.Equal("3", write.Frames[0].Locals.Single(l => l.Name == "box").Expand().Single(v => v.Name == "Number").Value);
        Assert.Equal("3", write.Frames[0].Locals.Single(l => l.Name == "alias").Expand().Single(v => v.Name == "Number").Value);
        Assert.Equal("1", box.Expand().Single(v => v.Name == "Number").Value);
        Assert.Equal(4, session.JumpToLastWrite("alias.Number")!.Location!.Line);
    }

    [Fact]
    public async Task Instance_fields_parameters_and_static_fields_are_recorded()
    {
        using var session = Recorded("""
            new Counter().Run(4);
            class Counter
            {
                int number;
                static int count;
                public void Run(int parameter)
                {
                    parameter = 5;
                    number = parameter;
                    count = 7;
                    Console.WriteLine(number + count);
                }
            }
            """);
        await RunAsync(session);
        Assert.Contains(Steps(session), s => s.WrittenPath == "parameter" && s.Frames[0].Locals.Any(v => v.Name == "parameter" && v.Value == "5"));
        Assert.Contains(Steps(session), s => s.WrittenPath == "this.number" && s.Frames[0].Locals.Single(v => v.Name == "this").Expand().Any(v => v.Name == "number" && v.Value == "5"));
        Assert.Contains(Steps(session), s => s.WrittenPath == "count" && s.Frames[0].Locals.Any(v => v.Name == "count" && v.Value == "7"));
    }

    [Fact]
    public async Task A_static_field_write_does_not_overwrite_a_same_named_local_in_history()
    {
        using var session = Recorded("""
            class Counter
            {
                static int count;
                static void Main()
                {
                    int count = 3;
                    Counter.count = 7;
                    Console.WriteLine(count + Counter.count);
                }
            }
            """);
        await RunAsync(session);
        var write = Steps(session).Single(s => s.WrittenPath == "Counter.count");
        Assert.Equal("3", write.Frames[0].Locals.Single(v => v.Name == "count").Value);
        Assert.Equal("7", write.Frames[0].Locals.Single(v => v.Name == "Counter.count").Value);
        Assert.Equal("3", session.SeekHistory(session.Recording!.Count - 1)!.FindValue("count")!.Value);
        Assert.Equal("7", session.HistoricalPause!.FindValue("Counter.count")!.Value);
    }

    [Fact]
    public async Task Recording_does_not_touch_unrelated_static_imports()
    {
        using var session = Recorded("""
            using static Other;
            Console.WriteLine("No unrelated initialization");
            static class Other
            {
                public static int Unused = Fail();
                static int Fail() => throw new Exception("Recording initialized an unrelated type");
            }
            """);
        await RunAsync(session);
    }

    [Fact]
    public async Task Binary_ring_wraps_on_complete_records_and_oldest_retained_state_is_self_contained()
    {
        using var session = Recorded("int x = 0; for (int i = 0; i < 200; i++) { x = i; } Console.WriteLine(x);", 1024);
        await RunAsync(session);
        var recording = session.Recording!;
        Assert.True(recording.EvictedSteps > 0);
        Assert.InRange(recording.UsedBytes, 1, recording.CapacityBytes);
        Assert.Equal(-1, recording.IndexOf(1));
        var first = session.SeekHistory(0)!;
        Assert.True(first.RecordedStep > 1);
        Assert.NotNull(first.FindValue("x"));
        Assert.Equal(0, recording.IndexOf(first.RecordedStep));
        Assert.Null(session.StepBackInto());
        Assert.Equal("199", session.SeekHistory(recording.Count - 1)!.FindValue("x")!.Value);
    }

    [Fact]
    public async Task Full_strings_are_preserved_by_value_and_oversize_events_are_counted()
    {
        using var session = Recorded("string text = new string('x', 2000); Console.WriteLine(text.Length);");
        await RunAsync(session);
        Assert.Equal(2002, session.SeekHistory(session.Recording!.Count - 1)!.FindValue("text")!.Value.Length);
        using var small = Recorded("string text = new string('x', 2000); Console.WriteLine(text.Length);", 1024);
        await RunAsync(small);
        Assert.True(small.Recording!.DroppedSteps > 0);
    }

    [Fact]
    public async Task Division_crash_reverse_continues_to_the_faulty_write_rather_than_an_unrelated_assignment()
    {
        using var session = Recorded("""
            int divisor = 2;
            divisor = 0;
            int unrelated = 99;
            int result = 10 / divisor;
            Console.WriteLine(result + unrelated);
            """);
        await RunAsync(session, RunOutcome.Crashed);
        var cause = session.Recording!.ExceptionCause;
        Assert.NotNull(cause);
        var historical = session.ReverseContinue()!;
        Assert.Equal(cause, historical.RecordedStep);
        Assert.Equal(2, historical.Location!.Line);
        Assert.Equal("0", historical.FindValue("divisor")!.Value);
    }

    [Fact]
    public async Task Unsupported_exception_does_not_guess_a_cause()
    {
        using var session = Recorded("int x = 7; throw new InvalidOperationException(x.ToString());");
        await RunAsync(session, RunOutcome.Crashed);
        Assert.Null(session.Recording!.ExceptionCause);
    }

    [Fact]
    public async Task A_handled_exception_does_not_become_the_cause_of_a_later_unrelated_crash()
    {
        using var session = Recorded("""
            int divisor = 0;
            try { Console.WriteLine(1 / divisor); } catch (DivideByZeroException) { }
            throw new InvalidOperationException("unrelated");
            """);
        await RunAsync(session, RunOutcome.Crashed);
        Assert.Null(session.Recording!.ExceptionCause);
    }

    [Fact]
    public async Task A_null_reference_links_only_to_the_null_write()
    {
        using var session = Recorded("""
            string text = "hello";
            text = null;
            int unrelated = 3;
            Console.WriteLine(text.Length + unrelated);
            """);
        await RunAsync(session, RunOutcome.Crashed);
        Assert.NotNull(session.Recording!.ExceptionCause);
        var cause = session.ReverseContinue()!;
        Assert.Equal(2, cause.Location!.Line);
        Assert.Equal("null", cause.FindValue("text")!.Value);
    }

    [Fact]
    public async Task Concurrent_threads_record_a_total_sequence_order_and_their_logical_frames()
    {
        using var session = Recorded("""
            using var barrier = new System.Threading.Barrier(2);
            var first = Task.Run(() => { int a = 0; barrier.SignalAndWait(); a = 1; Console.WriteLine(a); });
            var second = Task.Run(() => { int b = 0; barrier.SignalAndWait(); b = 2; Console.WriteLine(b); });
            await Task.WhenAll(first, second);
            """);
        await RunAsync(session);
        var steps = Steps(session).ToArray();
        Assert.Equal(steps.Select(s => s.Sequence).Order(), steps.Select(s => s.Sequence));
        Assert.True(steps.Select(s => s.ThreadId).Distinct().Count() >= 2);
        Assert.Contains(steps, s => s.WrittenPath == "a" && s.Frames[0].Locals.Any(v => v.Name == "a" && v.Value == "1"));
        Assert.Contains(steps, s => s.WrittenPath == "b" && s.Frames[0].Locals.Any(v => v.Name == "b" && v.Value == "2"));
    }

    [Fact]
    public async Task Step_back_over_skips_called_frames_and_step_back_into_enters_them()
    {
        using var session = Recorded("""
            int result = Add(2);
            Console.WriteLine(result);
            int Add(int n) { n++; return n; }
            """);
        await RunAsync(session);
        var caller = Steps(session).First(s => s.WrittenPath == "result");
        session.SeekHistory(session.Recording!.IndexOf(caller.Sequence));
        var into = session.StepBackInto()!;
        Assert.StartsWith("Add", into.CallStack[0].MethodName, StringComparison.Ordinal);
        session.SeekHistory(session.Recording.IndexOf(caller.Sequence));
        Assert.Single(session.StepBack()!.CallStack);
    }

    [Fact]
    public async Task Reverse_continue_uses_actual_conditional_breakpoint_hits_and_never_resumes_live_execution()
    {
        using var session = Recorded("""
            int count = 0;
            while (count < 4)
            {
                count++;
            }
            Console.WriteLine(count);
            """);
        session.SetBreakpoint(DebuggedProgram.FilePath, 4, "count == 2");
        session.Paused += (_, _) => session.Continue();
        await RunAsync(session);
        var pause = session.ReverseContinue()!;
        Assert.Equal(4, pause.Location!.Line);
        Assert.Equal("2", pause.FindValue("count")!.Value);
        Assert.Equal(DebugSessionState.Exited, session.State);
        Assert.False(session.Continue());
        Assert.Single(Steps(session), s => s.IsBreakpoint);
    }

    [Fact]
    public async Task Assignment_expressions_and_postfix_results_preserve_CSharp_semantics()
    {
        const string source = """
            int x = 1;
            int before = x++;
            int after = ++x;
            int sum = (x += 2);
            Console.WriteLine($"{before},{after},{sum},{x}");
            """;
        using var session = Recorded(source);
        var console = new CapturingConsole();
        await session.RunAsync([], console).WaitAsync(DebuggedProgram.Timeout);
        Assert.Contains("1,3,5,5", console.Output, StringComparison.Ordinal);
        Assert.Equal(new[] { "1", "2", "3", "5" }, Steps(session).Where(s => s.WrittenPath == "x")
            .Select(s => s.Frames[0].Locals.Single(v => v.Name == "x").Value));
    }

    [Fact]
    public async Task A_custom_postfix_operator_can_replace_its_receiver_without_the_hook_reading_it_again()
    {
        const string source = """
            Box box = new Box();
            var original = box;
            Counter.OnIncrement = () => box = null;
            var before = box.Value++;
            Console.WriteLine($"{before.Number},{original.Value.Number},{box is null}");
            class Box { public Counter Value = new Counter { Number = 1 }; }
            struct Counter
            {
                public int Number;
                public static Action OnIncrement;
                public static Counter operator ++(Counter value)
                {
                    OnIncrement();
                    value.Number++;
                    return value;
                }
            }
            """;
        foreach (var recording in new[] { false, true })
        {
            using var session = new DebugSession(DebuggedProgram.Compile(source), DebugSession.DefaultEvaluationTimeout,
                recording ? new RecordingOptions { CapacityBytes = 1024 * 1024 } : null);
            session.BreakOnUnhandledExceptions = false;
            var console = new CapturingConsole();
            var result = await session.RunAsync([], console).WaitAsync(DebuggedProgram.Timeout);
            Assert.True(result.Outcome == RunOutcome.Completed, result.Exception?.ToString());
            Assert.Contains("1,2,True", console.Output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_multiline_postfix_receiver_does_not_shift_exception_line_numbers()
    {
        const string source = """
            Box box = new Box();
            var before = box.
                Number++;
            Console.WriteLine(before);
            throw new InvalidOperationException("original line");
            class Box { public int Number = 1; }
            """;
        using var session = Recorded(source);
        session.BreakOnUnhandledExceptions = false;
        var result = await session.RunAsync([], new CapturingConsole()).WaitAsync(DebuggedProgram.Timeout);
        Assert.Equal(RunOutcome.Crashed, result.Outcome);
        Assert.Contains("Program.cs:line 5", result.Exception!.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recording_overhead_is_measured_against_the_same_instrumented_debug_build()
    {
        const string source = "long sum = 0; for (int i = 0; i < 10000; i++) { sum += i; } Console.WriteLine(sum);";
        var baseline = new List<double>();
        var recorded = new List<double>();
        var bytes = 0;
        for (var i = 0; i < 4; i++)
        {
            using var normal = new DebugSession(DebuggedProgram.Compile(source));
            var normalResult = await normal.RunAsync([], new CapturingConsole()).WaitAsync(DebuggedProgram.Timeout);
            using var recording = Recorded(source, 8 * 1024 * 1024);
            var result = await recording.RunAsync([], new CapturingConsole()).WaitAsync(DebuggedProgram.Timeout);
            if (i > 0) { baseline.Add(normalResult.Duration.TotalMilliseconds); recorded.Add(result.Duration.TotalMilliseconds); }
            bytes = recording.Recording!.UsedBytes;
        }
        var off = baseline.Order().ElementAt(1);
        var on = recorded.Order().ElementAt(1);
        output.WriteLine($"10,000 iterations; median of 3 after warmup; off={off:F2} ms; recording={on:F2} ms; ratio={on / off:F2}x; retained={bytes} bytes.");
        Assert.True(bytes > 0);
    }
}
