using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.Notebooks.Formats;

/// <summary>
/// Writes JSON the way Jupyter (Python's <c>json</c> module) and VS Code do, so that saved notebooks match
/// what those tools write byte for byte.
/// </summary>
/// <remarks>
/// <see cref="System.Text.Json"/>'s writer escapes non-ASCII and HTML characters and indents by two spaces;
/// notebooks are written with one space and with every character except quotes, backslashes and control
/// characters as it is. That keeps Hebrew text, emoji and HTML outputs readable in the file and in diffs.
/// </remarks>
internal static class NotebookJsonWriter
{
    /// <summary>Writes indented JSON in Jupyter's canonical form: keys sorted, one space per level, no final newline.</summary>
    public static string WriteIndented(JsonNode? node)
    {
        var builder = new StringBuilder();
        Write(builder, node, depth: 0, indented: true, sortKeys: true);
        return builder.ToString();
    }

    /// <summary>Writes JSON on one line without spaces, keeping the order of keys.</summary>
    public static string WriteCompact(JsonNode? node)
    {
        var builder = new StringBuilder();
        Write(builder, node, depth: 0, indented: false, sortKeys: false);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, JsonNode? node, int depth, bool indented, bool sortKeys)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                break;
            case JsonObject properties:
                WriteObject(builder, properties, depth, indented, sortKeys);
                break;
            case JsonArray items:
                WriteArray(builder, items, depth, indented, sortKeys);
                break;
            case JsonValue text when text.GetValueKind() == JsonValueKind.String:
                WriteString(builder, text.GetValue<string>());
                break;
            case JsonValue value:
                // Numbers keep the text they were read with; booleans are fixed words.
                builder.Append(value.ToJsonString());
                break;
        }
    }

    private static void WriteObject(StringBuilder builder, JsonObject properties, int depth, bool indented, bool sortKeys)
    {
        if (properties.Count == 0)
        {
            builder.Append("{}");
            return;
        }

        IEnumerable<KeyValuePair<string, JsonNode?>> ordered = sortKeys
            ? properties.OrderBy(p => p.Key, StringComparer.Ordinal)
            : properties;

        builder.Append('{');
        var first = true;
        foreach (var (key, value) in ordered)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            NewLine(builder, depth + 1, indented);
            WriteString(builder, key);
            builder.Append(indented ? ": " : ":");
            Write(builder, value, depth + 1, indented, sortKeys);
        }

        NewLine(builder, depth, indented);
        builder.Append('}');
    }

    private static void WriteArray(StringBuilder builder, JsonArray items, int depth, bool indented, bool sortKeys)
    {
        if (items.Count == 0)
        {
            builder.Append("[]");
            return;
        }

        builder.Append('[');
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            NewLine(builder, depth + 1, indented);
            Write(builder, items[i], depth + 1, indented, sortKeys);
        }

        NewLine(builder, depth, indented);
        builder.Append(']');
    }

    private static void NewLine(StringBuilder builder, int depth, bool indented)
    {
        if (indented)
        {
            builder.Append('\n').Append(' ', depth);
        }
    }

    /// <summary>Writes a JSON string, escaping only what JSON requires, as Python does with <c>ensure_ascii=False</c>.</summary>
    private static void WriteString(StringBuilder builder, string text)
    {
        builder.Append('"');
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var escape = Escape(text, i);
            if (escape is null)
            {
                continue;
            }

            builder.Append(text, start, i - start).Append(escape);
            start = i + 1;
        }

        builder.Append(text, start, text.Length - start).Append('"');
    }

    private static string? Escape(string text, int index)
    {
        var c = text[index];
        return c switch
        {
            '"' => "\\\"",
            '\\' => "\\\\",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            '\b' => "\\b",
            '\f' => "\\f",
            _ when c < ' ' || (char.IsSurrogate(c) && !IsPaired(text, index)) =>
                "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    /// <summary>Whether a surrogate character is half of a valid pair; a lone half is escaped, as JavaScript does.</summary>
    private static bool IsPaired(string text, int index) => char.IsHighSurrogate(text[index])
        ? index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
        : index > 0 && char.IsHighSurrogate(text[index - 1]);
}
