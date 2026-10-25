namespace TabStudio.Link.Framing;

/// <summary>What a frame carries.</summary>
public enum LinkFrameType : byte
{
    /// <summary>A message: a request, response, error or notification, as UTF-8 JSON.</summary>
    Message = 1,

    /// <summary>A piece of a binary attachment, such as a compiled assembly.</summary>
    AttachmentChunk = 2,

    /// <summary>A message too large for one frame, sent as an attachment first: the payload is that attachment's id.</summary>
    LargeMessage = 3,
}
