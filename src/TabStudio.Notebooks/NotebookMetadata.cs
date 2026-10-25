using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.Notebooks.Formats;

namespace TabStudio.Notebooks;

/// <summary>
/// An immutable JSON object: the metadata of a notebook, cell or output, or the properties of one that
/// TabStudio does not interpret.
/// </summary>
/// <remarks>
/// Notebooks carry settings written by many tools: Jupyter, VS Code, Polyglot Notebooks and their extensions.
/// TabStudio keeps all of them, including keys it has never heard of, so that saving a notebook never loses
/// another tool's data. Values are handed out as copies, so an instance never changes once it is created.
/// Two instances are equal when they hold the same JSON, whatever the order of their keys.
/// </remarks>
public sealed class NotebookMetadata : IEquatable<NotebookMetadata>
{
    private readonly JsonObject _properties;

    private NotebookMetadata(JsonObject properties)
    {
        _properties = properties;
    }

    /// <summary>Metadata without any properties.</summary>
    public static NotebookMetadata Empty { get; } = new(new JsonObject());

    /// <summary>The number of properties.</summary>
    public int Count => _properties.Count;

    /// <summary>Whether there are no properties.</summary>
    public bool IsEmpty => _properties.Count == 0;

    /// <summary>The property names, in the order they appear in the file.</summary>
    public IReadOnlyList<string> Keys => _properties.Select(p => p.Key).ToList();

    /// <summary>Creates metadata from a copy of a JSON object.</summary>
    /// <param name="properties">The properties, or <see langword="null"/> for none. The object is copied, not kept.</param>
    public static NotebookMetadata FromJson(JsonObject? properties) =>
        properties is null || properties.Count == 0 ? Empty : new((JsonObject)properties.DeepClone());

    /// <summary>Parses metadata from JSON text.</summary>
    /// <param name="json">A JSON object, such as <c>{"tags":["setup"]}</c>.</param>
    /// <exception cref="NotebookFormatException">The text is not a JSON object.</exception>
    public static NotebookMetadata Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            return JsonNode.Parse(json) is JsonObject properties
                ? Own(properties)
                : throw new NotebookFormatException("Metadata must be a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new NotebookFormatException($"Metadata is not valid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>Whether a property exists.</summary>
    /// <param name="key">The property name.</param>
    public bool ContainsKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _properties.ContainsKey(key);
    }

    /// <summary>Gets a copy of a property's value.</summary>
    /// <param name="key">The property name.</param>
    /// <param name="value">A copy of the value, which is <see langword="null"/> for a JSON <c>null</c>.</param>
    /// <returns>Whether the property exists.</returns>
    public bool TryGetValue(string key, out JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_properties.TryGetPropertyValue(key, out var node))
        {
            value = node?.DeepClone();
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Reads a string from a property, or from a property of nested objects.</summary>
    /// <param name="path">Property names from the outermost object inwards, such as <c>"polyglot_notebook", "kernelName"</c>.</param>
    /// <returns>The string, or <see langword="null"/> when the path does not lead to a string.</returns>
    public string? GetText(params string[] path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return JsonNodes.TextOf(Peek(path));
    }

    /// <summary>Returns metadata with a property added or replaced.</summary>
    /// <param name="key">The property name.</param>
    /// <param name="value">The value, which is copied. <see langword="null"/> stores a JSON <c>null</c>.</param>
    public NotebookMetadata With(string key, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        var copy = ToJsonObject();
        copy[key] = value?.DeepClone();
        return new NotebookMetadata(copy);
    }

    /// <summary>Returns metadata without a property.</summary>
    /// <param name="key">The property name.</param>
    public NotebookMetadata Without(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_properties.ContainsKey(key))
        {
            return this;
        }

        var copy = ToJsonObject();
        copy.Remove(key);
        return Own(copy);
    }

    /// <summary>Returns a copy of the properties as a JSON object that the caller may change.</summary>
    public JsonObject ToJsonObject() => (JsonObject)_properties.DeepClone();

    /// <summary>Returns the properties as compact JSON text.</summary>
    public string ToJsonString() => NotebookJsonWriter.WriteCompact(_properties);

    /// <inheritdoc />
    public bool Equals(NotebookMetadata? other) =>
        other is not null && (ReferenceEquals(this, other) || JsonNode.DeepEquals(_properties, other._properties));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as NotebookMetadata);

    /// <inheritdoc />
    public override int GetHashCode() => _properties.Count;

    /// <inheritdoc />
    public override string ToString() => ToJsonString();

    /// <summary>Wraps a JSON object that nothing else will change, without copying it.</summary>
    internal static NotebookMetadata Own(JsonObject properties) =>
        properties.Count == 0 ? Empty : new NotebookMetadata(properties);

    /// <summary>Reads a value without copying it. Callers must not change or re-parent the result.</summary>
    internal JsonNode? Peek(params string[] path)
    {
        JsonNode? node = _properties;
        foreach (var key in path)
        {
            if (node is not JsonObject properties || !properties.TryGetPropertyValue(key, out node))
            {
                return null;
            }
        }

        return node;
    }
}
