using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TabStudio.App.Swarm;

/// <summary>
/// The pointer events of a replayed touch: the same events Avalonia's touch device raises for a finger, with a
/// pointer of the touch type that the touched control can capture.
/// </summary>
/// <remarks>
/// Avalonia marks these constructors unstable: it may replace them in version 12, and suggests its headless window's
/// input methods for tests, which exist only on the headless platform. They are the public way to raise a pointer's
/// events on Android and the desktop alike, so they are used here, in this one place, and the app tests replay taps,
/// long presses and scrolls through them on every build.
/// </remarks>
internal static class ReplayedPointerEvents
{
#pragma warning disable CS0618 // The constructors are marked unstable; see the remarks above.

    /// <summary>A finger going down.</summary>
    /// <param name="source">The element under the finger.</param>
    /// <param name="pointer">The finger.</param>
    /// <param name="root">The visual <paramref name="position"/> is relative to.</param>
    /// <param name="position">Where the finger is.</param>
    public static PointerPressedEventArgs Pressed(Interactive source, IPointer pointer, Visual root, Point position) =>
        new(source, pointer, root, position, Timestamp(), new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None);

    /// <summary>A finger lifting.</summary>
    /// <param name="source">The element that captured the finger, or the one under it.</param>
    /// <param name="pointer">The finger.</param>
    /// <param name="root">The visual <paramref name="position"/> is relative to.</param>
    /// <param name="position">Where the finger is.</param>
    public static PointerReleasedEventArgs Released(Interactive source, IPointer pointer, Visual root, Point position) =>
        new(source, pointer, root, position, Timestamp(), new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left);

    /// <summary>A scroll over an element, in notches of the mouse wheel.</summary>
    /// <param name="source">The element under the pointer.</param>
    /// <param name="pointer">The pointer.</param>
    /// <param name="root">The visual <paramref name="position"/> is relative to.</param>
    /// <param name="position">Where the pointer is.</param>
    /// <param name="delta">How far to scroll, in notches; positive scrolls up and left.</param>
    public static PointerWheelEventArgs Wheel(Interactive source, IPointer pointer, Visual root, Point position, Vector delta) =>
        new(source, pointer, root, position, Timestamp(), new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None, delta);

#pragma warning restore CS0618

    private static ulong Timestamp() => (ulong)Environment.TickCount64;
}
