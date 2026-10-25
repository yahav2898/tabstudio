using System.Text;
using System.Threading.Channels;
using TabStudio.Compilation.Running;

namespace TabStudio.Swarm.Device;

/// <summary>
/// A console app's output on a device: shown on the device, and sent to the tablet in batches, so a program that
/// writes a character at a time does not send a message for each one.
/// </summary>
internal sealed class SwarmOutputSender : IConsoleSink
{
    /// <summary>The most characters one batch carries.</summary>
    private const int MaxBatchLength = 16 * 1024;

    private readonly Channel<(string Text, bool IsError)> _pending = Channel.CreateUnbounded<(string, bool)>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Func<string, bool, Task> _send;
    private readonly Action<string, bool> _show;
    private readonly Task _sending;

    /// <summary>Starts sending.</summary>
    /// <param name="send">Sends a batch to the tablet.</param>
    /// <param name="show">Shows output on the device at once.</param>
    public SwarmOutputSender(Func<string, bool, Task> send, Action<string, bool> show)
    {
        _send = send;
        _show = show;
        _sending = Task.Run(SendAsync);
    }

    /// <inheritdoc />
    public void WriteOutput(string text) => Write(text, isError: false);

    /// <inheritdoc />
    public void WriteError(string text) => Write(text, isError: true);

    /// <summary>Sends what is left, then stops.</summary>
    public async Task FlushAsync()
    {
        _pending.Writer.TryComplete();
        await _sending.ConfigureAwait(false);
    }

    private void Write(string text, bool isError)
    {
        if (text.Length == 0)
        {
            return;
        }

        _show(text, isError);
        _pending.Writer.TryWrite((text, isError));
    }

    private async Task SendAsync()
    {
        var batch = new StringBuilder();
        while (await _pending.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            // Output that arrives together travels together.
            await Task.Delay(30).ConfigureAwait(false);
            while (_pending.Reader.TryPeek(out var next))
            {
                var isError = next.IsError;
                batch.Clear();
                while (batch.Length < MaxBatchLength && _pending.Reader.TryPeek(out var item) && item.IsError == isError)
                {
                    _pending.Reader.TryRead(out _);
                    batch.Append(item.Text);
                }

                await _send(batch.ToString(), isError).ConfigureAwait(false);
            }
        }
    }
}
