namespace TabStudio.Link.Framing;

/// <summary>
/// The sizes of TabStudio Link's frames. A frame is a one-byte <see cref="LinkFrameType"/>, the payload's length as a
/// big-endian 32-bit integer, and the payload.
/// </summary>
/// <remarks>
/// Frames travel inside the TLS connection, so their headers are encrypted too. Large payloads, such as compiled
/// assemblies, are split into chunks of <see cref="ChunkLength"/> bytes, so other messages, such as a ping, can be
/// sent between two chunks.
/// </remarks>
public static class LinkFraming
{
    /// <summary>The length of a frame header: the type and the payload length.</summary>
    public const int HeaderLength = 5;

    /// <summary>The largest payload of one frame. A longer frame is a protocol error.</summary>
    public const int MaxPayloadLength = 4 * 1024 * 1024;

    /// <summary>The most attachment bytes carried by one chunk frame.</summary>
    public const int ChunkLength = 1024 * 1024;

    /// <summary>The longest message sent in one frame; longer ones are sent as an attachment.</summary>
    public const int MaxInlineMessageLength = 1024 * 1024;

    /// <summary>The largest attachment. Assemblies and PDBs are far smaller.</summary>
    public const int MaxAttachmentLength = 512 * 1024 * 1024;

    /// <summary>The most attachment bytes a connection buffers before the messages that refer to them arrive.</summary>
    public const long MaxBufferedAttachmentLength = 1024L * 1024 * 1024;
}
