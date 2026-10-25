using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using TabStudio.LanguageServers.JsonRpc;

namespace TabStudio.LanguageServers.DebugAdapters;

/// <summary>
/// A connection to a debug adapter: the Debug Adapter Protocol's requests, responses and events, numbered with
/// <c>seq</c>, over the same Content-Length framing as LSP, and the requests the adapter sends the client.
/// </summary>
/// <remarks>
/// Event handlers run one after another on the reading thread, in the order the events arrived, so they must be
/// quick. The connection does not own its streams.
/// </remarks>
internal sealed class DebugAdapterConnection : IAsyncDisposable
{
    private static readonly JsonElement NullElement = CreateNull();

    private readonly MessageReader _reader;
    private readonly MessageWriter _writer;
    private readonly ConcurrentDictionary<int, (string Command, TaskCompletionSource<JsonElement> Completion)> _pending = new();
    private readonly ConcurrentDictionary<string, Action<JsonElement>> _eventHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Func<JsonElement, CancellationToken, Task<JsonNode?>>> _requestHandlers = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _sequence;
    private int _started;

    /// <summary>Creates a connection. Nothing is read until <see cref="Start"/>.</summary>
    /// <param name="input">The stream messages arrive on.</param>
    /// <param name="output">The stream messages are sent on.</param>
    public DebugAdapterConnection(Stream input, Stream output)
    {
        _reader = new MessageReader(input);
        _writer = new MessageWriter(output);
    }

    /// <summary>Raised for every message sent and received, described for the message trace.</summary>
    public event EventHandler<JsonRpcTraceEventArgs>? Traced;

    /// <summary>Completes when the connection closed.</summary>
    public Task Completion => _closed.Task;

    /// <summary>Whether the connection closed.</summary>
    public bool IsClosed => _closed.Task.IsCompleted;

    /// <summary>Handles an event, such as <c>stopped</c>. Register before <see cref="Start"/>.</summary>
    /// <param name="name">The event.</param>
    /// <param name="handler">Receives the event's body.</param>
    public void AddEventHandler(string name, Action<JsonElement> handler) => _eventHandlers[name] = handler;

    /// <summary>Handles a request the adapter sends, such as <c>startDebugging</c>. Register before <see cref="Start"/>.</summary>
    /// <param name="command">The request.</param>
    /// <param name="handler">Answers with the response's body, or throws a <see cref="DebugAdapterException"/> to refuse.</param>
    public void AddRequestHandler(string command, Func<JsonElement, CancellationToken, Task<JsonNode?>> handler) => _requestHandlers[command] = handler;

    /// <summary>Starts reading messages.</summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _ = Task.Run(ReadLoopAsync);
        }
    }

    /// <summary>Sends a request and waits for the body of its response.</summary>
    /// <param name="command">The request, such as <c>threads</c>.</param>
    /// <param name="arguments">The arguments, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Stops waiting for the response.</param>
    /// <exception cref="DebugAdapterException">The adapter refused, or the connection closed.</exception>
    public Task<JsonElement> SendRequestAsync(string command, JsonNode? arguments, CancellationToken cancellationToken) =>
        SendRequestCoreAsync(command, arguments is null ? null : writer => arguments.WriteTo(writer), cancellationToken);

    /// <summary>Sends a request with typed arguments and waits for the body of its response.</summary>
    /// <typeparam name="TArguments">The type of the arguments.</typeparam>
    /// <param name="command">The request.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="argumentsType">How to write the arguments.</param>
    /// <param name="cancellationToken">Stops waiting for the response.</param>
    /// <exception cref="DebugAdapterException">The adapter refused, or the connection closed.</exception>
    public Task<JsonElement> SendRequestAsync<TArguments>(string command, TArguments arguments, JsonTypeInfo<TArguments> argumentsType, CancellationToken cancellationToken) =>
        SendRequestCoreAsync(command, writer => JsonSerializer.Serialize(writer, arguments, argumentsType), cancellationToken);

    /// <summary>Sends a request with typed arguments and reads a typed body.</summary>
    /// <typeparam name="TArguments">The type of the arguments.</typeparam>
    /// <typeparam name="TBody">The type of the response's body.</typeparam>
    /// <param name="command">The request.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="argumentsType">How to write the arguments.</param>
    /// <param name="bodyType">How to read the body.</param>
    /// <param name="cancellationToken">Stops waiting for the response.</param>
    /// <exception cref="DebugAdapterException">The adapter refused, or the connection closed.</exception>
    public async Task<TBody?> SendRequestAsync<TArguments, TBody>(string command, TArguments arguments, JsonTypeInfo<TArguments> argumentsType, JsonTypeInfo<TBody> bodyType, CancellationToken cancellationToken)
    {
        var body = await SendRequestAsync(command, arguments, argumentsType, cancellationToken).ConfigureAwait(false);
        return body.ValueKind == JsonValueKind.Object ? body.Deserialize(bodyType) : default;
    }

    /// <summary>Sends an event, as an adapter does; for adapters written in .NET, such as the tests' scripted one.</summary>
    /// <param name="name">The event, such as <c>stopped</c>.</param>
    /// <param name="body">Its body, or <see langword="null"/> for none.</param>
    public async Task SendEventAsync(string name, JsonNode? body)
    {
        var message = Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("seq", Interlocked.Increment(ref _sequence));
            writer.WriteString("type", "event");
            writer.WriteString("event", name);
            if (body is not null)
            {
                writer.WritePropertyName("body");
                body.WriteTo(writer);
            }

            writer.WriteEndObject();
        });

        try
        {
            await _writer.WriteAsync(message, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw Closed(name);
        }
    }

    /// <summary>Stops reading and fails the requests still waiting for a response.</summary>
    public ValueTask DisposeAsync()
    {
        if (!_stop.IsCancellationRequested)
        {
            _stop.Cancel();
            Close();
            _writer.Dispose();
            _stop.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private async Task<JsonElement> SendRequestCoreAsync(string command, Action<Utf8JsonWriter>? writeArguments, CancellationToken cancellationToken)
    {
        if (IsClosed)
        {
            throw Closed(command);
        }

        var sequence = Interlocked.Increment(ref _sequence);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[sequence] = (command, completion);
        if (IsClosed && _pending.TryRemove(sequence, out _))
        {
            throw Closed(command);
        }

        var body = Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("seq", sequence);
            writer.WriteString("type", "request");
            writer.WriteString("command", command);
            if (writeArguments is not null)
            {
                writer.WritePropertyName("arguments");
                writeArguments(writer);
            }

            writer.WriteEndObject();
        });

        Trace(string.Create(CultureInfo.InvariantCulture, $"Sending request '{command}' ({sequence})."));
        try
        {
            await _writer.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _pending.TryRemove(sequence, out _);
            throw Closed(command);
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(sequence, out _);
            throw;
        }

        using var registration = cancellationToken.Register(() =>
        {
            if (_pending.TryRemove(sequence, out var pending))
            {
                pending.Completion.TrySetCanceled(cancellationToken);
            }
        });
        return await completion.Task.ConfigureAwait(false);
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (true)
            {
                var body = await _reader.ReadAsync(_stop.Token).ConfigureAwait(false);
                if (body is null)
                {
                    break;
                }

                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(body);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (document)
                {
                    Dispatch(document.RootElement);
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // Disposed.
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidDataException or NotSupportedException)
        {
            Trace($"The connection to the debug adapter failed: {ex.Message}");
        }
        finally
        {
            Close();
        }
    }

    private void Dispatch(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("type", out var typeElement))
        {
            return;
        }

        switch (typeElement.GetString())
        {
            case "response":
                OnResponse(message);
                break;
            case "event" when message.TryGetProperty("event", out var name) && name.GetString() is { } eventName:
                Trace($"Received event '{eventName}'.");
                if (_eventHandlers.TryGetValue(eventName, out var handler))
                {
                    try
                    {
                        handler(message.TryGetProperty("body", out var eventBody) ? eventBody.Clone() : NullElement);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        Trace($"The handler of '{eventName}' failed: {ex.Message}");
                    }
                }

                break;
            case "request" when message.TryGetProperty("command", out var command) && command.GetString() is { } commandName
                && message.TryGetProperty("seq", out var seq) && seq.TryGetInt32(out var requestSequence):
                OnRequest(requestSequence, commandName, message.TryGetProperty("arguments", out var arguments) ? arguments.Clone() : NullElement);
                break;
        }
    }

    private void OnResponse(JsonElement message)
    {
        if (!message.TryGetProperty("request_seq", out var requestSeq) || requestSeq.ValueKind != JsonValueKind.Number
            || !requestSeq.TryGetInt32(out var sequence) || !_pending.TryRemove(sequence, out var pending))
        {
            return;
        }

        Trace(string.Create(CultureInfo.InvariantCulture, $"Received response '{pending.Command}' ({sequence})."));
        var success = message.TryGetProperty("success", out var flag) && flag.ValueKind == JsonValueKind.True;
        var body = message.TryGetProperty("body", out var value) ? value.Clone() : NullElement;
        if (success)
        {
            pending.Completion.TrySetResult(body);
            return;
        }

        var text = message.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String ? messageElement.GetString() : null;
        pending.Completion.TrySetException(new DebugAdapterException(pending.Command, ErrorText(body) ?? text ?? $"'{pending.Command}' failed.", isConnectionClosed: false));
    }

    private void OnRequest(int sequence, string command, JsonElement arguments)
    {
        Trace($"Received request '{command}'.");
        _ = Task.Run(async () =>
        {
            JsonNode? body = null;
            string? error = null;
            if (_requestHandlers.TryGetValue(command, out var handler))
            {
                try
                {
                    body = await handler(arguments, _stop.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    error = ex.Message;
                }
            }
            else
            {
                error = $"TabStudio does not handle '{command}'.";
            }

            var response = Serialize(writer =>
            {
                writer.WriteStartObject();
                writer.WriteNumber("seq", Interlocked.Increment(ref _sequence));
                writer.WriteString("type", "response");
                writer.WriteNumber("request_seq", sequence);
                writer.WriteBoolean("success", error is null);
                writer.WriteString("command", command);
                if (error is not null)
                {
                    writer.WriteString("message", error);
                }

                if (body is not null)
                {
                    writer.WritePropertyName("body");
                    body.WriteTo(writer);
                }

                writer.WriteEndObject();
            });

            try
            {
                await _writer.WriteAsync(response, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The adapter is gone.
            }
        });
    }

    private void Close()
    {
        if (_closed.Task.IsCompleted)
        {
            return;
        }

        foreach (var sequence in _pending.Keys)
        {
            if (_pending.TryRemove(sequence, out var pending))
            {
                pending.Completion.TrySetException(Closed(pending.Command));
            }
        }

        _closed.TrySetResult();
    }

    private void Trace(string message) => Traced?.Invoke(this, new JsonRpcTraceEventArgs(message));

    /// <summary>The message of an error body: its format with the variables filled in.</summary>
    private static string? ErrorText(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object
            || !error.TryGetProperty("format", out var format) || format.GetString() is not { } text)
        {
            return null;
        }

        if (!error.TryGetProperty("variables", out var variables) || variables.ValueKind != JsonValueKind.Object)
        {
            return text;
        }

        var result = new StringBuilder(text);
        foreach (var variable in variables.EnumerateObject())
        {
            result.Replace("{" + variable.Name + "}", variable.Value.ValueKind == JsonValueKind.String ? variable.Value.GetString() : variable.Value.GetRawText());
        }

        return result.ToString();
    }

    private static DebugAdapterException Closed(string command) =>
        new(command, "The connection to the debug adapter closed.", isConnectionClosed: true);

    private static byte[] Serialize(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static JsonElement CreateNull()
    {
        using var document = JsonDocument.Parse("null");
        return document.RootElement.Clone();
    }
}
