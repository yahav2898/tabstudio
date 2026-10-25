namespace TabStudio.Swarm;

/// <summary>How a device's screen looks to the app it runs.</summary>
/// <param name="Width">The app's width, in device-independent pixels.</param>
/// <param name="Height">The app's height, in device-independent pixels.</param>
/// <param name="Scale">Physical pixels per device-independent pixel.</param>
/// <param name="IsDark">Whether the device shows apps in their dark theme.</param>
/// <param name="FontScale">The system font scale, 1 for the default size.</param>
/// <param name="IsRightToLeft">Whether the device lays text out from the right, as in Hebrew and Arabic.</param>
public sealed record SwarmScreenInfo(double Width, double Height, double Scale = 1, bool IsDark = true, double FontScale = 1, bool IsRightToLeft = false)
{
    /// <summary>A screen of unknown size, before the device has measured its own.</summary>
    public static SwarmScreenInfo Unknown { get; } = new(0, 0);
}
