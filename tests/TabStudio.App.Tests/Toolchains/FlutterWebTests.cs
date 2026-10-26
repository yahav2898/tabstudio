using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Toolchains;

public sealed class FlutterWebTests
{
    [FlutterWebFact]
    public async Task Flutter_web_preview_restarts_after_an_editor_save()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        using var services = AppServices.Create(paths);
        var main = services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        window.Show();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var result = await SelfTest.RunFlutterWebAsync(main, services.GetRequiredService<ProjectTemplates>(), timeout.Token);
        UiTestHelpers.SaveScreenshot(window, "flutter-web-editor-after-save");
        Assert.True(result.Success, result.Message);
        main.Workspace?.Dispose();
        window.Close();
    }
}

[AttributeUsage(AttributeTargets.Method)]
[Xunit.Sdk.XunitTestCaseDiscoverer("Avalonia.Headless.XUnit.AvaloniaUIFactDiscoverer", "Avalonia.Headless.XUnit")]
public sealed class FlutterWebFactAttribute : FactAttribute
{
    public FlutterWebFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TABSTUDIO_FLUTTER_TESTS") != "1")
        {
            Skip = "Set TABSTUDIO_FLUTTER_TESTS=1 on a runner with a verified Flutter SDK.";
        }
    }
}
