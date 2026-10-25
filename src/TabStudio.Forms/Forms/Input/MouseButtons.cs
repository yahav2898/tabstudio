namespace System.Windows.Forms;

/// <summary>The mouse buttons. A finger on the screen is the left button.</summary>
[Flags]
public enum MouseButtons
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Left.</summary>
    Left = 1048576,

    /// <summary>Right.</summary>
    Right = 2097152,

    /// <summary>Middle.</summary>
    Middle = 4194304,

    /// <summary>X button1.</summary>
    XButton1 = 8388608,

    /// <summary>X button2.</summary>
    XButton2 = 16777216,
}
