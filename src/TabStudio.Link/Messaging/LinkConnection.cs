using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Authentication;
using System.Text.Json;
using TabStudio.Link.Framing;

namespace TabStudio.Link.Messaging;

/// <summary>
/// A message connection to another device over a stream: requests with ids and their answers, cancellation,
/// notifications in both directions, and binary attachments of any size.
/// </summary>
/// <remarks>
/// <para>
/// Messages are JSON frames. A request carries an id that its response or error repeats; a notification has none.
/// Canceling a request's token sends the built-in <c>$/cancel</c> notification and ends the wait at once. Binary
/// attachments, such as compiled assemblies, are sent in chunks before the message that refers to them, so other
/// messages can travel between two chunks.
/// </para>
/// <para>
/// Notifications are handled on the reading thread before the next message is read, and requests on the thread
/// pool, so a request always sees the notifications sent before it. Every member is thread-safe.
/// </para>
/// </remarks>
public sealed class LinkConnection : IAsyncDisposable, IDisposable
{
    /// <summary>The built-in request that measures the latency, answered by the connection itself.</summary>
    public const string PingMethod = "$/ping";

    /// <summary>The built-in notification that cancels a request.</summary>
    public const string CancelMethod = "$/cancel";

    private readonly Stream _stream;
    private readonly IDisposable? _owner;
    private readonly LinkConnectionOptions _options;
    private readonly LinkFrameReader _reader;
    private readonly LinkFrameWriter _writer;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<LinkPayload>> _pending = new();
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _incoming = new();
    private readonly Dictionary<int, AttachmentBuffer> _attachments = [];
    private readonly CancellationTokenSource _closing = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ILinkMessageHandler? _handler;
    private Task _readLoop = Task.CompletedTask;
    private long _bufferedAttachmentBytes;
    private long _nextRequestId;
    private int _nextAttachmentId;
    private long _lastReceived = Environment.TickCount64;
    private long _latencyTicks = -1;
    private int _started;
    private int _closed;
    private Exception? _closeError;

    /// <summary>Creates a connection over a stream. Call <see cref="Start"/> to begin reading.</summary>
    /// <param name="stream">The stream, usually a TLS stream. Owned by the connection from now on.</param>
    /// <param name="options">How the connection notices that the other device is gone.</param>
    /// <param name="owner">Something else to dispose when the connection closes, such as the socket.</param>
    public LinkConnection(Stream stream, LinkConnectionOptions? options = null, IDisposable? owner = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _owner = owner;
        _options = options ?? LinkConnectionOptions.Default;
        _reader = new LinkFrameReader(stream);
        _writer = new LinkFrameWriter(stream);
    }

    /// <summary>Raised once, on the thread pool, when the connection closes for any reason.</summary>
    public event EventHandler<LinkClosedEventArgs>? Closed;

    /// <summary>Raised on the thread pool when a ping measured a new <see cref="Latency"/>.</summary>
    public event EventHandler? LatencyChanged;

    /// <summary>Whether the connection is open.</summary>
    public bool IsOpen => Volatile.Read(ref _closed) == 0;

    /// <summary>Completes when the connection has closed.</summary>
    public Task Completion => _completion.Task;

    /// <summary>Why the connection failed, once it closed; <see langword="null"/> while open or when closed on purpose.</summary>
    public Exception? CloseError => Volatile.Read(ref _closeError);

    /// <summary>The round trip of the last ping, or <see langword="null"/> before the first one.</summary>
    public TimeSpan? Latency
    {
        get
        {
            var ticks = Interlocked.Read(ref _latencyTicks);
            return ticks < 0 ? null : TimeSpan.FromTicks(ticks);
        }
    }

    /// <summary>Starts reading messages, and pinging or watching for silence as the options say.</summary>
    /// <param name="handler">Answers requests and handles notifications.</param>
    /// <exception cref="InvalidOperationException">The connection was already started.</exception>
    public void Start(ILinkMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            throw new InvalidOperationException("The connection is already started.");
        }

        Volatile.Write(ref _handler, handler);
        _readLoop = Task.Run(ReadLoopAsync);
        if (_options.PingInterval is { } interval)
        {
            _ = Task.Run(() => PingRegularlyAsync(interval));
        }

        if (_options.IdleTimeout is { } timeout)
        {
            _ = Task.Run(() => WatchSilenceAsync(timeout));
        }
    }

    /// <summary>Hands the messages that arrive from now on to another handler, for example once a handshake succeeded.</summary>
    /// <param name="handler">The new handler.</param>
    public void ChangeHandler(ILinkMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Volatile.Write(ref _handler, handler);
    }

    /// <summary>Sends a request and waits for its answer.</summary>
    /// <param name="method">The method.</param>
    /// <param name="body">The parameters, if any.</param>
    /// <param name="attachments">Binary attachments the parameters refer to by position, if any.</param>
    /// <param name="cancellationToken">Cancels the request on both devices.</param>
    /// <returns>The answer's body and attachments.</returns>
    /// <exception cref="LinkRemoteException">The other device answered with an error.</exception>
    /// <exception cref="LinkConnectionLostException">The connection closed before the answer arrived.</exception>
    /// <exception cref="OperationCanceledException">The request was canceled.</exception>
    public async Task<LinkPayload> RequestAsync(
        string method,
        JsonElement? body = null,
        IReadOnlyList<ReadOnlyMemory<byte>>? attachments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfClosed();

        var id = Interlocked.Increment(ref _nextRequestId);
        var answer = new TaskCompletionSource<LinkPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = answer;
        if (!IsOpen)
        {
            _pending.TryRemove(id, out _);
            throw Lost();
        }

        using var registration = cancellationToken.Register(() => CancelRequest(id, cancellationToken));
        try
        {
            await SendAsync(new LinkEnvelope(LinkMessageKind.Request, id, method, body, null, null), attachments).ConfigureAwait(false);
        }
        catch
        {
            _pending.TryRemove(id, out _);
            throw;
        }

        return await answer.Task.ConfigureAwait(false);
    }

    /// <summary>Sends a notification, which the other device handles in order and does not answer.</summary>
    /// <param name="method">The method.</param>
    /// <param name="body">The parameters, if any.</param>
    /// <param name="attachments">Binary attachments the parameters refer to by position, if any.</param>
    /// <exception cref="LinkConnectionLostException">The connection is closed.</exception>
    public Task NotifyAsync(string method, JsonElement? body = null, IReadOnlyList<ReadOnlyMemory<byte>>? attachments = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ThrowIfClosed();
        return SendAsync(new LinkEnvelope(LinkMessageKind.Notification, 0, method, body, null, null), attachments);
    }

    /// <summary>Measures the round trip to the other device and updates <see cref="Latency"/>.</summary>
    /// <param name="cancellationToken">Gives up waiting.</param>
    public async Task<TimeSpan> PingAsync(CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        await RequestAsync(PingMethod, cancellationToken: cancellationToken).ConfigureAwait(false);
        var elapsed = clock.Elapsed;
        Interlocked.Exchange(ref _latencyTicks, elapsed.Ticks);
        LatencyChanged?.Invoke(this, EventArgs.Empty);
        return elapsed;
    }

    /// <summary>Closes the connection on purpose. Requests in flight fail with <see cref="LinkConnectionLostException"/>.</summary>
    public void Close() => CloseCore(null);

    /// <summary>Closes the connection and waits until it stopped reading.</summary>
    public async ValueTask DisposeAsync()
    {
        CloseCore(null);
        await _readLoop.ConfigureAwait(false);
        Dispose();
    }

    /// <summary>Closes the connection.</summary>
    public void Dispose()
    {
        CloseCore(null);
        _closing.Dispose();
        _writeGate.Dispose();
    }

    private void ThrowIfClosed()
    {
        if (!IsOpen)
        {
            throw Lost();
        }
    }

    private LinkConnectionLostException Lost(Exception? cause = null)
    {
        var error = cause ?? CloseError;
        return error is null
            ? new LinkConnectionLostException("The connection to the other device is closed.")
            : new LinkConnectionLostException($"The connection to the other device was lost: {error.Message}", error);
    }

    private void CancelRequest(long id, CancellationToken token)
    {
        if (!_pending.TryRemove(id, out var answer))
        {
            return;
        }

        answer.TrySetCanceled(token);
        if (IsOpen)
        {
            var body = JsonSerializer.SerializeToElement(new LinkCancelParams(id), LinkMessagingJsonContext.Default.LinkCancelParams);
            _ = SendSafelyAsync(new LinkEnvelope(LinkMessageKind.Notification, 0, CancelMethod, body, null, null), null);
        }
    }

    private async Task SendAsync(LinkEnvelope envelope, IReadOnlyList<ReadOnlyMemory<byte>>? attachments)
    {
        if (attachments is { Count: > 0 })
        {
            var ids = new int[attachments.Count];
            for (var i = 0; i < attachments.Count; i++)
            {
                ids[i] = Interlocked.Increment(ref _nextAttachmentId);
                await SendAttachmentAsync(ids[i], attachments[i]).ConfigureAwait(false);
            }

            envelope = envelope with { Attachments = ids };
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, LinkMessagingJsonContext.Default.LinkEnvelope);
        if (json.Length <= LinkFraming.MaxInlineMessageLength)
        {
            await WriteFrameAsync(LinkFrameType.Message, json).ConfigureAwait(false);
            return;
        }

        var messageId = Interlocked.Increment(ref _nextAttachmentId);
        await SendAttachmentAsync(messageId, json).ConfigureAwait(false);
        var reference = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(reference, messageId);
        await WriteFrameAsync(LinkFrameType.LargeMessage, reference).ConfigureAwait(false);
    }

    private async Task SendSafelyAsync(LinkEnvelope envelope, IReadOnlyList<ReadOnlyMemory<byte>>? attachments)
    {
        try
        {
            await SendAsync(envelope, attachments).ConfigureAwait(false);
        }
        catch (LinkConnectionLostException)
        {
            // The connection closed; whoever waits for this message learns it from the connection.
        }
    }

    private async Task SendAttachmentAsync(int id, ReadOnlyMemory<byte> data)
    {
        if (data.Length > LinkFraming.MaxAttachmentLength)
        {
            throw new ArgumentException($"An attachment can be at most {LinkFraming.MaxAttachmentLength} bytes.", nameof(data));
        }

        var offset = 0;
        do
        {
            var length = Math.Min(LinkFraming.ChunkLength, data.Length - offset);
            var isLast = offset + length == data.Length;
            var chunk = new LinkAttachmentChunk(id, isLast, data.Slice(offset, length));
            await WriteFrameAsync(LinkFrameType.AttachmentChunk, chunk.ToPayload()).ConfigureAwait(false);
            offset += length;
        }
        while (offset < data.Length);
    }

    private async Task WriteFrameAsync(LinkFrameType type, ReadOnlyMemory<byte> payload)
    {
        try
        {
            await _writeGate.WaitAsync(_closing.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            throw Lost();
        }

        try
        {
            await _writer.WriteAsync(type, payload, _closing.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException or NotSupportedException or InvalidOperationException or AuthenticationException)
        {
            CloseCore(ex is OperationCanceledException ? null : ex);
            throw Lost(ex is OperationCanceledException ? null : ex);
        }
        finally
        {
            try
            {
                _writeGate.Release();
            }
            catch (ObjectDisposedException)
            {
                // Disposed while this frame was written; nothing waits for the gate any more.
            }
        }
    }

    private async Task ReadLoopAsync()
    {
        Exception? error = null;
        try
        {
            while (true)
            {
                var frame = await _reader.ReadAsync(_closing.Token).ConfigureAwait(false);
                if (frame is not { } received)
                {
                    break;
                }

                Volatile.Write(ref _lastReceived, Environment.TickCount64);
                switch (received.Type)
                {
                    case LinkFrameType.Message:
                        Dispatch(ParseEnvelope(received.Payload));
                        break;
                    case LinkFrameType.AttachmentChunk:
                        AddChunk(LinkAttachmentChunk.Parse(received.Payload));
                        break;
                    case LinkFrameType.LargeMessage:
                        if (received.Payload.Length != sizeof(int))
                        {
                            throw new LinkProtocolException("A large message reference is malformed.");
                        }

                        Dispatch(ParseEnvelope(TakeAttachment(BinaryPrimitives.ReadInt32BigEndian(received.Payload))));
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested)
        {
            // Closed on purpose.
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or NotSupportedException or AuthenticationException)
        {
            error = ex;
        }
        finally
        {
            _attachments.Clear();
            CloseCore(error);
        }
    }

    private static LinkEnvelope ParseEnvelope(byte[] json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, LinkMessagingJsonContext.Default.LinkEnvelope)
                ?? throw new LinkProtocolException("A message is empty.");
        }
        catch (JsonException ex)
        {
            throw new LinkProtocolException($"A message is not valid JSON: {ex.Message}", ex);
        }
    }

    private void Dispatch(LinkEnvelope envelope)
    {
        var payload = new LinkPayload(envelope.Body, TakeAttachments(envelope.Attachments));
        switch (envelope.Kind)
        {
            case LinkMessageKind.Response:
                if (_pending.TryRemove(envelope.Id, out var answer))
                {
                    answer.TrySetResult(payload);
                }

                break;
            case LinkMessageKind.Error:
                if (_pending.TryRemove(envelope.Id, out var failed))
                {
                    var error = envelope.Error ?? new LinkError(LinkErrorCodes.Failed, "The other device could not answer.");
                    if (error.Code == LinkErrorCodes.Canceled)
                    {
                        failed.TrySetCanceled();
                    }
                    else
                    {
                        failed.TrySetException(new LinkRemoteException(error.Code, error.Message));
                    }
                }

                break;
            case LinkMessageKind.Notification:
                HandleNotification(envelope.Method ?? string.Empty, payload);
                break;
            case LinkMessageKind.Request:
                StartAnswering(envelope.Id, envelope.Method ?? string.Empty, payload);
                break;
            default:
                throw new LinkProtocolException($"Unknown message kind {envelope.Kind}.");
        }
    }

    private void HandleNotification(string method, LinkPayload payload)
    {
        if (method == CancelMethod)
        {
            var id = payload.Read(LinkMessagingJsonContext.Default.LinkCancelParams).Id;
            if (_incoming.TryGetValue(id, out var cancellation))
            {
                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The request finished in the meantime.
                }
            }

            return;
        }

        try
        {
            Volatile.Read(ref _handler)?.HandleNotification(method, payload);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A notification has nobody to answer to; a failing handler must not close the connection.
            Debug.WriteLine($"TabStudio Link: the '{method}' notification failed: {ex}");
        }
    }

    private void StartAnswering(long id, string method, LinkPayload payload)
    {
        if (method == PingMethod)
        {
            _ = SendSafelyAsync(new LinkEnvelope(LinkMessageKind.Response, id, null, null, null, null), null);
            return;
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
        if (!_incoming.TryAdd(id, cancellation))
        {
            cancellation.Dispose();
            throw new LinkProtocolException($"Request {id} was sent twice.");
        }

        var handler = Volatile.Read(ref _handler);
        _ = Task.Run(() => AnswerAsync(id, method, payload, handler, cancellation));
    }

    private async Task AnswerAsync(long id, string method, LinkPayload payload, ILinkMessageHandler? handler, CancellationTokenSource cancellation)
    {
        LinkEnvelope answer;
        IReadOnlyList<ReadOnlyMemory<byte>>? attachments = null;
        try
        {
            if (handler is null)
            {
                throw new LinkRemoteException(LinkErrorCodes.UnknownMethod, $"'{method}' is not supported.");
            }

            var reply = await handler.HandleRequestAsync(method, payload, cancellation.Token).ConfigureAwait(false);
            answer = new LinkEnvelope(LinkMessageKind.Response, id, null, reply.Body, null, null);
            attachments = reply.Attachments;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            answer = ErrorAnswer(id, LinkErrorCodes.Canceled, "The request was canceled.");
        }
        catch (LinkRemoteException ex)
        {
            answer = ErrorAnswer(id, ex.Code, ex.Message);
        }
        catch (LinkProtocolException ex)
        {
            answer = ErrorAnswer(id, LinkErrorCodes.InvalidRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            answer = ErrorAnswer(id, LinkErrorCodes.Failed, ex.Message);
        }
        finally
        {
            _incoming.TryRemove(id, out _);
            cancellation.Dispose();
        }

        await SendSafelyAsync(answer, attachments).ConfigureAwait(false);
    }

    private static LinkEnvelope ErrorAnswer(long id, string code, string message) =>
        new(LinkMessageKind.Error, id, null, null, new LinkError(code, message), null);

    private void AddChunk(LinkAttachmentChunk chunk)
    {
        if (!_attachments.TryGetValue(chunk.AttachmentId, out var buffer))
        {
            buffer = new AttachmentBuffer();
            _attachments[chunk.AttachmentId] = buffer;
        }

        if (buffer.IsComplete)
        {
            throw new LinkProtocolException($"Attachment {chunk.AttachmentId} continued after its last chunk.");
        }

        _bufferedAttachmentBytes += chunk.Data.Length;
        if (buffer.Length + chunk.Data.Length > LinkFraming.MaxAttachmentLength || _bufferedAttachmentBytes > LinkFraming.MaxBufferedAttachmentLength)
        {
            throw new LinkProtocolException("The other device sent more attachment data than allowed.");
        }

        buffer.Append(chunk.Data.Span);
        buffer.IsComplete = chunk.IsLast;
    }

    private byte[] TakeAttachment(int id)
    {
        if (!_attachments.Remove(id, out var buffer) || !buffer.IsComplete)
        {
            throw new LinkProtocolException($"A message refers to attachment {id}, which has not arrived completely.");
        }

        _bufferedAttachmentBytes -= buffer.Length;
        return buffer.ToArray();
    }

    private byte[][] TakeAttachments(IReadOnlyList<int>? ids)
    {
        if (ids is not { Count: > 0 })
        {
            return [];
        }

        var attachments = new byte[ids.Count][];
        for (var i = 0; i < ids.Count; i++)
        {
            attachments[i] = TakeAttachment(ids[i]);
        }

        return attachments;
    }

    private async Task PingRegularlyAsync(TimeSpan interval)
    {
        try
        {
            using var timer = new PeriodicTimer(interval);
            do
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
                timeout.CancelAfter(_options.PingTimeout);
                try
                {
                    await PingAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!_closing.IsCancellationRequested)
                {
                    CloseCore(new TimeoutException($"The other device did not answer for {_options.PingTimeout.TotalSeconds:0} seconds."));
                    return;
                }
            }
            while (await timer.WaitForNextTickAsync(_closing.Token).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or LinkConnectionLostException)
        {
            // Closed while pinging.
        }
    }

    private async Task WatchSilenceAsync(TimeSpan timeout)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(100, timeout.TotalMilliseconds / 4)));
            while (await timer.WaitForNextTickAsync(_closing.Token).ConfigureAwait(false))
            {
                if (Environment.TickCount64 - Volatile.Read(ref _lastReceived) > (long)timeout.TotalMilliseconds)
                {
                    CloseCore(new TimeoutException($"The other device was silent for {timeout.TotalSeconds:0} seconds."));
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // Closed.
        }
    }

    private void CloseCore(Exception? error)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
        {
            return;
        }

        Volatile.Write(ref _closeError, error);
        try
        {
            _closing.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }

        try
        {
            _stream.Dispose();
            _owner?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or AuthenticationException)
        {
            // Closing a broken connection can fail; it is closed either way.
        }

        var lost = Lost();
        foreach (var id in _pending.Keys)
        {
            if (_pending.TryRemove(id, out var answer))
            {
                answer.TrySetException(lost);
            }
        }

        foreach (var cancellation in _incoming.Values)
        {
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // That request just finished.
            }
        }

        _completion.TrySetResult();
        var closed = Closed;
        if (closed is not null)
        {
            var args = new LinkClosedEventArgs(error);
            _ = Task.Run(() => closed(this, args));
        }
    }

    /// <summary>The chunks of one attachment received so far.</summary>
    private sealed class AttachmentBuffer
    {
        private readonly ArrayBufferWriter<byte> _data = new();

        public bool IsComplete { get; set; }

        public int Length => _data.WrittenCount;

        public void Append(ReadOnlySpan<byte> chunk) => _data.Write(chunk);

        public byte[] ToArray() => _data.WrittenSpan.ToArray();
    }
}
