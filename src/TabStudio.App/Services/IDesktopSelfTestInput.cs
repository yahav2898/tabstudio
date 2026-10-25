using Avalonia;

namespace TabStudio.App.Services;

/// <summary>
/// What the desktop mode self test needs from outside the app: a keyboard connected to the tablet, the tablet's mouse
/// used on the external display, and screenshots of the device. On the emulator the CI script types and takes
/// screenshots with adb when the app asks in its log, and the app moves and clicks the mouse through Android's input
/// system; tests use Avalonia's headless input.
/// </summary>
public interface IDesktopSelfTestInput
{
    /// <summary>Types text on the keyboard connected to the tablet.</summary>
    /// <param name="text">Letters and spaces.</param>
    void Type(string text);

    /// <summary>Moves the tablet's mouse to a point of the external display and clicks there with the left button.</summary>
    /// <param name="displayId">The display.</param>
    /// <param name="point">The point, in the display's pixels from its top left corner.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Once the mouse has clicked.</returns>
    Task ClickAsync(int displayId, PixelPoint point, CancellationToken cancellationToken);

    /// <summary>Moves the tablet's mouse to a point of the external display and turns its wheel there.</summary>
    /// <param name="displayId">The display.</param>
    /// <param name="point">The point, in the display's pixels from its top left corner.</param>
    /// <param name="notches">How far, in notches of the wheel: positive scrolls up, negative down.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Once the wheel has turned.</returns>
    Task ScrollAsync(int displayId, PixelPoint point, double notches, CancellationToken cancellationToken);

    /// <summary>Takes screenshots of the device's displays, where the host can, and returns once they are taken.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Once the screenshots are taken.</returns>
    Task ScreenshotAsync(CancellationToken cancellationToken);
}
