using Avalonia;
using Avalonia.Headless;
using TabStudio.App;
using TabStudio.App.Services.Appearance;
using TabStudio.App.Tests.Theming;

[assembly: AvaloniaTestApplication(typeof(TabStudio.App.Tests.TestAppBuilder))]

namespace TabStudio.App.Tests;

/// <summary>
/// Starts the real TabStudio application on Avalonia's headless platform, rendering with Skia, in the dark theme or, with
/// <c>TABSTUDIO_THEME=Light</c>, the light one (CI runs the tests again that way for light screenshots).
/// </summary>
public static class TestAppBuilder
{
    /// <summary>Configures the headless app.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TabStudioApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .AfterSetup(_ => AppearanceService.Preview(TestTheme.Baseline));
}
