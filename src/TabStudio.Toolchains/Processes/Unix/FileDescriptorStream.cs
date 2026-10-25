using System.Runtime.InteropServices;
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
/// </remarks>
internal sealed class FileDescriptorStream : Stream
{
    private readonly SafeFileHandle _handle;
    private readonly bool _canRead;

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
            _handle.Dispose();
        }

        base.Dispose(disposing);
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
}
