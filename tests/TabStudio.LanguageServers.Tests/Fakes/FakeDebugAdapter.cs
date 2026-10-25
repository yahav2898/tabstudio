using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.JsonRpc;

namespace TabStudio.LanguageServers.Tests.Fakes;

/// <summary>
/// A debug adapter scripted by a test, debugging an imaginary program: it follows the protocol's launch sequence,
/// stops at the first breakpoint set, and answers threads, stack, scopes and variables from fixed data.
/// </summary>
internal sealed class FakeDebugAdapter : IAsyncDisposable
{
    private readonly InMemoryPipe _toAdapter = new();
    private readonly InMemoryPipe _toClient = new();
    private readonly ConcurrentQueue<(string Command, JsonElement Arguments)> _requests = new();
    private readonly TaskCompletionSource _configured = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<int> _breakpointLines = [];

    /// <summary>Creates an adapter the client reaches through <see cref="ClientInput"/> and <see cref="ClientOutput"/>.</summary>
    public FakeDebugAdapter()
    {
        Connection = new DebugAdapterConnection(_toAdapter.Reader, _toClient.Writer);
    }

    /// <summary>Creates an adapter that talks over other streams, such as a socket's.</summary>
    public FakeDebugAdapter(Stream input, Stream output)
    {
        Connection = new DebugAdapterConnection(input, output);
    }

    /// <summary>The adapter's end of the connection.</summary>
    public DebugAdapterConnection Connection { get; }

    /// <summary>What the client reads.</summary>
    public Stream ClientInput => _toClient.Reader;

    /// <summary>What the client writes.</summary>
    public Stream ClientOutput => _toAdapter.Writer;

    /// <summary>The capabilities the adapter answers <c>initialize</c> with.</summary>
    public JsonObject Capabilities { get; } = new()
    {
        ["supportsConfigurationDoneRequest"] = true,
        ["supportsConditionalBreakpoints"] = true,
        ["supportsHitConditionalBreakpoints"] = true,
        ["supportsLogPoints"] = true,
        ["supportsSetVariable"] = true,
        ["supportsEvaluateForHovers"] = true,
        ["supportsExceptionInfoRequest"] = true,
        ["supportsTerminateRequest"] = true,
        ["exceptionBreakpointFilters"] = new JsonArray(
            new JsonObject { ["filter"] = "raised", ["label"] = "Raised Exceptions", ["default"] = false },
            new JsonObject { ["filter"] = "uncaught", ["label"] = "Uncaught Exceptions", ["default"] = true }),
    };

    /// <summary>When set, <c>launch</c> fails with this message.</summary>
    public string? LaunchFailure { get; set; }

    /// <summary>The reason the program stops with once configured; <c>exception</c> stops as at an unhandled error.</summary>
    public string StopReason { get; set; } = "breakpoint";

    /// <summary>The requests received, in order.</summary>
    public IReadOnlyList<(string Command, JsonElement Arguments)> Requests => [.. _requests];

    /// <summary>Completes with the exit code when the adapter's process would have exited.</summary>
    public Task<int> Exited => _exited.Task;

    public void Start()
    {
        Handle("initialize", _ => Capabilities.DeepClone());
        Connection.AddRequestHandler("launch", async (arguments, _) =>
        {
            Record("launch", arguments);
            if (LaunchFailure is { } failure)
            {
                throw new DebugAdapterException(failure);
            }

            await Connection.SendEventAsync("initialized", null);
            await _configured.Task;
            return null;
        });
        Handle("setBreakpoints", arguments =>
        {
            var breakpoints = new JsonArray();
            var id = 1;
            foreach (var breakpoint in arguments.GetProperty("breakpoints").EnumerateArray())
            {
                var line = breakpoint.GetProperty("line").GetInt32();
                var verified = line < 99;
                if (verified)
                {
                    _breakpointLines.Add(line);
                }

                breakpoints.Add(new JsonObject { ["id"] = id++, ["verified"] = verified, ["line"] = line, ["message"] = verified ? null : "No code at this line" });
            }

            return new JsonObject { ["breakpoints"] = breakpoints };
        });
        Handle("setExceptionBreakpoints", _ => null);
        Connection.AddRequestHandler("configurationDone", (arguments, cancellationToken) =>
        {
            Record("configurationDone", arguments);
            _configured.TrySetResult();
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                if (_breakpointLines.Count > 0 || StopReason == "exception")
                {
                    await Connection.SendEventAsync("stopped", new JsonObject { ["reason"] = StopReason, ["threadId"] = 1, ["allThreadsStopped"] = true, ["hitBreakpointIds"] = new JsonArray(1) });
                }
                else
                {
                    await FinishAsync();
                }
            }, CancellationToken.None);
            return Task.FromResult<JsonNode?>(null);
        });
        Handle("threads", _ => new JsonObject { ["threads"] = new JsonArray(new JsonObject { ["id"] = 1, ["name"] = "MainThread" }) });
        Handle("stackTrace", _ => new JsonObject
        {
            ["stackFrames"] = new JsonArray(
                new JsonObject { ["id"] = 10, ["name"] = "greet", ["source"] = new JsonObject { ["path"] = "/work/program.py" }, ["line"] = 3, ["column"] = 5, ["endLine"] = 3, ["endColumn"] = 20 },
                new JsonObject { ["id"] = 11, ["name"] = "<module>", ["source"] = new JsonObject { ["path"] = "/work/program.py" }, ["line"] = 6, ["column"] = 1 },
                new JsonObject { ["id"] = 12, ["name"] = "_run_code", ["source"] = new JsonObject { ["path"] = "/usr/lib/python3/runpy.py", ["presentationHint"] = "deemphasize" }, ["line"] = 86, ["column"] = 1 }),
            ["totalFrames"] = 3,
        });
        Handle("scopes", _ => new JsonObject
        {
            ["scopes"] = new JsonArray(
                new JsonObject { ["name"] = "Locals", ["presentationHint"] = "locals", ["variablesReference"] = 100, ["expensive"] = false },
                new JsonObject { ["name"] = "Globals", ["variablesReference"] = 200, ["expensive"] = false }),
        });
        Handle("variables", arguments => arguments.GetProperty("variablesReference").GetInt32() switch
        {
            100 => Variables(("name", "'Ada'", "str", 0), ("items", "[1, 2]", "list", 101)),
            101 => Variables(("0", "1", "int", 0), ("1", "2", "int", 0)),
            _ => Variables(("__name__", "'__main__'", "str", 0)),
        });
        Handle("evaluate", arguments => arguments.GetProperty("expression").GetString() switch
        {
            "name" => new JsonObject { ["result"] = "'Ada'", ["type"] = "str", ["variablesReference"] = 0 },
            "items" => new JsonObject { ["result"] = "[1, 2]", ["type"] = "list", ["variablesReference"] = 101 },
            var other => throw new DebugAdapterException($"NameError: name '{other}' is not defined"),
        });
        Handle("setVariable", arguments => new JsonObject { ["value"] = arguments.GetProperty("value").GetString(), ["type"] = "str" });
        Handle("exceptionInfo", _ => new JsonObject
        {
            ["exceptionId"] = "ZeroDivisionError",
            ["description"] = "division by zero",
            ["breakMode"] = "unhandled",
            ["details"] = new JsonObject { ["message"] = "division by zero", ["typeName"] = "builtins.ZeroDivisionError", ["stackTrace"] = "  File \"program.py\", line 3" },
        });
        Handle("next", arguments =>
        {
            _ = SendLaterAsync("stopped", new JsonObject { ["reason"] = "step", ["threadId"] = 1 });
            return null;
        });
        Handle("stepIn", arguments =>
        {
            _ = SendLaterAsync("stopped", new JsonObject { ["reason"] = "step", ["threadId"] = 1 });
            return null;
        });
        Handle("stepOut", arguments =>
        {
            _ = SendLaterAsync("stopped", new JsonObject { ["reason"] = "step", ["threadId"] = 1 });
            return null;
        });
        Handle("pause", arguments =>
        {
            _ = SendLaterAsync("stopped", new JsonObject { ["reason"] = "pause", ["threadId"] = 1 });
            return null;
        });
        Handle("continue", arguments =>
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(20);
                await FinishAsync();
            });
            return new JsonObject { ["allThreadsContinued"] = true };
        });
        Handle("terminate", arguments =>
        {
            _ = SendLaterAsync("terminated", new JsonObject());
            return null;
        });
        Handle("disconnect", arguments =>
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(20);
                Stop(0);
            });
            return null;
        });
        Connection.Start();
    }

    /// <summary>Ends the adapter as if its process crashed.</summary>
    public void Crash() => Stop(1);

    public async ValueTask DisposeAsync()
    {
        Stop(0);
        await Connection.DisposeAsync();
        _toAdapter.Dispose();
        _toClient.Dispose();
    }

    private static JsonObject Variables(params (string Name, string Value, string Type, int Reference)[] variables) => new()
    {
        ["variables"] = new JsonArray(variables.Select(v => (JsonNode?)new JsonObject
        {
            ["name"] = v.Name,
            ["value"] = v.Value,
            ["type"] = v.Type,
            ["evaluateName"] = v.Name,
            ["variablesReference"] = v.Reference,
        }).ToArray()),
    };

    private void Handle(string command, Func<JsonElement, JsonNode?> answer) =>
        Connection.AddRequestHandler(command, (arguments, _) =>
        {
            Record(command, arguments);
            return Task.FromResult(answer(arguments));
        });

    private void Record(string command, JsonElement arguments) => _requests.Enqueue((command, arguments));

    private async Task FinishAsync()
    {
        await Connection.SendEventAsync("output", new JsonObject { ["category"] = "stdout", ["output"] = "Hello, Ada\n" });
        await Connection.SendEventAsync("exited", new JsonObject { ["exitCode"] = 0 });
        await Connection.SendEventAsync("terminated", new JsonObject());
    }

    private async Task SendLaterAsync(string name, JsonNode body)
    {
        await Task.Delay(20);
        await Connection.SendEventAsync(name, body);
    }

    private void Stop(int exitCode)
    {
        _toClient.Complete();
        _toAdapter.Complete();
        _exited.TrySetResult(exitCode);
    }

    /// <summary>Starts <see cref="FakeDebugAdapter"/>s instead of programs.</summary>
    internal sealed class Launcher(Action<FakeDebugAdapter>? configure = null) : IServerProcessLauncher
    {
        private readonly List<FakeDebugAdapter> _started = [];

        public IReadOnlyList<FakeDebugAdapter> Started => _started;

        public ServerStartInfo? LastStart { get; private set; }

        public string? Resolve(string command) => "/fake/bin/" + command;

        public IServerProcess Start(ServerStartInfo startInfo)
        {
            LastStart = startInfo;
            var adapter = new FakeDebugAdapter();
            configure?.Invoke(adapter);
            adapter.Start();
            _started.Add(adapter);
            return new FakeProcess(adapter);
        }

        private sealed class FakeProcess(FakeDebugAdapter adapter) : IServerProcess
        {
            public Stream StandardInput => adapter.ClientOutput;

            public Stream StandardOutput => adapter.ClientInput;

            public Stream StandardError { get; } = new MemoryStream();

            public Task<int> Exited => adapter.Exited;

            public void Kill() => adapter.Stop(-1);

            public void Dispose() => StandardError.Dispose();
        }
    }
}
