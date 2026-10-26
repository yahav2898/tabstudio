namespace TabStudio.Compilation.Wpf.Xaml;

/// <summary>An argument of a markup extension: <c>Path=Name</c>, or a positional value such as <c>Name</c>.</summary>
/// <param name="Name">The argument's name, or <see langword="null"/> for a positional one.</param>
/// <param name="NameStart">The offset of its name in the attribute value, or of its value for a positional one.</param>
/// <param name="Value">Its value as written, trimmed; a nested extension's text for one such as <c>{StaticResource Key}</c>.</param>
/// <param name="ValueStart">The offset of its value in the attribute value.</param>
public sealed record XamlExtensionArgument(string? Name, int NameStart, string Value, int ValueStart);

/// <summary>
/// A markup extension in an attribute value, such as <c>{Binding Path=Name, Mode=TwoWay}</c>, with the offsets of its
/// parts in the value, read the tolerant way: an extension being typed, without its closing brace, still has its name
/// and arguments.
/// </summary>
/// <param name="Prefix">The prefix of its name, such as <c>x</c> in <c>{x:Static}</c>; empty when there is none.</param>
/// <param name="Name">Its name, such as <c>Binding</c> or <c>StaticResource</c>.</param>
/// <param name="Start">The offset of its <c>{</c> in the value.</param>
/// <param name="NameStart">The offset of its name.</param>
/// <param name="End">The offset just past its <c>}</c>, or the value's length when it is not closed.</param>
/// <param name="Arguments">Its arguments, in order.</param>
public sealed record XamlMarkupExtension(string Prefix, string Name, int Start, int NameStart, int End, IReadOnlyList<XamlExtensionArgument> Arguments)
{
    /// <summary>The name as written, such as <c>x:Static</c>.</summary>
    public string QualifiedName => Prefix.Length == 0 ? Name : $"{Prefix}:{Name}";

    /// <summary>The offset just past its name.</summary>
    public int NameEnd => NameStart + QualifiedName.Length;

    /// <summary>Whether an attribute value is a markup extension rather than text: it starts with <c>{</c>, not <c>{}</c>.</summary>
    /// <param name="value">The value.</param>
    public static bool IsExtension(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var trimmed = value.TrimStart();
        return trimmed.StartsWith('{') && !trimmed.StartsWith("{}", StringComparison.Ordinal);
    }

    /// <summary>Reads the extension that starts at an offset of a value.</summary>
    /// <param name="value">The attribute value.</param>
    /// <param name="start">The offset of the extension's <c>{</c>.</param>
    public static XamlMarkupExtension Parse(string value, int start)
    {
        ArgumentNullException.ThrowIfNull(value);
        var position = start + 1;
        while (position < value.Length && char.IsWhiteSpace(value[position]))
        {
            position++;
        }

        var nameStart = position;
        while (position < value.Length && XamlText.IsNameChar(value[position]) && value[position] != '.')
        {
            position++;
        }

        var qualified = value[nameStart..position];
        var colon = qualified.IndexOf(':', StringComparison.Ordinal);
        var prefix = colon < 0 ? string.Empty : qualified[..colon];
        var name = colon < 0 ? qualified : qualified[(colon + 1)..];

        var arguments = new List<XamlExtensionArgument>();
        while (position < value.Length && value[position] != '}')
        {
            while (position < value.Length && (char.IsWhiteSpace(value[position]) || value[position] == ','))
            {
                position++;
            }

            if (position >= value.Length || value[position] == '}')
            {
                break;
            }

            var argumentStart = position;
            var equals = -1;
            var depth = 0;
            while (position < value.Length)
            {
                var c = value[position];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    if (depth == 0)
                    {
                        break;
                    }

                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    break;
                }
                else if (c == '=' && depth == 0 && equals < 0)
                {
                    equals = position;
                }
                else if (c is '\'' or '"' && depth == 0)
                {
                    var close = value.IndexOf(c, position + 1);
                    position = close < 0 ? value.Length - 1 : close;
                }

                position++;
            }

            var text = value[argumentStart..position];
            if (equals < 0)
            {
                var leading = text.Length - text.TrimStart().Length;
                arguments.Add(new XamlExtensionArgument(null, argumentStart + leading, text.Trim(), argumentStart + leading));
            }
            else
            {
                var argumentName = value[argumentStart..equals].Trim();
                var valueStart = equals + 1;
                while (valueStart < position && char.IsWhiteSpace(value[valueStart]))
                {
                    valueStart++;
                }

                arguments.Add(new XamlExtensionArgument(argumentName, argumentStart, value[valueStart..position].Trim(), valueStart));
            }
        }

        var end = position < value.Length && value[position] == '}' ? position + 1 : value.Length;
        return new XamlMarkupExtension(prefix, name, start, nameStart, end, arguments);
    }

    /// <summary>The innermost extension of a value that an offset is in, such as the <c>{StaticResource}</c> in a converter.</summary>
    /// <param name="value">The attribute value.</param>
    /// <param name="offset">The offset in the value.</param>
    /// <returns>The extension, or <see langword="null"/> when the offset is in none.</returns>
    public static XamlMarkupExtension? At(string value, int offset)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsExtension(value))
        {
            return null;
        }

        var open = new Stack<int>();
        for (var i = 0; i < value.Length && i < offset; i++)
        {
            if (value[i] == '{')
            {
                open.Push(i);
            }
            else if (value[i] == '}' && open.Count > 0)
            {
                open.Pop();
            }
        }

        return open.Count > 0 ? Parse(value, open.Peek()) : null;
    }
}
