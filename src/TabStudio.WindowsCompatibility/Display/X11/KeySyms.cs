using TabStudio.WindowsCompatibility.Input;

namespace TabStudio.WindowsCompatibility.Display.X11;

/// <summary>The X11 key symbols of the keys TabStudio sends, and the symbol of any character.</summary>
internal static class KeySyms
{
    /// <summary>Backspace.</summary>
    public const uint BackSpace = 0xFF08;

    /// <summary>Tab.</summary>
    public const uint Tab = 0xFF09;

    /// <summary>Enter.</summary>
    public const uint Return = 0xFF0D;

    /// <summary>Escape.</summary>
    public const uint Escape = 0xFF1B;

    /// <summary>Delete.</summary>
    public const uint Delete = 0xFFFF;

    /// <summary>Home.</summary>
    public const uint Home = 0xFF50;

    /// <summary>Left arrow.</summary>
    public const uint Left = 0xFF51;

    /// <summary>Up arrow.</summary>
    public const uint Up = 0xFF52;

    /// <summary>Right arrow.</summary>
    public const uint Right = 0xFF53;

    /// <summary>Down arrow.</summary>
    public const uint Down = 0xFF54;

    /// <summary>Page Up.</summary>
    public const uint PageUp = 0xFF55;

    /// <summary>Page Down.</summary>
    public const uint PageDown = 0xFF56;

    /// <summary>End.</summary>
    public const uint End = 0xFF57;

    /// <summary>Insert.</summary>
    public const uint Insert = 0xFF63;

    /// <summary>The context menu key.</summary>
    public const uint Menu = 0xFF67;

    /// <summary>F1; F2 to F12 follow it.</summary>
    public const uint F1 = 0xFFBE;

    /// <summary>The left Shift key.</summary>
    public const uint ShiftL = 0xFFE1;

    /// <summary>The left Control key.</summary>
    public const uint ControlL = 0xFFE3;

    /// <summary>Caps Lock.</summary>
    public const uint CapsLock = 0xFFE5;

    /// <summary>The left Alt key.</summary>
    public const uint AltL = 0xFFE9;

    /// <summary>The left Windows key.</summary>
    public const uint SuperL = 0xFFEB;

    /// <summary>The key symbol that types a character: Latin-1 characters are their own symbols, others are Unicode symbols.</summary>
    /// <param name="codepoint">The character's code point.</param>
    public static uint ForCharacter(int codepoint) => codepoint switch
    {
        '\r' or '\n' => Return,
        '\t' => Tab,
        '\b' => BackSpace,
        0x1B => Escape,
        >= 0x20 and <= 0x7E or >= 0xA0 and <= 0xFF => (uint)codepoint,
        _ => 0x01000000u | (uint)codepoint,
    };

    /// <summary>The key symbol of a key.</summary>
    /// <param name="key">The key.</param>
    public static uint For(WindowsKey key) => key switch
    {
        WindowsKey.Backspace => BackSpace,
        WindowsKey.Tab => Tab,
        WindowsKey.Enter => Return,
        WindowsKey.Escape => Escape,
        WindowsKey.Space => ' ',
        WindowsKey.PageUp => PageUp,
        WindowsKey.PageDown => PageDown,
        WindowsKey.End => End,
        WindowsKey.Home => Home,
        WindowsKey.Left => Left,
        WindowsKey.Up => Up,
        WindowsKey.Right => Right,
        WindowsKey.Down => Down,
        WindowsKey.Insert => Insert,
        WindowsKey.Delete => Delete,
        WindowsKey.Shift => ShiftL,
        WindowsKey.Control => ControlL,
        WindowsKey.Alt => AltL,
        WindowsKey.Windows => SuperL,
        WindowsKey.Menu => Menu,
        WindowsKey.CapsLock => CapsLock,
        >= WindowsKey.F1 and <= WindowsKey.F12 => F1 + (uint)(key - WindowsKey.F1),
        >= WindowsKey.A and <= WindowsKey.Z => 'a' + (uint)(key - WindowsKey.A),
        >= WindowsKey.D0 and <= WindowsKey.D9 => '0' + (uint)(key - WindowsKey.D0),
        _ => 0,
    };
}
