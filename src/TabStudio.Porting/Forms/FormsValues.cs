using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.Porting.Forms;

/// <summary>A font as the designer writes one: <c>new Font("Segoe UI", 12F, FontStyle.Bold)</c>.</summary>
/// <param name="Family">The family.</param>
/// <param name="Points">The size in points.</param>
/// <param name="Styles">Bold, Italic, Underline or Strikeout.</param>
internal sealed record FormsFont(string Family, double Points, IReadOnlyList<string> Styles);

/// <summary>Reads the values the Windows Forms designer writes: numbers, text, points, sizes, colors, fonts and enums.</summary>
internal static class FormsValues
{
    /// <summary>The font of Windows Forms, which Avalonia's default font stands in for.</summary>
    public const string DefaultFamily = "Segoe UI";

    /// <summary>The size of Windows Forms' default font, in points.</summary>
    public const double DefaultPoints = 9;

    /// <summary>A number: <c>12</c>, <c>12F</c>, <c>-3</c>, or <c>new decimal(new int[] { 120, 0, 0, 0 })</c>.</summary>
    public static double? Number(ExpressionSyntax? expression) => expression switch
    {
        LiteralExpressionSyntax { Token.Value: var value } when value is int or long or float or double or decimal => System.Convert.ToDouble(value, CultureInfo.InvariantCulture),
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression, Operand: var operand } => -Number(operand),
        CastExpressionSyntax cast => Number(cast.Expression),
        ParenthesizedExpressionSyntax parenthesized => Number(parenthesized.Expression),
        ObjectCreationExpressionSyntax { Type: var type, ArgumentList.Arguments: [{ Expression: var bits }] } when type.ToString() is "decimal" or "System.Decimal" => DecimalBits(bits) ?? Number(bits),
        _ => null,
    };

    /// <summary>A string literal, or <see langword="null"/>.</summary>
    public static string? String(ExpressionSyntax? expression) => expression switch
    {
        LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression, Token.ValueText: var text } => text,
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression, Left: var left, Right: var right } when String(left) is { } a && String(right) is { } b => a + b,
        _ => null,
    };

    /// <summary>A boolean literal, or <see langword="null"/>.</summary>
    public static bool? Boolean(ExpressionSyntax? expression) => expression switch
    {
        LiteralExpressionSyntax { RawKind: (int)SyntaxKind.TrueLiteralExpression } => true,
        LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression } => false,
        _ => null,
    };

    /// <summary>Two numbers of <c>new Point(x, y)</c>, <c>new Size(w, h)</c> or <c>new SizeF(w, h)</c>.</summary>
    public static (double A, double B)? Pair(ExpressionSyntax? expression) =>
        expression is BaseObjectCreationExpressionSyntax { ArgumentList.Arguments: [{ Expression: var a }, { Expression: var b }] }
        && Number(a) is { } first && Number(b) is { } second
            ? (first, second)
            : null;

    /// <summary>
    /// A Padding as a XAML thickness: <c>new Padding(3)</c> is <c>3</c>, <c>new Padding(1, 2, 3, 4)</c> is
    /// <c>1,2,3,4</c>.
    /// </summary>
    public static string? Thickness(ExpressionSyntax? expression)
    {
        if (expression is not BaseObjectCreationExpressionSyntax { ArgumentList.Arguments: var arguments })
        {
            return null;
        }

        var values = arguments.Select(a => Number(a.Expression)).ToList();
        if (values.Count is not (1 or 4) || values.Any(v => v is null))
        {
            return null;
        }

        return string.Join(',', values.Select(v => v!.Value.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>The member of an enum value, such as <c>Fill</c> of <c>DockStyle.Fill</c>.</summary>
    public static string? EnumMember(ExpressionSyntax? expression) => expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        ParenthesizedExpressionSyntax parenthesized => EnumMember(parenthesized.Expression),
        _ => null,
    };

    /// <summary>The members of a flags value, such as Top and Left of <c>AnchorStyles.Top | AnchorStyles.Left</c>.</summary>
    public static HashSet<string> Flags(ExpressionSyntax? expression)
    {
        var flags = new HashSet<string>(StringComparer.Ordinal);
        Collect(expression);
        return flags;

        void Collect(ExpressionSyntax? part)
        {
            switch (part)
            {
                case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseOrExpression } or:
                    Collect(or.Left);
                    Collect(or.Right);
                    break;
                case ParenthesizedExpressionSyntax parenthesized:
                    Collect(parenthesized.Expression);
                    break;
                case CastExpressionSyntax cast:
                    Collect(cast.Expression);
                    break;
                case MemberAccessExpressionSyntax access:
                    flags.Add(access.Name.Identifier.ValueText);
                    break;
            }
        }
    }

    /// <summary>
    /// A color as XAML writes it: a known color's name, or <c>#AARRGGBB</c>; <see langword="null"/> for the system's
    /// colors, which Avalonia's theme provides.
    /// </summary>
    public static string? Color(ExpressionSyntax? expression)
    {
        switch (expression)
        {
            case MemberAccessExpressionSyntax { Expression: var system } when system.ToString().EndsWith("SystemColors", StringComparison.Ordinal):
                return null;
            case MemberAccessExpressionSyntax { Expression: var owner, Name.Identifier.ValueText: var name } when owner.ToString().EndsWith("Color", StringComparison.Ordinal):
                return name == "Transparent" || name.All(char.IsLetter) ? name : null;
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "FromArgb" }, ArgumentList.Arguments: var arguments }:
                var values = arguments.Select(a => Number(a.Expression)).ToList();
                if (values.Any(v => v is null))
                {
                    return null;
                }

                var bytes = values.Select(v => (int)v!.Value).ToList();
                return bytes.Count switch
                {
                    3 => string.Create(CultureInfo.InvariantCulture, $"#FF{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}"),
                    4 => string.Create(CultureInfo.InvariantCulture, $"#{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}"),
                    1 => string.Create(CultureInfo.InvariantCulture, $"#{bytes[0]:X8}"),
                    _ => null,
                };
            default:
                return null;
        }
    }

    /// <summary>A font: <c>new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point)</c>.</summary>
    public static FormsFont? Font(ExpressionSyntax? expression)
    {
        if (expression is not BaseObjectCreationExpressionSyntax { ArgumentList.Arguments: var arguments } || arguments.Count < 2
            || String(arguments[0].Expression) is not { } family || Number(arguments[1].Expression) is not { } size)
        {
            return null;
        }

        var styles = arguments.Count > 2 ? Flags(arguments[2].Expression).Where(s => s is "Bold" or "Italic" or "Underline" or "Strikeout").ToList() : [];
        var unit = arguments.Skip(2).Select(a => EnumMember(a.Expression)).FirstOrDefault(m => m is "Pixel" or "Point" or "World");
        return new FormsFont(family, unit == "Pixel" ? size * 72 / 96 : size, styles);
    }

    /// <summary>A shortcut, such as <c>Keys.Control | Keys.S</c>, as an Avalonia key gesture: <c>Ctrl+S</c>.</summary>
    public static string? KeyGesture(ExpressionSyntax? expression)
    {
        var keys = Flags(expression);
        var modifiers = new List<string>();
        foreach (var (key, gesture) in (ReadOnlySpan<(string, string)>)[("Control", "Ctrl"), ("Shift", "Shift"), ("Alt", "Alt")])
        {
            if (keys.Remove(key))
            {
                modifiers.Add(gesture);
            }
        }

        if (keys.Count != 1)
        {
            return null;
        }

        var name = keys.Single() switch
        {
            "Return" => "Enter",
            "Next" => "PageDown",
            "Prior" => "PageUp",
            "Oemplus" => "OemPlus",
            var other => other,
        };

        return string.Join('+', modifiers.Append(name));
    }

    /// <summary>A WinForms text with its mnemonic (<c>&amp;File</c>) as Avalonia's access key (<c>_File</c>).</summary>
    public static string AccessKeyText(string text)
    {
        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '&' && i + 1 < text.Length)
            {
                if (text[i + 1] == '&')
                {
                    result.Append('&');
                    i++;
                }
                else
                {
                    result.Append('_');
                }

                continue;
            }

            if (text[i] == '_')
            {
                result.Append("__");
            }
            else
            {
                result.Append(text[i]);
            }
        }

        return result.ToString();
    }

    /// <summary>The value of <c>new decimal(new int[] { lo, mid, hi, flags })</c>, as the designer writes NumericUpDown values.</summary>
    private static double? DecimalBits(ExpressionSyntax bits)
    {
        var parts = bits switch
        {
            ArrayCreationExpressionSyntax { Initializer.Expressions: var expressions } => expressions.Select(Number).ToList(),
            ImplicitArrayCreationExpressionSyntax { Initializer.Expressions: var expressions } => expressions.Select(Number).ToList(),
            _ => [],
        };

        if (parts.Count != 4 || parts.Any(p => p is null))
        {
            return null;
        }

        var value = new decimal((int)parts[0]!.Value, (int)parts[1]!.Value, (int)parts[2]!.Value, ((int)parts[3]!.Value & int.MinValue) != 0, (byte)(((int)parts[3]!.Value >> 16) & 0xFF));
        return (double)value;
    }
}
