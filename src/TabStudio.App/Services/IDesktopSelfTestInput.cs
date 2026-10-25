namespace TabStudio.App.Services;

/// <summary>
/// What the desktop mode self test needs from outside the app: a keyboard connected to the tablet, a mouse on the
/// external display, and screenshots of the device. On the emulator the CI script types and takes screenshots with
/// adb when the app asks in its log, and the app gives the display's window a mouse's click; tests use Avalonia's
/// headless input.
/// </summary>
public interface IDesktopSelfTestInput
{
    /// <summary>Types text on the keyboard connected to the tablet.</summary>
    /// <param name="text">Letters and spaces.</param>
    void Type(string text);

    /// <summary>Clicks a point of the external display.</summary>
    /// <param name="displayId">The display.</param>
    /// <param name="x">The point's distance from the display's left edge, in its pixels.</param>
    /// <param name="y">The point's distance from the display's top edge, in its pixels.</param>
    void Click(int displayId, int x, int y);

    /// <summary>Takes screenshots of the device's displays, where the host can, and returns once they are taken.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    Task ScreenshotAsync(CancellationToken cancellationToken);
}
