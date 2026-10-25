using System.Globalization;

namespace TabStudio.App.Platform;

/// <summary>
/// A display connected to the device besides its own screen: a monitor on USB-C or HDMI, or a wireless display.
/// Desktop mode shows the IDE on it.
/// </summary>
/// <param name="Id">The platform's id of the display.</param>
/// <param name="Name">Its name, such as "HDMI Screen".</param>
/// <param name="PixelWidth">Its width, in pixels.</param>
/// <param name="PixelHeight">Its height, in pixels.</param>
/// <param name="SystemScale">
/// How many pixels the operating system draws for one device-independent unit on it, such as 1.5 for a 240 dpi
/// display on Android.
/// </param>
public sealed record ExternalDisplay(int Id, string Name, int PixelWidth, int PixelHeight, double SystemScale)
{
    /// <summary>"1920 × 1080".</summary>
    public string Resolution => string.Create(CultureInfo.InvariantCulture, $"{PixelWidth} × {PixelHeight}");
}
