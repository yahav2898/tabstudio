using System.Buffers.Binary;
using System.Text;
using TabStudio.Link.Framing;

namespace TabStudio.Link.Tests;

public sealed class FramingTests
{
    [Fact]
    public async Task Frames_round_trip_with_their_type_and_payload()
    {
        using var stream = new MemoryStream();
        var writer = new LinkFrameWriter(stream);
        await writer.WriteAsync(LinkFrameType.Message, "hello"u8.ToArray());
        await writer.WriteAsync(LinkFrameType.AttachmentChunk, new byte[300_000]);
        await writer.WriteAsync(LinkFrameType.Message, ReadOnlyMemory<byte>.Empty);

        stream.Position = 0;
        var reader = new LinkFrameReader(stream);
        var first = await reader.ReadAsync();
        var second = await reader.ReadAsync();
        var third = await reader.ReadAsync();

        Assert.Equal(LinkFrameType.Message, first!.Value.Type);
        Assert.Equal("hello", Encoding.UTF8.GetString(first.Value.Payload));
        Assert.Equal(LinkFrameType.AttachmentChunk, second!.Value.Type);
        Assert.Equal(300_000, second.Value.Payload.Length);
        Assert.Empty(third!.Value.Payload);
        Assert.Null(await reader.ReadAsync());
    }

    [Fact]
    public async Task A_stream_that_ends_inside_a_frame_is_a_protocol_error()
    {
        using var stream = new MemoryStream();
        await new LinkFrameWriter(stream).WriteAsync(LinkFrameType.Message, new byte[100]);
        using var truncated = new MemoryStream(stream.ToArray()[..50]);

        await Assert.ThrowsAsync<LinkProtocolException>(async () => await new LinkFrameReader(truncated).ReadAsync());
    }

    [Fact]
    public async Task A_header_cut_short_is_a_protocol_error()
    {
        using var stream = new MemoryStream([(byte)LinkFrameType.Message, 0, 0]);

        await Assert.ThrowsAsync<LinkProtocolException>(async () => await new LinkFrameReader(stream).ReadAsync());
    }

    [Fact]
    public async Task A_frame_larger_than_allowed_is_refused_before_it_is_read()
    {
        var header = new byte[LinkFraming.HeaderLength];
        header[0] = (byte)LinkFrameType.Message;
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(1), LinkFraming.MaxPayloadLength + 1);
        using var stream = new MemoryStream(header);

        var error = await Assert.ThrowsAsync<LinkProtocolException>(async () => await new LinkFrameReader(stream).ReadAsync());
        Assert.Contains("larger than", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_frame_type_is_refused()
    {
        using var stream = new MemoryStream([42, 0, 0, 0, 0]);

        await Assert.ThrowsAsync<LinkProtocolException>(async () => await new LinkFrameReader(stream).ReadAsync());
    }

    [Fact]
    public async Task Writing_a_payload_larger_than_a_frame_is_refused()
    {
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await new LinkFrameWriter(stream).WriteAsync(LinkFrameType.Message, new byte[LinkFraming.MaxPayloadLength + 1]));
    }

    [Fact]
    public void Attachment_chunks_round_trip()
    {
        var chunk = new LinkAttachmentChunk(7, IsLast: true, "abc"u8.ToArray());

        var parsed = LinkAttachmentChunk.Parse(chunk.ToPayload());

        Assert.Equal(7, parsed.AttachmentId);
        Assert.True(parsed.IsLast);
        Assert.Equal("abc", Encoding.UTF8.GetString(parsed.Data.Span));
    }

    [Fact]
    public void A_malformed_chunk_is_refused()
    {
        Assert.Throws<LinkProtocolException>(() => LinkAttachmentChunk.Parse([0, 0, 0, 1]));
        Assert.Throws<LinkProtocolException>(() => LinkAttachmentChunk.Parse([0, 0, 0, 1, 2]));
    }
}
