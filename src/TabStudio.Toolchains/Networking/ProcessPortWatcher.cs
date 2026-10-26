using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Networking;

/// <summary>Combines socket ownership with output detection for one run; notifications are deduplicated.</summary>
public sealed class ProcessPortWatcher : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private readonly HashSet<int> _seen = [];
    private readonly Action<int> _listening;
    private readonly Dictionary<bool, string> _tails = [];
    private bool _disposed;

    /// <summary>Starts watching a process's descendants until disposal or exit.</summary>
    public ProcessPortWatcher(ToolchainProcess process, Action<int> listening)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(listening);
        _listening = listening;
        _ = PollAsync(process, _stop.Token);
    }

    /// <summary>Feeds a chunk from one of the process's streams, preserving URLs split between reads.</summary>
    public void Feed(string text, bool standardError = false)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var combined = _tails.GetValueOrDefault(standardError, string.Empty) + text;
            // Only complete lines are parsed: a port split after its first digit must not be offered early.
            var end = combined.LastIndexOf('\n');
            if (end >= 0)
            {
                foreach (var port in ListeningPorts.ParseOutput(combined[..(end + 1)]))
                {
                    Offer(port);
                }
            }

            var tail = combined[(end + 1)..];
            _tails[standardError] = tail.Length > 4096 ? tail[^4096..] : tail;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task PollAsync(ToolchainProcess process, CancellationToken cancellationToken)
    {
        try
        {
            while (!process.HasExited)
            {
                foreach (var port in ListeningPorts.ForProcess(process.Id))
                {
                    lock (_gate)
                    {
                        if (!_disposed)
                        {
                            Offer(port);
                        }
                    }
                }

                await Task.Delay(750, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The run ended.
        }
    }

    private void Offer(int port)
    {
        if (_seen.Add(port))
        {
            _listening(port);
        }
    }
}
