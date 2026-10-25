using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace TabStudio.LanguageServers.JsonRpc;

/// <summary>
/// A JSON-RPC 2.0 connection over a pair of streams with Content-Length framing, as the Language Server Protocol
/// uses it: requests with ids and their responses, notifications, cancellation with <c>$/cancelRequest</c> in both
/// directions, and requests the other side sends.
/// </summary>
/// <remarks>
/// <para>
/// Register handlers, then call <see cref="Start"/>. Notification handlers run one after another on the reading
/// thread, in the order the notifications arrived, so they must be quick; request handlers run on the thread pool.
/// </para>
/// <para>
/// The connection does not own its streams: whoever created them closes them, which ends <see cref="Completion"/>.
/// Every method may be called from any thread, and messages are written whole, in the order they were sent.
/// </para>
/// </remarks>
public sealed class JsonRpcConnection : IAsyncDisposable
{
    private static readonly JsonElement NullElement = CreateNull();

    private readonly MessageReader _reader;
    private readonly MessageWriter _writer;
    private readonly ConcurrentDictionary<long, PendingRequest> _pending = new();
    private readonly ConcurrentDictionary<string, Func<JsonElement, CancellationToken, Task<JsonNode?>>> _requestHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Action<JsonElement>> _notificationHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _incoming = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _nextId;
    private int _started;
    private volatile Exception? _closeReason;

    /// <summary>Creates a connection. Nothing is read until <see cref="Start"/>.</summary>
    /// <param name="input">The stream messages arrive on, such as a server's standard output.</param>
    /// <param name="output">The stream messages are sent on, such as a server's standard input.</param>
    public JsonRpcConnection(Stream input, Stream output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        _reader = new MessageReader(input);
        _writer = new MessageWriter(output);
    }

    /// <summary>Raised for every message sent and received, with a line describing it, for the message trace.</summary>
    public event EventHandler<JsonRpcTraceEventArgs>? Traced;

    /// <summary>Completes when the connection closed: the input ended, failed or the connection was disposed.</summary>
    public Task Completion => _closed.Task;

    /// <summary>Why the connection closed, when reading failed; <see langword="null"/> for an orderly end.</summary>
    public Exception? CloseReason => _closeReason;

    /// <summary>Whether the connection closed.</summary>
    public bool IsClosed => _closed.Task.IsCompleted;

    /// <summary>Handles a request the other side sends, such as <c>workspace/configuration</c>. Register before <see cref="Start"/>.</summary>
    /// <param name="method">The method.</param>
    /// <param name="handler">Answers with the result, or throws a <see cref="JsonRpcException"/> to answer with an error.</param>
    public void AddRequestHandler(string method, Func<JsonElement, CancellationToken, Task<JsonNode?>> handler)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(handler);
        _requestHandlers[method] = handler;
    }

    /// <summary>Handles a notification the other side sends, such as <c>window/logMessage</c>. Register before <see cref="Start"/>.</summary>
    /// <param name="method">The method.</param>
    /// <param name="handler">Receives the parameters; runs on the reading thread, so it must be quick.</param>
    public void AddNotificationHandler(string method, Action<JsonElement> handler)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(handler);
        _notificationHandlers[method] = handler;
    }

    /// <summary>Starts reading messages. Calling it again does nothing.</summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _ = Task.Run(ReadLoopAsync);
        }
    }

    /// <summary>Sends a request and waits for its result.</summary>
    /// <param name="method">The method, such as <c>textDocument/hover</c>.</param>
    /// <param name="parameters">The parameters, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the request, which also tells the other side with <c>$/cancelRequest</c>.</param>
    /// <returns>The result; a JSON <c>null</c> when there is none.</returns>
    /// <exception cref="JsonRpcException">The other side answered with an error, or the connection closed.</exception>
    public Task<JsonElement> SendRequestAsync(string method, JsonNode? parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(method);
        return SendRequestCoreAsync(method, WriterFor(parameters), cancellationToken);
    }

    /// <summary>Sends a request with typed parameters and reads a typed result.</summary>
    /// <typeparam name="TParameters">The type of the parameters.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="method">The method.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="parametersType">How to write the parameters.</param>
    /// <param name="resultType">How to read the result.</param>
    /// <param name="cancellationToken">Cancels the request, which also tells the other side with <c>$/cancelRequest</c>.</param>
    /// <returns>The result, or the default value when the result is a JSON <c>null</c>.</returns>
    /// <exception cref="JsonRpcException">The other side answered with an error, or the connection closed.</exception>
    public async Task<TResult?> SendRequestAsync<TParameters, TResult>(
        string method,
        TParameters parameters,
        JsonTypeInfo<TParameters> parametersType,
        JsonTypeInfo<TResult> resultType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(parametersType);
        ArgumentNullException.ThrowIfNull(resultType);
        var result = await SendRequestCoreAsync(method, writer => JsonSerializer.Serialize(writer, parameters, parametersType), cancellationToken).ConfigureAwait(false);
        return result.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? default : result.Deserialize(resultType);
    }

    /// <summary>Sends a notification.</summary>
    /// <param name="method">The method, such as <c>initialized</c>.</param>
    /// <param name="parameters">The parameters, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels waiting to send; a message being written is always written whole.</param>
    /// <exception cref="JsonRpcException">The connection closed.</exception>
    public Task SendNotificationAsync(string method, JsonNode? parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        return SendNotificationCoreAsync(method, WriterFor(parameters), cancellationToken);
    }

    /// <summary>Sends a notification with typed parameters.</summary>
    /// <typeparam name="TParameters">The type of the parameters.</typeparam>
    /// <param name="method">The method.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="parametersType">How to write the parameters.</param>
    /// <param name="cancellationToken">Cancels waiting to send; a message being written is always written whole.</param>
    /// <exception cref="JsonRpcException">The connection closed.</exception>
    public Task SendNotificationAsync<TParameters>(string method, TParameters parameters, JsonTypeInfo<TParameters> parametersType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(parametersType);
        return SendNotificationCoreAsync(method, writer => JsonSerializer.Serialize(writer, parameters, parametersType), cancellationToken);
    }

    /// <summary>Stops reading and fails the requests still waiting for an answer. The streams are left to their owner.</summary>
    public ValueTask DisposeAsync()
    {
        if (!_stop.IsCancellationRequested)
        {
            _stop.Cancel();
            Close(null);
            _writer.Dispose();
            _stop.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private static Action<Utf8JsonWriter>? WriterFor(JsonNode? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        return writer => parameters.WriteTo(writer);
    }

    private async Task<JsonElement> SendRequestCoreAsync(string method, Action<Utf8JsonWriter>? writeParameters, CancellationToken cancellationToken)
    {
        ThrowIfClosed();
        cancellationToken.ThrowIfCancellationRequested();
        var id = Interlocked.Increment(ref _nextId);
        var pending = new PendingRequest(method, Stopwatch.GetTimestamp());
        _pending[id] = pending;
        if (IsClosed && _pending.TryRemove(id, out _))
        {
            throw ConnectionClosed();
        }

        var body = Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WriteNumber("id", id);
            writer.WriteString("method", method);
            if (writeParameters is not null)
            {
                writer.WritePropertyName("params");
                writeParameters(writer);
            }

            writer.WriteEndObject();
        });

        using var registration = cancellationToken.Register(() => CancelRequest(id, cancellationToken));
        try
        {
            Trace(string.Create(CultureInfo.InvariantCulture, $"Sending request '{method} - ({id})'."));
            await _writer.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _pending.TryRemove(id, out _);
            throw ConnectionClosed(ex);
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(id, out _);
            throw;
        }

        return await pending.Completion.Task.ConfigureAwait(false);
    }

    private async Task SendNotificationCoreAsync(string method, Action<Utf8JsonWriter>? writeParameters, CancellationToken cancellationToken)
    {
        ThrowIfClosed();
        var body = Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WriteString("method", method);
            if (writeParameters is not null)
            {
                writer.WritePropertyName("params");
                writeParameters(writer);
            }

            writer.WriteEndObject();
        });

        try
        {
            Trace($"Sending notification '{method}'.");
            await _writer.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw ConnectionClosed(ex);
        }
    }

    /// <summary>Gives up on a request the caller cancelled, and tells the other side so it can stop working on it.</summary>
    private void CancelRequest(long id, CancellationToken cancellationToken)
    {
        if (!_pending.TryRemove(id, out var pending))
        {
            return;
        }

        pending.Completion.TrySetCanceled(cancellationToken);
        if (!IsClosed)
        {
            _ = SendQuietlyAsync("$/cancelRequest", writer =>
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", id);
                writer.WriteEndObject();
            });
        }
    }

    private async Task SendQuietlyAsync(string method, Action<Utf8JsonWriter> writeParameters)
    {
        try
        {
            await SendNotificationCoreAsync(method, writeParameters, CancellationToken.None).ConfigureAwait(false);
        }
        catch (JsonRpcException)
        {
            // The connection closed; nothing is waiting for this message.
        }
    }

    private async Task ReadLoopAsync()
    {
        Exception? failure = null;
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
                    // A message that is not JSON cannot be answered; the next one may be fine.
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
            failure = ex;
        }
        finally
        {
            Close(failure);
        }
    }

    private void Dispatch(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var hasId = message.TryGetProperty("id", out var id) && id.ValueKind is JsonValueKind.Number or JsonValueKind.String;
        if (message.TryGetProperty("method", out var methodElement) && methodElement.ValueKind == JsonValueKind.String)
        {
            var method = methodElement.GetString() ?? string.Empty;
            var parameters = message.TryGetProperty("params", out var value) ? value.Clone() : NullElement;
            if (hasId)
            {
                HandleRequest(id.GetRawText(), method, parameters);
            }
            else
            {
                HandleNotification(method, parameters);
            }
        }
        else if (hasId && TryGetResponseId(id, out var responseId))
        {
            HandleResponse(responseId, message);
        }
    }

    private void HandleResponse(long id, JsonElement message)
    {
        if (!_pending.TryRemove(id, out var pending))
        {
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(pending.Started).TotalMilliseconds;
        Trace(string.Create(CultureInfo.InvariantCulture, $"Received response '{pending.Method} - ({id})' in {elapsed:0}ms."));
        if (message.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
        {
            var code = error.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.Number && codeElement.TryGetInt32(out var number)
                ? number
                : JsonRpcErrorCodes.UnknownErrorCode;
            var text = error.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString() ?? string.Empty
                : "The request failed.";
            pending.Completion.TrySetException(new JsonRpcException(code, text));
            return;
        }

        pending.Completion.TrySetResult(message.TryGetProperty("result", out var result) ? result.Clone() : NullElement);
    }

    private void HandleRequest(string id, string method, JsonElement parameters)
    {
        Trace($"Received request '{method} - ({id})'.");
        if (!_requestHandlers.TryGetValue(method, out var handler))
        {
            _ = RespondAsync(id, null, JsonRpcErrorCodes.MethodNotFound, $"TabStudio does not handle '{method}'.");
            return;
        }

        var cancellation = new CancellationTokenSource();
        _incoming[id] = cancellation;
        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode? result;
                try
                {
                    result = await handler(parameters, cancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await RespondAsync(id, null, JsonRpcErrorCodes.RequestCancelled, "The request was cancelled.").ConfigureAwait(false);
                    return;
                }
                catch (JsonRpcException ex)
                {
                    await RespondAsync(id, null, ex.Code, ex.Message).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    await RespondAsync(id, null, JsonRpcErrorCodes.InternalError, ex.Message).ConfigureAwait(false);
                    return;
                }

                await RespondAsync(id, result, null, null).ConfigureAwait(false);
            }
            finally
            {
                _incoming.TryRemove(id, out _);
                cancellation.Dispose();
            }
        });
    }

    private void HandleNotification(string method, JsonElement parameters)
    {
        if (string.Equals(method, "$/cancelRequest", StringComparison.Ordinal)
            && parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("id", out var id)
            && _incoming.TryGetValue(id.GetRawText(), out var cancellation))
        {
            TryCancel(cancellation);
        }

        Trace($"Received notification '{method}'.");
        if (_notificationHandlers.TryGetValue(method, out var handler))
        {
            try
            {
                handler(parameters);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A handler's mistake must not stop the messages after it.
                Trace($"The handler of '{method}' failed: {ex.Message}");
            }
        }
    }

    /// <summary>Answers a request the other side sent, with its result or an error.</summary>
    private async Task RespondAsync(string id, JsonNode? result, int? errorCode, string? errorMessage)
    {
        var body = Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WritePropertyName("id");
            writer.WriteRawValue(id, skipInputValidation: true);
            if (errorCode is { } code)
            {
                writer.WriteStartObject("error");
                writer.WriteNumber("code", code);
                writer.WriteString("message", errorMessage ?? string.Empty);
                writer.WriteEndObject();
            }
            else if (result is null)
            {
                writer.WriteNull("result");
            }
            else
            {
                writer.WritePropertyName("result");
                result.WriteTo(writer);
            }

            writer.WriteEndObject();
        });

        try
        {
            await _writer.WriteAsync(body, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The connection closed; the other side is gone and needs no answer.
        }
    }

    private void Close(Exception? reason)
    {
        if (_closed.Task.IsCompleted)
        {
            return;
        }

        _closeReason = reason;
        foreach (var id in _pending.Keys)
        {
            if (_pending.TryRemove(id, out var pending))
            {
                pending.Completion.TrySetException(ConnectionClosed(reason));
            }
        }

        foreach (var cancellation in _incoming.Values)
        {
            TryCancel(cancellation);
        }

        _closed.TrySetResult();
    }

    private void Trace(string message) => Traced?.Invoke(this, new JsonRpcTraceEventArgs(message));

    private void ThrowIfClosed()
    {
        if (IsClosed)
        {
            throw ConnectionClosed(_closeReason);
        }
    }

    private static JsonRpcException ConnectionClosed(Exception? reason = null) =>
        reason is null
            ? new JsonRpcException(JsonRpcErrorCodes.ConnectionClosed, "The connection closed.")
            : new JsonRpcException(JsonRpcErrorCodes.ConnectionClosed, $"The connection closed: {reason.Message}");

    private static bool TryGetResponseId(JsonElement id, out long value)
    {
        if (id.ValueKind == JsonValueKind.Number)
        {
            return id.TryGetInt64(out value);
        }

        return long.TryParse(id.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static void TryCancel(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The handler finished in the meantime.
        }
    }

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

    /// <summary>A request waiting for its response.</summary>
    private sealed class PendingRequest(string method, long started)
    {
        public string Method { get; } = method;

        public long Started { get; } = started;

        public TaskCompletionSource<JsonElement> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
