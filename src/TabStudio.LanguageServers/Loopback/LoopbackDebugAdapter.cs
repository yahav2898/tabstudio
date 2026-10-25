using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.JsonRpc;

namespace TabStudio.LanguageServers.Loopback;

/// <summary>
/// A debug adapter for <see cref="LoopbackScript"/> that runs inside TabStudio, over in-memory streams: it runs the
/// script line by line, stops at breakpoints (with conditions, hit counts and logpoints), steps, pauses, and shows
/// the names set so far as locals, which can be changed. The self test debugs with it on a device without real
/// adapters, and the IDE's tests use it.
/// </summary>
public sealed class LoopbackDebugAdapter : IAsyncDisposable
{
    private const int LocalsReference = 1;
    private const int ThreadId = 1;
    private const int FrameId = 1;

    private readonly InMemoryPipe _toAdapter = new();
    private readonly InMemoryPipe _toClient = new();
    private readonly DebugAdapterConnection _connection;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<int, int> _hits = [];
    private readonly SemaphoreSlim _resume = new(0);
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Dictionary<int, DapSourceBreakpoint> _breakpoints = [];
    private List<LoopbackStatement> _program = [];
    private string _programPath = string.Empty;
    private bool _stopOnEntry;
    private bool _noDebug;
    private int _line;
    private Mode _mode = Mode.Run;
    private bool _pauseRequested;
    private bool _stopped;

    /// <summary>Creates the adapter; it answers once <see cref="Start"/> was called.</summary>
    public LoopbackDebugAdapter()
    {
        _connection = new DebugAdapterConnection(_toAdapter.Reader, _toClient.Writer);
    }

    private enum Mode
    {
        Run,
        Step,
    }

    /// <summary>What the client writes: the adapter's standard input.</summary>
    public Stream StandardInput => _toAdapter.Writer;

    /// <summary>What the client reads: the adapter's standard output.</summary>
    public Stream StandardOutput => _toClient.Reader;

    /// <summary>Completes with the exit code once the client disconnected or the adapter was stopped.</summary>
    public Task<int> Exited => _exited.Task;

    /// <summary>Starts answering.</summary>
    public void Start()
    {
        Handle("initialize", _ => new JsonObject
        {
            ["supportsConfigurationDoneRequest"] = true,
            ["supportsConditionalBreakpoints"] = true,
            ["supportsHitConditionalBreakpoints"] = true,
            ["supportsLogPoints"] = true,
            ["supportsSetVariable"] = true,
            ["supportsEvaluateForHovers"] = true,
            ["supportsExceptionInfoRequest"] = true,
            ["supportsTerminateRequest"] = true,
            ["exceptionBreakpointFilters"] = new JsonArray(new JsonObject { ["filter"] = "uncaught", ["label"] = "Uncaught Errors", ["default"] = true }),
        });
        _connection.AddRequestHandler("launch", async (arguments, _) =>
        {
            _programPath = arguments.TryGetProperty("program", out var program) ? program.GetString() ?? string.Empty : string.Empty;
            _stopOnEntry = arguments.TryGetProperty("stopOnEntry", out var entry) && entry.ValueKind == JsonValueKind.True;
            _noDebug = arguments.TryGetProperty("noDebug", out var noDebug) && noDebug.ValueKind == JsonValueKind.True;
            try
            {
                _program = LoopbackScript.Parse(await File.ReadAllTextAsync(_programPath).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new DebugAdapterException($"Cannot read {_programPath}: {ex.Message}", ex);
            }

            await _connection.SendEventAsync("initialized", null).ConfigureAwait(false);
            return null;
        });
        Handle("setBreakpoints", arguments =>
        {
            var lines = new Dictionary<int, DapSourceBreakpoint>();
            var result = new JsonArray();
            foreach (var breakpoint in arguments.GetProperty("breakpoints").EnumerateArray())
            {
                var line = breakpoint.GetProperty("line").GetInt32();
                var statement = line >= 1 && line <= _program.Count ? _program[line - 1] : null;
                var verified = statement is not null && statement.Kind != LoopbackStatementKind.None;
                if (verified)
                {
                    lines[line] = new DapSourceBreakpoint(
                        line,
                        breakpoint.TryGetProperty("condition", out var condition) ? condition.GetString() : null,
                        breakpoint.TryGetProperty("hitCondition", out var hits) ? hits.GetString() : null,
                        breakpoint.TryGetProperty("logMessage", out var log) ? log.GetString() : null);
                }

                result.Add(new JsonObject { ["verified"] = verified, ["line"] = line, ["message"] = verified ? null : "There is no statement on this line." });
            }

            lock (_gate)
            {
                _breakpoints = lines;
            }

            return new JsonObject { ["breakpoints"] = result };
        });
        Handle("setExceptionBreakpoints", _ => null);
        Handle("configurationDone", arguments =>
        {
            _ = Task.Run(RunAsync);
            return null;
        });
        Handle("threads", _ => new JsonObject { ["threads"] = new JsonArray(new JsonObject { ["id"] = ThreadId, ["name"] = "Main" }) });
        Handle("stackTrace", _ => new JsonObject
        {
            ["stackFrames"] = new JsonArray(new JsonObject
            {
                ["id"] = FrameId,
                ["name"] = Path.GetFileNameWithoutExtension(_programPath),
                ["source"] = new JsonObject { ["name"] = Path.GetFileName(_programPath), ["path"] = _programPath },
                ["line"] = _line + 1,
                ["column"] = _line < _program.Count ? _program[_line].Column + 1 : 1,
            }),
            ["totalFrames"] = 1,
        });
        Handle("scopes", _ => new JsonObject
        {
            ["scopes"] = new JsonArray(new JsonObject { ["name"] = "Locals", ["presentationHint"] = "locals", ["variablesReference"] = LocalsReference, ["expensive"] = false }),
        });
        Handle("variables", arguments =>
        {
            var variables = new JsonArray();
            if (arguments.GetProperty("variablesReference").GetInt32() == LocalsReference)
            {
                lock (_gate)
                {
                    foreach (var (name, value) in _variables)
                    {
                        variables.Add(new JsonObject { ["name"] = name, ["value"] = value, ["type"] = LoopbackScript.TypeOf(value), ["evaluateName"] = name, ["variablesReference"] = 0 });
                    }
                }
            }

            return new JsonObject { ["variables"] = variables };
        });
        Handle("evaluate", arguments =>
        {
            var expression = arguments.GetProperty("expression").GetString() ?? string.Empty;
            string? value;
            lock (_gate)
            {
                value = LoopbackScript.Evaluate(expression, _variables);
            }

            return value is null
                ? throw new DebugAdapterException($"'{expression.Trim()}' is not defined.")
                : new JsonObject { ["result"] = value, ["type"] = LoopbackScript.TypeOf(value), ["variablesReference"] = 0 };
        });
        Handle("setVariable", arguments =>
        {
            var name = arguments.GetProperty("name").GetString() ?? string.Empty;
            var expression = arguments.GetProperty("value").GetString() ?? string.Empty;
            lock (_gate)
            {
                var value = LoopbackScript.Evaluate(expression, _variables) ?? throw new DebugAdapterException($"'{expression}' is not a value.");
                _variables[name] = value;
                return new JsonObject { ["value"] = value, ["type"] = LoopbackScript.TypeOf(value), ["variablesReference"] = 0 };
            }
        });
        Handle("exceptionInfo", _ =>
        {
            var name = _line < _program.Count ? Undefined(_program[_line]) : string.Empty;
            return new JsonObject
            {
                ["exceptionId"] = "NameError",
                ["description"] = $"'{name}' is not defined.",
                ["breakMode"] = "unhandled",
                ["details"] = new JsonObject { ["message"] = $"'{name}' is not defined.", ["typeName"] = "NameError" },
            };
        });
        Handle("continue", _ => Resume(Mode.Run, new JsonObject { ["allThreadsContinued"] = true }));
        Handle("next", _ => Resume(Mode.Step, null));
        Handle("stepIn", _ => Resume(Mode.Step, null));
        Handle("stepOut", _ => Resume(Mode.Run, null));
        Handle("pause", _ =>
        {
            _pauseRequested = true;
            return null;
        });
        Handle("terminate", arguments =>
        {
            _ = FinishAsync(-1);
            return null;
        });
        Handle("disconnect", arguments =>
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(20).ConfigureAwait(false);
                Stop(0);
            });
            return null;
        });
        _connection.Start();
    }

    /// <summary>Ends the adapter, as if its process exited.</summary>
    /// <param name="exitCode">The exit code to report.</param>
    public void Stop(int exitCode)
    {
        _resume.Release();
        _toClient.Complete();
        _toAdapter.Complete();
        _exited.TrySetResult(exitCode);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Stop(0);
        await _connection.DisposeAsync().ConfigureAwait(false);
        _toAdapter.Dispose();
        _toClient.Dispose();
        _resume.Dispose();
    }

    /// <summary>The name a statement uses that has no value: what a <c>let</c> gives, or what a <c>print</c> writes.</summary>
    private static string Undefined(LoopbackStatement statement) =>
        statement.Kind == LoopbackStatementKind.Let ? statement.Value : statement.Name;

    private void Handle(string command, Func<JsonElement, JsonNode?> answer) =>
        _connection.AddRequestHandler(command, (arguments, _) => Task.FromResult(answer(arguments)));

    private JsonObject? Resume(Mode mode, JsonObject? body)
    {
        _mode = mode;
        if (_stopped)
        {
            _stopped = false;
            _resume.Release();
        }

        return body;
    }

    /// <summary>Runs the script, stopping where breakpoints, steps, pauses and errors say.</summary>
    private async Task RunAsync()
    {
        try
        {
            var first = true;
            for (_line = 0; _line < _program.Count && !_exited.Task.IsCompleted; _line++)
            {
                var statement = _program[_line];
                if (statement.Kind == LoopbackStatementKind.None)
                {
                    continue;
                }

                var reason = first && _stopOnEntry ? "entry" : _pauseRequested ? "pause" : _mode == Mode.Step && !first ? "step" : await BreakpointReasonAsync(_line + 1).ConfigureAwait(false);
                first = false;
                if (reason is not null && !_noDebug)
                {
                    _pauseRequested = false;
                    await StopAsync(reason).ConfigureAwait(false);
                }

                if (!await ExecuteAsync(statement).ConfigureAwait(false))
                {
                    await StopAsync("exception", $"'{Undefined(statement)}' is not defined.").ConfigureAwait(false);
                    await FinishAsync(1).ConfigureAwait(false);
                    return;
                }
            }

            await FinishAsync(0).ConfigureAwait(false);
        }
        catch (DebugAdapterException)
        {
            // The client went away.
        }
    }

    /// <summary>Whether a breakpoint stops the script at a line, after logging what a logpoint logs.</summary>
    private async Task<string?> BreakpointReasonAsync(int line)
    {
        DapSourceBreakpoint? breakpoint;
        lock (_gate)
        {
            _breakpoints.TryGetValue(line, out breakpoint);
        }

        if (breakpoint is null)
        {
            return null;
        }

        if (breakpoint.Condition is { Length: > 0 } condition && !IsTrue(condition))
        {
            return null;
        }

        var hits = _hits[line] = _hits.GetValueOrDefault(line) + 1;
        if (breakpoint.HitCount is { Length: > 0 } hitCount && int.TryParse(hitCount.TrimStart('=', ' '), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && hits < count)
        {
            return null;
        }

        if (breakpoint.LogMessage is { } message)
        {
            await OutputAsync("console", Interpolate(message) + "\n").ConfigureAwait(false);
            return null;
        }

        return "breakpoint";
    }

    private bool IsTrue(string condition)
    {
        var parts = condition.Split("==", 2, StringSplitOptions.TrimEntries);
        lock (_gate)
        {
            return parts.Length == 2
                && LoopbackScript.Evaluate(parts[0], _variables) is { } left
                && string.Equals(left, LoopbackScript.Evaluate(parts[1], _variables), StringComparison.Ordinal);
        }
    }

    private string Interpolate(string message)
    {
        var text = new System.Text.StringBuilder();
        var i = 0;
        while (i < message.Length)
        {
            var open = message.IndexOf('{', i);
            var close = open < 0 ? -1 : message.IndexOf('}', open);
            if (open < 0 || close < 0)
            {
                text.Append(message, i, message.Length - i);
                break;
            }

            text.Append(message, i, open - i);
            lock (_gate)
            {
                var value = LoopbackScript.Evaluate(message[(open + 1)..close], _variables);
                text.Append(value is null ? message[open..(close + 1)] : LoopbackScript.Display(value));
            }

            i = close + 1;
        }

        return text.ToString();
    }

    private async Task<bool> ExecuteAsync(LoopbackStatement statement)
    {
        string? value;
        lock (_gate)
        {
            value = LoopbackScript.Evaluate(statement.Kind == LoopbackStatementKind.Let ? statement.Value : statement.Name, _variables);
            if (value is not null && statement.Kind == LoopbackStatementKind.Let)
            {
                _variables[statement.Name] = value;
            }
        }

        if (value is null && statement.Kind is LoopbackStatementKind.Let or LoopbackStatementKind.Print)
        {
            return false;
        }

        if (statement.Kind == LoopbackStatementKind.Print)
        {
            await OutputAsync("stdout", LoopbackScript.Display(value!) + "\n").ConfigureAwait(false);
        }

        return true;
    }

    private async Task StopAsync(string reason, string? text = null)
    {
        _stopped = true;
        await _connection.SendEventAsync("stopped", new JsonObject
        {
            ["reason"] = reason,
            ["threadId"] = ThreadId,
            ["allThreadsStopped"] = true,
            ["text"] = text,
        }).ConfigureAwait(false);
        await _resume.WaitAsync().ConfigureAwait(false);
    }

    private Task OutputAsync(string category, string output) =>
        _connection.SendEventAsync("output", new JsonObject { ["category"] = category, ["output"] = output });

    private async Task FinishAsync(int exitCode)
    {
        if (_exited.Task.IsCompleted)
        {
            return;
        }

        try
        {
            await _connection.SendEventAsync("exited", new JsonObject { ["exitCode"] = exitCode }).ConfigureAwait(false);
            await _connection.SendEventAsync("terminated", new JsonObject()).ConfigureAwait(false);
        }
        catch (DebugAdapterException)
        {
            // The client went away first.
        }

        // A stopped script waiting for a resume ends too.
        _program = [];
        _resume.Release();
    }
}
