using System.Text.Json;
using System.Threading.Channels;
using TabStudio.Link;
using TabStudio.Link.Messaging;

namespace TabStudio.WindowsBridge.Client;

/// <summary>
/// Sends the tablet's notifications to the bridge one after another, in the order they were posted: input must
/// arrive in the order it happened, and the debugger's bytes in the order they were written.
/// </summary>
internal sealed class OrderedSender
{
    private readonly LinkConnection _connection;
    private readonly Channel<(string Method, JsonElement Body, byte[]? Attachment)> _queue =
        Channel.CreateUnbounded<(string, JsonElement, byte[]?)>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Starts sending on a connection.</summary>
    /// <param name="connection">The connection to the bridge.</param>
    public OrderedSender(LinkConnection connection)
    {
        _connection = connection;
        _ = Task.Run(SendAsync);
    }

    /// <summary>Queues a notification.</summary>
    /// <param name="method">The method.</param>
    /// <param name="body">The body.</param>
    /// <param name="attachment">Its attachment, if any.</param>
    public void Post(string method, JsonElement body, byte[]? attachment = null) => _queue.Writer.TryWrite((method, body, attachment));

    /// <summary>Stops sending.</summary>
    public void Complete() => _queue.Writer.TryComplete();

    private async Task SendAsync()
    {
        await foreach (var (method, body, attachment) in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await _connection.NotifyAsync(method, body, attachment is null ? null : [attachment]).ConfigureAwait(false);
            }
            catch (LinkConnectionLostException)
            {
                _queue.Writer.TryComplete();
                return;
            }
        }
    }
}
