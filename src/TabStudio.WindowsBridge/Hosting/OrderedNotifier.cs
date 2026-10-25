using System.Text.Json;
using System.Threading.Channels;
using TabStudio.Link;
using TabStudio.Link.Messaging;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>
/// Sends notifications to the tablet one after another, in the order they were posted, without making the code that
/// posts them wait: build output, a program's console and its window list arrive as they happened.
/// </summary>
internal sealed class OrderedNotifier
{
    private readonly LinkConnection _connection;
    private readonly Channel<(string? Method, JsonElement Body, TaskCompletionSource? Flushed)> _queue =
        Channel.CreateUnbounded<(string?, JsonElement, TaskCompletionSource?)>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Starts sending on a connection.</summary>
    /// <param name="connection">The tablet's connection.</param>
    public OrderedNotifier(LinkConnection connection)
    {
        _connection = connection;
        _ = Task.Run(SendAsync);
    }

    /// <summary>Queues a notification.</summary>
    /// <param name="method">The method.</param>
    /// <param name="body">The body.</param>
    public void Post(string method, JsonElement body) => _queue.Writer.TryWrite((method, body, null));

    /// <summary>Waits until every notification posted so far was sent, or the connection closed.</summary>
    public Task FlushAsync()
    {
        var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_queue.Writer.TryWrite((null, default, flushed)))
        {
            flushed.TrySetResult();
        }

        return flushed.Task;
    }

    /// <summary>Stops sending: what is still queued is dropped.</summary>
    public void Complete() => _queue.Writer.TryComplete();

    private async Task SendAsync()
    {
        var connected = true;
        await foreach (var (method, body, flushed) in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (flushed is not null)
            {
                flushed.TrySetResult();
                continue;
            }

            if (!connected)
            {
                continue;
            }

            try
            {
                await _connection.NotifyAsync(method!, body).ConfigureAwait(false);
            }
            catch (LinkConnectionLostException)
            {
                // The tablet left; it asks for the current state when it comes back.
                connected = false;
            }
        }
    }
}
