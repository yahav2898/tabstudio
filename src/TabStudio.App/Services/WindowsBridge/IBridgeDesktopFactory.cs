using TabStudio.WindowsBridge.Hosting;

namespace TabStudio.App.Services.WindowsBridge;

/// <summary>
/// Makes the PC's desktop session for the Windows Bridge. Only TabStudio's desktop build on Windows registers one;
/// without it, this device can use a bridge but not be one.
/// </summary>
public interface IBridgeDesktopFactory
{
    /// <summary>Starts a desktop session: the PC's windows, their pictures, the mouse and keyboard, the clipboard.</summary>
    IBridgeDesktop Create();
}
