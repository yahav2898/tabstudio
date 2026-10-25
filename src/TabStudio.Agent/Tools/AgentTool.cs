using System.Text.Json.Nodes;
using TabStudio.Agent.Anthropic;

namespace TabStudio.Agent.Tools;

/// <summary>What a tool call returned.</summary>
/// <param name="Content">The result as text, for Claude.</param>
/// <param name="IsError">Whether the call failed; Claude sees the content as the error.</param>
public sealed record ToolOutcome(string Content, bool IsError = false)
{
    /// <summary>A failed call.</summary>
    /// <param name="message">What went wrong, for Claude.</param>
    public static ToolOutcome Error(string message) => new(message, IsError: true);
}

/// <summary>A tool the agent can call.</summary>
public abstract class AgentTool
{
    /// <summary>The name, description and argument schema Claude sees.</summary>
    public abstract ToolDefinition Definition { get; }

    /// <summary>A short description of a call for the conversation view, such as "Read src/Program.cs".</summary>
    /// <param name="input">The call's arguments.</param>
    public virtual string Describe(JsonObject input) => Definition.Name;

    /// <summary>Runs the tool.</summary>
    /// <param name="input">The arguments Claude passed.</param>
    /// <param name="cancellationToken">Stops the tool.</param>
    public abstract Task<ToolOutcome> InvokeAsync(JsonObject input, CancellationToken cancellationToken);
}

/// <summary>A tool made from a definition and a function.</summary>
/// <param name="definition">The definition.</param>
/// <param name="describe">Describes a call.</param>
/// <param name="invoke">Runs a call.</param>
internal sealed class DelegateTool(
    ToolDefinition definition,
    Func<JsonObject, string> describe,
    Func<JsonObject, CancellationToken, Task<ToolOutcome>> invoke) : AgentTool
{
    public override ToolDefinition Definition => definition;

    public override string Describe(JsonObject input) => describe(input);

    public override Task<ToolOutcome> InvokeAsync(JsonObject input, CancellationToken cancellationToken) => invoke(input, cancellationToken);
}

/// <summary>Supplies tools from somewhere else, such as an MCP server.</summary>
public interface IAgentToolProvider
{
    /// <summary>Lists the provider's tools.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    Task<IReadOnlyList<AgentTool>> GetToolsAsync(CancellationToken cancellationToken);
}
