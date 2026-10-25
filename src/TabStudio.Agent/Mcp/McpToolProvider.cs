using System.Text;
using System.Text.Json.Nodes;
using TabStudio.Agent.Anthropic;
using TabStudio.Agent.Tools;

namespace TabStudio.Agent.Mcp;

/// <summary>
/// Offers an MCP server's tools to the agent, named <c>server__tool</c> so tools of different servers never
/// clash with each other or with the built-in tools.
/// </summary>
/// <param name="client">The server's client.</param>
public sealed class McpToolProvider(McpClient client) : IAgentToolProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentTool>> GetToolsAsync(CancellationToken cancellationToken)
    {
        var tools = await client.ListToolsAsync(cancellationToken).ConfigureAwait(false);
        return [.. tools.Select(t => new McpAgentTool(client, t))];
    }

    /// <summary>The name Claude sees: the server's and the tool's names, limited to the characters and length the API allows.</summary>
    internal static string QualifiedName(string server, string tool)
    {
        var builder = new StringBuilder();
        foreach (var c in $"{server}__{tool}")
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' ? c : '_');
        }

        return builder.Length <= 64 ? builder.ToString() : builder.ToString(0, 64);
    }

    private sealed class McpAgentTool(McpClient client, McpTool tool) : AgentTool
    {
        public override ToolDefinition Definition { get; } = new(
            QualifiedName(client.Server.Name, tool.Name),
            $"{tool.Description} (from the MCP server '{client.Server.Name}')".Trim(),
            tool.InputSchema);

        public override string Describe(JsonObject input) => $"{tool.Name} on {client.Server.Name}";

        public override async Task<ToolOutcome> InvokeAsync(JsonObject input, CancellationToken cancellationToken)
        {
            try
            {
                var result = await client.CallToolAsync(tool.Name, input, cancellationToken).ConfigureAwait(false);
                return new ToolOutcome(result.Text.Length == 0 ? "(no output)" : result.Text, result.IsError);
            }
            catch (McpException ex)
            {
                return ToolOutcome.Error(ex.Message);
            }
        }
    }
}
