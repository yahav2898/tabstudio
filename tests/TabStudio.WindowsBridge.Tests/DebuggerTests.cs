using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.Testing;
using TabStudio.WindowsBridge.Client;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>
/// The tablet's debugger drives netcoredbg on the PC through the bridge's tunnel, with the tablet's own paths: it sets
/// a breakpoint in the tablet's file, stops there, reads the locals and lets the program finish.
/// </summary>
public sealed class DebuggerTests : IDisposable
{
    private const string Program = """
        var total = 0;
        for (var i = 1; i <= 3; i++)
        {
            total += i;
        }
        Console.WriteLine($"Total {total}");
        """;

    private readonly TempDirectory _folder = new();

    [RealDebuggerFact]
    public async Task The_tablets_debugger_stops_at_a_breakpoint_in_a_program_on_the_PC()
    {
        await using var bridge = await BridgeLoopback.StartAsync(_folder);
        using (var http = new HttpClient())
        {
            await NetcoredbgInstaller.InstallAsync(bridge.ToolsFolder, http);
        }

        await bridge.Server.RefreshToolchainsAsync();
        var (workspace, project) = bridge.CreateConsoleProject("Sum", Program);
        await bridge.SyncAsync(workspace);
        var build = await bridge.Client.BuildAsync(project, "Debug", false, _ => { });
        var file = _folder.Resolve("Tablet/Sum/Program.cs");
        var started = Waits.ForAsync<AppStarted>(h => bridge.Client.AppStarted += h, h => bridge.Client.AppStarted -= h, a => a.IsDebugging, "the debugged program");

        await using var session = await DebugAdapterSession.StartAsync(
            BridgeDebugLauncher.Adapter("Test PC"),
            BridgeDebugLauncher.Command,
            Path.GetDirectoryName(project)!,
            new BridgeDebugLauncher(bridge.Client, project));
        var stopped = new TaskCompletionSource<DapStoppedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new ConcurrentQueue<string>();
        session.Stopped += (_, e) => stopped.TrySetResult(e);
        session.Ended += (_, _) => ended.TrySetResult();
        session.OutputReceived += (_, e) => output.Enqueue(e.Output);
        var configuration = new LaunchConfiguration
        {
            Name = "Sum",
            Type = "coreclr",
            Request = "launch",
            Properties = new JsonObject { ["program"] = build.Program!.Path, ["cwd"] = Path.GetDirectoryName(project), ["stopAtEntry"] = false },
        };
        await session.LaunchAsync(configuration, new Dictionary<string, IReadOnlyList<DapSourceBreakpoint>> { [file] = [new DapSourceBreakpoint(6)] }, [], CancellationToken.None);

        var stop = await stopped.Task.WaitAsync(Waits.Patience);
        var thread = stop.ThreadId ?? (await session.GetThreadsAsync(CancellationToken.None))[0].Id;
        var frames = await session.GetStackTraceAsync(thread, CancellationToken.None);
        var scopes = await session.GetScopesAsync(frames[0].Id, CancellationToken.None);
        var locals = await session.GetVariablesAsync((scopes.FirstOrDefault(s => s.IsLocals) ?? scopes[0]).VariablesReference, CancellationToken.None);
        var program = await started;
        await session.ContinueAsync(thread, CancellationToken.None);
        await ended.Task.WaitAsync(Waits.Patience);

        Assert.Equal("breakpoint", stop.Reason);
        Assert.Equal(file, frames[0].FilePath);
        Assert.Equal(6, frames[0].Line);
        Assert.Equal("6", locals.Single(v => v.Name == "total").Value);
        Assert.Equal("Sum", program.Name);
        Assert.Equal(project, program.ProjectPath);
        Assert.Contains(output, text => text.Contains("Total 6", StringComparison.Ordinal));
    }

    public void Dispose() => _folder.Dispose();
}
