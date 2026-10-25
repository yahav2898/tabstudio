using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.LanguageServers.Configuration;

/// <summary>
/// Helpers for the JSON files people edit by hand: comments and trailing commas are allowed, as in VS Code's
/// settings, and objects merge key by key.
/// </summary>
public static class JsonSettings
{
    /// <summary>How hand-edited files are read: <c>//</c> and <c>/* */</c> comments and trailing commas are fine.</summary>
    public static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads a JSON object from hand-edited text.</summary>
    /// <param name="json">The text.</param>
    /// <returns>The object.</returns>
    /// <exception cref="JsonException">The text is not a JSON object.</exception>
    public static JsonObject ParseObject(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonNode.Parse(json, documentOptions: DocumentOptions) as JsonObject
            ?? throw new JsonException("The file must contain a JSON object.");
    }

    /// <summary>
    /// Merges one object into a copy of another: objects merge key by key, other values replace, and a
    /// <c>null</c> removes the key.
    /// </summary>
    /// <param name="target">The object to start from; it is not changed.</param>
    /// <param name="overrides">The values that win.</param>
    /// <returns>A new object.</returns>
    public static JsonObject Merge(JsonObject? target, JsonObject? overrides)
    {
        var result = target?.DeepClone() as JsonObject ?? new JsonObject();
        if (overrides is not null)
        {
            MergeInto(result, overrides);
        }

        return result;
    }

    /// <summary>
    /// Turns VS Code's dotted setting names into nested objects: <c>"python.analysis.typeCheckingMode": "strict"</c>
    /// becomes <c>{ "python": { "analysis": { "typeCheckingMode": "strict" } } }</c>.
    /// </summary>
    /// <param name="settings">Settings with dotted or nested names.</param>
    /// <returns>A new object with nested names only.</returns>
    public static JsonObject ExpandDottedNames(JsonObject settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var result = new JsonObject();
        foreach (var (name, value) in settings)
        {
            var parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            var expanded = value is JsonObject nested ? ExpandDottedNames(nested) : value?.DeepClone();
            for (var i = parts.Length - 1; i >= 1; i--)
            {
                expanded = new JsonObject { [parts[i]] = expanded };
            }

            MergeInto(result, new JsonObject { [parts[0]] = expanded });
        }

        return result;
    }

    /// <summary>The value at a dotted path, such as <c>python.analysis</c>, or <see langword="null"/>.</summary>
    /// <param name="settings">Nested settings.</param>
    /// <param name="path">The path; empty for the whole object.</param>
    public static JsonNode? Select(JsonObject settings, string? path)
    {
        ArgumentNullException.ThrowIfNull(settings);
        JsonNode? current = settings;
        foreach (var part in (path ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is not JsonObject value || !value.TryGetPropertyValue(part, out current))
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>A string property, or <see langword="null"/>.</summary>
    /// <param name="value">The object.</param>
    /// <param name="name">The property.</param>
    public static string? GetString(JsonObject value, string name)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value[name] is JsonValue text && text.TryGetValue(out string? result) ? result : null;
    }

    /// <summary>A Boolean property, or <see langword="null"/>.</summary>
    /// <param name="value">The object.</param>
    /// <param name="name">The property.</param>
    public static bool? GetBoolean(JsonObject value, string name)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value[name] is JsonValue flag && flag.TryGetValue(out bool result) ? result : null;
    }

    /// <summary>A property that is an array of strings; a single string counts as an array of one.</summary>
    /// <param name="value">The object.</param>
    /// <param name="name">The property.</param>
    public static IReadOnlyList<string> GetStrings(JsonObject value, string name)
    {
        ArgumentNullException.ThrowIfNull(value);
        switch (value[name])
        {
            case JsonArray array:
            {
                var strings = new List<string>();
                foreach (var item in array)
                {
                    if (item is JsonValue text && text.TryGetValue(out string? element))
                    {
                        strings.Add(element);
                    }
                }

                return strings;
            }

            case JsonValue single when single.TryGetValue(out string? only):
                return [only];
            default:
                return [];
        }
    }

    /// <summary>A property that is an object of string values, such as environment variables.</summary>
    /// <param name="value">The object.</param>
    /// <param name="name">The property.</param>
    public static IReadOnlyDictionary<string, string?> GetStringMap(JsonObject value, string name)
    {
        ArgumentNullException.ThrowIfNull(value);
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (value[name] is JsonObject entries)
        {
            foreach (var (key, item) in entries)
            {
                map[key] = item switch
                {
                    null => null,
                    JsonValue text when text.TryGetValue(out string? result) => result,
                    _ => item.ToJsonString(),
                };
            }
        }

        return map;
    }

    private static void MergeInto(JsonObject target, JsonObject overrides)
    {
        foreach (var (name, value) in overrides)
        {
            if (value is null)
            {
                target.Remove(name);
            }
            else if (value is JsonObject nested && target[name] is JsonObject existing)
            {
                MergeInto(existing, nested);
            }
            else
            {
                target[name] = value.DeepClone();
            }
        }
    }
}
