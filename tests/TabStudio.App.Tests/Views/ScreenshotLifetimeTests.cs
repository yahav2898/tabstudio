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
[Collection(ScreenshotLifetimeTestGroup.Name)]
public sealed class ScreenshotLifetimeTests
{
    [AvaloniaFact]
    public async Task A_screenshot_is_saved_while_the_garbage_collector_runs()
    {
        using var temp = new TempDirectory();
        var window = new Window
        {
            Width = 1280,
            Height = 800,
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
        var collections = 0;
        var collector = Task.Factory.StartNew(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                GC.Collect();
                Interlocked.Increment(ref collections);
                // Finalizers run concurrently without waiting here. Waiting for the entire process's finalizer
                // queue is not cancelable and can stall this test and every UI test queued behind it.
                // A short, cancelable interval also avoids monopolizing the pool or continuously suspending it.
                if (stop.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(5))) break;
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        try
        {
            // Enough saves for the helper and Avalonia's Save to be recompiled with optimizations, whose code no longer
            // keeps a local alive after its last use, as in a full test run.
            for (var i = 0; i < 100; i++)
            {
                var path = Path.Combine(temp.Path, $"frame-{i}.png");
                UiTestHelpers.SaveFrame(window, path);
                Assert.True(new FileInfo(path).Length > 0);
            }

            Assert.True(Volatile.Read(ref collections) > 0, "The screenshots must overlap forced garbage collections.");
        }
        finally
        {
            try
            {
                await stop.CancelAsync();
                await collector.WaitAsync(TimeSpan.FromSeconds(15));
            }
            finally
            {
                window.Close();
            }
        }
    }
}

/// <summary>Process-wide GC stress runs without other collections manipulating native UI resources.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ScreenshotLifetimeTestGroup
{
    public const string Name = "Screenshot lifetime GC stress";
}
