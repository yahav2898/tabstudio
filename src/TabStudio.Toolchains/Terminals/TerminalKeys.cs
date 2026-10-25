using System.Globalization;
using System.Text;

namespace TabStudio.Toolchains.Terminals;

/// <summary>A key that sends a control sequence rather than text.</summary>
public enum TerminalKey
{
    /// <summary>Enter.</summary>
    Enter,

    /// <summary>Backspace.</summary>
    Backspace,

    /// <summary>Tab.</summary>
    Tab,

    /// <summary>Escape.</summary>
    Escape,

    /// <summary>The up arrow.</summary>
    Up,

    /// <summary>The down arrow.</summary>
    Down,

    /// <summary>The right arrow.</summary>
    Right,

    /// <summary>The left arrow.</summary>
    Left,

    /// <summary>Home.</summary>
    Home,

    /// <summary>End.</summary>
    End,

    /// <summary>Insert.</summary>
    Insert,

    /// <summary>Delete (forward).</summary>
    Delete,

    /// <summary>Page Up.</summary>
    PageUp,

    /// <summary>Page Down.</summary>
    PageDown,

    /// <summary>F1.</summary>
    F1,

    /// <summary>F2.</summary>
    F2,

    /// <summary>F3.</summary>
    F3,

    /// <summary>F4.</summary>
    F4,

    /// <summary>F5.</summary>
    F5,

    /// <summary>F6.</summary>
    F6,

    /// <summary>F7.</summary>
    F7,

    /// <summary>F8.</summary>
    F8,

    /// <summary>F9.</summary>
    F9,

    /// <summary>F10.</summary>
    F10,

    /// <summary>F11.</summary>
    F11,

    /// <summary>F12.</summary>
    F12,
}

/// <summary>Modifier keys held with a key.</summary>
[Flags]
public enum TerminalModifiers
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Shift.</summary>
    Shift = 1,

    /// <summary>Alt (Meta).</summary>
    Alt = 2,

    /// <summary>Control.</summary>
    Control = 4,
}

/// <summary>What keys send to a program in a terminal, as xterm sends them.</summary>
public static class TerminalKeys
{
    /// <summary>The sequence a key sends.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held with it.</param>
    /// <param name="applicationCursorKeys">Whether the program asked for application cursor keys (<see cref="TerminalEmulator.ApplicationCursorKeys"/>).</param>
    public static string Encode(TerminalKey key, TerminalModifiers modifiers = TerminalModifiers.None, bool applicationCursorKeys = false)
    {
        var alt = modifiers.HasFlag(TerminalModifiers.Alt) ? "\u001b" : string.Empty;
        var parameter = 1 + (int)modifiers;
        switch (key)
        {
            case TerminalKey.Enter:
                return alt + "\r";
            case TerminalKey.Backspace:
                return alt + (modifiers.HasFlag(TerminalModifiers.Control) ? "\b" : "\u007f");
            case TerminalKey.Tab:
                return modifiers.HasFlag(TerminalModifiers.Shift) ? "\u001b[Z" : alt + "\t";
            case TerminalKey.Escape:
                return alt + "\u001b";
        }

        if (Cursor(key) is { } final)
        {
            return modifiers == TerminalModifiers.None
                ? (applicationCursorKeys ? "\u001bO" : "\u001b[") + final
                : string.Create(CultureInfo.InvariantCulture, $"\u001b[1;{parameter}{final}");
        }

        if (Tilde(key) is { } number)
        {
            return modifiers == TerminalModifiers.None
                ? string.Create(CultureInfo.InvariantCulture, $"\u001b[{number}~")
                : string.Create(CultureInfo.InvariantCulture, $"\u001b[{number};{parameter}~");
        }

        var function = key switch
        {
            TerminalKey.F1 => 'P',
            TerminalKey.F2 => 'Q',
            TerminalKey.F3 => 'R',
            _ => 'S',
        };
        return modifiers == TerminalModifiers.None
            ? "\u001bO" + function
            : string.Create(CultureInfo.InvariantCulture, $"\u001b[1;{parameter}{function}");
    }

    /// <summary>
    /// What typing a character sends: the character, or with Control the control character (Ctrl+C is 0x03),
    /// with Alt preceded by Escape.
    /// </summary>
    /// <param name="text">The character typed, or more, such as from a keyboard's text input.</param>
    /// <param name="modifiers">Control and Alt held with it; Shift is already in the text.</param>
    public static string EncodeText(string text, TerminalModifiers modifiers = TerminalModifiers.None)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return text;
        }

        if (modifiers.HasFlag(TerminalModifiers.Control) && text.Length == 1 && ControlCharacter(text[0]) is { } control)
        {
            text = control.ToString();
        }

        text = text.Replace("\r\n", "\r", StringComparison.Ordinal).Replace('\n', '\r');
        return modifiers.HasFlag(TerminalModifiers.Alt) ? "\u001b" + text : text;
    }

    /// <summary>
    /// What pasting text sends: line breaks as Enter, and with bracketed paste on, the markers that tell the shell
    /// it was pasted rather than typed, so it does not run each line.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="bracketed">Whether the program turned bracketed paste on.</param>
    public static string EncodePaste(string text, bool bracketed)
    {
        ArgumentNullException.ThrowIfNull(text);
        var body = new StringBuilder(text.Replace("\r\n", "\r", StringComparison.Ordinal).Replace('\n', '\r'));

        // The end marker inside the text would end the paste early.
        body.Replace("\u001b[201~", string.Empty);
        return bracketed ? "\u001b[200~" + body + "\u001b[201~" : body.ToString();
    }

    /// <summary>The control character Control and a key make, such as 0x03 for C.</summary>
    /// <param name="c">The key's character.</param>
    public static char? ControlCharacter(char c) => c switch
    {
        >= 'a' and <= 'z' => (char)(c - 'a' + 1),
        >= 'A' and <= 'Z' => (char)(c - 'A' + 1),
        ' ' or '@' or '2' => '\0',
        '[' or '3' => '\u001b',
        '\\' or '4' => '\u001c',
        ']' or '5' => '\u001d',
        '^' or '6' => '\u001e',
        '_' or '-' or '7' => '\u001f',
        '?' or '8' => '\u007f',
        _ => null,
    };

    private static char? Cursor(TerminalKey key) => key switch
    {
        TerminalKey.Up => 'A',
        TerminalKey.Down => 'B',
        TerminalKey.Right => 'C',
        TerminalKey.Left => 'D',
        TerminalKey.Home => 'H',
        TerminalKey.End => 'F',
        _ => null,
    };

    private static int? Tilde(TerminalKey key) => key switch
    {
        TerminalKey.Insert => 2,
        TerminalKey.Delete => 3,
        TerminalKey.PageUp => 5,
        TerminalKey.PageDown => 6,
        TerminalKey.F5 => 15,
        TerminalKey.F6 => 17,
        TerminalKey.F7 => 18,
        TerminalKey.F8 => 19,
        TerminalKey.F9 => 20,
        TerminalKey.F10 => 21,
        TerminalKey.F11 => 23,
        TerminalKey.F12 => 24,
        _ => null,
    };
}
