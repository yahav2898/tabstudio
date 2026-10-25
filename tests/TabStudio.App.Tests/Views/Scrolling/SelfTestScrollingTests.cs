using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>The self test's scrolling part, as the emulator runs it, with a finger on Avalonia's headless touch screen.</summary>
public sealed class SelfTestScrollingTests
{
    [AvaloniaFact]
    public async Task The_self_test_drags_and_flicks_a_long_file_and_reports_the_frames()
    {
        using var temp = new TempDirectory();
        await using var services = AppServices.Create(new AppPaths(temp.Resolve("app")));
        var main = services.GetRequiredService<MainViewModel>();
        var samples = services.GetRequiredService<SampleLibrary>();
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        window.Show();
        try
        {
            var before = temp.Resolve("before");
            Directory.CreateDirectory(before);
            await File.WriteAllTextAsync(Path.Combine(before, "notes.txt"), "Notes.\n");
            await main.OpenWorkspaceAsync(before);
            var lines = new List<string>();

            var result = await SelfTest.ScrollEditorAsync(
                main,
                samples,
                document => window.GetVisualDescendants().OfType<CodeEditorView>().FirstOrDefault(v => ReferenceEquals(v.DataContext, document) && v.IsEffectivelyVisible),
                new HeadlessTouchDevice(window),
                lines.Add,
                CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.Contains("a flick glided", result.Message, StringComparison.Ordinal);
            Assert.Contains(lines, l => l.StartsWith("TabStudio editor scrolling:", StringComparison.Ordinal) && l.Contains("UI thread per frame", StringComparison.Ordinal));

            // The workspace that was open before is open again, and the long file is gone from the sample.
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(before)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(main.Workspace!.Workspace!.RootDirectory)));
            var solution = await samples.EnsureExtractedAsync();
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(solution)!, "src", "Greetings", SelfTest.ScrollingFileName)));
        }
        finally
        {
            window.Close();
            main.Workspace?.Dispose();
        }
    }

    /// <summary>Swipes through Avalonia's touch device, a move every 8 ms of real time, and counts the render loop's frames.</summary>
    private sealed class HeadlessTouchDevice(Window window) : ITouchSelfTestDevice
    {
        private readonly HeadlessTouchScreen _touch = new(window);

        public async Task SwipeAsync(PixelPoint start, PixelPoint end, TimeSpan duration, CancellationToken cancellationToken)
        {
            var scaling = window.RenderScaling;
            var first = start.ToPoint(scaling);
            var last = end.ToPoint(scaling);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            _touch.Down(first);
            while (clock.Elapsed < duration)
            {
                await Task.Delay(8, cancellationToken);
                _touch.Wait(8);
                var t = Math.Min(1, clock.Elapsed / duration);
                _touch.Move(first + ((last - first) * t));
            }

            _touch.Wait(4);
            _touch.Up(last);
        }

        public IDisposable RecordFrames(Action frame)
        {
            var recording = new Recording();
            void Next(TimeSpan time)
            {
                if (!recording.Stopped)
                {
                    frame();
                    window.RequestAnimationFrame(Next);
                }
            }

            window.RequestAnimationFrame(Next);
            return recording;
        }

        private sealed class Recording : IDisposable
        {
            public bool Stopped { get; private set; }

            public void Dispose() => Stopped = true;
        }
    }
}
