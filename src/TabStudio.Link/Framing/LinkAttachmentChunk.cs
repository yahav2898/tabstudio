using System.Buffers.Binary;

namespace TabStudio.Link.Framing;

/// <summary>
/// The payload of an <see cref="LinkFrameType.AttachmentChunk"/> frame: the attachment's id (big-endian 32-bit), a
/// flag byte that is 1 on the attachment's last chunk, and the chunk's bytes.
/// </summary>
/// <param name="AttachmentId">The attachment the chunk belongs to.</param>
/// <param name="IsLast">Whether this is the attachment's last chunk.</param>
/// <param name="Data">The chunk's bytes.</param>
public readonly record struct LinkAttachmentChunk(int AttachmentId, bool IsLast, ReadOnlyMemory<byte> Data)
{
    /// <summary>The length of a chunk's header inside the frame payload.</summary>
    public const int HeaderLength = 5;

    /// <summary>Encodes a chunk as a frame payload.</summary>
    public byte[] ToPayload()
    {
        var payload = new byte[HeaderLength + Data.Length];
        BinaryPrimitives.WriteInt32BigEndian(payload, AttachmentId);
        payload[4] = IsLast ? (byte)1 : (byte)0;
        Data.Span.CopyTo(payload.AsSpan(HeaderLength));
        return payload;
    }

    /// <summary>Decodes a frame payload.</summary>
    /// <param name="payload">The payload of an attachment chunk frame.</param>
    /// <exception cref="LinkProtocolException">The payload is too short or its flag is invalid.</exception>
    public static LinkAttachmentChunk Parse(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length < HeaderLength || payload[4] > 1)
        {
            throw new LinkProtocolException("An attachment chunk is malformed.");
        }

        return new LinkAttachmentChunk(BinaryPrimitives.ReadInt32BigEndian(payload), payload[4] == 1, payload.AsMemory(HeaderLength));
    }
}
