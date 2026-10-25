using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.Notebooks.Formats;

/// <summary>Small helpers for reading and writing the JSON of notebook files.</summary>
internal static class JsonNodes
{
    /// <summary>Removes a property from an object and returns its value, detached so it can be kept elsewhere.</summary>
    public static JsonNode? Take(JsonObject owner, string key)
    {
        if (!owner.TryGetPropertyValue(key, out var value))
        {
            return null;
        }

        owner.Remove(key);
        return value;
    }

    /// <summary>Removes a property and returns it when it is an object.</summary>
    public static JsonObject? TakeObject(JsonObject owner, string key) => Take(owner, key) as JsonObject;

    /// <summary>The value of a JSON string, or <see langword="null"/> for anything else.</summary>
    public static string? TextOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    /// <summary>The value of a JSON number that fits an <see cref="int"/>, or <see langword="null"/> for anything else.</summary>
    public static int? IntOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<int>(out var number)
            ? number
            : null;

    /// <summary>
    /// Reads multi-line text the way nbformat stores it: either one string or a list of strings that are joined.
    /// </summary>
    public static string JoinText(JsonNode? node) => node switch
    {
        JsonArray lines => string.Concat(lines.Select(line => TextOf(line) ?? string.Empty)),
        _ => TextOf(node) ?? string.Empty,
    };

    /// <summary>Reads a list of strings, accepting a single string as a list of one.</summary>
    public static List<string> ReadList(JsonNode? node) => node switch
    {
        JsonArray items => items.Select(item => TextOf(item) ?? string.Empty).ToList(),
        _ when TextOf(node) is { } text => [text],
        _ => [],
    };

    /// <summary>
    /// Splits text into lines that each keep their line break, the way Jupyter and VS Code write multi-line
    /// text. Empty text becomes an empty list.
    /// </summary>
    public static JsonArray SplitLines(string text)
    {
        var lines = new JsonArray();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines.Add(JsonValue.Create(text[start..(i + 1)]));
                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(JsonValue.Create(text[start..]));
        }

        return lines;
    }

    /// <summary>Gets a child object, creating it when it does not exist.</summary>
    public static JsonObject GetOrAddObject(JsonObject owner, string key)
    {
        if (owner[key] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        owner[key] = created;
        return created;
    }
}
