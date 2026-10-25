using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(TabStudio.Forms.Tests.Infrastructure.TestApp))]

namespace TabStudio.Forms.Tests.Infrastructure;

/// <summary>An empty Avalonia app on the headless platform, so text is measured and forms are drawn with Skia.</summary>
public sealed class TestApp : Application
{
    /// <summary>Configures the headless app.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}
