using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(TabStudio.Forms.Design.Tests.TestApp))]

namespace TabStudio.Forms.Design.Tests;

/// <summary>An empty Avalonia app on the headless platform, so the designer measures text and draws forms with Skia.</summary>
public sealed class TestApp : Application
{
    /// <summary>Configures the headless app.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}
