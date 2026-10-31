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
    /// "screenshots" folder next to the test assembly). Fails when a single line of text in it is stretched to a fixed
    /// height instead of centered in it (docs/design/README.md, "Type"), which Android's fonts show as text sitting high,
    /// or when a control in it breaks the accessibility rules (docs/design/README.md, "Accessibility"): a tap target under
    /// the screen's touch target, a control with no accessible name, or text or an icon below WCAG AA contrast.
    /// </summary>
    public static string SaveScreenshot(Window window, string name)
    {
        var folder = Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS")
            ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".png");
        SaveFrame(window, path);
        Design.StretchedText.Check(window, folder, name);
        var found = Accessibility.AccessibilityReport.Check(window, folder, name);
        if (Accessibility.AccessibilityReport.Enforced)
        {
            Accessibility.AccessibilityReport.AssertNone(found, name);
        }

        return path;
    }

    /// <summary>Renders a window and saves it as a PNG at the given path.</summary>
    public static void SaveFrame(Window window, string path)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        // The frame owns the pixels that Skia encodes, and Avalonia's Save does not keep it alive while Skia reads them.
        // Without the using, a collection during the encode can finalize the frame and unmap its pixels under Skia: a
        // SIGSEGV in libSkiaSharp.so that kills the test host.
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nothing was rendered.");
        frame.Save(path);
    }
}
