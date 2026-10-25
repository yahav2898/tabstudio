using System.Buffers;
using System.Buffers.Binary;

namespace TabStudio.Link.Framing;

/// <summary>Writes frames to a stream.</summary>
/// <remarks>
/// Not thread-safe: the caller writes one frame at a time. Each frame is flushed, so it leaves as soon as it is
/// written; small frames go out in a single write, which over TLS is a single record.
/// </remarks>
public sealed class LinkFrameWriter
{
    /// <summary>Frames up to this length are copied into one buffer and written at once.</summary>
    private const int CoalesceLength = 64 * 1024;

    private readonly Stream _stream;

    /// <summary>Creates a writer.</summary>
    /// <param name="stream">The stream to write, usually a TLS stream.</param>
    public LinkFrameWriter(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
    }

    /// <summary>Writes a frame and flushes it.</summary>
    /// <param name="type">What the frame carries.</param>
    /// <param name="payload">The payload, at most <see cref="LinkFraming.MaxPayloadLength"/> bytes.</param>
    /// <param name="cancellationToken">Cancels the write. A canceled write leaves the stream unusable.</param>
    public async ValueTask WriteAsync(LinkFrameType type, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (payload.Length > LinkFraming.MaxPayloadLength)
        {
            throw new ArgumentException($"A frame payload can be at most {LinkFraming.MaxPayloadLength} bytes.", nameof(payload));
        }

        var length = LinkFraming.HeaderLength + payload.Length;
        if (length <= CoalesceLength)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                WriteHeader(buffer, type, payload.Length);
                payload.Span.CopyTo(buffer.AsSpan(LinkFraming.HeaderLength));
                await _stream.WriteAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        else
        {
            var header = new byte[LinkFraming.HeaderLength];
            WriteHeader(header, type, payload.Length);
            await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void WriteHeader(Span<byte> header, LinkFrameType type, int payloadLength)
    {
        header[0] = (byte)type;
        BinaryPrimitives.WriteInt32BigEndian(header[1..], payloadLength);
    }
}
