using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace TabStudio.Agent.Tests;

/// <summary>
/// Claude's Messages API, scripted: each request is answered with the next queued response, streamed as
/// server-sent events exactly as the API sends them.
/// </summary>
internal sealed class FakeClaude : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    /// <summary>The JSON body of every request, in order.</summary>
    public List<JsonObject> Requests { get; } = [];

    /// <summary>The API key of every request.</summary>
    public List<string?> ApiKeys { get; } = [];

    /// <summary>Queues a streamed answer of text.</summary>
    public FakeClaude Text(string text, string stopReason = "end_turn") =>
        Stream(Events.Start() + Events.TextBlock(0, text) + Events.Stop(stopReason));

    /// <summary>Queues an answer that calls one tool, optionally after some text.</summary>
    public FakeClaude ToolCall(string id, string name, JsonObject input, string? text = null)
    {
        var events = Events.Start();
        var index = 0;
        if (text is not null)
        {
            events += Events.TextBlock(index++, text);
        }

        return Stream(events + Events.ToolBlock(index, id, name, input) + Events.Stop("tool_use"));
    }

    /// <summary>Queues a raw event stream.</summary>
    public FakeClaude Stream(string events)
    {
        _responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(events, Encoding.UTF8, "text/event-stream") });
        return this;
    }

    /// <summary>Queues an error response.</summary>
    public FakeClaude Error(HttpStatusCode status, string type, string message)
    {
        _responses.Enqueue(() => new HttpResponseMessage(status)
        {
            Content = new StringContent($$$"""{"type":"error","error":{"type":"{{{type}}}","message":"{{{message}}}"}}""", Encoding.UTF8, "application/json"),
        });
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((JsonObject)JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!);
        ApiKeys.Add(request.Headers.TryGetValues("x-api-key", out var keys) ? keys.Single() : null);
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());
        return _responses.Count > 0 ? _responses.Dequeue()() : throw new InvalidOperationException("No scripted response left.");
    }

    /// <summary>Builds the API's server-sent events.</summary>
    internal static class Events
    {
        public static string Start() =>
            Event("message_start", """{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","content":[],"usage":{"input_tokens":120,"output_tokens":1,"cache_read_input_tokens":100,"cache_creation_input_tokens":20}}}""");

        public static string TextBlock(int index, string text)
        {
            var events = Event("content_block_start", $$$"""{"type":"content_block_start","index":{{{index}}},"content_block":{"type":"text","text":""}}""");

            // Split the text in two deltas, as the API streams it in pieces.
            var half = text.Length / 2;
            foreach (var piece in new[] { text[..half], text[half..] })
            {
                var delta = new JsonObject { ["type"] = "content_block_delta", ["index"] = index, ["delta"] = new JsonObject { ["type"] = "text_delta", ["text"] = piece } };
                events += Event("content_block_delta", delta.ToJsonString());
            }

            return events + Event("content_block_stop", $$$"""{"type":"content_block_stop","index":{{{index}}}}""") + Event("ping", """{"type":"ping"}""");
        }

        public static string ToolBlock(int index, string id, string name, JsonObject input)
        {
            var start = new JsonObject
            {
                ["type"] = "content_block_start",
                ["index"] = index,
                ["content_block"] = new JsonObject { ["type"] = "tool_use", ["id"] = id, ["name"] = name, ["input"] = new JsonObject() },
            };
            var events = Event("content_block_start", start.ToJsonString());
            var json = input.ToJsonString();
            var third = json.Length / 3;
            foreach (var piece in new[] { json[..third], json[third..(2 * third)], json[(2 * third)..] })
            {
                var delta = new JsonObject { ["type"] = "content_block_delta", ["index"] = index, ["delta"] = new JsonObject { ["type"] = "input_json_delta", ["partial_json"] = piece } };
                events += Event("content_block_delta", delta.ToJsonString());
            }

            return events + Event("content_block_stop", $$$"""{"type":"content_block_stop","index":{{{index}}}}""");
        }

        public static string Stop(string reason) =>
            Event("message_delta", $$$"""{"type":"message_delta","delta":{"stop_reason":"{{{reason}}}","stop_sequence":null},"usage":{"output_tokens":42}}""") +
            Event("message_stop", """{"type":"message_stop"}""");

        private static string Event(string name, string data) => $"event: {name}\ndata: {data}\n\n";
    }
}
