using System.Buffers.Binary;

namespace TabStudio.Link.Framing;

/// <summary>Reads frames from a stream, one at a time.</summary>
/// <remarks>Not thread-safe: one reader reads a stream.</remarks>
public sealed class LinkFrameReader
{
    private readonly Stream _stream;
    private readonly int _maxPayloadLength;
    private readonly byte[] _header = new byte[LinkFraming.HeaderLength];

    /// <summary>Creates a reader.</summary>
    /// <param name="stream">The stream to read, usually a TLS stream.</param>
    /// <param name="maxPayloadLength">The longest payload accepted; longer frames are a protocol error.</param>
    public LinkFrameReader(Stream stream, int maxPayloadLength = LinkFraming.MaxPayloadLength)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(maxPayloadLength);
        _stream = stream;
        _maxPayloadLength = maxPayloadLength;
    }

    /// <summary>Reads the next frame.</summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The frame, or <see langword="null"/> when the stream ended cleanly between two frames.</returns>
    /// <exception cref="LinkProtocolException">The stream ended inside a frame, or the frame is invalid.</exception>
    public async ValueTask<LinkFrame?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var read = await _stream.ReadAtLeastAsync(_header, LinkFraming.HeaderLength, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            return null;
        }

        if (read < LinkFraming.HeaderLength)
        {
            throw new LinkProtocolException("The connection ended in the middle of a frame.");
        }

        var type = (LinkFrameType)_header[0];
        if (!Enum.IsDefined(type))
        {
            throw new LinkProtocolException($"Unknown frame type {_header[0]}.");
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(_header.AsSpan(1));
        if (length < 0 || length > _maxPayloadLength)
        {
            throw new LinkProtocolException($"A frame of {length} bytes is larger than the {_maxPayloadLength} bytes allowed.");
        }

        byte[] payload = length == 0 ? [] : new byte[length];
        try
        {
            await _stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException ex)
        {
            throw new LinkProtocolException("The connection ended in the middle of a frame.", ex);
        }

        return new LinkFrame(type, payload);
    }
}
