using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.Agent.Mcp;

/// <summary>A Model Context Protocol server the agent connects to.</summary>
/// <param name="Name">A short name, used to prefix its tools (letters, digits, <c>_</c> and <c>-</c>).</param>
/// <param name="Url">The server's Streamable HTTP endpoint.</param>
/// <param name="BearerToken">A token for servers that require one, or <see langword="null"/>.</param>
public sealed record McpServer(string Name, Uri Url, string? BearerToken = null);

/// <summary>A tool an MCP server offers.</summary>
/// <param name="Name">The tool's name on the server.</param>
/// <param name="Description">What it does.</param>
/// <param name="InputSchema">The JSON schema of its arguments.</param>
public sealed record McpTool(string Name, string Description, JsonObject InputSchema);

/// <summary>The result of an MCP tool call.</summary>
/// <param name="Text">The text content of the result.</param>
/// <param name="IsError">Whether the tool reported an error.</param>
public sealed record McpToolResult(string Text, bool IsError);

/// <summary>An MCP server failed or could not be reached. The message is written for the user.</summary>
public sealed class McpException : Exception
{
    /// <summary>Creates the exception.</summary>
    public McpException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public McpException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public McpException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A client of one MCP server over the Streamable HTTP transport: JSON-RPC requests sent by POST, answered with
/// JSON or a server-sent event stream. Servers that run as local processes (stdio) are not reachable from an
/// Android app; servers on the network or in the cloud are.
/// </summary>
public sealed class McpClient
{
    /// <summary>The protocol version TabStudio speaks.</summary>
    public const string ProtocolVersion = "2025-06-18";

    private readonly HttpClient _http;
    private readonly McpServer _server;
    private readonly Lock _gate = new();
    private Task? _initialized;
    private string? _sessionId;
    private int _nextId;

    /// <summary>Creates a client. It connects on first use.</summary>
    /// <param name="http">The HTTP client.</param>
    /// <param name="server">The server.</param>
    public McpClient(HttpClient http, McpServer server)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(server);
        _http = http;
        _server = server;
    }

    /// <summary>The server.</summary>
    public McpServer Server => _server;

    /// <summary>Lists the server's tools.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IReadOnlyList<McpTool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var tools = new List<McpTool>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var parameters = cursor is null ? new JsonObject() : new JsonObject { ["cursor"] = cursor };
            var result = await RequestAsync("tools/list", parameters, cancellationToken).ConfigureAwait(false);
            if (result["tools"] is JsonArray list)
            {
                foreach (var tool in list.OfType<JsonObject>())
                {
                    if (tool["name"]?.GetValue<string>() is { } name)
                    {
                        tools.Add(new McpTool(
                            name,
                            tool["description"]?.GetValue<string>() ?? string.Empty,
                            tool["inputSchema"] is JsonObject schema ? (JsonObject)schema.DeepClone() : new JsonObject { ["type"] = "object" }));
                    }
                }
            }

            // An empty or repeated cursor would ask for the same page forever.
            var next = result["nextCursor"] is JsonValue value && value.TryGetValue<string>(out var c) && c.Length > 0 ? c : null;
            cursor = next == cursor || ++pages >= 100 ? null : next;
        }
        while (cursor is not null);

        return tools;
    }

    /// <summary>Calls a tool.</summary>
    /// <param name="name">The tool's name on the server.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<McpToolResult> CallToolAsync(string name, JsonObject arguments, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(arguments);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var result = await RequestAsync("tools/call", new JsonObject { ["name"] = name, ["arguments"] = arguments.DeepClone() }, cancellationToken).ConfigureAwait(false);

        var text = new StringBuilder();
        if (result["content"] is JsonArray content)
        {
            foreach (var item in content.OfType<JsonObject>())
            {
                var part = item["type"]?.GetValue<string>() switch
                {
                    "text" => item["text"]?.GetValue<string>(),
                    "resource" => item["resource"]?["text"]?.GetValue<string>(),
                    var other => $"[{other} content]",
                };
                if (part is not null)
                {
                    text.Append(text.Length > 0 ? "\n" : string.Empty).Append(part);
                }
            }
        }

        if (text.Length == 0 && result["structuredContent"] is { } structured)
        {
            text.Append(structured.ToJsonString());
        }

        return new McpToolResult(text.ToString(), result["isError"]?.GetValue<bool>() == true);
    }

    private Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_initialized is null || _initialized.IsFaulted || _initialized.IsCanceled)
            {
                _initialized = InitializeAsync(cancellationToken);
            }

            return _initialized;
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _sessionId = null;
        var result = await RequestAsync(
            "initialize",
            new JsonObject
            {
                ["protocolVersion"] = ProtocolVersion,
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject { ["name"] = "TabStudio", ["version"] = "1.0" },
            },
            cancellationToken).ConfigureAwait(false);

        if (result["capabilities"]?["tools"] is null)
        {
            throw new McpException($"The MCP server '{_server.Name}' offers no tools.");
        }

        await NotifyAsync("notifications/initialized", cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonObject> RequestAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var message = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters };
        using var response = await PostAsync(message, cancellationToken).ConfigureAwait(false);

        JsonObject? reply;
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            reply = await ReadEventStreamAsync(stream, id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            reply = Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)) as JsonObject;
        }

        if (reply is null)
        {
            throw new McpException($"The MCP server '{_server.Name}' did not answer {method}.");
        }

        if (reply["error"] is JsonObject error)
        {
            throw new McpException($"The MCP server '{_server.Name}' refused {method}: {error["message"]?.GetValue<string>() ?? "unknown error"}");
        }

        return reply["result"] as JsonObject ?? [];
    }

    private async Task NotifyAsync(string method, CancellationToken cancellationToken)
    {
        using var response = await PostAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> PostAsync(JsonObject message, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _server.Url)
        {
            Content = new StringContent(message.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("MCP-Protocol-Version", ProtocolVersion);
        if (_sessionId is not null)
        {
            request.Headers.Add("Mcp-Session-Id", _sessionId);
        }

        if (_server.BearerToken is { Length: > 0 } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new McpException($"Could not reach the MCP server '{_server.Name}': {ex.Message}", ex);
        }

        if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessions))
        {
            _sessionId = sessions.FirstOrDefault() ?? _sessionId;
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            response.Dispose();
            throw new McpException(status switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"The MCP server '{_server.Name}' needs a valid token.",
                HttpStatusCode.NotFound when _sessionId is not null => $"The MCP server '{_server.Name}' ended the session. Try again.",
                _ => $"The MCP server '{_server.Name}' answered {(int)status}.",
            });
        }

        return response;
    }

    /// <summary>Reads server-sent events until the response to a request arrives.</summary>
    private static async Task<JsonObject?> ReadEventStreamAsync(Stream stream, int id, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                data.Append(line.AsSpan(5).TrimStart());
                continue;
            }

            if (line.Length == 0 && data.Length > 0)
            {
                if (Parse(data.ToString()) is JsonObject message && message["id"]?.GetValue<int>() == id)
                {
                    return message;
                }

                data.Clear();
            }
        }

        return data.Length > 0 ? Parse(data.ToString()) as JsonObject : null;
    }

    private static JsonNode? Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new McpException("An MCP server sent an answer TabStudio could not read.", ex);
        }
    }
}
