namespace TabStudio.Link.Framing;

/// <summary>One frame: a type and its payload.</summary>
/// <param name="Type">What the frame carries.</param>
/// <param name="Payload">The bytes after the frame header.</param>
public readonly record struct LinkFrame(LinkFrameType Type, byte[] Payload);
