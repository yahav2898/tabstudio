using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.JsonRpc;

namespace TabStudio.WindowsBridge.Client;

/// <summary>
/// netcoredbg on the PC, as the tablet's debugger sees it: a process with standard streams, whose bytes travel
/// through the bridge's tunnel. Writing to <see cref="StandardInput"/> sends to netcoredbg; what it answers is read
/// from <see cref="StandardOutput"/>; why the tunnel closed is in <see cref="StandardError"/>.
/// </summary>
public sealed class BridgeDebugProcess : IServerProcess
{
    private readonly BridgeClient _client;
    private readonly InMemoryPipe _output = new();
    private readonly InMemoryPipe _error = new();
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal BridgeDebugProcess(BridgeClient client, int tunnelId)
    {
        _client = client;
        TunnelId = tunnelId;
        StandardInput = new TunnelStream(client, tunnelId);
    }

    /// <summary>The tunnel's number.</summary>
    public int TunnelId { get; }

    /// <summary>Why the tunnel closed, when it was not on purpose.</summary>
    public string? CloseReason { get; private set; }

    /// <inheritdoc />
    public Stream StandardInput { get; }

    /// <inheritdoc />
    public Stream StandardOutput => _output.Reader;

    /// <inheritdoc />
    public Stream StandardError => _error.Reader;

    /// <inheritdoc />
    public Task<int> Exited => _exited.Task;

    /// <inheritdoc />
    public void Kill() => _client.CloseDebugger(TunnelId);

    /// <inheritdoc />
    public void Dispose()
    {
        Kill();
        OnClosed(null);
        _output.Dispose();
        _error.Dispose();
        StandardInput.Dispose();
    }

    /// <summary>Bytes from netcoredbg arrived.</summary>
    internal void Receive(byte[] data) => _output.Writer.Write(data);

    /// <summary>The tunnel closed: the streams end, and the reason is on standard error.</summary>
    internal void OnClosed(string? reason)
    {
        if (_exited.Task.IsCompleted)
        {
            return;
        }

        CloseReason = reason;
        if (reason is not null)
        {
            _error.Writer.Write(System.Text.Encoding.UTF8.GetBytes(reason + Environment.NewLine));
        }

        _output.Complete();
        _error.Complete();
        _exited.TrySetResult(reason is null ? 0 : 1);
    }

    /// <summary>The write end of the tunnel: each write is sent to netcoredbg, in order.</summary>
    private sealed class TunnelStream(BridgeClient client, int tunnelId) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > 0)
            {
                client.SendDebuggerData(tunnelId, buffer.ToArray());
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer.AsSpan(offset, count));
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
    }
}
