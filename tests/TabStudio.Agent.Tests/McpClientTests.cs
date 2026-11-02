using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using TabStudio.Agent.Mcp;

namespace TabStudio.Agent.Tests;

public sealed class McpClientTests
{
    [Fact]
    public async Task Connects_lists_tools_over_event_streams_and_calls_them_in_a_session()
    {
        var server = new FakeMcpServer();
        var client = new McpClient(new HttpClient(server), new McpServer("issues", new Uri("https://mcp.test/mcp"), BearerToken: "secret"));

        var tools = await client.ListToolsAsync();
        var result = await client.CallToolAsync("search_issues", new JsonObject { ["query"] = "crash" });

        Assert.Equal("search_issues", Assert.Single(tools).Name);
        Assert.Equal("Search the issue tracker", tools[0].Description);
        Assert.Equal("Found 2 issues about crash", result.Text);
        Assert.False(result.IsError);
        Assert.Equal(new[] { "initialize", "notifications/initialized", "tools/list", "tools/call" }, server.Methods);
        Assert.All(server.SessionIds.Skip(1), id => Assert.Equal("session-1", id));
        Assert.All(server.Tokens, token => Assert.Equal("secret", token));
    }

    [Fact]
    public async Task Tools_are_offered_to_the_agent_with_the_server_name_in_front()
    {
        var provider = new McpToolProvider(new McpClient(new HttpClient(new FakeMcpServer()), new McpServer("my issues", new Uri("https://mcp.test/mcp"))));

        var tool = Assert.Single(await provider.GetToolsAsync(CancellationToken.None));
        var outcome = await tool.InvokeAsync(new JsonObject { ["query"] = "slow" }, CancellationToken.None);

        Assert.Equal("my_issues__search_issues", tool.Definition.Name);
        Assert.Equal("Found 2 issues about slow", outcome.Content);
    }

    [Fact]
    public void Qualified_names_are_limited_to_what_the_API_accepts() =>
        Assert.Equal(64, McpToolProvider.QualifiedName(new string('s', 40), new string('t', 40)).Length);

    [Fact]
    public void Long_tool_names_stay_unique_when_cut()
    {
        var server = "company-wide-issue-tracker-production";
        var first = McpToolProvider.QualifiedName(server, "list_issues_for_repository_by_label");
        var second = McpToolProvider.QualifiedName(server, "list_issues_for_repository_by_milestone");
        Assert.NotEqual(first, second);
        Assert.All(new[] { first, second }, name => Assert.Matches("^[A-Za-z0-9_-]{1,64}$", name));
    }

    [Fact]
    public async Task Errors_are_explained()
    {
        var client = new McpClient(new HttpClient(new FakeMcpServer { Status = HttpStatusCode.Unauthorized }), new McpServer("locked", new Uri("https://mcp.test/mcp")));

        var error = await Assert.ThrowsAsync<McpException>(() => client.ListToolsAsync());

        Assert.Contains("needs a valid token", error.Message, StringComparison.Ordinal);
    }

    private sealed class FakeMcpServer : HttpMessageHandler
    {
        public List<string> Methods { get; } = [];

        public List<string?> SessionIds { get; } = [];

        public List<string?> Tokens { get; } = [];

        public HttpStatusCode? Status { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Status is { } status)
            {
                return new HttpResponseMessage(status);
            }

            var message = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            var method = message["method"]!.GetValue<string>();
            Methods.Add(method);
            SessionIds.Add(request.Headers.TryGetValues("Mcp-Session-Id", out var ids) ? ids.Single() : null);
            Tokens.Add(request.Headers.Authorization?.Parameter);
            var id = message["id"]?.GetValue<int>();

            switch (method)
            {
                case "initialize":
                    var initialized = Json(Result(id, new JsonObject
                    {
                        ["protocolVersion"] = McpClient.ProtocolVersion,
                        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                        ["serverInfo"] = new JsonObject { ["name"] = "issues", ["version"] = "1" },
                    }));
                    initialized.Headers.Add("Mcp-Session-Id", "session-1");
                    return initialized;
                case "notifications/initialized":
                    return new HttpResponseMessage(HttpStatusCode.Accepted);
                case "tools/list":
                    var tools = Result(id, new JsonObject
                    {
                        ["tools"] = new JsonArray(new JsonObject
                        {
                            ["name"] = "search_issues",
                            ["description"] = "Search the issue tracker",
                            ["inputSchema"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["query"] = new JsonObject { ["type"] = "string" } } },
                        }),
                    });

                    // Answer as a stream, after a notification the client must skip.
                    const string Progress = """{"jsonrpc":"2.0","method":"notifications/progress","params":{}}""";
                    var stream = $"event: message\ndata: {Progress}\n\nevent: message\ndata: {tools.ToJsonString()}\n\n";
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(stream, Encoding.UTF8, "text/event-stream") };
                case "tools/call":
                    var query = message["params"]!["arguments"]!["query"]!.GetValue<string>();
                    return Json(Result(id, new JsonObject
                    {
                        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = $"Found 2 issues about {query}" }),
                        ["isError"] = false,
                    }));
                default:
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        private static JsonObject Result(int? id, JsonObject result) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

        private static HttpResponseMessage Json(JsonObject body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
    }
}
