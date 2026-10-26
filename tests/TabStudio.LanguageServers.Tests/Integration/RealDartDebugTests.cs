using System.Threading.Channels;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Testing;

namespace TabStudio.LanguageServers.Tests.Integration;

public sealed class RealDartDebugTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [DartServerFact]
    public async Task Dart_DAP_hits_a_breakpoint_steps_and_reads_variables()
    {
        using var temp = new TempDirectory();
        var file = temp.Write("main.dart", "void main() {\n  var counter = 1;\n  counter += 2;\n  print(counter);\n}\n");
        var adapter = LanguageServerRegistry.BuiltIn.FindAdapter("dart")!;
        var executable = ProcessServerLauncher.Instance.Resolve("dart")!;
        await using var session = await DebugAdapterSession.StartAsync(adapter, executable, temp.Path, ProcessServerLauncher.Instance,
            new DebugAdapterSessionOptions { StartTimeout = TestTimeouts.RealServer });
        var stops = Channel.CreateUnbounded<DapStoppedEventArgs>();
        session.Stopped += (_, stop) => { output.WriteLine($"{stop.Reason}:{stop.ThreadId}"); stops.Writer.TryWrite(stop); };
        session.Continued += (_, _) => output.WriteLine("continued");
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Ended += (_, _) => ended.TrySetResult();
        var configuration = LaunchConfiguration.ForCurrentFile(adapter, "Dart").Substitute(new LaunchVariables(temp.Path, file));
        await session.LaunchAsync(configuration, new Dictionary<string, IReadOnlyList<DapSourceBreakpoint>> { [file] = [new(3)] }, [], CancellationToken.None).WaitAsync(TestTimeouts.RealServer);
        var stop = await stops.Reader.ReadAsync().AsTask().WaitAsync(TestTimeouts.RealServer);
        var thread = stop.ThreadId ?? (await session.GetThreadsAsync(CancellationToken.None))[0].Id;
        var frames = await session.GetStackTraceAsync(thread, CancellationToken.None);
        Assert.Equal(3, frames[0].Line);
        var before = await session.EvaluateAsync("counter", frames[0].Id, DapEvaluateContext.Watch, CancellationToken.None);
        Assert.Equal("1", before.Result);
        await session.StepOverAsync(thread, CancellationToken.None);
        do
        {
            stop = await stops.Reader.ReadAsync().AsTask().WaitAsync(TestTimeouts.RealServer);
        }
        while (stop.Reason != "step");
        thread = stop.ThreadId ?? thread;
        frames = await session.GetStackTraceAsync(thread, CancellationToken.None);
        Assert.Equal(4, frames[0].Line);
        var scopes = await session.GetScopesAsync(frames[0].Id, CancellationToken.None);
        var locals = await session.GetVariablesAsync(scopes[0].VariablesReference, CancellationToken.None);
        Assert.Contains(locals, variable => variable.Name == "counter" && variable.Value == "3");
        await session.ContinueAsync(thread, CancellationToken.None);
        // Dart sends terminated, while exited (and its exit code) is optional in DAP.
        await ended.Task.WaitAsync(TestTimeouts.RealServer);
        Assert.True(session.HasEnded);
    }
}
