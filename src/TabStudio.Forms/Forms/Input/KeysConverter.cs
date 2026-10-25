using System.ComponentModel;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>Converts key values to and from text such as <c>Ctrl+S</c>, as menus show shortcuts.</summary>
public class KeysConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text)
        {
            return base.ConvertFrom(context, culture, value);
        }

        var keys = Keys.None;
        foreach (var part in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            keys |= part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => Keys.Control,
                "SHIFT" => Keys.Shift,
                "ALT" => Keys.Alt,
                "DEL" => Keys.Delete,
                "INS" => Keys.Insert,
                "ESC" => Keys.Escape,
                "PGUP" => Keys.PageUp,
                "PGDN" => Keys.PageDown,
                _ when part.Length == 1 && char.IsDigit(part[0]) => Keys.D0 + (part[0] - '0'),
                _ => Enum.Parse<Keys>(part, ignoreCase: true),
            };
        }

        return keys;
    }

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType != typeof(string) || value is not Keys keys)
        {
            return base.ConvertTo(context, culture, value, destinationType);
        }

        return ToText(keys);
    }

    /// <summary>A key value as menus show it, such as <c>Ctrl+Shift+S</c>.</summary>
    /// <param name="keys">The key value.</param>
    internal static string ToText(Keys keys)
    {
        var parts = new List<string>(4);
        if ((keys & Keys.Control) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((keys & Keys.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((keys & Keys.Alt) != 0)
        {
            parts.Add("Alt");
        }

        var code = keys & Keys.KeyCode;
        if (code != Keys.None)
        {
            parts.Add(code switch
            {
                >= Keys.D0 and <= Keys.D9 => new string((char)('0' + (code - Keys.D0)), 1),
                Keys.Delete => "Del",
                Keys.Insert => "Ins",
                Keys.Escape => "Esc",
                Keys.Prior => "PgUp",
                Keys.Next => "PgDn",
                Keys.Oemplus => "+",
                Keys.OemMinus => "-",
                Keys.Oemcomma => ",",
                Keys.OemPeriod => ".",
                _ => code.ToString(),
            });
        }

        return string.Join("+", parts);
    }
}
