using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// Saves screenshots while the garbage collector runs. Skia encodes the PNG from pixels that the captured frame owns,
/// and Avalonia's Save does not keep the frame alive while Skia reads them, so <see cref="UiTestHelpers.SaveFrame"/>
/// must: a frame collected during the encode unmaps its pixels under Skia, a SIGSEGV in libSkiaSharp.so that kills
/// the test host (one IDE test run in four, before it did).
/// </summary>
public sealed class ScreenshotLifetimeTests
{
    [AvaloniaFact]
    public async Task A_screenshot_is_saved_while_the_garbage_collector_runs()
    {
        using var temp = new TempDirectory();
        var window = new Window
        {
            Width = 1600,
            Height = 1200,
            // Pixels that differ from row to row, so the encode takes long enough for collections to happen during it.
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops { new GradientStop(Colors.MidnightBlue, 0), new GradientStop(Colors.Orange, 1) },
            },
            Content = new TextBlock
            {
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Text = string.Join(' ', Enumerable.Repeat("The frame owns the pixels that Skia encodes.", 300)),
            },
        };
        window.Show();

        using var stop = new CancellationTokenSource();
        var collector = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        });

        try
        {
            for (var i = 0; i < 30; i++)
            {
                var path = Path.Combine(temp.Path, $"frame-{i}.png");
                UiTestHelpers.SaveFrame(window, path);
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            await stop.CancelAsync();
            await collector;
            window.Close();
        }
    }
}
