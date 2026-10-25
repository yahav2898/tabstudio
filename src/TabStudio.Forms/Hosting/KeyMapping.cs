using System.Windows.Forms;
using Avalonia.Input;

namespace TabStudio.Forms.Hosting;

/// <summary>Turns Avalonia's keys into the Windows virtual-key codes that Windows Forms' <see cref="Keys"/> are.</summary>
internal static class KeyMapping
{
    /// <summary>The Windows Forms key of an Avalonia key, with the modifiers held.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <returns>The key with its modifiers, or <see cref="Keys.None"/> for a key Windows has no code for.</returns>
    public static Keys ToKeys(Key key, KeyModifiers modifiers)
    {
        var code = ToKeyCode(key);
        if (code == Keys.None)
        {
            return Keys.None;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            code |= Keys.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            code |= Keys.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            code |= Keys.Alt;
        }

        return code;
    }

    /// <summary>The Windows virtual-key code of an Avalonia key, without modifiers.</summary>
    /// <param name="key">The key.</param>
    public static Keys ToKeyCode(Key key) => key switch
    {
        >= Key.A and <= Key.Z => Keys.A + (key - Key.A),
        >= Key.D0 and <= Key.D9 => Keys.D0 + (key - Key.D0),
        >= Key.NumPad0 and <= Key.NumPad9 => Keys.NumPad0 + (key - Key.NumPad0),
        >= Key.F1 and <= Key.F24 => Keys.F1 + (key - Key.F1),
        Key.Back => Keys.Back,
        Key.Tab => Keys.Tab,
        Key.Clear => Keys.Clear,
        Key.Enter => Keys.Enter,
        Key.Pause => Keys.Pause,
        Key.CapsLock => Keys.CapsLock,
        Key.Escape => Keys.Escape,
        Key.Space => Keys.Space,
        Key.PageUp => Keys.PageUp,
        Key.PageDown => Keys.PageDown,
        Key.End => Keys.End,
        Key.Home => Keys.Home,
        Key.Left => Keys.Left,
        Key.Up => Keys.Up,
        Key.Right => Keys.Right,
        Key.Down => Keys.Down,
        Key.Select => Keys.Select,
        Key.Print => Keys.Print,
        Key.Execute => Keys.Execute,
        Key.PrintScreen => Keys.PrintScreen,
        Key.Insert => Keys.Insert,
        Key.Delete => Keys.Delete,
        Key.Help => Keys.Help,
        Key.LWin => Keys.LWin,
        Key.RWin => Keys.RWin,
        Key.Apps => Keys.Apps,
        Key.Sleep => Keys.Sleep,
        Key.Multiply => Keys.Multiply,
        Key.Add => Keys.Add,
        Key.Separator => Keys.Separator,
        Key.Subtract => Keys.Subtract,
        Key.Decimal => Keys.Decimal,
        Key.Divide => Keys.Divide,
        Key.NumLock => Keys.NumLock,
        Key.Scroll => Keys.Scroll,

        // Windows Forms reports either Shift, Ctrl or Alt key as the generic one.
        Key.LeftShift or Key.RightShift => Keys.ShiftKey,
        Key.LeftCtrl or Key.RightCtrl => Keys.ControlKey,
        Key.LeftAlt or Key.RightAlt => Keys.Menu,
        Key.OemSemicolon => Keys.OemSemicolon,
        Key.OemPlus => Keys.Oemplus,
        Key.OemComma => Keys.Oemcomma,
        Key.OemMinus => Keys.OemMinus,
        Key.OemPeriod => Keys.OemPeriod,
        Key.OemQuestion => Keys.OemQuestion,
        Key.OemTilde => Keys.Oemtilde,
        Key.OemOpenBrackets => Keys.OemOpenBrackets,
        Key.OemPipe => Keys.OemPipe,
        Key.OemCloseBrackets => Keys.OemCloseBrackets,
        Key.OemQuotes => Keys.OemQuotes,
        Key.Oem8 => Keys.Oem8,
        Key.OemBackslash => Keys.OemBackslash,
        _ => Keys.None,
    };
}
