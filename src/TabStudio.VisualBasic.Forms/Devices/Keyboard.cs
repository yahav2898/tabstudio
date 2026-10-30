using System.Windows.Forms;

namespace Microsoft.VisualBasic.Devices;

/// <summary>
/// The keyboard (<c>My.Computer.Keyboard</c>): its modifier keys as the app's last key event reported them. The
/// device does not report the state of the lock keys, which read as off.
/// </summary>
public class Keyboard
{
    /// <summary>Creates the keyboard object.</summary>
    public Keyboard()
    {
    }

    /// <summary>Whether Alt is down.</summary>
    public bool AltKeyDown => (Control.ModifierKeys & Keys.Alt) != 0;

    /// <summary>Whether Caps Lock is on: the tablet does not report it, so <see langword="false"/>.</summary>
    public bool CapsLock => false;

    /// <summary>Whether Ctrl is down.</summary>
    public bool CtrlKeyDown => (Control.ModifierKeys & Keys.Control) != 0;

    /// <summary>Whether Num Lock is on: the tablet does not report it, so <see langword="false"/>.</summary>
    public bool NumLock => false;

    /// <summary>Whether Scroll Lock is on: the tablet does not report it, so <see langword="false"/>.</summary>
    public bool ScrollLock => false;

    /// <summary>Whether Shift is down.</summary>
    public bool ShiftKeyDown => (Control.ModifierKeys & Keys.Shift) != 0;

    /// <summary>Sends keys to the active window. Not supported: there is no Windows input queue on the tablet.</summary>
    /// <param name="keys">The keys, in SendKeys syntax.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void SendKeys(string keys) => SendKeys(keys, wait: false);

    /// <summary>Sends keys to the active window. Not supported: there is no Windows input queue on the tablet.</summary>
    /// <param name="keys">The keys, in SendKeys syntax.</param>
    /// <param name="wait">Whether to wait until they are handled.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void SendKeys(string keys, bool wait) =>
        throw Unsupported.Member(
            "My.Computer.Keyboard.SendKeys",
            "there is no Windows input queue to send keys to. Set the control's Text, or call its methods or PerformClick, instead.");
}
