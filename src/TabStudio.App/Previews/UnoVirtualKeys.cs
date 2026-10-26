using Avalonia.Input;

namespace TabStudio.App.Previews;

/// <summary>Translates Avalonia keys to WinUI's Windows.System.VirtualKey values.</summary>
internal static class UnoVirtualKeys
{
    /// <summary>The virtual key, or null for a key WinUI does not represent.</summary>
    public static int? From(Key key) => key switch
    {
        >= Key.A and <= Key.Z => 0x41 + key - Key.A,
        >= Key.D0 and <= Key.D9 => 0x30 + key - Key.D0,
        >= Key.NumPad0 and <= Key.NumPad9 => 0x60 + key - Key.NumPad0,
        >= Key.F1 and <= Key.F24 => 0x70 + key - Key.F1,
        Key.Back => 8,
        Key.Tab => 9,
        Key.Enter => 13,
        Key.LeftShift or Key.RightShift => 16,
        Key.LeftCtrl or Key.RightCtrl => 17,
        Key.LeftAlt or Key.RightAlt => 18,
        Key.Pause => 19,
        Key.CapsLock => 20,
        Key.Escape => 27,
        Key.Space => 32,
        Key.PageUp => 33,
        Key.PageDown => 34,
        Key.End => 35,
        Key.Home => 36,
        Key.Left => 37,
        Key.Up => 38,
        Key.Right => 39,
        Key.Down => 40,
        Key.PrintScreen => 44,
        Key.Insert => 45,
        Key.Delete => 46,
        Key.LWin => 91,
        Key.RWin => 92,
        Key.Apps => 93,
        Key.Multiply => 106,
        Key.Add => 107,
        Key.Subtract => 109,
        Key.Decimal => 110,
        Key.Divide => 111,
        Key.NumLock => 144,
        Key.Scroll => 145,
        Key.OemSemicolon => 186,
        Key.OemPlus => 187,
        Key.OemComma => 188,
        Key.OemMinus => 189,
        Key.OemPeriod => 190,
        Key.OemQuestion => 191,
        Key.OemTilde => 192,
        Key.OemOpenBrackets => 219,
        Key.OemPipe => 220,
        Key.OemCloseBrackets => 221,
        Key.OemQuotes => 222,
        _ => null,
    };
}
