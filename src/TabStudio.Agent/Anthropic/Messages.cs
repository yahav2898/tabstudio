using System.Text.Json.Nodes;

namespace TabStudio.Agent.Anthropic;

/// <summary>Who wrote a message.</summary>
public enum MessageRole
{
    /// <summary>The person, or tool results sent on their behalf.</summary>
    User,

    /// <summary>Claude.</summary>
    Assistant,
}

/// <summary>A part of a message.</summary>
public abstract record ContentBlock;

/// <summary>Text.</summary>
/// <param name="Text">The text.</param>
public sealed record TextBlock(string Text) : ContentBlock;

/// <summary>Claude asking to use a tool.</summary>
/// <param name="Id">The call's id, which the result refers to.</param>
/// <param name="Name">The tool.</param>
/// <param name="Input">The arguments, as a JSON object.</param>
public sealed record ToolUseBlock(string Id, string Name, JsonObject Input) : ContentBlock;

/// <summary>The result of a tool call, sent back to Claude.</summary>
/// <param name="ToolUseId">The call it answers.</param>
/// <param name="Content">What the tool returned, as text.</param>
/// <param name="IsError">Whether the tool failed.</param>
public sealed record ToolResultBlock(string ToolUseId, string Content, bool IsError = false) : ContentBlock;

/// <summary>A message in a conversation.</summary>
/// <param name="Role">Who wrote it.</param>
/// <param name="Content">Its parts.</param>
public sealed record ChatMessage(MessageRole Role, IReadOnlyList<ContentBlock> Content)
{
    /// <summary>A message holding only text.</summary>
    /// <param name="role">Who wrote it.</param>
    /// <param name="text">The text.</param>
    public static ChatMessage FromText(MessageRole role, string text) => new(role, [new TextBlock(text)]);

    /// <summary>The message's text parts joined.</summary>
    public string Text => string.Concat(Content.OfType<TextBlock>().Select(b => b.Text));
}

/// <summary>A tool Claude may call.</summary>
/// <param name="Name">Its name: letters, digits, underscores and hyphens.</param>
/// <param name="Description">What it does and when to use it, for Claude.</param>
/// <param name="InputSchema">A JSON schema of its arguments.</param>
public sealed record ToolDefinition(string Name, string Description, JsonObject InputSchema);

/// <summary>A request to the Messages API.</summary>
/// <param name="Model">The model, such as <c>claude-opus-5-5</c>.</param>
/// <param name="System">The system prompt.</param>
/// <param name="Messages">The conversation so far.</param>
/// <param name="Tools">The tools Claude may call.</param>
/// <param name="MaxTokens">The longest answer to allow.</param>
public sealed record MessageRequest(string Model, string? System, IReadOnlyList<ChatMessage> Messages, IReadOnlyList<ToolDefinition> Tools, int MaxTokens = 8192);

/// <summary>Why Claude stopped.</summary>
public enum StopReason
{
    /// <summary>The answer is complete.</summary>
    EndTurn,

    /// <summary>Claude is waiting for tool results.</summary>
    ToolUse,

    /// <summary>The answer reached the token limit.</summary>
    MaxTokens,

    /// <summary>A stop sequence was produced.</summary>
    StopSequence,

    /// <summary>Claude declined to continue.</summary>
    Refusal,

    /// <summary>A reason this version of TabStudio does not know.</summary>
    Other,
}

/// <summary>Tokens a request used.</summary>
/// <param name="InputTokens">Prompt tokens not read from the cache.</param>
/// <param name="OutputTokens">Tokens Claude wrote.</param>
/// <param name="CacheReadTokens">Prompt tokens read from the prompt cache.</param>
/// <param name="CacheWriteTokens">Prompt tokens written to the prompt cache.</param>
public sealed record TokenUsage(int InputTokens, int OutputTokens, int CacheReadTokens = 0, int CacheWriteTokens = 0)
{
    /// <summary>Adds two usages.</summary>
    /// <param name="other">The other usage.</param>
    public TokenUsage Add(TokenUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(InputTokens + other.InputTokens, OutputTokens + other.OutputTokens, CacheReadTokens + other.CacheReadTokens, CacheWriteTokens + other.CacheWriteTokens);
    }
}

/// <summary>Claude's answer.</summary>
/// <param name="Message">The assistant message.</param>
/// <param name="StopReason">Why it stopped.</param>
/// <param name="Usage">Tokens used.</param>
public sealed record MessageResponse(ChatMessage Message, StopReason StopReason, TokenUsage Usage);
