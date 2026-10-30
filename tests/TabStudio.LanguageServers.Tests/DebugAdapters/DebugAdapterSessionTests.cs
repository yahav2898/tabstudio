using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.Tests.Fakes;

namespace TabStudio.LanguageServers.Tests.DebugAdapters;

public sealed class DebugAdapterSessionTests
{
    private const string Program = "/work/program.py";

    [Fact]
    public async Task Launching_follows_the_protocol_order_and_stops_at_the_breakpoint()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        await using var session = await StartAsync(launcher);
        var events = new Recorder(session);

        var results = await LaunchAsync(session, new DapSourceBreakpoint(3, "name == 'Ada'", "2"), new DapSourceBreakpoint(120));

        var adapter = Assert.Single(launcher.Started);
        Assert.Equal(new[] { "initialize", "launch", "setBreakpoints", "setExceptionBreakpoints", "configurationDone" }, adapter.Requests.Select(r => r.Command));
        var initialize = adapter.Requests[0].Arguments;
        Assert.Equal("tabstudio", initialize.GetProperty("clientID").GetString());
        Assert.Equal("fake", initialize.GetProperty("adapterID").GetString());
        Assert.True(initialize.GetProperty("linesStartAt1").GetBoolean());
        Assert.Equal("path", initialize.GetProperty("pathFormat").GetString());
        Assert.False(initialize.GetProperty("supportsRunInTerminalRequest").GetBoolean());

        var launch = adapter.Requests[1].Arguments;
        Assert.Equal(Program, launch.GetProperty("program").GetString());
        Assert.Equal("/work", launch.GetProperty("cwd").GetString());
        Assert.Equal("internalConsole", launch.GetProperty("console").GetString());
        Assert.Equal("Python: Current File", launch.GetProperty("name").GetString());

        var setBreakpoints = adapter.Requests[2].Arguments;
        Assert.Equal(Program, setBreakpoints.GetProperty("source").GetProperty("path").GetString());
        var first = setBreakpoints.GetProperty("breakpoints")[0];
        Assert.Equal(3, first.GetProperty("line").GetInt32());
        Assert.Equal("name == 'Ada'", first.GetProperty("condition").GetString());
        Assert.Equal("2", first.GetProperty("hitCondition").GetString());
        Assert.Equal("uncaught", Assert.Single(adapter.Requests[3].Arguments.GetProperty("filters").EnumerateArray()).GetString());

        var set = results[Program];
        Assert.True(set[0].IsVerified);
        Assert.Equal(3, set[0].Line);
        Assert.False(set[1].IsVerified);
        Assert.Equal("No code at this line", set[1].Message);

        var stop = await events.NextStopAsync();
        Assert.Equal("breakpoint", stop.Reason);
        Assert.Equal(1, stop.ThreadId);
        Assert.True(stop.AllThreadsStopped);
        Assert.Equal(1, Assert.Single(stop.HitBreakpointIds));
    }

    [Fact]
    public async Task Starting_runs_the_adapter_and_reads_its_capabilities()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        await using var session = await StartAsync(launcher);

        var start = launcher.LastStart!;
        Assert.Equal("/fake/bin/fake-adapter", start.FileName);
        Assert.Equal(new[] { "--stdio" }, start.Arguments);
        Assert.Equal("/work", start.WorkingDirectory);
        Assert.True(session.Capabilities.ConfigurationDone);
        Assert.True(session.Capabilities.LogPoints);
        Assert.True(session.Capabilities.SetVariable);
        Assert.False(session.Capabilities.SetExpression);
        Assert.Collection(
            session.Capabilities.ExceptionFilters,
            raised => Assert.Equal(("raised", "Raised Exceptions", false), (raised.Filter, raised.Label, raised.IsDefault)),
            uncaught => Assert.Equal(("uncaught", "Uncaught Exceptions", true), (uncaught.Filter, uncaught.Label, uncaught.IsDefault)));
    }

    [Fact]
    public async Task Adapter_startup_stderr_is_available_before_its_TCP_connection_opens()
    {
        var logged = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var launcher = new TcpLauncher(logged.Task);
        var adapter = new DebugAdapterDefinition
        {
            Id = "test", Name = "Test", Command = "test", Transport = DebugAdapterTransport.Tcp,
            Arguments = ["--port", "${port}"],
        };
        var starting = DebugAdapterSession.StartAsync(adapter, "/fake/bin/test", "/work", launcher,
            new DebugAdapterSessionOptions { StartupLog = line => logged.TrySetResult(line) });

        Assert.Equal("waiting for debugger", await logged.Task.WaitAsync(TestTimeouts.Default));
        await using var session = await starting;
    }

    [Fact]
    public async Task Breakpoint_settings_the_adapter_does_not_support_are_left_out()
    {
        var launcher = new FakeDebugAdapter.Launcher(adapter =>
        {
            adapter.Capabilities.Remove("supportsConditionalBreakpoints");
            adapter.Capabilities.Remove("supportsHitConditionalBreakpoints");
            adapter.Capabilities.Remove("supportsLogPoints");
        });
        await using var session = await StartAsync(launcher);

        await LaunchAsync(session, new DapSourceBreakpoint(3, "x > 1", "5", "x is {x}"));

        var breakpoint = launcher.Started[0].Requests.First(r => r.Command == "setBreakpoints").Arguments.GetProperty("breakpoints")[0];
        Assert.Equal(3, breakpoint.GetProperty("line").GetInt32());
        Assert.False(breakpoint.TryGetProperty("condition", out _));
        Assert.False(breakpoint.TryGetProperty("hitCondition", out _));
        Assert.False(breakpoint.TryGetProperty("logMessage", out _));
    }

    [Fact]
    public async Task A_capabilities_event_changes_only_what_it_names()
    {
        // lldb-dap 21 sends {"supportsRestartRequest": true} after every launch: replacing every capability with it
        // turned conditional breakpoints into plain ones and dropped the exception filters.
        var launcher = new FakeDebugAdapter.Launcher();
        await using var session = await StartAsync(launcher);
        await LaunchAsync(session, new DapSourceBreakpoint(3));
        var before = session.Capabilities;

        await launcher.Started[0].Connection.SendEventAsync("capabilities", new JsonObject
        {
            ["capabilities"] = new JsonObject { ["supportsRestartRequest"] = true, ["supportsSetVariable"] = false },
        });
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (ReferenceEquals(session.Capabilities, before) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        var after = session.Capabilities;
        Assert.NotSame(before, after);
        Assert.True(after.ConditionalBreakpoints && after.HitConditionalBreakpoints && after.LogPoints && after.ExceptionInfo);
        Assert.False(after.SetVariable);
        Assert.Equal(["raised", "uncaught"], after.ExceptionFilters.Select(f => f.Filter));

        await session.SetBreakpointsAsync(Program, [new DapSourceBreakpoint(3, "x > 1")], CancellationToken.None);
        var request = launcher.Started[0].Requests.Last(r => r.Command == "setBreakpoints").Arguments.GetProperty("breakpoints");
        Assert.Equal("x > 1", request[0].GetProperty("condition").GetString());
    }

    [Fact]
    public async Task Logpoints_are_sent_when_the_adapter_supports_them()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        await using var session = await StartAsync(launcher);
        await LaunchAsync(session, new DapSourceBreakpoint(3));

        var updated = await session.SetBreakpointsAsync(Program, [new DapSourceBreakpoint(3), new DapSourceBreakpoint(6, LogMessage: "items: {items}")], CancellationToken.None);

        Assert.Equal(2, updated.Count);
        Assert.All(updated, b => Assert.True(b.IsVerified));
        var request = launcher.Started[0].Requests.Last(r => r.Command == "setBreakpoints").Arguments.GetProperty("breakpoints");
        Assert.Equal("items: {items}", request[1].GetProperty("logMessage").GetString());
        Assert.False(request[0].TryGetProperty("logMessage", out _));
    }

    [Fact]
    public async Task A_paused_program_shows_its_threads_frames_scopes_and_variables()
    {
        await using var session = await StartAsync(new FakeDebugAdapter.Launcher());
        var events = new Recorder(session);
        await LaunchAsync(session, new DapSourceBreakpoint(3));
        await events.NextStopAsync();

        var thread = Assert.Single(await session.GetThreadsAsync(CancellationToken.None));
        Assert.Equal(new DapThread(1, "MainThread"), thread);

        var frames = await session.GetStackTraceAsync(thread.Id, CancellationToken.None);
        Assert.Equal(new[] { "greet", "<module>", "_run_code" }, frames.Select(f => f.Name));
        Assert.Equal(new DapStackFrame(10, "greet", Program, 3, 5, 3, 20, false), frames[0]);
        Assert.True(frames[2].IsSubtle);

        var scopes = await session.GetScopesAsync(frames[0].Id, CancellationToken.None);
        Assert.Equal(new DapScope("Locals", 100, false, true), scopes[0]);
        Assert.False(scopes[1].IsLocals);

        var locals = await session.GetVariablesAsync(scopes[0].VariablesReference, CancellationToken.None);
        Assert.Equal(new DapVariable("name", "'Ada'", "str", "name", 0), locals[0]);
        Assert.False(locals[0].HasChildren);
        Assert.True(locals[1].HasChildren);
        var items = await session.GetVariablesAsync(locals[1].VariablesReference, CancellationToken.None);
        Assert.Equal(new[] { "1", "2" }, items.Select(i => i.Value));
    }

    [Fact]
    public async Task Expressions_are_evaluated_in_a_frame_and_variables_can_be_changed()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        await using var session = await StartAsync(launcher);
        var events = new Recorder(session);
        await LaunchAsync(session, new DapSourceBreakpoint(3));
        await events.NextStopAsync();

        var watch = await session.EvaluateAsync("items", 10, DapEvaluateContext.Watch, CancellationToken.None);
        Assert.Equal(new DapEvaluation("[1, 2]", "list", 101), watch);
        var hover = await session.EvaluateAsync("name", 10, DapEvaluateContext.Hover, CancellationToken.None);
        Assert.Equal("'Ada'", hover.Result);
        var error = await Assert.ThrowsAsync<DebugAdapterException>(() => session.EvaluateAsync("missing", 10, DapEvaluateContext.Repl, CancellationToken.None));
        Assert.Equal("NameError: name 'missing' is not defined", error.Message);
        Assert.Equal("evaluate", error.Command);
        Assert.False(error.IsConnectionClosed);

        var evaluations = launcher.Started[0].Requests.Where(r => r.Command == "evaluate").Select(r => r.Arguments).ToList();
        Assert.Equal(new[] { "watch", "hover", "repl" }, evaluations.Select(e => e.GetProperty("context").GetString()));
        Assert.Equal(10, evaluations[0].GetProperty("frameId").GetInt32());

        var changed = await session.SetVariableAsync(100, "name", "'Grace'", CancellationToken.None);
        Assert.Equal("'Grace'", changed.Value);
        Assert.Equal("str", changed.Type);
        var setVariable = launcher.Started[0].Requests.Single(r => r.Command == "setVariable").Arguments;
        Assert.Equal(100, setVariable.GetProperty("variablesReference").GetInt32());
        Assert.Equal("name", setVariable.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Hovers_are_evaluated_as_watches_when_the_adapter_has_no_hover_context()
    {
        var launcher = new FakeDebugAdapter.Launcher(adapter => adapter.Capabilities.Remove("supportsEvaluateForHovers"));
        await using var session = await StartAsync(launcher);
        var events = new Recorder(session);
        await LaunchAsync(session, new DapSourceBreakpoint(3));
        await events.NextStopAsync();

        await session.EvaluateAsync("name", 10, DapEvaluateContext.Hover, CancellationToken.None);

        var evaluate = launcher.Started[0].Requests.Single(r => r.Command == "evaluate").Arguments;
        Assert.Equal("watch", evaluate.GetProperty("context").GetString());
    }

    [Fact]
    public async Task Stepping_stops_again_and_continuing_runs_the_program_to_its_end()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        await using var session = await StartAsync(launcher);
        var events = new Recorder(session);
        await LaunchAsync(session, new DapSourceBreakpoint(3));
        await events.NextStopAsync();

        await session.StepOverAsync(1, CancellationToken.None);
        Assert.Equal("step", (await events.NextStopAsync()).Reason);
        await session.StepIntoAsync(1, CancellationToken.None);
        Assert.Equal("step", (await events.NextStopAsync()).Reason);
        await session.StepOutAsync(1, CancellationToken.None);
        Assert.Equal("step", (await events.NextStopAsync()).Reason);
        await session.PauseAsync(1, CancellationToken.None);
        Assert.Equal("pause", (await events.NextStopAsync()).Reason);

        await session.ContinueAsync(1, CancellationToken.None);

        Assert.Equal(0, await events.Exited.WaitAsync(TestTimeouts.Default));
        await events.Ended.WaitAsync(TestTimeouts.Default);
        Assert.True(session.HasEnded);
        Assert.Equal(0, session.ExitCode);
        var output = Assert.Single(events.Output);
        Assert.Equal(("stdout", "Hello, Ada\n"), (output.Category, output.Output));
        var commands = launcher.Started[0].Requests.Select(r => r.Command).ToList();
        Assert.Equal(new[] { "next", "stepIn", "stepOut", "pause", "continue" }, commands.Skip(5));
        Assert.All(launcher.Started[0].Requests.Skip(5), r => Assert.Equal(1, r.Arguments.GetProperty("threadId").GetInt32()));
    }

    [Fact]
    public async Task A_program_without_breakpoints_runs_to_its_end()
    {
        await using var session = await StartAsync(new FakeDebugAdapter.Launcher());
        var events = new Recorder(session);

        await LaunchAsync(session);

        Assert.Equal(0, await events.Exited.WaitAsync(TestTimeouts.Default));
        await events.Ended.WaitAsync(TestTimeouts.Default);
    }

    [Fact]
    public async Task Stopping_at_an_exception_tells_what_the_exception_is()
    {
        await using var session = await StartAsync(new FakeDebugAdapter.Launcher(adapter => adapter.StopReason = "exception"));
        var events = new Recorder(session);
        await LaunchAsync(session);
        var stop = await events.NextStopAsync();
        Assert.Equal("exception", stop.Reason);

        var exception = await session.GetExceptionInfoAsync(1, CancellationToken.None);

        Assert.NotNull(exception);
        Assert.Equal("ZeroDivisionError", exception.ExceptionId);
        Assert.Equal("division by zero", exception.Description);
        Assert.Equal("builtins.ZeroDivisionError", exception.TypeName);
        Assert.Equal("division by zero", exception.Message);
        Assert.Contains("line 3", exception.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exception_info_is_not_asked_of_an_adapter_that_cannot_tell()
    {
        var launcher = new FakeDebugAdapter.Launcher(adapter =>
        {
            adapter.StopReason = "exception";
            adapter.Capabilities.Remove("supportsExceptionInfoRequest");
        });
        await using var session = await StartAsync(launcher);
        var events = new Recorder(session);
        await LaunchAsync(session);
        await events.NextStopAsync();

        Assert.Null(await session.GetExceptionInfoAsync(1, CancellationToken.None));
        Assert.DoesNotContain(launcher.Started[0].Requests, r => r.Command == "exceptionInfo");
    }

    [Fact]
    public async Task A_launch_the_adapter_refuses_fails_with_its_reason()
    {
        await using var session = await StartAsync(new FakeDebugAdapter.Launcher(adapter => adapter.LaunchFailure = "Program not found: /work/missing.py"));

        var error = await Assert.ThrowsAsync<DebugAdapterException>(() => LaunchAsync(session));

        Assert.Equal("Program not found: /work/missing.py", error.Message);
        Assert.Equal("launch", error.Command);
    }

    [Fact]
    public async Task Stopping_terminates_the_program_then_disconnects()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        await using var session = await StartAsync(launcher);
        var events = new Recorder(session);
        await LaunchAsync(session, new DapSourceBreakpoint(3));
        await events.NextStopAsync();

        await session.TerminateAsync(CancellationToken.None).WaitAsync(TestTimeouts.Default);

        Assert.True(session.HasEnded);
        await events.Ended.WaitAsync(TestTimeouts.Default);
        var commands = launcher.Started[0].Requests.Select(r => r.Command).ToList();
        Assert.Equal(new[] { "terminate", "disconnect" }, commands.Skip(5));
        Assert.True(launcher.Started[0].Requests[^1].Arguments.GetProperty("terminateDebuggee").GetBoolean());
    }

    [Fact]
    public async Task Disposing_disconnects_and_ends_the_adapter()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        var session = await StartAsync(launcher);
        var events = new Recorder(session);
        await LaunchAsync(session, new DapSourceBreakpoint(3));
        await events.NextStopAsync();

        await session.DisposeAsync().AsTask().WaitAsync(TestTimeouts.Default);

        var adapter = launcher.Started[0];
        Assert.Equal("disconnect", adapter.Requests[^1].Command);
        Assert.True(adapter.Exited.IsCompleted);
        Assert.True(session.HasEnded);
        var error = await Assert.ThrowsAsync<DebugAdapterException>(() => session.GetThreadsAsync(CancellationToken.None));
        Assert.True(error.IsConnectionClosed);
    }

    [Fact]
    public async Task An_adapter_that_crashes_ends_the_session()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        await using var session = await StartAsync(launcher);
        var events = new Recorder(session);
        await LaunchAsync(session, new DapSourceBreakpoint(3));
        await events.NextStopAsync();

        launcher.Started[0].Crash();

        await events.Ended.WaitAsync(TestTimeouts.Default);
        Assert.True(session.HasEnded);
        Assert.Null(session.ExitCode);
        var error = await Assert.ThrowsAsync<DebugAdapterException>(() => session.StepOverAsync(1, CancellationToken.None));
        Assert.True(error.IsConnectionClosed);
    }

    [Fact]
    public async Task An_adapter_that_does_not_start_fails_clearly()
    {
        var launcher = new FailingLauncher();

        var error = await Assert.ThrowsAsync<ServerStartException>(() => DebugAdapterSession.StartAsync(Adapter(), "/fake/bin/fake-adapter", "/work", launcher));

        Assert.Equal("fake-adapter: permission denied", error.Message);
    }

    [Fact]
    public async Task Child_sessions_the_adapter_asks_for_go_to_the_handler()
    {
        var launcher = new FakeDebugAdapter.Launcher(adapter => adapter.AskForChildSession = true);
        await using var session = await DebugAdapterSession.StartAsync(Adapter(), "/fake/bin/fake-adapter", "/work", launcher, new DebugAdapterSessionOptions { SupportsChildSessions = true })
            .WaitAsync(TestTimeouts.Default);
        var requested = new TaskCompletionSource<DapChildSessionRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.ChildSessionHandler = (request, _) =>
        {
            requested.TrySetResult(request);
            return Task.CompletedTask;
        };

        await LaunchAsync(session);
        var child = await requested.Task.WaitAsync(TestTimeouts.Default);

        Assert.True(launcher.Started[0].Requests[0].Arguments.GetProperty("supportsStartDebuggingRequest").GetBoolean());
        Assert.Equal("attach", child.Request);
        Assert.Equal("child-1", child.Configuration["__pendingTargetId"]?.GetValue<string>());
        Assert.True(await launcher.Started[0].ChildSessionAnswered.Task.WaitAsync(TestTimeouts.Default));
    }

    [Fact]
    public async Task Child_sessions_are_refused_without_a_handler()
    {
        var launcher = new FakeDebugAdapter.Launcher(adapter => adapter.AskForChildSession = true);
        await using var session = await StartAsync(launcher);

        await LaunchAsync(session);

        Assert.False(launcher.Started[0].Requests[0].Arguments.GetProperty("supportsStartDebuggingRequest").GetBoolean());
        Assert.False(await launcher.Started[0].ChildSessionAnswered.Task.WaitAsync(TestTimeouts.Default));
    }

    [Fact]
    public async Task Run_in_terminal_requests_are_refused_with_a_way_out()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        await using var session = await StartAsync(launcher);

        var error = await Assert.ThrowsAsync<DebugAdapterException>(() =>
            launcher.Started[0].Connection.SendRequestAsync("runInTerminal", new JsonObject { ["kind"] = "integrated", ["args"] = new JsonArray("python3") }, CancellationToken.None));

        Assert.Contains("internalConsole", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_in_terminal_requests_go_to_the_handler_and_launches_use_the_terminal_defaults()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        var definition = Adapter() with { TerminalDefaults = new JsonObject { ["console"] = "integratedTerminal", ["redirectOutput"] = false } };
        await using var session = await DebugAdapterSession.StartAsync(definition, "/fake/bin/fake-adapter", "/work", launcher, new DebugAdapterSessionOptions { SupportsRunInTerminal = true })
            .WaitAsync(TestTimeouts.Default);
        var received = new TaskCompletionSource<DapRunInTerminalRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.RunInTerminalHandler = (request, _) =>
        {
            received.TrySetResult(request);
            return Task.FromResult<int?>(4242);
        };

        await LaunchAsync(session);
        var body = await launcher.Started[0].Connection.SendRequestAsync(
            "runInTerminal",
            new JsonObject
            {
                ["kind"] = "integrated",
                ["title"] = "Python Debug Console",
                ["cwd"] = "/work",
                ["args"] = new JsonArray("/usr/bin/python3", "/opt/debugpy/launcher", "--", "/work/program.py"),
                ["env"] = new JsonObject { ["PYTHONUNBUFFERED"] = "1", ["PYTHONSTARTUP"] = null },
            },
            CancellationToken.None).WaitAsync(TestTimeouts.Default);
        var request = await received.Task.WaitAsync(TestTimeouts.Default);

        var adapter = launcher.Started[0];
        Assert.True(adapter.Requests[0].Arguments.GetProperty("supportsRunInTerminalRequest").GetBoolean());
        var launch = adapter.Requests.First(r => r.Command == "launch").Arguments;
        Assert.Equal("integratedTerminal", launch.GetProperty("console").GetString());
        Assert.False(launch.GetProperty("redirectOutput").GetBoolean());
        Assert.True(launch.GetProperty("justMyCode").GetBoolean());
        Assert.True(session.RunsInTerminal);
        Assert.Equal(4242, body.GetProperty("processId").GetInt32());
        Assert.Equal(["/usr/bin/python3", "/opt/debugpy/launcher", "--", "/work/program.py"], request.Arguments);
        Assert.Equal("/work", request.WorkingDirectory);
        Assert.Equal("Python Debug Console", request.Title);
        Assert.False(request.IsExternal);
        Assert.Equal("1", request.Environment["PYTHONUNBUFFERED"]);
        Assert.True(request.Environment.ContainsKey("PYTHONSTARTUP"));
        Assert.Null(request.Environment["PYTHONSTARTUP"]);
    }

    [Fact]
    public async Task Without_a_terminal_launches_keep_the_adapters_console()
    {
        var launcher = new FakeDebugAdapter.Launcher();
        var definition = Adapter() with { TerminalDefaults = new JsonObject { ["console"] = "integratedTerminal" } };
        await using var session = await DebugAdapterSession.StartAsync(definition, "/fake/bin/fake-adapter", "/work", launcher).WaitAsync(TestTimeouts.Default);

        await LaunchAsync(session);

        Assert.False(session.RunsInTerminal);
        Assert.Equal("internalConsole", launcher.Started[0].Requests.First(r => r.Command == "launch").Arguments.GetProperty("console").GetString());
    }

    [Fact]
    public async Task Adapters_that_listen_on_a_port_are_reached_over_tcp()
    {
        await using var launcher = new TcpLauncher();
        var adapter = Adapter() with { Transport = DebugAdapterTransport.Tcp, Arguments = ["--port", "${port}"] };

        await using var session = await DebugAdapterSession.StartAsync(adapter, "/fake/bin/fake-adapter", "/work", launcher).WaitAsync(TestTimeouts.Default);
        var events = new Recorder(session);
        await LaunchAsync(session, new DapSourceBreakpoint(3));

        Assert.Equal("breakpoint", (await events.NextStopAsync()).Reason);
        var start = launcher.StartInfo!;
        Assert.Equal("--port", start.Arguments[0]);
        Assert.Equal(launcher.Port.ToString(CultureInfo.InvariantCulture), start.Arguments[1]);
    }

    [Fact]
    public async Task Debug_servers_already_running_are_connected_to()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepting = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var adapter = new FakeDebugAdapter(client.GetStream(), client.GetStream());
            adapter.Start();
            return (client, adapter);
        });

        try
        {
            await using var session = await DebugAdapterSession.ConnectAsync(Adapter(), port).WaitAsync(TestTimeouts.Default);
            var events = new Recorder(session);
            await LaunchAsync(session, new DapSourceBreakpoint(3));

            Assert.Equal("breakpoint", (await events.NextStopAsync()).Reason);
            var (client, adapter) = await accepting;
            Assert.Equal("configurationDone", adapter.Requests[^1].Command);
            await adapter.DisposeAsync();
            client.Dispose();
        }
        finally
        {
            listener.Stop();
            listener.Dispose();
        }
    }

    private static DebugAdapterDefinition Adapter() => new()
    {
        Id = "fake",
        Name = "Fake Debugger",
        Command = "fake-adapter",
        Arguments = ["--stdio"],
        Languages = ["python"],
        Defaults = new JsonObject { ["console"] = "internalConsole", ["justMyCode"] = true },
    };

    private static Task<DebugAdapterSession> StartAsync(IServerProcessLauncher launcher) =>
        DebugAdapterSession.StartAsync(Adapter(), "/fake/bin/fake-adapter", "/work", launcher).WaitAsync(TestTimeouts.Default);

    private static Task<IReadOnlyDictionary<string, IReadOnlyList<DapBreakpoint>>> LaunchAsync(DebugAdapterSession session, params DapSourceBreakpoint[] breakpoints)
    {
        var configuration = LaunchConfiguration.ForCurrentFile(session.Adapter, "Python").Substitute(new LaunchVariables("/work", Program));
        var byFile = new Dictionary<string, IReadOnlyList<DapSourceBreakpoint>>(StringComparer.Ordinal);
        if (breakpoints.Length > 0)
        {
            byFile[Program] = breakpoints;
        }

        return session.LaunchAsync(configuration, byFile, ["uncaught"], CancellationToken.None).WaitAsync(TestTimeouts.Default);
    }

    /// <summary>Collects what a session reports, for tests to wait on.</summary>
    private sealed class Recorder
    {
        private readonly Channel<DapStoppedEventArgs> _stops = Channel.CreateUnbounded<DapStoppedEventArgs>();
        private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Recorder(DebugAdapterSession session)
        {
            session.Stopped += (_, e) => _stops.Writer.TryWrite(e);
            session.OutputReceived += (_, e) => Output.Enqueue(e);
            session.Exited += (_, e) => _exited.TrySetResult(e.ExitCode);
            session.Ended += (_, _) => _ended.TrySetResult();
        }

        public ConcurrentQueue<DapOutputEventArgs> Output { get; } = new();

        public Task<int> Exited => _exited.Task;

        public Task Ended => _ended.Task;

        public Task<DapStoppedEventArgs> NextStopAsync() => _stops.Reader.ReadAsync().AsTask().WaitAsync(TestTimeouts.Default);
    }

    private sealed class FailingLauncher : IServerProcessLauncher
    {
        public string? Resolve(string command) => "/fake/bin/" + command;

        public IServerProcess Start(ServerStartInfo startInfo) => throw new ServerStartException("fake-adapter: permission denied");
    }

    /// <summary>Starts a fake adapter that listens on the port it is given, a little later, as a real one would.</summary>
    private sealed class TcpLauncher(Task? allowListen = null) : IServerProcessLauncher, IAsyncDisposable
    {
        private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TcpListener? _listener;
        private TcpClient? _client;
        private FakeDebugAdapter? _adapter;

        public ServerStartInfo? StartInfo { get; private set; }

        public int Port { get; private set; }

        public string? Resolve(string command) => "/fake/bin/" + command;

        public IServerProcess Start(ServerStartInfo startInfo)
        {
            StartInfo = startInfo;
            Port = int.Parse(startInfo.Arguments[1], CultureInfo.InvariantCulture);
            _ = Task.Run(async () =>
            {
                await Task.Delay(300);
                if (allowListen is not null)
                {
                    await allowListen;
                }

                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _client = await _listener.AcceptTcpClientAsync();
                _adapter = new FakeDebugAdapter(_client.GetStream(), _client.GetStream());
                _adapter.Start();
                _ = _adapter.Exited.ContinueWith(exit => _exited.TrySetResult(exit.Result), TaskScheduler.Default);
            });
            return new ListeningProcess(_exited);
        }

        public async ValueTask DisposeAsync()
        {
            _exited.TrySetResult(0);
            if (_adapter is not null)
            {
                await _adapter.DisposeAsync();
            }

            _client?.Dispose();
            _listener?.Stop();
            _listener?.Dispose();
        }

        private sealed class ListeningProcess(TaskCompletionSource<int> exited) : IServerProcess
        {
            public Stream StandardInput { get; } = new MemoryStream();

            public Stream StandardOutput { get; } = new MemoryStream();

            public Stream StandardError { get; } = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("waiting for debugger\n"));

            public Task<int> Exited => exited.Task;

            public void Kill() => exited.TrySetResult(-1);

            public void Dispose()
            {
                exited.TrySetResult(0);
                StandardInput.Dispose();
                StandardOutput.Dispose();
                StandardError.Dispose();
            }
        }
    }
}
