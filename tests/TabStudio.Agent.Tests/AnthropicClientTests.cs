using System.Net;
using System.Text.Json.Nodes;
using TabStudio.Agent.Anthropic;

namespace TabStudio.Agent.Tests;

public sealed class AnthropicClientTests
{
    private static MessageRequest Request(params ChatMessage[] messages) => new(
        "claude-test",
        "Be helpful.",
        messages.Length == 0 ? [ChatMessage.FromText(MessageRole.User, "Hi")] : messages,
        [new ToolDefinition("one", "First", new JsonObject { ["type"] = "object" }), new ToolDefinition("two", "Second", new JsonObject { ["type"] = "object" })]);

    [Fact]
    public async Task Streams_text_and_returns_the_whole_message_with_usage()
    {
        var claude = new FakeClaude().Text("Hello from Claude!");
        var client = new AnthropicClient(new HttpClient(claude), "sk-test");
        var streamed = new List<string>();

        var response = await client.SendAsync(Request(), streamed.Add);

        Assert.Equal("Hello from Claude!", response.Message.Text);
        Assert.Equal("Hello from Claude!", string.Concat(streamed));
        Assert.True(streamed.Count >= 2);
        Assert.Equal(StopReason.EndTurn, response.StopReason);
        Assert.Equal(new TokenUsage(120, 42, 100, 20), response.Usage);
        Assert.Equal("sk-test", claude.ApiKeys.Single());
    }

    [Fact]
    public async Task Assembles_tool_calls_from_partial_json()
    {
        var input = new JsonObject { ["path"] = "src/Program.cs", ["start_line"] = 3 };
        var claude = new FakeClaude().ToolCall("toolu_1", "read_file", input, text: "Let me look.");
        var client = new AnthropicClient(new HttpClient(claude), "sk-test");

        var response = await client.SendAsync(Request());

        Assert.Equal(StopReason.ToolUse, response.StopReason);
        Assert.Equal("Let me look.", response.Message.Text);
        var call = Assert.IsType<ToolUseBlock>(response.Message.Content[1]);
        Assert.Equal(("toolu_1", "read_file"), (call.Id, call.Name));
        Assert.Equal("src/Program.cs", call.Input["path"]!.GetValue<string>());
        Assert.Equal(3, call.Input["start_line"]!.GetValue<int>());
    }

    [Fact]
    public async Task Requests_are_streamed_and_cache_the_tools_the_system_prompt_and_the_conversation()
    {
        var claude = new FakeClaude().Text("ok");
        var client = new AnthropicClient(new HttpClient(claude), "sk-test");

        await client.SendAsync(Request(
            ChatMessage.FromText(MessageRole.User, "Read it"),
            new ChatMessage(MessageRole.Assistant, [new ToolUseBlock("toolu_1", "one", new JsonObject { ["a"] = 1 })]),
            new ChatMessage(MessageRole.User, [new ToolResultBlock("toolu_1", "file text", IsError: false)])));

        var body = claude.Requests.Single();
        Assert.True(body["stream"]!.GetValue<bool>());
        Assert.Equal("claude-test", body["model"]!.GetValue<string>());
        Assert.Null(body["tools"]![0]!["cache_control"]);
        Assert.Equal("ephemeral", body["tools"]![1]!["cache_control"]!["type"]!.GetValue<string>());
        Assert.Equal("ephemeral", body["system"]![0]!["cache_control"]!["type"]!.GetValue<string>());
        var messages = body["messages"]!.AsArray();
        Assert.Equal("assistant", messages[1]!["role"]!.GetValue<string>());
        Assert.Equal("tool_use", messages[1]!["content"]![0]!["type"]!.GetValue<string>());
        var result = messages[2]!["content"]![0]!;
        Assert.Equal("tool_result", result["type"]!.GetValue<string>());
        Assert.Equal("toolu_1", result["tool_use_id"]!.GetValue<string>());
        Assert.Equal("ephemeral", result["cache_control"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task Overloaded_requests_are_retried()
    {
        var claude = new FakeClaude()
            .Error((HttpStatusCode)529, "overloaded_error", "Overloaded")
            .Error(HttpStatusCode.TooManyRequests, "rate_limit_error", "Slow down")
            .Text("Finally");
        var waits = new List<TimeSpan>();
        var client = new AnthropicClient(new HttpClient(claude), "sk-test", null, (delay, _) =>
        {
            waits.Add(delay);
            return Task.CompletedTask;
        });

        var response = await client.SendAsync(Request());

        Assert.Equal("Finally", response.Message.Text);
        Assert.Equal(2, waits.Count);
    }

    [Fact]
    public async Task Errors_are_explained()
    {
        var claude = new FakeClaude().Error(HttpStatusCode.Unauthorized, "authentication_error", "invalid x-api-key");
        var client = new AnthropicClient(new HttpClient(claude), "sk-wrong");

        var error = await Assert.ThrowsAsync<AnthropicException>(() => client.SendAsync(Request()));

        Assert.True(error.IsAuthenticationError);
        Assert.Contains("API key", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stream_that_ends_early_is_an_error()
    {
        var claude = new FakeClaude().Stream(FakeClaude.Events.Start() + FakeClaude.Events.TextBlock(0, "cut off"));
        var client = new AnthropicClient(new HttpClient(claude), "sk-test");

        await Assert.ThrowsAsync<AnthropicException>(() => client.SendAsync(Request()));
    }

    [Fact]
    public async Task A_stream_that_goes_quiet_ends_with_an_error_instead_of_waiting_forever()
    {
        // A dead connection mid-answer held the agent's spinner until the user pressed Stop.
        using var quiet = new QuietStream("event: message_start\ndata: {\"type\":\"ping\"}\n\n"u8.ToArray());

        var error = await Assert.ThrowsAsync<AnthropicException>(() => MessageStreamReader.ReadAsync(quiet, null, CancellationToken.None, TimeSpan.FromMilliseconds(200)));

        Assert.Contains("quiet", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Sends its bytes, then nothing, without closing.</summary>
    private sealed class QuietStream(byte[] first) : MemoryStream(first)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position < Length)
            {
                return await base.ReadAsync(buffer, cancellationToken);
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }
}
