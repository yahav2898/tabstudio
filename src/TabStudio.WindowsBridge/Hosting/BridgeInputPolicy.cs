namespace TabStudio.WindowsBridge.Hosting;

/// <summary>How the bridge plays the tablet's input into a program on the PC.</summary>
public enum BridgeInputPolicy
{
    /// <summary>
    /// Brings the program to the front and uses the PC's mouse and keyboard while nobody is working at the PC, and
    /// otherwise sends the input to the program's window without taking the PC's mouse, keyboard or focus. Keyboard
    /// shortcuts with Ctrl, Alt or Windows always need the front.
    /// </summary>
    Automatic,

    /// <summary>Always brings the program to the front and uses the PC's mouse and keyboard, as a person at the PC would.</summary>
    Foreground,

    /// <summary>Never takes the PC's mouse, keyboard or focus: the input goes to the program's window as messages.</summary>
    Background,
}
