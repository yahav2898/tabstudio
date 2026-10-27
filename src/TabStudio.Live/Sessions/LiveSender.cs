using System.Text.Json;
using System.Threading.Channels;
using TabStudio.Link;
using TabStudio.Link.Messaging;

namespace TabStudio.Live.Sessions;

/// <summary>
/// The notifications one device sends another, in the order they were made. A participant's edits must arrive in the
/// order it made them, and the connection's writers do not queue in order, so every notification goes through one
/// queue and one writer. A slow device only holds up its own queue.
/// </summary>
internal sealed class LiveSender
{
    private readonly Channel<Item> _queue = Channel.CreateUnbounded<Item>(new UnboundedChannelOptions { SingleReader = true });
    private readonly LinkConnection _connection;
    private readonly Task _sending;

    /// <summary>Starts sending on a connection.</summary>
    /// <param name="connection">The connection.</param>
    public LiveSender(LinkConnection connection)
    {
        _connection = connection;
        _sending = Task.Run(SendAsync);
    }

    /// <summary>The connection the notifications go on.</summary>
    public LinkConnection Connection => _connection;

    /// <summary>Queues a notification.</summary>
    /// <param name="method">The method.</param>
    /// <param name="body">The body, if any.</param>
    /// <param name="attachment">A binary attachment, if any.</param>
    public void Notify(string method, JsonElement? body = null, ReadOnlyMemory<byte>? attachment = null) =>
        _queue.Writer.TryWrite(new Item(method, body, attachment, null));

    /// <summary>Completes once every notification queued before it was written, or the connection closed.</summary>
    public Task FlushAsync()
    {
        var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_queue.Writer.TryWrite(new Item(string.Empty, null, null, flushed)))
        {
            flushed.TrySetResult();
        }

        return flushed.Task;
    }

    /// <summary>Stops sending once what is queued was written.</summary>
    public Task CompleteAsync()
    {
        _queue.Writer.TryComplete();
        return _sending;
    }

    private async Task SendAsync()
    {
        var lost = false;
        await foreach (var item in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (item.Flushed is { } flushed)
            {
                flushed.TrySetResult();
                continue;
            }

            if (lost)
            {
                continue;
            }

            try
            {
                await _connection.NotifyAsync(item.Method, item.Body, item.Attachment is { } data ? [data] : null).ConfigureAwait(false);
            }
            catch (LinkConnectionLostException)
            {
                // The device is gone; what is still queued is dropped, and a reconnection starts a new queue.
                lost = true;
            }
        }
    }

    /// <summary>A queued notification, or a marker that completes a flush.</summary>
    private sealed record Item(string Method, JsonElement? Body, ReadOnlyMemory<byte>? Attachment, TaskCompletionSource? Flushed);
}
