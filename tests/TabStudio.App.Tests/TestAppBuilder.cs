using Avalonia;
using Avalonia.Headless;
using TabStudio.App;

[assembly: AvaloniaTestApplication(typeof(TabStudio.App.Tests.TestAppBuilder))]

namespace TabStudio.App.Tests;

/// <summary>Starts the real TabStudio application on Avalonia's headless platform, rendering with Skia.</summary>
public static class TestAppBuilder
{
    /// <summary>Configures the headless app.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TabStudioApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}
