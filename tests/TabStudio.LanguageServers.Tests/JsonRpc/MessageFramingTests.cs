using System.Text;
using TabStudio.LanguageServers.JsonRpc;

namespace TabStudio.LanguageServers.Tests.JsonRpc;

public sealed class MessageFramingTests
{
    [Fact]
    public async Task Reads_messages_one_after_another()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Frame("{\"a\":1}") + Frame("{\"b\":2}")));
        var reader = new MessageReader(stream);

        Assert.Equal("{\"a\":1}", Text(await reader.ReadAsync(CancellationToken.None)));
        Assert.Equal("{\"b\":2}", Text(await reader.ReadAsync(CancellationToken.None)));
        Assert.Null(await reader.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Counts_the_content_length_in_bytes_of_UTF8()
    {
        var body = "{\"text\":\"héllo ✓\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Frame(body)));

        Assert.Equal(body, Text(await new MessageReader(stream).ReadAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task Ignores_other_headers_and_lines_printed_before_the_first_header()
    {
        var input = "Starting the server...\r\nContent-Type: application/vscode-jsonrpc; charset=utf-8\r\nContent-Length: 2\r\n\r\n{}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        Assert.Equal("{}", Text(await new MessageReader(stream).ReadAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task Reads_a_message_that_arrives_a_byte_at_a_time()
    {
        using var pipe = new InMemoryPipe();
        var reader = new MessageReader(pipe.Reader);
        var read = reader.ReadAsync(CancellationToken.None);
        foreach (var value in Encoding.UTF8.GetBytes(Frame("{\"slow\":true}")))
        {
            await pipe.Writer.WriteAsync(new[] { value });
        }

        Assert.Equal("{\"slow\":true}", Text(await read.WaitAsync(TestTimeouts.Default)));
    }

    [Fact]
    public async Task Rejects_a_header_without_a_number()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("Content-Length: lots\r\n\r\n{}"));

        await Assert.ThrowsAsync<InvalidDataException>(() => new MessageReader(stream).ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Writes_the_header_and_the_body()
    {
        using var stream = new MemoryStream();
        using var writer = new MessageWriter(stream);

        await writer.WriteAsync(Encoding.UTF8.GetBytes("{\"x\":\"✓\"}"), CancellationToken.None);

        Assert.Equal("Content-Length: 12\r\n\r\n{\"x\":\"✓\"}", Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static string Frame(string body) => $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}";

    private static string? Text(byte[]? body) => body is null ? null : Encoding.UTF8.GetString(body);
}
