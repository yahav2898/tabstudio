using System.Threading.Channels;

namespace TabStudio.LanguageServers.JsonRpc;

/// <summary>
/// A one-way byte pipe inside the process: what is written to <see cref="Writer"/> is read from
/// <see cref="Reader"/>. Two pipes connect a client to a server running in the same process, as the tests and the
/// IDE's self test do.
/// </summary>
public sealed class InMemoryPipe : IDisposable
{
    private readonly Channel<byte[]> _channel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Creates an empty pipe.</summary>
    public InMemoryPipe()
    {
        Reader = new ReadEnd(_channel.Reader);
        Writer = new WriteEnd(_channel.Writer);
    }

    /// <summary>The end to read from. Reading returns 0 once the pipe is completed and everything was read.</summary>
    public Stream Reader { get; }

    /// <summary>The end to write to. Disposing it completes the pipe.</summary>
    public Stream Writer { get; }

    /// <summary>Completes the pipe, as closing a process's standard output does: the reader reaches its end.</summary>
    public void Complete() => _channel.Writer.TryComplete();

    /// <inheritdoc />
    public void Dispose()
    {
        Complete();
        Writer.Dispose();
        Reader.Dispose();
    }

    private sealed class ReadEnd(ChannelReader<byte[]> channel) : Stream
    {
        private byte[] _current = [];
        private int _offset;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (_offset >= _current.Length)
            {
                if (!await channel.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }

                if (channel.TryRead(out var next))
                {
                    _current = next;
                    _offset = 0;
                }
            }

            var count = Math.Min(buffer.Length, _current.Length - _offset);
            _current.AsSpan(_offset, count).CopyTo(buffer.Span);
            _offset += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class WriteEnd(ChannelWriter<byte[]> channel) : Stream
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

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > 0 && !channel.TryWrite(buffer.ToArray()))
            {
                throw new IOException("The pipe is closed.");
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                channel.TryComplete();
            }

            base.Dispose(disposing);
        }
    }
}
