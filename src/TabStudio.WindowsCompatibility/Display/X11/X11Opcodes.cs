namespace TabStudio.WindowsCompatibility.Display.X11;

/// <summary>The X11 core requests, events and constants TabStudio uses.</summary>
internal static class X11Opcodes
{
    /// <summary>ConfigureWindow: moves, resizes or restacks a window.</summary>
    public const byte ConfigureWindow = 12;

    /// <summary>GetGeometry: a drawable's position and size.</summary>
    public const byte GetGeometry = 14;

    /// <summary>QueryTree: a window's parent and children.</summary>
    public const byte QueryTree = 15;

    /// <summary>InternAtom: the atom for a name.</summary>
    public const byte InternAtom = 16;

    /// <summary>GetProperty: a window property's value.</summary>
    public const byte GetProperty = 20;

    /// <summary>GetInputFocus, used as a round trip.</summary>
    public const byte GetInputFocus = 43;

    /// <summary>QueryExtension: whether an extension is there, and its opcode and event base.</summary>
    public const byte QueryExtension = 98;

    /// <summary>ChangeKeyboardMapping: gives key codes new key symbols.</summary>
    public const byte ChangeKeyboardMapping = 100;

    /// <summary>GetKeyboardMapping: the key symbols of key codes.</summary>
    public const byte GetKeyboardMapping = 101;

    /// <summary>The MappingNotify event: the keyboard mapping changed.</summary>
    public const byte MappingNotify = 34;

    /// <summary>The XTEST extension's GetVersion request.</summary>
    public const byte XTestGetVersion = 0;

    /// <summary>The XTEST extension's FakeInput request.</summary>
    public const byte XTestFakeInput = 2;

    /// <summary>The DAMAGE extension's QueryVersion request.</summary>
    public const byte DamageQueryVersion = 0;

    /// <summary>The DAMAGE extension's Create request.</summary>
    public const byte DamageCreate = 1;

    /// <summary>The DAMAGE extension's Subtract request.</summary>
    public const byte DamageSubtract = 3;

    /// <summary>A damage object that reports when the bounding box of what changed grows.</summary>
    public const byte DamageReportBoundingBox = 2;

    /// <summary>The core KeyPress event type, as XTEST's FakeInput takes it.</summary>
    public const byte KeyPress = 2;

    /// <summary>The core KeyRelease event type.</summary>
    public const byte KeyRelease = 3;

    /// <summary>The core ButtonPress event type.</summary>
    public const byte ButtonPress = 4;

    /// <summary>The core ButtonRelease event type.</summary>
    public const byte ButtonRelease = 5;

    /// <summary>The core MotionNotify event type.</summary>
    public const byte MotionNotify = 6;
}
