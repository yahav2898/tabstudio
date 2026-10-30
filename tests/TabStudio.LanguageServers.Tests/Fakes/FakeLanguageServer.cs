using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.JsonRpc;

namespace TabStudio.LanguageServers.Tests.Fakes;

/// <summary>
/// A language server scripted by a test, running in the test's process over in-memory pipes: it answers
/// <c>initialize</c> with <see cref="Capabilities"/>, records every notification, answers requests with the handlers
/// the test registered, and can send requests of its own or crash.
/// </summary>
internal sealed class FakeLanguageServer : IAsyncDisposable
{
    private readonly InMemoryPipe _toServer = new();
    private readonly InMemoryPipe _toClient = new();
    private readonly ConcurrentQueue<(string Method, JsonElement Parameters)> _notifications = new();
    private readonly ConcurrentQueue<(string Method, JsonElement Parameters)> _requests = new();
    private readonly SemaphoreSlim _received = new(0);
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<string, Func<JsonElement, Task<JsonNode?>>> _handlers = new(StringComparer.Ordinal);

    public FakeLanguageServer()
    {
        Connection = new JsonRpcConnection(_toServer.Reader, _toClient.Writer);
    }

    /// <summary>The server's end of the connection, for sending requests and notifications to the client.</summary>
    public JsonRpcConnection Connection { get; }

    /// <summary>What the client reads: the server's output.</summary>
    public Stream ClientInput => _toClient.Reader;

    /// <summary>What the client writes: the server's input.</summary>
    public Stream ClientOutput => _toServer.Writer;

    /// <summary>The capabilities the server answers <c>initialize</c> with.</summary>
    public JsonObject Capabilities { get; } = new()
    {
        ["textDocumentSync"] = new JsonObject { ["openClose"] = true, ["change"] = 2, ["save"] = new JsonObject { ["includeText"] = false } },
    };

    /// <summary>Completes with the exit code when the server exits or crashes.</summary>
    public Task<int> Exited => _exited.Task;

    /// <summary>The notifications received, in order.</summary>
    public IReadOnlyList<(string Method, JsonElement Parameters)> Notifications => [.. _notifications];

    /// <summary>The requests received, in order, <c>initialize</c> first.</summary>
    public IReadOnlyList<(string Method, JsonElement Parameters)> Requests => [.. _requests];

    /// <summary>Answers a request with a handler. Register before the client starts the server.</summary>
    public void On(string method, Func<JsonElement, JsonNode?> handler) => _handlers[method] = parameters => Task.FromResult(handler(parameters));

    /// <summary>Answers a request with a handler that may itself wait, such as for a request to the client.</summary>
    public void OnAsync(string method, Func<JsonElement, Task<JsonNode?>> handler) => _handlers[method] = handler;

    /// <summary>Starts answering.</summary>
    public void Start()
    {
        Connection.AddRequestHandler("initialize", (parameters, _) => Record("initialize", parameters, () => Task.FromResult<JsonNode?>(new JsonObject
        {
            ["capabilities"] = Capabilities.DeepClone(),
            ["serverInfo"] = new JsonObject { ["name"] = "fake-server", ["version"] = "1.0" },
        })));
        Connection.AddRequestHandler("shutdown", (parameters, _) => Record("shutdown", parameters, () => Task.FromResult<JsonNode?>(null)));
        foreach (var (method, handler) in _handlers)
        {
            Connection.AddRequestHandler(method, (parameters, _) => Record(method, parameters, () => handler(parameters)));
        }

        foreach (var method in new[] { "initialized", "textDocument/didOpen", "textDocument/didChange", "textDocument/didSave", "textDocument/didClose", "workspace/didChangeConfiguration", "workspace/didChangeWatchedFiles", "$/cancelRequest" })
        {
            Connection.AddNotificationHandler(method, parameters => Notify(method, parameters));
        }

        Connection.AddNotificationHandler("exit", parameters =>
        {
            Notify("exit", parameters);
            Stop(0);
        });
        Connection.Start();
    }

    /// <summary>Waits for a notification that matches, which may have arrived already.</summary>
    public Task<JsonElement> WaitForNotificationAsync(string method, Func<JsonElement, bool>? matches = null) =>
        WaitForAsync(() => _notifications.Where(n => n.Method == method && (matches?.Invoke(n.Parameters) ?? true)).Select(n => (JsonElement?)n.Parameters).LastOrDefault());

    /// <summary>Waits for a request that matches, which may have arrived already.</summary>
    public Task<JsonElement> WaitForRequestAsync(string method) =>
        WaitForAsync(() => _requests.Where(n => n.Method == method).Select(n => (JsonElement?)n.Parameters).LastOrDefault());

    /// <summary>Ends the server as a crash does: its output closes and it exits with a failure code.</summary>
    public void Crash(int exitCode = 1) => Stop(exitCode);

    /// <summary>Sends a notification to the client.</summary>
    public Task NotifyClientAsync(string method, JsonNode parameters) => Connection.SendNotificationAsync(method, parameters);

    /// <summary>Sends a request to the client and waits for its answer.</summary>
    public Task<JsonElement> RequestClientAsync(string method, JsonNode parameters) =>
        Connection.SendRequestAsync(method, parameters, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        Stop(0);
        await Connection.DisposeAsync();
        _toServer.Dispose();
        _toClient.Dispose();
        _received.Dispose();
    }

    private Task<JsonNode?> Record(string method, JsonElement parameters, Func<Task<JsonNode?>> answer)
    {
        _requests.Enqueue((method, parameters));
        _received.Release();
        return answer();
    }

    private void Notify(string method, JsonElement parameters)
    {
        _notifications.Enqueue((method, parameters));
        _received.Release();
    }

    private void Stop(int exitCode)
    {
        _toClient.Complete();
        _toServer.Complete();
        _exited.TrySetResult(exitCode);
    }

    private async Task<JsonElement> WaitForAsync(Func<JsonElement?> find)
    {
        var deadline = DateTime.UtcNow + TestTimeouts.Default;
        while (true)
        {
            if (find() is { } found)
            {
                return found;
            }

            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero)
            {
                throw new TimeoutException("The fake server did not receive the message in time.");
            }

            await _received.WaitAsync(left < TimeSpan.FromMilliseconds(200) ? left : TimeSpan.FromMilliseconds(200));
        }
    }
}
