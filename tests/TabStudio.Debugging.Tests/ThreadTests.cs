namespace TabStudio.Debugging.Tests;

/// <summary>The paused program's threads: the one that paused, and those stopped at their next statement.</summary>
public sealed class ThreadTests
{
    private const string Program = """
        var running = true;
        var worker = new Thread(() =>
        {
            var spins = 0;
            while (Volatile.Read(ref running))
            {
                spins++;
            }
        }) { Name = "Worker", IsBackground = true };
        worker.Start();
        Thread.Sleep(300);
        Console.WriteLine("paused here");
        running = false;
        worker.Join();
        """;

    [Fact]
    public async Task A_pause_lists_the_threads_waiting_in_the_programs_code_with_their_call_stacks()
    {
        using var program = new DebuggedProgram(Program, 12);
        program.Start();

        var pause = await program.NextPauseAsync();
        var deadline = DateTime.UtcNow + DebuggedProgram.Timeout;
        while (program.Session.Threads.Count < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        var threads = program.Session.Threads;
        Assert.Equal(2, threads.Count);
        Assert.Equal(pause.ThreadId, threads[0].Id);
        Assert.Same(pause.CallStack, threads[0].CallStack);
        var worker = threads[1];
        Assert.Equal("Worker", worker.Name);
        Assert.Contains(worker.CallStack[0].Location!.Line, new[] { 5, 7 });

        program.Session.Continue();
        await program.FinishAsync();
        Assert.Empty(program.Session.Threads);
        Assert.Equal("paused here\n", program.Console.Output);
    }
}
