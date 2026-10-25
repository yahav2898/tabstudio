using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace TabStudio.App.Tests;

/// <summary>Helpers for tests that drive the UI on the headless dispatcher.</summary>
internal static class UiTestHelpers
{
    /// <summary>Processes UI work until a condition holds, or fails after a timeout.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null, string? because = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(60));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Timed out waiting{(because is null ? string.Empty : " for " + because)}.");
            }

            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Renders a window and saves it as a PNG in the screenshot folder (TABSTUDIO_SCREENSHOTS, or a
    /// "screenshots" folder next to the test assembly).
    /// </summary>
    public static string SaveScreenshot(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nothing was rendered.");

        var folder = Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS")
            ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".png");
        frame.Save(path);
        return path;
    }
}
