namespace TabStudio.Uno.Hosting;

/// <summary>What happened to a pointer over the app preview.</summary>
public enum UnoPointerAction
{
    /// <summary>It moved.</summary>
    Moved,

    /// <summary>A finger or pen touched, or a mouse button went down.</summary>
    Pressed,

    /// <summary>The finger or pen lifted, or the button went up.</summary>
    Released,

    /// <summary>A mouse or pen came over the preview.</summary>
    Entered,

    /// <summary>A mouse or pen left the preview.</summary>
    Exited,

    /// <summary>The system took the pointer away.</summary>
    Cancelled,

    /// <summary>The wheel turned, or a touchpad scrolled.</summary>
    Wheel,
}

/// <summary>The kind of pointer, with WinUI's values.</summary>
public enum UnoPointerDevice
{
    /// <summary>A finger.</summary>
    Touch = 0,

    /// <summary>A stylus.</summary>
    Pen = 1,

    /// <summary>A mouse or touchpad.</summary>
    Mouse = 2,
}

/// <summary>The modifier keys held, with WinUI's <c>VirtualKeyModifiers</c> values.</summary>
[Flags]
public enum UnoKeyModifiers
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Control.</summary>
    Control = 1,

    /// <summary>Alt, which WinUI calls Menu.</summary>
    Menu = 2,

    /// <summary>Shift.</summary>
    Shift = 4,

    /// <summary>The Windows key, or Command.</summary>
    Windows = 8,
}

/// <summary>One pointer event over the app preview, in the preview's device-independent pixels.</summary>
public sealed record UnoPointerEvent
{
    /// <summary>What happened.</summary>
    public required UnoPointerAction Action { get; init; }

    /// <summary>Which finger, mouse or pen.</summary>
    public uint PointerId { get; init; }

    /// <summary>The kind of pointer.</summary>
    public UnoPointerDevice Device { get; init; }

    /// <summary>The horizontal position.</summary>
    public double X { get; init; }

    /// <summary>The vertical position.</summary>
    public double Y { get; init; }

    /// <summary>Whether the left button is down, or the finger or pen touches.</summary>
    public bool IsLeftPressed { get; init; }

    /// <summary>Whether the right button, or the pen's barrel button, is down.</summary>
    public bool IsRightPressed { get; init; }

    /// <summary>Whether the middle button is down.</summary>
    public bool IsMiddlePressed { get; init; }

    /// <summary>How far the wheel turned, in WinUI's units of 120 a notch; positive is away from the user.</summary>
    public int WheelDelta { get; init; }

    /// <summary>Whether the wheel scrolls sideways.</summary>
    public bool IsHorizontalWheel { get; init; }

    /// <summary>The modifier keys held.</summary>
    public UnoKeyModifiers Modifiers { get; init; }

    /// <summary>When it happened, in microseconds.</summary>
    public ulong Timestamp { get; init; }
}
