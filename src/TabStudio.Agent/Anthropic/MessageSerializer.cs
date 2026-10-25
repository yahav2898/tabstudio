using System.Text.Json.Nodes;

namespace TabStudio.Agent.Anthropic;

/// <summary>Writes Messages API requests, with prompt-cache breakpoints after the tools, the system prompt and the latest message.</summary>
internal static class MessageSerializer
{
    public static JsonObject Serialize(MessageRequest request, bool stream)
    {
        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens,
            ["stream"] = stream,
        };

        if (request.Tools.Count > 0)
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["input_schema"] = tool.InputSchema.DeepClone(),
                });
            }

            tools[^1]!["cache_control"] = Ephemeral();
            body["tools"] = tools;
        }

        if (!string.IsNullOrEmpty(request.System))
        {
            body["system"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = request.System, ["cache_control"] = Ephemeral() });
        }

        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            var content = new JsonArray();
            foreach (var block in message.Content)
            {
                content.Add(Serialize(block));
            }

            messages.Add(new JsonObject { ["role"] = message.Role == MessageRole.User ? "user" : "assistant", ["content"] = content });
        }

        // Caching the conversation up to the latest message makes each turn of a long session cheap.
        if (messages.Count > 0 && messages[^1]!["content"] is JsonArray { Count: > 0 } last)
        {
            last[^1]!["cache_control"] = Ephemeral();
        }

        body["messages"] = messages;
        return body;
    }

    private static JsonObject Serialize(ContentBlock block) => block switch
    {
        TextBlock text => new JsonObject { ["type"] = "text", ["text"] = text.Text },
        ImageBlock image => new JsonObject
        {
            ["type"] = "image",
            ["source"] = new JsonObject { ["type"] = "base64", ["media_type"] = image.MediaType, ["data"] = Convert.ToBase64String(image.Data.Span) },
        },
        ToolUseBlock use => new JsonObject { ["type"] = "tool_use", ["id"] = use.Id, ["name"] = use.Name, ["input"] = use.Input.DeepClone() },
        ToolResultBlock result => new JsonObject
        {
            ["type"] = "tool_result",
            ["tool_use_id"] = result.ToolUseId,
            ["content"] = result.Content,
            ["is_error"] = result.IsError,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(block), block, "Unknown content block."),
    };

    private static JsonObject Ephemeral() => new() { ["type"] = "ephemeral" };
}
