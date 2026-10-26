using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.Agent.Anthropic;

namespace TabStudio.Agent.Local;

/// <summary>ChatML chat with a deliberately small proposal-only tool protocol, entirely on the device.</summary>
public sealed class LocalAgentClient(ILocalTextGenerator generator, string? instructions = null) : IAgentClient
{
    /// <summary>The tools local models can call; no MCP, shell, deletion or network tools.</summary>
    public static IReadOnlySet<string> AllowedTools { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "list_files", "read_file", "edit_file", "create_file",
    };

    /// <inheritdoc />
    public async Task<MessageResponse> SendAsync(MessageRequest request, Action<string>? onText = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Messages.Any(m => m.Content.OfType<ImageBlock>().Any()))
            throw new InvalidOperationException("Local models support text. Remove the screenshot to continue on device.");
        var tools = request.Tools.Where(t => AllowedTools.Contains(t.Name)).ToList();
        var system = new StringBuilder("You are TabStudio's offline coding assistant. Explain code, propose fixes and generate tests. File changes need user approval. Do not claim to run code or tests.\n");
        system.Append("To call ONE tool, respond with only a JSON object: {\"tool\":\"read_file\",\"arguments\":{\"path\":\"src/Program.cs\"}}. Otherwise answer in plain text. Available tools:\n");
        foreach (var tool in tools) system.Append(tool.Name).Append(' ').Append(tool.InputSchema.ToJsonString()).Append('\n');
        if (!string.IsNullOrWhiteSpace(instructions)) system.Append("User instructions: ").Append(FimPromptBuilder.Head(instructions, 350));
        var prompt = new StringBuilder("<|im_start|>system\n").Append(FimPromptBuilder.Head(system.ToString(), 1700)).Append("<|im_end|>\n");
        // Fit whole recent messages, always including the current user request/tool result.
        var recent = new List<(string Role, string Text)>();
        var remaining = 1200;
        foreach (var message in request.Messages.Reverse())
        {
            if (remaining <= 0) break;
            var text = new StringBuilder(message.Text);
            foreach (var call in message.Content.OfType<ToolUseBlock>()) text.Append(JsonSerializer.Serialize(new { tool = call.Name, arguments = call.Input }));
            foreach (var result in message.Content.OfType<ToolResultBlock>()) text.Append("Tool result: ").Append(result.Content);
            var bounded = FimPromptBuilder.Head(text.ToString(), remaining);
            recent.Add((message.Role == MessageRole.Assistant ? "assistant" : "user", bounded));
            remaining -= Encoding.UTF8.GetByteCount(bounded) + 50;
        }

        foreach (var (role, text) in recent.AsEnumerable().Reverse()) prompt.Append("<|im_start|>").Append(role).Append('\n').Append(text.Replace("<|im_end|>", "", StringComparison.Ordinal)).Append("<|im_end|>\n");
        prompt.Append("<|im_start|>assistant\n");
        var pending = new StringBuilder();
        var decided = false;
        var toolCandidate = false;
        var streamed = false;
        void Stream(string delta)
        {
            if (!decided)
            {
                pending.Append(delta);
                var leading = pending.ToString().TrimStart();
                if (leading.Length == 0) return;
                decided = true;
                toolCandidate = leading[0] is '{' or '`';
                if (!toolCandidate) { onText?.Invoke(pending.ToString()); streamed = true; }
            }
            else if (!toolCandidate) { onText?.Invoke(delta); streamed = true; }
        }
        var completion = await generator.GenerateAsync(prompt.ToString(), Math.Min(request.MaxTokens, 768), Stream, cancellationToken).ConfigureAwait(false);
        var output = completion.Text.Trim();
        var jsonText = output.StartsWith("```json", StringComparison.Ordinal) ? output[7..].Trim().TrimEnd('`').Trim() : output;
        if (jsonText.StartsWith('{'))
        {
            try
            {
                if (JsonNode.Parse(jsonText) is JsonObject json && json["tool"]?.GetValue<string>() is { } name)
                {
                    if (!tools.Any(t => string.Equals(t.Name, name, StringComparison.Ordinal)))
                        throw new InvalidOperationException($"The local model requested unavailable tool '{name}'. No action was taken.");
                    var arguments = json["arguments"] as JsonObject ?? throw new InvalidDataException("The local tool arguments must be an object.");
                    return new MessageResponse(new ChatMessage(MessageRole.Assistant, [new ToolUseBlock(Guid.NewGuid().ToString("N"), name, arguments)]), StopReason.ToolUse, new TokenUsage(0, completion.Tokens));
                }
            }
            catch (JsonException) { /* Incomplete JSON is shown for review, never executed. */ }
        }

        if (!streamed) onText?.Invoke(completion.Text);
        return new MessageResponse(ChatMessage.FromText(MessageRole.Assistant, completion.Text), StopReason.EndTurn, new TokenUsage(0, completion.Tokens));
    }
}
