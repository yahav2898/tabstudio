namespace TabStudio.WindowsCompatibility.Input;

/// <summary>A mouse button of the Windows desktop.</summary>
public enum PointerButton
{
    /// <summary>The left button: what a tap clicks.</summary>
    Left = 1,

    /// <summary>The middle button.</summary>
    Middle = 2,

    /// <summary>The right button: what a long press clicks.</summary>
    Right = 3,
}

/// <summary>Where touch, mouse and keyboard input for a Windows app goes: the desktop it runs on.</summary>
/// <remarks>
/// Positions are in the desktop's pixels. Calls return at once; the input is delivered in order, in the background.
/// </remarks>
public interface IDesktopInput
{
    /// <summary>Moves the mouse pointer.</summary>
    /// <param name="x">The position's x.</param>
    /// <param name="y">The position's y.</param>
    void MovePointer(int x, int y);

    /// <summary>Presses or releases a mouse button where the pointer is.</summary>
    /// <param name="button">The button.</param>
    /// <param name="pressed">Whether it goes down.</param>
    void Button(PointerButton button, bool pressed);

    /// <summary>Turns the mouse wheel by notches, as Windows counts them: positive is up or to the right.</summary>
    /// <param name="horizontal">Notches to the right (positive) or left.</param>
    /// <param name="vertical">Notches up (positive) or down.</param>
    void Wheel(int horizontal, int vertical);

    /// <summary>Presses or releases a key that is not text, such as Ctrl, an arrow or F5.</summary>
    /// <param name="key">The key.</param>
    /// <param name="pressed">Whether it goes down.</param>
    void Key(WindowsKey key, bool pressed);

    /// <summary>Types text, as if typed key by key.</summary>
    /// <param name="text">The text.</param>
    void Type(string text);
}
