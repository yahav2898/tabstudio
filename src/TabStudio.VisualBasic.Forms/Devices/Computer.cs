using System.Windows.Forms;
using Microsoft.VisualBasic.MyServices;

namespace Microsoft.VisualBasic.Devices;

/// <summary>
/// The computer (<c>My.Computer</c>): the device's clock, files, information, network, clipboard, keyboard, mouse and
/// screen, which is TabStudio's app preview.
/// </summary>
public class Computer : ServerComputer
{
    private static ClipboardProxy? _clipboard;
    private static Keyboard? _keyboardInstance;
    private static Mouse? _mouse;
    private Audio? _audio;

    /// <summary>Creates the computer object, as the My namespace does.</summary>
    public Computer()
    {
    }

    /// <summary>Sound output, which TabStudio's app preview does not have.</summary>
    public Audio Audio => _audio ??= new Audio();

    /// <summary>The device's clipboard: text and images are shared with other apps, other data stays in the app.</summary>
    public ClipboardProxy Clipboard => _clipboard ??= new ClipboardProxy();

    /// <summary>The keyboard's modifier keys, as the app's last key event reported them.</summary>
    public Keyboard Keyboard => _keyboardInstance ??= new Keyboard();

    /// <summary>The mouse, or the finger on the touch screen.</summary>
    public Mouse Mouse => _mouse ??= new Mouse();

    /// <summary>The screen: TabStudio's app preview, where the app's forms are shown.</summary>
    public Screen Screen => Screen.PrimaryScreen;
}
