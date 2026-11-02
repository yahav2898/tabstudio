using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// Writes property values as Visual Basic, as Visual Studio's designer writes them: <c>New Point(12, 12)</c>,
/// <c>AnchorStyles.Top Or AnchorStyles.Left</c>, <c>Color.FromArgb(255, 128, 0)</c>, <c>New SizeF(8F, 20F)</c> and
/// <c>True</c>, with the file's style of type names.
/// </summary>
/// <remarks>
/// A file written with <c>Me.</c> is one older Visual Studio wrote, whose values it also wrote its own way, which the
/// designer keeps: <c>New System.Drawing.SizeF(6.0!, 13.0!)</c>,
/// <c>Color.FromArgb(CType(CType(0, Byte), Integer), ...)</c> and
/// <c>CType((AnchorStyles.Top Or AnchorStyles.Left), AnchorStyles)</c>.
/// </remarks>
internal static class VisualBasicValueWriter
{
    /// <summary>The function a string's control characters are written with, as Visual Studio writes them.</summary>
    private const string ChrW = "Global.Microsoft.VisualBasic.ChrW";

    /// <summary>A value as Visual Basic, or <see langword="null"/> for a value the designer cannot write.</summary>
    /// <param name="value">The value.</param>
    /// <param name="style">How the file writes code.</param>
    public static string? Write(object? value, CodeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        string Type(Type type) => TypeResolver.VisualBasicNameOf(type, style.FullTypeNames);
        var classic = style.QualifyWithThis;
        return value switch
        {
            null => "Nothing",
            string text => Literal(text),
            bool flag => flag ? "True" : "False",
            char character => CharacterLiteral(character),
            int number => Number(number),
            long number => Number(number) + "L",
            byte number => $"CType({Number(number)}, Byte)",
            short number => $"CType({Number(number)}, Short)",
            float number => SingleLiteral(number, classic),
            double number => DoubleLiteral(number, classic),
            decimal number => $"New Decimal(New Integer() {{{string.Join(", ", decimal.GetBits(number).Select(b => Number(b)))}}})",
            Point point => $"New {Type(typeof(Point))}({Number(point.X)}, {Number(point.Y)})",
            Size size => $"New {Type(typeof(Size))}({Number(size.Width)}, {Number(size.Height)})",
            SizeF size => $"New {Type(typeof(SizeF))}({SingleLiteral(size.Width, classic)}, {SingleLiteral(size.Height, classic)})",
            Padding padding => padding.All >= 0
                ? $"New {Type(typeof(Padding))}({Number(padding.All)})"
                : $"New {Type(typeof(Padding))}({Number(padding.Left)}, {Number(padding.Top)}, {Number(padding.Right)}, {Number(padding.Bottom)})",
            Color color => WriteColor(color, Type, classic),
            Font font => WriteFont(font, Type, classic),
            DateTime date => $"New Date({Number(date.Year)}, {Number(date.Month)}, {Number(date.Day)}, {Number(date.Hour)}, {Number(date.Minute)}, {Number(date.Second)}, {Number(date.Millisecond)})",
            Keys keys => WriteKeys(keys, Type, classic),
            Enum flags => WriteEnum(flags, Type, classic),
            _ => null,
        };
    }

    /// <summary>
    /// A string as a Visual Basic literal: <c>"Say ""hi"""</c>; line breaks and other control characters, which a
    /// Visual Basic string cannot hold, joined with <c>&amp;</c> as <c>Global.Microsoft.VisualBasic.ChrW(13)</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    public static string Literal(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = new List<string>();
        var quoted = new StringBuilder();
        foreach (var character in text)
        {
            if (NeedsChrW(character))
            {
                if (quoted.Length > 0)
                {
                    parts.Add(quoted.Append('"').ToString());
                    quoted.Clear();
                }

                parts.Add($"{ChrW}({Number(character)})");
                continue;
            }

            if (quoted.Length == 0)
            {
                quoted.Append('"');
            }

            if (character == '"')
            {
                quoted.Append("\"\"");
            }
            else
            {
                quoted.Append(character);
            }
        }

        if (quoted.Length > 0)
        {
            parts.Add(quoted.Append('"').ToString());
        }

        return parts.Count == 0 ? "\"\"" : string.Join(" & ", parts);
    }

    /// <summary>
    /// Whether a character is written with ChrW: a control character, and the typographic quotes Visual Basic reads as
    /// a string's quote.
    /// </summary>
    private static bool NeedsChrW(char character) => character < ' ' || character is '“' or '”' or '＂' or '\u0085' or '\u2028' or '\u2029';

    private static string CharacterLiteral(char character) => character switch
    {
        '"' => "\"\"\"\"c",
        _ when NeedsChrW(character) => $"{ChrW}({Number(character)})",
        _ => "\"" + character + "\"c",
    };

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A Single: <c>8F</c>, or <c>8.0!</c> as older Visual Studio writes it.</summary>
    private static string SingleLiteral(float value, bool classic)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return classic ? WithDecimalPoint(text) + "!" : text + "F";
    }

    /// <summary>A Double: <c>8R</c>, or <c>8.0R</c> as older Visual Studio writes it.</summary>
    private static string DoubleLiteral(double value, bool classic)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return (classic ? WithDecimalPoint(text) : text) + "R";
    }

    private static string WithDecimalPoint(string number) =>
        number.Contains('.', StringComparison.Ordinal) || number.Contains('E', StringComparison.Ordinal) ? number : number + ".0";

    private static string WriteColor(Color color, Func<Type, string> type, bool classic)
    {
        if (color.IsSystemColor)
        {
            return $"{type(typeof(SystemColors))}.{color.Name}";
        }

        if (color.IsNamedColor)
        {
            return $"{type(typeof(Color))}.{color.Name}";
        }

        string Part(byte value) => classic ? $"CType(CType({Number(value)}, Byte), Integer)" : Number(value);
        return color.A == 255
            ? $"{type(typeof(Color))}.FromArgb({Part(color.R)}, {Part(color.G)}, {Part(color.B)})"
            : $"{type(typeof(Color))}.FromArgb({Part(color.A)}, {Part(color.R)}, {Part(color.G)}, {Part(color.B)})";
    }

    private static string WriteFont(Font font, Func<Type, string> type, bool classic)
    {
        var style = WriteEnum(font.Style, type, classic);
        var charSet = classic ? $"CType({Number(font.GdiCharSet)}, Byte)" : Number(font.GdiCharSet);
        return $"New {type(typeof(Font))}({Literal(font.Name)}, {SingleLiteral(font.Size, classic)}, {style}, {type(typeof(GraphicsUnit))}.{font.Unit}, {charSet})";
    }

    /// <summary>A shortcut: its modifiers, then its key, as in <c>Keys.Control Or Keys.S</c>.</summary>
    private static string WriteKeys(Keys keys, Func<Type, string> type, bool classic)
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

        return Combine(parts, name, classic);
    }

    /// <summary>An enum value: one name, or flags joined with <c>Or</c> from the lowest, as Visual Studio writes them.</summary>
    private static string WriteEnum(Enum value, Func<Type, string> type, bool classic)
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
                return Combine([.. parts.Select(v => $"{name}.{v.Value}")], name, classic);
            }
        }

        return $"CType({Number(Convert.ToInt64(value, CultureInfo.InvariantCulture))}, {name})";
    }

    /// <summary>
    /// Flags joined with <c>Or</c>: <c>AnchorStyles.Top Or AnchorStyles.Left</c>, or as older Visual Studio wrote them,
    /// <c>CType((AnchorStyles.Top Or AnchorStyles.Left), AnchorStyles)</c>.
    /// </summary>
    private static string Combine(List<string> parts, string typeName, bool classic)
    {
        if (!classic || parts.Count < 2)
        {
            return string.Join(" Or ", parts);
        }

        var combined = parts[0];
        foreach (var part in parts.Skip(1))
        {
            combined = $"({combined} Or {part})";
        }

        return $"CType({combined}, {typeName})";
    }
}
