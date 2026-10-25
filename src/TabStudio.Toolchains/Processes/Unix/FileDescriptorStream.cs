using System.Runtime.InteropServices;

namespace TabStudio.Toolchains.Processes.Unix;

/// <summary>
/// A blocking stream over one end of a pipe or a pseudo terminal's master, read and written with the C library's
/// <c>read</c> and <c>write</c>.
/// </summary>
/// <remarks>
/// A pseudo terminal's master reports <c>EIO</c> once every process that had the terminal open has closed it;
/// this stream reads that as the end of the stream, as it reads the end of a pipe.
/// </remarks>
internal sealed class FileDescriptorStream : Stream
{
    private readonly bool _canRead;
    private readonly Lock _gate = new();
    private int _fd;

    /// <summary>Takes ownership of a file descriptor.</summary>
    /// <param name="fd">The descriptor, closed when the stream is disposed.</param>
    /// <param name="canRead">Whether the stream reads (<see langword="true"/>) or writes.</param>
    public FileDescriptorStream(int fd, bool canRead)
    {
        _fd = fd;
        _canRead = canRead;
    }

    /// <inheritdoc />
    public override bool CanRead => _canRead && _fd >= 0;

    /// <inheritdoc />
    public override bool CanWrite => !_canRead && _fd >= 0;

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

        if (buffer.IsEmpty)
        {
            return 0;
        }

        while (true)
        {
            var fd = Volatile.Read(ref _fd);
            if (fd < 0)
            {
                return 0;
            }

            nint read;
            fixed (byte* pointer = buffer)
            {
                read = UnixNative.read(fd, pointer, (nuint)buffer.Length);
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

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(() => Read(buffer, offset, count), cancellationToken, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(Task.Factory.StartNew(() => Read(buffer.Span), cancellationToken, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default));

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override unsafe void Write(ReadOnlySpan<byte> buffer)
    {
        if (_canRead)
        {
            throw new NotSupportedException();
        }

        while (!buffer.IsEmpty)
        {
            var fd = Volatile.Read(ref _fd);
            if (fd < 0)
            {
                throw new ObjectDisposedException(nameof(FileDescriptorStream));
            }

            nint written;
            fixed (byte* pointer = buffer)
            {
                written = UnixNative.write(fd, pointer, (nuint)buffer.Length);
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
        lock (_gate)
        {
            var fd = _fd;
            _fd = -1;
            if (fd >= 0)
            {
                _ = UnixNative.close(fd);
            }
        }

        base.Dispose(disposing);
    }
}
