using System.Reflection;
using System.Runtime.Loader;

namespace TabStudio.Debugging.Tests;

/// <summary>
/// Code that the caller loads and runs under a session, as Test Explorer runs the tests of a build for debugging:
/// breakpoints and steps on the caller's threads, and Stop cancelling the run without unwinding them.
/// </summary>
public sealed class HostedRunTests
{
    private const string Library = """
        Console.WriteLine("not run");

        public static class Calculator
        {
            public static int Sum(int count)
            {
                var total = 0;
                for (var i = 0; i < count; i++)
                {
                    total += i;
                }

                return total;
            }
        }
        """;

    [Fact]
    public async Task Code_the_caller_loads_and_runs_stops_at_breakpoints_and_returns_its_result()
    {
        var build = DebuggedProgram.Compile(Library);
        using var session = new DebugSession(build);
        var breakpoint = session.SetBreakpoint(DebuggedProgram.FilePath, 10, "i == 2");
        var paused = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Paused += (_, pause) => paused.TrySetResult(pause);
        var exited = false;
        session.Exited += (_, _) => exited = true;

        var run = session.RunHostedAsync(_ => Task.Run(() => Invoke(build, 5)));
        var pause = await paused.Task.WaitAsync(DebuggedProgram.Timeout);

        Assert.Same(breakpoint, pause.Breakpoint);
        Assert.Equal("2", DebuggedProgram.Local(pause, "i").Value);
        Assert.Contains("Sum", pause.CallStack[0].MethodName, StringComparison.Ordinal);
        Assert.Equal(DebugSessionState.Paused, session.State);
        session.Continue();

        Assert.Equal(10, await run.WaitAsync(DebuggedProgram.Timeout));
        Assert.True(exited);
        Assert.Equal(DebugSessionState.Exited, session.State);
    }

    [Fact]
    public async Task Stop_releases_the_paused_thread_and_cancels_the_run()
    {
        var build = DebuggedProgram.Compile(Library);
        using var session = new DebugSession(build);
        session.SetBreakpoint(DebuggedProgram.FilePath, 10);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Paused += (_, _) => paused.TrySetResult();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = session.RunHostedAsync(async stop =>
        {
            using var registration = stop.Register(() => cancelled.TrySetResult());
            var result = await Task.Run(() => Invoke(build, 3));
            await cancelled.Task;
            return result;
        });
        await paused.Task.WaitAsync(DebuggedProgram.Timeout);
        session.Stop();

        // The hooks are off once stopped: the loop runs to its end on the caller's thread, which is never unwound.
        Assert.Equal(3, await run.WaitAsync(DebuggedProgram.Timeout));
        Assert.True(cancelled.Task.IsCompleted);
    }

    [Fact]
    public async Task A_session_runs_once()
    {
        var build = DebuggedProgram.Compile(Library);
        using var session = new DebugSession(build);
        await session.RunHostedAsync(_ => Task.FromResult(0));

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunHostedAsync(_ => Task.FromResult(0)));
    }

    private static int Invoke(TabStudio.Debugging.Building.DebugBuild build, int count)
    {
        var context = new AssemblyLoadContext("hosted-" + Guid.NewGuid().ToString("N"), isCollectible: false);
        var assembly = context.LoadFromStream(new MemoryStream(build.Result.EntryAssembly!.Image));
        var sum = assembly.GetType("Calculator", throwOnError: true)!.GetMethod("Sum", BindingFlags.Public | BindingFlags.Static)!;
        return (int)sum.Invoke(null, [count])!;
    }
}
