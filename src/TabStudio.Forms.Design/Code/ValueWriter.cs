using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// Writes property values as C#, as Visual Studio's designer writes them: <c>new Point(12, 12)</c>,
/// <c>AnchorStyles.Top | AnchorStyles.Left</c>, <c>Color.FromArgb(255, 128, 0)</c>,
/// <c>new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0)</c>, with the file's style of type names.
/// </summary>
internal static class ValueWriter
{
    /// <summary>A value as C#, or <see langword="null"/> for a value the designer cannot write.</summary>
    /// <param name="value">The value.</param>
    /// <param name="style">How the file writes code.</param>
    public static string? Write(object? value, CodeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        string Type(Type type) => TypeResolver.NameOf(type, style.FullTypeNames);
        return value switch
        {
            null => "null",
            string text => Literal(text),
            bool flag => flag ? "true" : "false",
            char character => SymbolDisplay(character),
            int number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture) + "L",
            byte number => number.ToString(CultureInfo.InvariantCulture),
            short number => number.ToString(CultureInfo.InvariantCulture),
            float number => Float(number) + "F",
            double number => Float(number) + "D",
            decimal number => $"new decimal(new int[] {{ {string.Join(", ", decimal.GetBits(number).Select(b => b.ToString(CultureInfo.InvariantCulture)))} }})",
            Point point => $"new {Type(typeof(Point))}({point.X.ToString(CultureInfo.InvariantCulture)}, {point.Y.ToString(CultureInfo.InvariantCulture)})",
            Size size => $"new {Type(typeof(Size))}({size.Width.ToString(CultureInfo.InvariantCulture)}, {size.Height.ToString(CultureInfo.InvariantCulture)})",
            SizeF size => $"new {Type(typeof(SizeF))}({Float(size.Width)}F, {Float(size.Height)}F)",
            Padding padding => padding.All >= 0
                ? $"new {Type(typeof(Padding))}({padding.All.ToString(CultureInfo.InvariantCulture)})"
                : $"new {Type(typeof(Padding))}({padding.Left.ToString(CultureInfo.InvariantCulture)}, {padding.Top.ToString(CultureInfo.InvariantCulture)}, {padding.Right.ToString(CultureInfo.InvariantCulture)}, {padding.Bottom.ToString(CultureInfo.InvariantCulture)})",
            Color color => WriteColor(color, Type),
            Font font => WriteFont(font, Type),
            DateTime date => $"new {Type(typeof(DateTime))}({date.Year}, {date.Month}, {date.Day}, {date.Hour}, {date.Minute}, {date.Second}, {date.Millisecond})",
            Keys keys => WriteKeys(keys, Type),
            Enum flags => WriteEnum(flags, Type),
            _ => null,
        };
    }

    /// <summary>A string as a C# literal.</summary>
    /// <param name="text">The text.</param>
    public static string Literal(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder("\"", text.Length + 2);
        foreach (var character in text)
        {
            builder.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\0' => "\\0",
                // C# reads U+0085, U+2028 and U+2029 as line breaks, which a string literal cannot contain: text
                // pasted from a web page or Word broke the build.
                < ' ' or '\u0085' or '\u2028' or '\u2029' => $"\\u{(int)character:X4}",
                _ => character.ToString(),
            });
        }

        return builder.Append('"').ToString();
    }

    private static string SymbolDisplay(char character) => character switch
    {
        '\'' => "'\\''",
        '\\' => "'\\\\'",
        '\0' => "'\\0'",
        < ' ' => $"'\\u{(int)character:X4}'",
        _ => $"'{character}'",
    };

    private static string Float(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    // A float's own shortest text: 9.9F, as Visual Studio writes it, not the widened 9.899999618530273F.
    private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string WriteColor(Color color, Func<Type, string> type)
    {
        if (color.IsSystemColor)
        {
            return $"{type(typeof(SystemColors))}.{color.Name}";
        }

        if (color.IsNamedColor)
        {
            return $"{type(typeof(Color))}.{color.Name}";
        }

        return color.A == 255
            ? $"{type(typeof(Color))}.FromArgb({color.R}, {color.G}, {color.B})"
            : $"{type(typeof(Color))}.FromArgb({color.A}, {color.R}, {color.G}, {color.B})";
    }

    private static string WriteFont(Font font, Func<Type, string> type)
    {
        var style = WriteEnum(font.Style, type);
        return $"new {type(typeof(Font))}({Literal(font.Name)}, {Float(font.Size)}F, {style}, {type(typeof(GraphicsUnit))}.{font.Unit}, {font.GdiCharSet.ToString(CultureInfo.InvariantCulture)})";
    }

    /// <summary>A shortcut: its modifiers, then its key, as in <c>Keys.Control | Keys.S</c>.</summary>
    private static string WriteKeys(Keys keys, Func<Type, string> type)
    {
        var name = type(typeof(Keys));
        var parts = new List<string>();
        foreach (var modifier in (ReadOnlySpan<Keys>)[Keys.Control, Keys.Shift, Keys.Alt])
        {
            if ((keys & modifier) == modifier)
            {
                parts.Add($"{name}.{modifier}");
            }
        }

        var code = keys & Keys.KeyCode;
        if (code != Keys.None || parts.Count == 0)
        {
            parts.Add($"{name}.{code}");
        }

        return string.Join(" | ", parts);
    }

    /// <summary>An enum value: one name, or flags joined with <c>|</c> from the lowest, as Visual Studio writes them.</summary>
    private static string WriteEnum(Enum value, Func<Type, string> type)
    {
        var enumType = value.GetType();
        var name = type(enumType);
        if (Enum.IsDefined(enumType, value))
        {
            return $"{name}.{value}";
        }

        if (enumType.IsDefined(typeof(FlagsAttribute), inherit: false))
        {
            var bits = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            var parts = Enum.GetValues(enumType).Cast<Enum>()
                .Select(v => (Value: v, Bits: Convert.ToInt64(v, CultureInfo.InvariantCulture)))
                .Where(v => v.Bits != 0 && (v.Bits & (v.Bits - 1)) == 0 && (bits & v.Bits) == v.Bits)
                .OrderBy(v => v.Bits)
                .ToList();
            if (parts.Count > 0 && parts.Aggregate(0L, (all, v) => all | v.Bits) == bits)
            {
                return string.Join(" | ", parts.Select(v => $"{name}.{v.Value}"));
            }
        }

        return $"({name}){Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)}";
    }
}
