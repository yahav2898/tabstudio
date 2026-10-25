namespace TabStudio.WindowsBridge.Input;

/// <summary>What a <see cref="BridgeInputEvent"/> does on the PC.</summary>
public enum BridgeInputKind
{
    /// <summary>The mouse moves to a position, with the buttons in <see cref="BridgeInputEvent.Button"/> held.</summary>
    PointerMove,

    /// <summary>A mouse button goes down at a position.</summary>
    PointerDown,

    /// <summary>A mouse button comes up at a position.</summary>
    PointerUp,

    /// <summary>The mouse wheel turns at a position, by <see cref="BridgeInputEvent.DeltaX"/> and <see cref="BridgeInputEvent.DeltaY"/>.</summary>
    Wheel,

    /// <summary>A key goes down: <see cref="BridgeInputEvent.VirtualKey"/>.</summary>
    KeyDown,

    /// <summary>A key comes up.</summary>
    KeyUp,

    /// <summary>Text is typed, such as from the on-screen keyboard: <see cref="BridgeInputEvent.Text"/>.</summary>
    Text,
}

/// <summary>A mouse button.</summary>
public enum BridgePointerButton
{
    /// <summary>No button.</summary>
    None,

    /// <summary>The left (primary) button.</summary>
    Left,

    /// <summary>The right button, which opens context menus.</summary>
    Right,

    /// <summary>The middle button.</summary>
    Middle,
}

/// <summary>
/// One mouse or keyboard event for a program's window on the PC. Touch on the tablet is turned into these by
/// <see cref="TouchGestureRecognizer"/>; a mouse or keyboard attached to the tablet sends them as they are.
/// </summary>
/// <param name="Kind">What happens.</param>
/// <param name="X">For pointer events: the position from the window's left edge, in the PC's pixels.</param>
/// <param name="Y">For pointer events: the position from the window's top edge, in the PC's pixels.</param>
/// <param name="Button">For pointer events: the button pressed, released, or held while moving.</param>
/// <param name="DeltaX">For the wheel: how far it turns sideways, 120 for one notch to the right.</param>
/// <param name="DeltaY">For the wheel: how far it turns, 120 for one notch away from the user (scrolling up).</param>
/// <param name="VirtualKey">For keys: the Windows virtual-key code, such as 0x0D for Enter.</param>
/// <param name="Text">For text: what is typed.</param>
public sealed record BridgeInputEvent(
    BridgeInputKind Kind,
    int X = 0,
    int Y = 0,
    BridgePointerButton Button = BridgePointerButton.None,
    int DeltaX = 0,
    int DeltaY = 0,
    int VirtualKey = 0,
    string? Text = null)
{
    /// <summary>One notch of a mouse wheel, as Windows counts it.</summary>
    public const int WheelNotch = 120;

    /// <summary>A pointer event at a position.</summary>
    /// <param name="kind">Move, down or up.</param>
    /// <param name="x">The position from the window's left edge.</param>
    /// <param name="y">The position from the window's top edge.</param>
    /// <param name="button">The button.</param>
    public static BridgeInputEvent Pointer(BridgeInputKind kind, int x, int y, BridgePointerButton button = BridgePointerButton.None) => new(kind, x, y, button);

    /// <summary>A turn of the wheel at a position.</summary>
    /// <param name="x">The position from the window's left edge.</param>
    /// <param name="y">The position from the window's top edge.</param>
    /// <param name="deltaX">Sideways, 120 per notch to the right.</param>
    /// <param name="deltaY">Up and down, 120 per notch away from the user.</param>
    public static BridgeInputEvent Wheel(int x, int y, int deltaX, int deltaY) => new(BridgeInputKind.Wheel, x, y, DeltaX: deltaX, DeltaY: deltaY);

    /// <summary>A key going down or up.</summary>
    /// <param name="virtualKey">The Windows virtual-key code.</param>
    /// <param name="down">Whether it goes down.</param>
    public static BridgeInputEvent Key(int virtualKey, bool down) => new(down ? BridgeInputKind.KeyDown : BridgeInputKind.KeyUp, VirtualKey: virtualKey);

    /// <summary>Typed text.</summary>
    /// <param name="text">The text.</param>
    public static BridgeInputEvent Typed(string text) => new(BridgeInputKind.Text, Text: text);
}
