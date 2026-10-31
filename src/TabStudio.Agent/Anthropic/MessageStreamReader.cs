using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.Agent.Anthropic;

/// <summary>Reads a streamed Messages API answer (server-sent events) into a complete message.</summary>
internal static class MessageStreamReader
{
    /// <summary>
    /// How long the stream may go without a line. The API sends a ping every few seconds while it thinks, so a
    /// longer silence is a dead connection, which otherwise held the agent until the user pressed Stop.
    /// </summary>
    internal static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(90);

    public static async Task<MessageResponse> ReadAsync(Stream stream, Action<string>? onText, CancellationToken cancellationToken, TimeSpan? idleTimeout = null)
    {
        var idle = idleTimeout ?? IdleTimeout;
        var blocks = new SortedDictionary<int, BlockBuilder>();
        var stop = StopReason.Other;
        var usage = new TokenUsage(0, 0);
        var finished = false;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var data = new StringBuilder();
        while (await ReadLineAsync(reader, idle, cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    finished |= Handle(data.ToString(), blocks, ref stop, ref usage, onText);
                    data.Clear();
                }

                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                data.Append(line.AsSpan(5).TrimStart());
            }
        }

        if (data.Length > 0)
        {
            finished |= Handle(data.ToString(), blocks, ref stop, ref usage, onText);
        }

        if (!finished)
        {
            throw new AnthropicException("The connection to Claude ended before the answer was complete.");
        }

        var content = blocks.Values.Select(b => b.Build()).ToList();
        return new MessageResponse(new ChatMessage(MessageRole.Assistant, content), stop, usage);
    }

    private static async Task<string?> ReadLineAsync(StreamReader reader, TimeSpan idleTimeout, CancellationToken cancellationToken)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idle.CancelAfter(idleTimeout);
        try
        {
            return await reader.ReadLineAsync(idle.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AnthropicException("Claude stopped answering: the connection went quiet. Check the network and try again.", ex);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            throw new AnthropicException("The connection to Claude broke during the answer. Check the network and try again.", ex);
        }
    }

    /// <returns>Whether the message is complete.</returns>
    private static bool Handle(string json, SortedDictionary<int, BlockBuilder> blocks, ref StopReason stop, ref TokenUsage usage, Action<string>? onText)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new AnthropicException("Claude sent an answer TabStudio could not read.", ex);
        }

        switch (node?["type"]?.GetValue<string>())
        {
            case "message_start":
                usage = ReadUsage(node["message"]?["usage"], usage);
                break;

            case "content_block_start":
            {
                var index = node["index"]!.GetValue<int>();
                var block = node["content_block"]!;
                blocks[index] = block["type"]?.GetValue<string>() == "tool_use"
                    ? BlockBuilder.ToolUse(block["id"]!.GetValue<string>(), block["name"]!.GetValue<string>())
                    : BlockBuilder.Text(block["text"]?.GetValue<string>() ?? string.Empty);
                break;
            }

            case "content_block_delta":
            {
                var index = node["index"]!.GetValue<int>();
                var delta = node["delta"]!;
                if (!blocks.TryGetValue(index, out var block))
                {
                    break;
                }

                switch (delta["type"]?.GetValue<string>())
                {
                    case "text_delta":
                        var text = delta["text"]?.GetValue<string>() ?? string.Empty;
                        block.Append(text);
                        onText?.Invoke(text);
                        break;
                    case "input_json_delta":
                        block.Append(delta["partial_json"]?.GetValue<string>() ?? string.Empty);
                        break;
                }

                break;
            }

            case "message_delta":
                stop = ParseStopReason(node["delta"]?["stop_reason"]?.GetValue<string>());
                usage = ReadUsage(node["usage"], usage);
                break;

            case "message_stop":
                return true;

            case "error":
                var error = node["error"];
                throw new AnthropicException(null, error?["type"]?.GetValue<string>(), error?["message"]?.GetValue<string>() ?? "Claude reported an error.");
        }

        return false;
    }

    /// <summary>Merges the usage fields present in an event; output tokens are cumulative in the API.</summary>
    private static TokenUsage ReadUsage(JsonNode? node, TokenUsage current)
    {
        if (node is null)
        {
            return current;
        }

        int Read(string name, int fallback) => node[name] is { } value ? value.GetValue<int>() : fallback;
        return new TokenUsage(
            Read("input_tokens", current.InputTokens),
            Read("output_tokens", current.OutputTokens),
            Read("cache_read_input_tokens", current.CacheReadTokens),
            Read("cache_creation_input_tokens", current.CacheWriteTokens));
    }

    private static StopReason ParseStopReason(string? value) => value switch
    {
        "end_turn" => StopReason.EndTurn,
        "tool_use" => StopReason.ToolUse,
        "max_tokens" => StopReason.MaxTokens,
        "stop_sequence" => StopReason.StopSequence,
        "refusal" => StopReason.Refusal,
        _ => StopReason.Other,
    };

    private sealed class BlockBuilder
    {
        private readonly StringBuilder _content = new();
        private readonly string? _id;
        private readonly string? _name;

        private BlockBuilder(string? id, string? name, string initial)
        {
            _id = id;
            _name = name;
            _content.Append(initial);
        }

        public static BlockBuilder Text(string initial) => new(null, null, initial);

        public static BlockBuilder ToolUse(string id, string name) => new(id, name, string.Empty);

        public void Append(string text) => _content.Append(text);

        public ContentBlock Build()
        {
            if (_id is null)
            {
                return new TextBlock(_content.ToString());
            }

            var json = _content.Length == 0 ? "{}" : _content.ToString();
            JsonObject input;
            try
            {
                input = JsonNode.Parse(json) as JsonObject ?? [];
            }
            catch (JsonException)
            {
                // A tool call cut off by the token limit: its arguments are incomplete.
                input = [];
            }

            return new ToolUseBlock(_id, _name!, input);
        }
    }
}
