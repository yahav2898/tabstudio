using System.Security.Cryptography;

namespace TabStudio.SourceControl.Git;

/// <summary>
/// A write-only stream that hashes what is written to it as a Git object of a known kind and size, and optionally
/// passes the bytes on, so a large file is named and stored without being held in memory.
/// </summary>
internal sealed class HashingStream : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
    private readonly Stream? _next;
    private long _written;

    /// <summary>Starts hashing an object.</summary>
    /// <param name="kind">The object's kind.</param>
    /// <param name="size">Its size, which Git hashes first.</param>
    /// <param name="next">Where the bytes also go, if anywhere.</param>
    public HashingStream(GitObjectKind kind, long size, Stream? next = null)
    {
        _hash.AppendData(GitHash.Header(kind, size));
        _next = next;
    }

    /// <summary>How many bytes were written.</summary>
    public long Written => _written;

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => _written;

    /// <inheritdoc />
    public override long Position
    {
        get => _written;
        set => throw new NotSupportedException();
    }

    /// <summary>The object's name, once everything was written.</summary>
    public string Finish() => Convert.ToHexStringLower(_hash.GetHashAndReset());

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _hash.AppendData(buffer);
        _next?.Write(buffer);
        _written += buffer.Length;
    }

    /// <inheritdoc />
    public override void Flush() => _next?.Flush();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
        }

        base.Dispose(disposing);
    }
}
