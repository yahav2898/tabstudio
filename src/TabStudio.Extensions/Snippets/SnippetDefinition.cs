using System.Text.Json;
using TabStudio.Extensions.Json;

namespace TabStudio.Extensions.Snippets;

/// <summary>A snippet from a snippet file.</summary>
/// <param name="Name">The snippet's name, the key in its file.</param>
/// <param name="Prefixes">What the user types to be offered it; empty when it is only inserted from Insert Snippet.</param>
/// <param name="Body">The body, with lines joined by <c>\n</c>.</param>
/// <param name="Description">What it is for, or <see langword="null"/>.</param>
/// <param name="Languages">For a snippet in a file shared by languages, the languages it is for (its <c>scope</c>); empty for all.</param>
public sealed record SnippetDefinition(string Name, IReadOnlyList<string> Prefixes, string Body, string? Description, IReadOnlyList<string> Languages)
{
    /// <summary>Reads a snippet file: <c>{ "Name": { "prefix": ..., "body": ..., "description": ... } }</c>.</summary>
    /// <param name="json">The file's text.</param>
    /// <returns>The snippets; entries without a body are left out.</returns>
    /// <exception cref="JsonException">The text is not JSON.</exception>
    public static IReadOnlyList<SnippetDefinition> ReadFile(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = Jsonc.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var snippets = new List<SnippetDefinition>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var snippet = property.Value;
            if (snippet.ValueKind != JsonValueKind.Object || snippet.GetElement("body") is not { } body)
            {
                continue;
            }

            var text = body.ValueKind == JsonValueKind.Array
                ? string.Join('\n', Jsonc.Strings(body))
                : body.ValueKind == JsonValueKind.String ? body.GetString()! : null;
            if (text is null)
            {
                continue;
            }

            var description = snippet.GetElement("description") is { } value
                ? value.ValueKind == JsonValueKind.Array ? string.Join('\n', Jsonc.Strings(value)) : value.ValueKind == JsonValueKind.String ? value.GetString() : null
                : null;
            var languages = (snippet.GetString("scope") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            snippets.Add(new SnippetDefinition(property.Name, [.. snippet.GetStrings("prefix").Where(p => p.Length > 0)], text, description, languages));
        }

        return snippets;
    }
}
