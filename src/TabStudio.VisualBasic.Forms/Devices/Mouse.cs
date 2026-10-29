using System.Windows.Forms;

namespace Microsoft.VisualBasic.Devices;

/// <summary>The mouse (<c>My.Computer.Mouse</c>): on the tablet a finger, or a mouse connected to it.</summary>
public class Mouse
{
    /// <summary>Creates the mouse object.</summary>
    public Mouse()
    {
    }

    /// <summary>Whether the left and right buttons are swapped: they are not.</summary>
    public bool ButtonsSwapped => SystemInformation.MouseButtonsSwapped;

    /// <summary>Whether the mouse has a wheel: scrolling with a finger or a mouse wheel works as one.</summary>
    public bool WheelExists => SystemInformation.MouseWheelPresent;

    /// <summary>The lines one notch of the wheel scrolls.</summary>
    /// <exception cref="InvalidOperationException">The mouse has no wheel.</exception>
    public int WheelScrollLines =>
        WheelExists ? SystemInformation.MouseWheelScrollLines : throw new InvalidOperationException("No mouse wheel is present.");
}
