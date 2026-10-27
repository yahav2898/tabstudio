using System.Diagnostics;
using System.Threading.Channels;

namespace TabStudio.Live.Sessions;

/// <summary>
/// A session thread of its own, for a session with no UI: work runs one item at a time, in the order it was posted,
/// on the thread pool.
/// </summary>
public sealed class LiveSerialDispatcher : ILiveDispatcher, IDisposable
{
    private readonly Channel<Action> _work = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _running;

    /// <summary>Starts the thread.</summary>
    public LiveSerialDispatcher()
    {
        _running = Task.Run(RunAsync);
    }

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _work.Writer.TryWrite(action);
    }

    /// <summary>Completes once everything posted before has run.</summary>
    public Task WhenIdleAsync() => this.InvokeAsync(() => { });

    /// <summary>Stops taking work; what was posted still runs.</summary>
    public void Dispose()
    {
        _work.Writer.TryComplete();
        _running.Wait(TimeSpan.FromSeconds(5));
    }

    private async Task RunAsync()
    {
        await foreach (var action in _work.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                action();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // One failing item must not stop the session; the item reports its own failure where it matters.
                Debug.WriteLine($"TabStudio Live: work on the session's thread failed: {ex}");
            }
        }
    }
}
