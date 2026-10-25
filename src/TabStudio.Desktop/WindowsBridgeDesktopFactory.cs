using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App;
using TabStudio.App.Services.WindowsBridge;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Windows;

namespace TabStudio.Desktop;

/// <summary>Makes this PC's desktop session for the Windows Bridge, so TabStudio for Windows can be a tablet's bridge.</summary>
internal sealed class WindowsBridgeDesktopFactory : IBridgeDesktopFactory
{
    /// <inheritdoc />
    public IBridgeDesktop Create()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Only TabStudio for Windows can be a Windows Bridge.");
        }

        return new WindowsBridgeDesktop();
    }
}

/// <summary>Registers the Windows Bridge's desktop session on Windows.</summary>
internal static class WindowsBridgeAppBuilderExtensions
{
    /// <summary>On Windows, lets TabStudio be a Windows Bridge for a tablet; elsewhere, changes nothing.</summary>
    /// <param name="builder">The app builder.</param>
    /// <returns>The app builder.</returns>
    public static AppBuilder UseWindowsBridge(this AppBuilder builder) =>
        OperatingSystem.IsWindows()
            ? builder.UseTabStudioPlatform(services => services.AddSingleton<IBridgeDesktopFactory, WindowsBridgeDesktopFactory>())
            : builder;
}
