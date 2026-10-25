using System.Collections.Concurrent;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Testing;
using Xunit.Abstractions;

namespace TabStudio.LanguageServers.Tests.Integration;

/// <summary>debugpy, the Python debug adapter, as installed with pip: a session that stops at a breakpoint and reads a local.</summary>
public sealed class RealDebugAdapterTests(ITestOutputHelper output)
{
    private const string Program = """
        def greet(name):
            message = "Hello, " + name
            return message


        print(greet("Ada"))
        """;

    [RealServerFact("python3", PythonModule = "debugpy")]
    public async Task Debugpy_stops_at_a_breakpoint_and_reads_the_locals()
    {
        using var temp = new TempDirectory();
        var file = temp.Write("program.py", Program + "\n");
        var adapter = LanguageServerRegistry.BuiltIn.FindAdapter("python") ?? throw new InvalidOperationException("No Python adapter.");
        var executable = ProcessServerLauncher.Instance.ResolveFirst(adapter.Commands) ?? throw new InvalidOperationException("No Python.");
        await using var session = await DebugAdapterSession.StartAsync(adapter, executable, temp.Path, ProcessServerLauncher.Instance, new DebugAdapterSessionOptions { StartTimeout = TestTimeouts.RealServer })
            .WaitAsync(TestTimeouts.RealServer);
        var stopped = new TaskCompletionSource<DapStoppedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var programOutput = new ConcurrentQueue<string>();
        session.Stopped += (_, e) => stopped.TrySetResult(e);
        session.Exited += (_, e) => exited.TrySetResult(e.ExitCode);
        session.OutputReceived += (_, e) =>
        {
            programOutput.Enqueue(e.Output);
            Log($"[{e.Category}] {e.Output.TrimEnd()}");
        };
        session.AdapterLog += (_, line) => Log("[adapter] " + line);
        Assert.Contains(session.Capabilities.ExceptionFilters, f => f.Filter == "uncaught");

        var configuration = LaunchConfiguration.ForCurrentFile(adapter, "Python").Substitute(new LaunchVariables(temp.Path, file));
        var breakpoints = new Dictionary<string, IReadOnlyList<DapSourceBreakpoint>> { [file] = [new DapSourceBreakpoint(3)] };
        var set = await session.LaunchAsync(configuration, breakpoints, ["uncaught"], CancellationToken.None).WaitAsync(TestTimeouts.RealServer);
        var stop = await stopped.Task.WaitAsync(TestTimeouts.RealServer);

        Assert.Equal(3, Assert.Single(set[file]).Line ?? 3);
        Assert.Equal("breakpoint", stop.Reason);
        var thread = stop.ThreadId ?? (await session.GetThreadsAsync(CancellationToken.None))[0].Id;
        var frames = await session.GetStackTraceAsync(thread, CancellationToken.None);
        Assert.Equal("greet", frames[0].Name);
        Assert.Equal(3, frames[0].Line);
        Assert.Equal("program.py", Path.GetFileName(frames[0].FilePath));
        Assert.Contains(frames, f => f.Name == "<module>" && f.Line == 6);

        var scopes = await session.GetScopesAsync(frames[0].Id, CancellationToken.None);
        var locals = await session.GetVariablesAsync(scopes.First(s => s.IsLocals).VariablesReference, CancellationToken.None);
        Assert.Contains(locals, v => v.Name == "name" && v.Value == "'Ada'" && v.Type == "str");
        Assert.Contains(locals, v => v.Name == "message" && v.Value == "'Hello, Ada'");
        var length = await session.EvaluateAsync("len(message)", frames[0].Id, DapEvaluateContext.Watch, CancellationToken.None);
        Assert.Equal("10", length.Result);

        await session.ContinueAsync(thread, CancellationToken.None);

        Assert.Equal(0, await exited.Task.WaitAsync(TestTimeouts.RealServer));
        await WaitUntilAsync(() => programOutput.Any(o => o.Contains("Hello, Ada", StringComparison.Ordinal)));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TestTimeouts.Default;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out.");
            await Task.Delay(50);
        }
    }

    private void Log(string line)
    {
        try
        {
            output.WriteLine(line);
        }
        catch (InvalidOperationException)
        {
            // The test already ended.
        }
    }
}
