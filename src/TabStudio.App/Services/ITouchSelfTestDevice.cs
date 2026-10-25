using Avalonia;

namespace TabStudio.App.Services;

/// <summary>
/// What the self test's scrolling part needs from the device: a finger on the touch screen, through the platform's own
/// input system, and the display's frames. On Android, touches are injected into TabStudio's window as the touch screen
/// sends them, and the frames come from the Choreographer; desktop has no such device.
/// </summary>
public interface ITouchSelfTestDevice
{
    /// <summary>
    /// Puts a finger down, slides it in a straight line at a steady speed, and lifts it, as the touch screen reports a
    /// swipe: a move about every 8 ms. Returns once the finger has lifted.
    /// </summary>
    /// <param name="origin">Where the finger goes down, in pixels from the top left of TabStudio's view.</param>
    /// <param name="destination">Where it lifts.</param>
    /// <param name="duration">How long the swipe takes.</param>
    /// <param name="cancellationToken">Gives up.</param>
    Task SwipeAsync(PixelPoint origin, PixelPoint destination, TimeSpan duration, CancellationToken cancellationToken);

    /// <summary>
    /// Calls back on the UI thread at the start of every frame of the display (its vsync), as the UI thread gets to it:
    /// a frame the UI thread was too busy for shows as a gap between calls. Call on the UI thread; stop by disposing the
    /// result.
    /// </summary>
    /// <param name="frame">Called at the start of each frame.</param>
    IDisposable RecordFrames(Action frame);
}
