namespace TabStudio.Toolchains.Terminals;

/// <summary>How a <see cref="TerminalColor"/> names its color.</summary>
public enum TerminalColorKind : byte
{
    /// <summary>The terminal's default foreground or background.</summary>
    Default,

    /// <summary>One of the 256 indexed colors: the 16 ANSI colors, the 6×6×6 cube and the grays.</summary>
    Indexed,

    /// <summary>A 24-bit color.</summary>
    Rgb,
}

/// <summary>A cell's foreground or background color, as a program set it with SGR.</summary>
/// <param name="Kind">How the color is named.</param>
/// <param name="Value">The index for <see cref="TerminalColorKind.Indexed"/>, or <c>0xRRGGBB</c> for <see cref="TerminalColorKind.Rgb"/>.</param>
public readonly record struct TerminalColor(TerminalColorKind Kind, uint Value)
{
    /// <summary>The default color.</summary>
    public static TerminalColor Default => default;

    /// <summary>An indexed color.</summary>
    /// <param name="index">0 to 255.</param>
    public static TerminalColor Indexed(int index) => new(TerminalColorKind.Indexed, (uint)Math.Clamp(index, 0, 255));

    /// <summary>A 24-bit color.</summary>
    public static TerminalColor FromRgb(int red, int green, int blue) =>
        new(TerminalColorKind.Rgb, (uint)((Math.Clamp(red, 0, 255) << 16) | (Math.Clamp(green, 0, 255) << 8) | Math.Clamp(blue, 0, 255)));

    /// <summary>Whether it is the default color.</summary>
    public bool IsDefault => Kind == TerminalColorKind.Default;

    /// <summary>The color as <c>0xRRGGBB</c>, with indexed colors from xterm's palette; <paramref name="fallback"/> for the default.</summary>
    /// <param name="fallback">The default color, as <c>0xRRGGBB</c>.</param>
    public uint ToRgb(uint fallback) => Kind switch
    {
        TerminalColorKind.Rgb => Value,
        TerminalColorKind.Indexed => Palette(Value),
        _ => fallback,
    };

    /// <summary>xterm's 256-color palette, with softer ANSI colors that read well on a dark background.</summary>
    public static uint Palette(uint index)
    {
        if (index < 16)
        {
            return AnsiColors[index];
        }

        if (index < 232)
        {
            var cube = index - 16;
            static uint Level(uint value) => value == 0 ? 0u : 55u + (value * 40u);
            return (Level(cube / 36) << 16) | (Level(cube / 6 % 6) << 8) | Level(cube % 6);
        }

        var gray = 8u + ((Math.Min(index, 255u) - 232u) * 10u);
        return (gray << 16) | (gray << 8) | gray;
    }

    private static readonly uint[] AnsiColors =
    [
        0x1B1D23, 0xF47067, 0x6FD6A0, 0xE3B341, 0x6CB6FF, 0xDCBDFB, 0x56D4DD, 0xC5CAD3,
        0x6E7685, 0xFF938A, 0x8DDB8C, 0xF5D06F, 0x96D0FF, 0xE8D3FF, 0x8BE9F0, 0xF0F3F6,
    ];
}

/// <summary>How a cell's text is drawn, besides its colors.</summary>
[Flags]
public enum TerminalAttributes : ushort
{
    /// <summary>Plain text.</summary>
    None = 0,

    /// <summary>Bold (SGR 1).</summary>
    Bold = 1,

    /// <summary>Faint (SGR 2).</summary>
    Dim = 2,

    /// <summary>Italic (SGR 3).</summary>
    Italic = 4,

    /// <summary>Underlined (SGR 4).</summary>
    Underline = 8,

    /// <summary>Blinking (SGR 5), drawn steady.</summary>
    Blink = 16,

    /// <summary>Foreground and background swapped (SGR 7).</summary>
    Inverse = 32,

    /// <summary>Invisible (SGR 8).</summary>
    Hidden = 64,

    /// <summary>Struck through (SGR 9).</summary>
    Strikethrough = 128,
}

/// <summary>A cell's colors and text flags.</summary>
/// <param name="Foreground">The text color.</param>
/// <param name="Background">The background color.</param>
/// <param name="Attributes">Bold, underline and the rest.</param>
public readonly record struct TerminalStyle(TerminalColor Foreground, TerminalColor Background, TerminalAttributes Attributes)
{
    /// <summary>Default colors, plain text.</summary>
    public static TerminalStyle Default => default;

    /// <summary>Whether a flag is set.</summary>
    /// <param name="flag">The flag.</param>
    public bool Has(TerminalAttributes flag) => (Attributes & flag) == flag;
}
