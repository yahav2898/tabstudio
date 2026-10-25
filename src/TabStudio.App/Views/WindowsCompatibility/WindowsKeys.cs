using Avalonia.Input;
using TabStudio.WindowsCompatibility.Input;

namespace TabStudio.App.Views.WindowsCompatibility;

/// <summary>
/// Which Avalonia keys go to a Windows app as keys: those that are not text (arrows, Enter, F-keys, modifiers),
/// and letters and digits while Ctrl or Alt is held, as shortcuts. Typed text arrives as text input instead.
/// </summary>
public static class WindowsKeys
{
    /// <summary>The Windows key for an Avalonia key, or <see langword="null"/> when it arrives as text or is not sent.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    public static WindowsKey? For(Key key, KeyModifiers modifiers)
    {
        var shortcut = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Alt);
        return key switch
        {
            Key.Back => WindowsKey.Backspace,
            Key.Tab => WindowsKey.Tab,
            Key.Enter => WindowsKey.Enter,
            Key.Escape => WindowsKey.Escape,
            Key.PageUp => WindowsKey.PageUp,
            Key.PageDown => WindowsKey.PageDown,
            Key.End => WindowsKey.End,
            Key.Home => WindowsKey.Home,
            Key.Left => WindowsKey.Left,
            Key.Up => WindowsKey.Up,
            Key.Right => WindowsKey.Right,
            Key.Down => WindowsKey.Down,
            Key.Insert => WindowsKey.Insert,
            Key.Delete => WindowsKey.Delete,
            Key.LeftShift or Key.RightShift => WindowsKey.Shift,
            Key.LeftCtrl or Key.RightCtrl => WindowsKey.Control,
            Key.LeftAlt or Key.RightAlt => WindowsKey.Alt,
            Key.LWin or Key.RWin => WindowsKey.Windows,
            Key.Apps => WindowsKey.Menu,
            Key.CapsLock => WindowsKey.CapsLock,
            >= Key.F1 and <= Key.F12 => WindowsKey.F1 + (key - Key.F1),
            Key.Space when shortcut => WindowsKey.Space,
            >= Key.A and <= Key.Z when shortcut => WindowsKey.A + (key - Key.A),
            >= Key.D0 and <= Key.D9 when shortcut => WindowsKey.D0 + (key - Key.D0),
            _ => null,
        };
    }
}
