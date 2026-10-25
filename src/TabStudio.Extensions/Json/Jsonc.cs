using System.Text;
using System.Text.Json;

namespace TabStudio.Extensions.Json;

/// <summary>
/// Reads the JSON that VS Code extensions are written in: comments and trailing commas are allowed, as VS Code
/// allows them in package.json, language configuration, snippet and theme files.
/// </summary>
internal static class Jsonc
{
    /// <summary>The options every reader uses.</summary>
    public static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 256,
    };

    /// <summary>Parses JSON text. A leading byte order mark is ignored.</summary>
    /// <param name="text">The text.</param>
    /// <exception cref="JsonException">The text is not JSON.</exception>
    public static JsonDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return JsonDocument.Parse(text.AsMemory(text.StartsWith('﻿') ? 1 : 0), Options);
    }

    /// <summary>Reads a file as UTF-8 text, without its byte order mark.</summary>
    /// <param name="path">The file.</param>
    public static string ReadText(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        return text.StartsWith('﻿') ? text[1..] : text;
    }

    /// <summary>A string property, or <see langword="null"/> when it is missing or not a string.</summary>
    public static string? GetString(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>A Boolean property, or <see langword="null"/> when it is missing or not a Boolean.</summary>
    public static bool? GetBoolean(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    /// <summary>A number property as a double, or <see langword="null"/>.</summary>
    public static double? GetDouble(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
            ? number
            : null;

    /// <summary>A number property as a whole number, or <see langword="null"/>.</summary>
    public static long? GetInt64(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.TryGetInt64(out var whole) ? whole : value.TryGetDouble(out var real) ? (long)real : null
            : null;

    /// <summary>A property of any kind, or <see langword="null"/> when it is missing or <c>null</c>.</summary>
    public static JsonElement? GetElement(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value
            : null;

    /// <summary>An object property, or <see langword="null"/> when it is missing or not an object.</summary>
    public static JsonElement? GetObject(this JsonElement element, string name) =>
        element.GetElement(name) is { ValueKind: JsonValueKind.Object } value ? value : null;

    /// <summary>The items of an array property; none when it is missing or not an array.</summary>
    public static IEnumerable<JsonElement> GetArray(this JsonElement element, string name) =>
        element.GetElement(name) is { ValueKind: JsonValueKind.Array } value ? value.EnumerateArray() : [];

    /// <summary>
    /// A property that VS Code allows as one string or an array of strings, such as a snippet's <c>prefix</c>.
    /// Items that are not strings are skipped.
    /// </summary>
    public static IReadOnlyList<string> GetStrings(this JsonElement element, string name) =>
        element.GetElement(name) is { } value ? Strings(value) : [];

    /// <summary>One string, or the strings of an array, from a value that may be either.</summary>
    public static IReadOnlyList<string> Strings(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => [value.GetString()!],
        JsonValueKind.Array => [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)],
        _ => [],
    };

    /// <summary>The string properties of an object property, such as a grammar's <c>embeddedLanguages</c>.</summary>
    public static IReadOnlyDictionary<string, string> GetStringMap(this JsonElement element, string name)
    {
        if (element.GetObject(name) is not { } map)
        {
            return new Dictionary<string, string>();
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in map.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                result[property.Name] = property.Value.GetString()!;
            }
        }

        return result;
    }
}
