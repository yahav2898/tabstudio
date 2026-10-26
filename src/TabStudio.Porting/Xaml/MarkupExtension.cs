using System.Buffers;
using System.Text;

namespace TabStudio.Porting.Xaml;

/// <summary>A value inside a markup extension: plain text, or another markup extension.</summary>
internal sealed class MarkupValue
{
    /// <summary>Characters that make text need quotes inside a markup extension.</summary>
    private static readonly SearchValues<char> Special = SearchValues.Create("{},'\"=");

    /// <summary>Creates a text value.</summary>
    public MarkupValue(string text) => Text = text;

    /// <summary>Creates a nested markup extension.</summary>
    public MarkupValue(MarkupExtension extension)
    {
        Extension = extension;
        Text = string.Empty;
    }

    /// <summary>The text, when the value is not a markup extension.</summary>
    public string Text { get; set; }

    /// <summary>The nested markup extension, such as <c>{StaticResource Brush}</c>.</summary>
    public MarkupExtension? Extension { get; set; }

    /// <inheritdoc />
    public override string ToString() => Extension?.ToString() ?? Quote(Text);

    /// <summary>Text as it can stand inside a markup extension: quoted when it holds braces, commas or quotes.</summary>
    private static string Quote(string text)
    {
        if (text.StartsWith("{}", StringComparison.Ordinal) || !text.AsSpan().ContainsAny(Special))
        {
            return text;
        }

        return "'" + text.Replace("'", "\\'", StringComparison.Ordinal) + "'";
    }
}

/// <summary>One argument of a markup extension: positional, or named (<c>Mode=TwoWay</c>).</summary>
/// <param name="Key">The name, or <see langword="null"/> for a positional argument.</param>
/// <param name="Value">The value.</param>
internal sealed record MarkupArgument(string? Key, MarkupValue Value)
{
    /// <summary>The value, which may be replaced.</summary>
    public MarkupValue Value { get; set; } = Value;
}

/// <summary>
/// A markup extension, such as <c>{Binding Name, Mode=TwoWay}</c>, parsed so its arguments can be changed and
/// written back. Both WPF and Avalonia use this syntax.
/// </summary>
internal sealed class MarkupExtension
{
    private MarkupExtension(string name) => Name = name;

    /// <summary>The extension, such as <c>Binding</c> or <c>x:Static</c>.</summary>
    public string Name { get; set; }

    /// <summary>The arguments in their order.</summary>
    public List<MarkupArgument> Arguments { get; } = [];

    /// <summary>Whether an attribute value is a markup extension rather than text (<c>{}</c> escapes braces).</summary>
    /// <param name="value">The attribute value.</param>
    public static bool IsExtension(string value) =>
        value.Length > 1 && value[0] == '{' && value[1] != '}';

    /// <summary>Parses a markup extension.</summary>
    /// <param name="value">The attribute value, such as <c>{Binding Name}</c>.</param>
    /// <returns>The extension, or <see langword="null"/> when the value is text or malformed.</returns>
    public static MarkupExtension? Parse(string value)
    {
        if (!IsExtension(value))
        {
            return null;
        }

        var position = 0;
        var extension = ParseExtension(value, ref position);
        return extension is not null && value.AsSpan(position).Trim().IsEmpty ? extension : null;
    }

    /// <summary>The argument with a name, or <see langword="null"/>.</summary>
    /// <param name="key">The name, such as <c>Mode</c>.</param>
    public MarkupArgument? Find(string key) =>
        Arguments.Find(a => string.Equals(a.Key, key, StringComparison.Ordinal));

    /// <summary>The text of a named argument, or <see langword="null"/>.</summary>
    /// <param name="key">The name.</param>
    public string? GetText(string key) => Find(key) is { Value.Extension: null } argument ? argument.Value.Text : null;

    /// <summary>The value of a positional argument, or <see langword="null"/>.</summary>
    /// <param name="index">The 0-based position among the positional arguments.</param>
    public MarkupValue? Positional(int index)
    {
        var positional = Arguments.Where(a => a.Key is null).ToList();
        return index < positional.Count ? positional[index].Value : null;
    }

    /// <summary>Sets a named argument, adding it at the end when it is missing.</summary>
    /// <param name="key">The name.</param>
    /// <param name="value">The new value.</param>
    public void Set(string key, MarkupValue value)
    {
        if (Find(key) is { } argument)
        {
            argument.Value = value;
        }
        else
        {
            Arguments.Add(new MarkupArgument(key, value));
        }
    }

    /// <summary>Removes a named argument.</summary>
    /// <param name="key">The name.</param>
    /// <returns>Whether it was there.</returns>
    public bool Remove(string key) => Arguments.RemoveAll(a => string.Equals(a.Key, key, StringComparison.Ordinal)) > 0;

    /// <summary>The extension as XAML.</summary>
    public override string ToString()
    {
        var text = new StringBuilder("{").Append(Name);
        for (var i = 0; i < Arguments.Count; i++)
        {
            text.Append(i == 0 ? " " : ", ");
            if (Arguments[i].Key is { } key)
            {
                text.Append(key).Append('=');
            }

            text.Append(Arguments[i].Value);
        }

        return text.Append('}').ToString();
    }

    private static MarkupExtension? ParseExtension(string text, ref int position)
    {
        SkipSpaces(text, ref position);
        if (position >= text.Length || text[position] != '{')
        {
            return null;
        }

        position++;
        SkipSpaces(text, ref position);
        var start = position;
        while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] is not ('}' or ','))
        {
            position++;
        }

        if (position == start)
        {
            return null;
        }

        var extension = new MarkupExtension(text[start..position]);
        while (true)
        {
            SkipSpaces(text, ref position);
            if (position >= text.Length)
            {
                return null;
            }

            if (text[position] == '}')
            {
                position++;
                return extension;
            }

            if (text[position] == ',')
            {
                position++;
                continue;
            }

            if (ParseArgument(text, ref position) is not { } argument)
            {
                return null;
            }

            extension.Arguments.Add(argument);
        }
    }

    private static MarkupArgument? ParseArgument(string text, ref int position)
    {
        // A key is a name followed by '='; anything else is a positional value.
        var start = position;
        while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] is '.' or ':' or '_'))
        {
            position++;
        }

        var keyEnd = position;
        SkipSpaces(text, ref position);
        string? key = null;
        if (keyEnd > start && position < text.Length && text[position] == '=')
        {
            key = text[start..keyEnd];
            position++;
        }
        else
        {
            position = start;
        }

        return ParseValue(text, ref position) is { } value ? new MarkupArgument(key, value) : null;
    }

    private static MarkupValue? ParseValue(string text, ref int position)
    {
        SkipSpaces(text, ref position);
        if (position >= text.Length)
        {
            return null;
        }

        var first = text[position];
        if (first == '{' && (position + 1 >= text.Length || text[position + 1] != '}'))
        {
            return ParseExtension(text, ref position) is { } nested ? new MarkupValue(nested) : null;
        }

        if (first is '\'' or '"')
        {
            var quoted = new StringBuilder();
            position++;
            while (position < text.Length && text[position] != first)
            {
                if (text[position] == '\\' && position + 1 < text.Length)
                {
                    position++;
                }

                quoted.Append(text[position]);
                position++;
            }

            position++;
            return new MarkupValue(quoted.ToString());
        }

        // Plain text runs to a comma or the closing brace; braces inside it, as in StringFormat={}{0:N2}, nest.
        var value = new StringBuilder();
        var depth = 0;
        while (position < text.Length)
        {
            var c = text[position];
            if (depth == 0 && c is ',' or '}')
            {
                break;
            }

            if (c == '\\' && position + 1 < text.Length)
            {
                value.Append(text[position + 1]);
                position += 2;
                continue;
            }

            depth += c switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0,
            };
            value.Append(c);
            position++;
        }

        return new MarkupValue(value.ToString().Trim());
    }

    private static void SkipSpaces(string text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }
    }
}
