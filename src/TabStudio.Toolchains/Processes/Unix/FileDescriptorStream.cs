using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace TabStudio.Toolchains.Processes.Unix;

/// <summary>
/// A blocking stream over one end of a pipe or a pseudo terminal's master, read and written with the C library's
/// <c>read</c> and <c>write</c>.
/// </summary>
/// <remarks>
/// <para>
/// A pseudo terminal's master reports <c>EIO</c> once every process that had the terminal open has closed it;
/// this stream reads that as the end of the stream, as it reads the end of a pipe.
/// </para>
/// <para>
/// The descriptor is held by a <see cref="SafeFileHandle"/>, and every read and write holds a reference to it, so
/// disposing the stream while another thread is blocked in a read closes the descriptor only after that read
/// returns. Closing it at once would let the system give its number to the next file opened, which the blocked
/// thread would then read from.
/// </para>
/// <para>
/// A read blocks until the program writes, which may be never while it waits, as a language server does between
/// requests. Asynchronous reads therefore wait on a thread of the stream's own, started by the first one, rather
/// than on one of the thread pool's: a program per thread pool thread would leave none for the rest of the IDE.
/// </para>
/// </remarks>
internal sealed class FileDescriptorStream : Stream
{
    private readonly SafeFileHandle _handle;
    private readonly bool _canRead;
    private readonly Lock _gate = new();
    private Channel<ReadRequest>? _reads;
    private bool _disposed;

    /// <summary>Takes ownership of a file descriptor.</summary>
    /// <param name="fd">The descriptor, closed when the stream is disposed.</param>
    /// <param name="canRead">Whether the stream reads (<see langword="true"/>) or writes.</param>
    public FileDescriptorStream(int fd, bool canRead)
    {
        _handle = new SafeFileHandle(fd, ownsHandle: true);
        _canRead = canRead;
    }

    /// <inheritdoc />
    public override bool CanRead => _canRead && !_handle.IsClosed;

    /// <inheritdoc />
    public override bool CanWrite => !_canRead && !_handle.IsClosed;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override unsafe int Read(Span<byte> buffer)
    {
        if (!_canRead)
        {
            throw new NotSupportedException();
        }

        if (buffer.IsEmpty || !TryAddReference())
        {
            return 0;
        }

        try
        {
            while (true)
            {
                nint read;
                fixed (byte* pointer = buffer)
                {
                    read = UnixNative.read((int)_handle.DangerousGetHandle(), pointer, (nuint)buffer.Length);
                }

                if (read >= 0)
                {
                    return (int)read;
                }

                var error = Marshal.GetLastPInvokeError();
                switch (error)
                {
                    case UnixConstants.ErrorInterrupted:
                        continue;
                    case UnixConstants.ErrorInputOutput:
                        return 0;
                    default:
                        throw new IOException($"Reading from the program failed (errno {error}).", error);
                }
            }
        }
        finally
        {
            _handle.DangerousRelease();
        }
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    /// <remarks>
    /// The read waits on the stream's reading thread, after the reads asked for before it. Cancelling stops a read
    /// that has not started waiting yet; one that waits ends when the program writes, or exits.
    /// </remarks>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!_canRead)
        {
            throw new NotSupportedException();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        var request = new ReadRequest(buffer, cancellationToken);
        return Reads() is { } reads && reads.Writer.TryWrite(request) ? new ValueTask<int>(request.Result.Task) : ValueTask.FromResult(0);
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override unsafe void Write(ReadOnlySpan<byte> buffer)
    {
        if (_canRead)
        {
            throw new NotSupportedException();
        }

        ObjectDisposedException.ThrowIf(!TryAddReference(), this);
        try
        {
            while (!buffer.IsEmpty)
            {
                nint written;
                fixed (byte* pointer = buffer)
                {
                    written = UnixNative.write((int)_handle.DangerousGetHandle(), pointer, (nuint)buffer.Length);
                }

                if (written < 0)
                {
                    var error = Marshal.GetLastPInvokeError();
                    if (error == UnixConstants.ErrorInterrupted)
                    {
                        continue;
                    }

                    throw new IOException($"Writing to the program failed (errno {error}): it may have exited.", error);
                }

                buffer = buffer[(int)written..];
            }
        }
        finally
        {
            _handle.DangerousRelease();
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(() => Write(buffer, offset, count), cancellationToken, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(Task.Factory.StartNew(() => Write(buffer.Span), cancellationToken, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default));

    /// <inheritdoc />
    public override void Flush()
    {
        // Writes go straight to the descriptor.
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_gate)
            {
                _disposed = true;
                _reads?.Writer.TryComplete();
            }

            _handle.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// The asynchronous reads waiting to be served, with the thread that serves them started on first use; or
    /// <see langword="null"/> once the stream is disposed, when every read is at the end of the stream.
    /// </summary>
    private Channel<ReadRequest>? Reads()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return null;
            }

            if (_reads is null)
            {
                _reads = Channel.CreateUnbounded<ReadRequest>(new UnboundedChannelOptions { SingleReader = true });
                new Thread(ServeReads) { IsBackground = true, Name = "TabStudio program output" }.Start(_reads.Reader);
            }

            return _reads;
        }
    }

    /// <summary>The reading thread: serves the asynchronous reads one after another, until the stream is disposed.</summary>
    private void ServeReads(object? state)
    {
        var reads = (ChannelReader<ReadRequest>)state!;
        while (reads.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            while (reads.TryRead(out var request))
            {
                if (request.CancellationToken.IsCancellationRequested)
                {
                    request.Result.TrySetCanceled(request.CancellationToken);
                    continue;
                }

                try
                {
                    request.Result.TrySetResult(Read(request.Buffer.Span));
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // The read's caller gets the failure; the thread goes on serving the reads after it.
                    request.Result.TrySetException(ex);
                }
            }
        }

        // Reads asked for as the stream was disposed see the end of the stream.
        while (reads.TryRead(out var late))
        {
            late.Result.TrySetResult(0);
        }
    }

    private bool TryAddReference()
    {
        if (_handle.IsClosed)
        {
            return false;
        }

        var added = false;
        try
        {
            _handle.DangerousAddRef(ref added);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        return added;
    }

    /// <summary>An asynchronous read waiting for the reading thread.</summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Cancels the read until it starts waiting.</param>
    private sealed class ReadRequest(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        /// <summary>Where the bytes go.</summary>
        public Memory<byte> Buffer => buffer;

        /// <summary>Cancels the read until it starts waiting.</summary>
        public CancellationToken CancellationToken => cancellationToken;

        /// <summary>Completes with the number of bytes read, 0 at the end of the stream.</summary>
        public TaskCompletionSource<int> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
