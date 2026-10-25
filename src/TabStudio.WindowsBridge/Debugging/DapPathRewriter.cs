using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.WindowsBridge.Builds;

namespace TabStudio.WindowsBridge.Debugging;

/// <summary>
/// Rewrites the paths in debug adapter messages crossing the bridge: the tablet's paths become the mirror's on the
/// way to the adapter (breakpoints' sources, the program to launch, its folder), and the mirror's become the tablet's
/// on the way back (stack frames' sources, output, exception details), so the debugger on the tablet opens its own
/// files and the adapter on the PC finds the program it built.
/// </summary>
public sealed class DapPathRewriter
{
    /// <summary>The properties that hold a path, in both directions.</summary>
    private static readonly HashSet<string> PathProperties = new(StringComparer.Ordinal) { "path", "program", "cwd" };

    /// <summary>The properties of messages for people, whose text may mention paths, from the adapter.</summary>
    private static readonly HashSet<string> TextProperties = new(StringComparer.Ordinal) { "output", "message", "description", "stackTrace", "text", "format" };

    private readonly BridgePathMapper _paths;

    /// <summary>Creates a rewriter for a workspace's paths.</summary>
    /// <param name="paths">The workspace's paths.</param>
    public DapPathRewriter(BridgePathMapper paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    /// <summary>A message from the tablet with the mirror's paths, for the adapter.</summary>
    /// <param name="json">The message's JSON.</param>
    /// <returns>The rewritten JSON, and the request's command, such as <c>launch</c>, when it is a request.</returns>
    public (byte[] Json, string? Command) ToAdapter(byte[] json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Parse(json) is not JsonObject message)
        {
            return (json, null);
        }

        var command = StringOf(message["type"]) == "request" ? StringOf(message["command"]) : null;
        Rewrite(message, inbound: true);
        return (ToBytes(message), command);
    }

    /// <summary>A message from the adapter with the tablet's paths.</summary>
    /// <param name="json">The message's JSON.</param>
    /// <returns>The rewritten JSON, and the event's name, such as <c>terminated</c>, when it is an event.</returns>
    public (byte[] Json, string? Event) ToTablet(byte[] json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Parse(json) is not JsonObject message)
        {
            return (json, null);
        }

        var name = StringOf(message["type"]) == "event" ? StringOf(message["event"]) : null;
        Rewrite(message, inbound: false);
        return (ToBytes(message), name);
    }

    private static string? StringOf(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static byte[] ToBytes(JsonNode node)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            node.WriteTo(writer);
        }

        return buffer.ToArray();
    }

    private static JsonNode? Parse(byte[] json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            // Not JSON: passed through as it is, and the other side says what is wrong with it.
            return null;
        }
    }

    private void Rewrite(JsonNode node, bool inbound)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj.ToList())
                {
                    if (value is JsonValue leaf && leaf.TryGetValue<string>(out var text))
                    {
                        var rewritten = inbound ? RewriteInbound(name, text) : RewriteOutbound(name, text);
                        if (!ReferenceEquals(rewritten, text))
                        {
                            obj[name] = rewritten;
                        }
                    }
                    else if (value is not null)
                    {
                        Rewrite(value, inbound);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        Rewrite(item, inbound);
                    }
                }

                break;
        }
    }

    private string RewriteInbound(string name, string text) =>
        PathProperties.Contains(name) && _paths.ToHost(text) is { } host ? host : text;

    private string RewriteOutbound(string name, string text)
    {
        if (PathProperties.Contains(name))
        {
            var client = _paths.ToClient(text);
            return string.Equals(client, text, StringComparison.Ordinal) ? text : client;
        }

        if (TextProperties.Contains(name))
        {
            var client = _paths.ToClientText(text);
            return string.Equals(client, text, StringComparison.Ordinal) ? text : client;
        }

        return text;
    }
}
