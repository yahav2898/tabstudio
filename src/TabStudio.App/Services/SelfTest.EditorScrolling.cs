using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's scrolling part, on the device: a long C# file in the sample, colored by Roslyn, scrolled with a
/// finger through the platform's touch screen input (a slow drag, then a flick), with the display's frames and the UI
/// thread's time for each frame recorded while the text moves.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The long file the scrolling part writes into the sample's library project, and removes afterwards.</summary>
    public const string ScrollingFileName = "SelfTestScrolling.cs";

    private static readonly TimeSpan ScrollingColorTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan GlideTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Opens a long C# file in the sample, drags it up slowly and flicks it with a finger, and reports how far the text
    /// followed and glided, how many of the display's frames it moved on, and the UI and render threads' frame times.
    /// Opens the workspace that was open before when done.
    /// </summary>
    /// <param name="main">The app's root view model.</param>
    /// <param name="samples">The sample library.</param>
    /// <param name="device">The touch screen and the display's frames.</param>
    /// <param name="log">Writes a line of detail to the log.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the text followed the drag and glided after the flick, with a one-line summary.</returns>
    public static Task<SelfTestResult> ScrollEditorAsync(MainViewModel main, SampleLibrary samples, ITouchSelfTestDevice device, Action<string> log, CancellationToken cancellationToken) =>
        ScrollEditorAsync(main, samples, FindEditor, device, log, cancellationToken);

    /// <summary>
    /// Opens a long C# file in the sample, drags it up slowly and flicks it with a finger, and reports how far the text
    /// followed and glided; finds the file's editor with <paramref name="findEditor"/>, as in a test's window.
    /// </summary>
    /// <param name="main">The app's root view model.</param>
    /// <param name="samples">The sample library.</param>
    /// <param name="findEditor">Finds the shown editor of a document, or <see langword="null"/> while it is not shown.</param>
    /// <param name="device">The touch screen and the display's frames.</param>
    /// <param name="log">Writes a line of detail to the log.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the text followed the drag and glided after the flick, with a one-line summary.</returns>
    public static async Task<SelfTestResult> ScrollEditorAsync(
        MainViewModel main, SampleLibrary samples, Func<DocumentViewModel, CodeEditorView?> findEditor, ITouchSelfTestDevice device,
        Action<string> log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(findEditor);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(log);
        var clock = Stopwatch.StartNew();
        var previous = main.Workspace?.Workspace is { } loaded ? loaded.Solution?.FilePath ?? loaded.RootDirectory : null;
        var solution = await samples.EnsureExtractedAsync(cancellationToken);
        var path = Path.Combine(Path.GetDirectoryName(solution)!, "src", "Greetings", ScrollingFileName);
        await File.WriteAllTextAsync(path, LongSampleCode.CSharp(), cancellationToken);
        try
        {
            await main.OpenWorkspaceAsync(solution);
            if (main.Workspace is not { } workspace || await workspace.OpenDocumentAsync(path) is not { } document)
            {
                return new SelfTestResult(false, $"Scrolling: {ScrollingFileName} did not open.", clock.Elapsed);
            }

            // Roslyn's colors first, as the user sees the file; on a slow device the scroll goes ahead without them.
            var deadline = clock.Elapsed + ScrollingColorTimeout;
            CodeEditorView? view = null;
            while (clock.Elapsed < deadline && ((view = findEditor(document)) is null || document.Classifications.Count < 1000 || document.Diagnostics.Count == 0))
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (view is null || view.Editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroll
                || TopLevel.GetTopLevel(view) is not { } topLevel)
            {
                return new SelfTestResult(false, "Scrolling: the editor did not show.", clock.Elapsed);
            }

            return await ScrollAsync(view, document, scroll, topLevel, device, log, clock, cancellationToken);
        }
        finally
        {
            if (previous is not null)
            {
                await main.OpenWorkspaceAsync(previous);
            }

            File.Delete(path);
        }
    }

    private static async Task<SelfTestResult> ScrollAsync(
        CodeEditorView view, DocumentViewModel document, ScrollViewer scroll, TopLevel topLevel, ITouchSelfTestDevice device,
        Action<string> log, Stopwatch clock, CancellationToken cancellationToken)
    {
        var textView = view.Editor.TextArea.TextView;
        var top = textView.TranslatePoint(new Point(textView.Bounds.Width * 0.5, textView.Bounds.Height * 0.2), topLevel);
        var bottom = textView.TranslatePoint(new Point(textView.Bounds.Width * 0.5, textView.Bounds.Height * 0.8), topLevel);
        if (top is not { } upper || bottom is not { } lower)
        {
            return new SelfTestResult(false, "Scrolling: the editor is not in the window.", clock.Elapsed);
        }

        // The finger moves up the editor, from near its bottom to near its top: the text scrolls down.
        var start = PixelPoint.FromPoint(lower, topLevel.RenderScaling);
        var end = PixelPoint.FromPoint(upper, topLevel.RenderScaling);
        var swipe = lower.Y - upper.Y;

        // Every offset the text takes and every frame the display starts, while the finger works.
        var moves = new List<(TimeSpan Time, double Offset)>();
        var frames = new List<TimeSpan>();
        void OnOffset(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.OffsetProperty)
            {
                moves.Add((clock.Elapsed, scroll.Offset.Y));
            }
        }

        // A finger that rests while the UI thread is stalled becomes a long press, as on any Android view: the drag waits
        // until the display's frames come regularly after opening the file.
        await SettleAsync(device, clock, cancellationToken);
        using var uiFrames = new UiFrameTimer(topLevel);
        var collections = GC.CollectionCount(0);
        var allocated = GC.GetTotalAllocatedBytes();
        scroll.PropertyChanged += OnOffset;
        try
        {
            using (device.RecordFrames(() => frames.Add(clock.Elapsed)))
            {
                // A slow drag up, finger from the bottom of the editor to the top in 1.2 s, then a flick in 120 ms.
                var dragStart = scroll.Offset.Y;
                await device.SwipeAsync(start, end, TimeSpan.FromMilliseconds(1200), cancellationToken);
                await WaitForGlideAsync(view, clock, cancellationToken);
                var dragged = scroll.Offset.Y - dragStart;

                var flickStart = scroll.Offset.Y;
                await device.SwipeAsync(start, end, TimeSpan.FromMilliseconds(120), cancellationToken);
                var released = clock.Elapsed;
                var glide = view.TouchScrolling.VerticalFling;
                await WaitForGlideAsync(view, clock, cancellationToken);
                var flicked = scroll.Offset.Y - flickStart;
                var stopped = moves.Count > 0 ? moves[^1].Time : released;

                var report = ScrollReport.From(moves, frames, released, stopped, uiFrames.Frames, GC.CollectionCount(0) - collections, GC.GetTotalAllocatedBytes() - allocated);
                var summary = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Scrolling: a {swipe:0} dip drag moved the text {dragged:0} dip; a flick glided {flicked:0} dip ({glide.Distance:0} dip in {glide.Duration.TotalMilliseconds:0} ms on Android's curve); {report.Summary}");
                log($"TabStudio editor scrolling: {summary}; {report.Detail}; {document.Classifications.Count} classified spans");

                // Past the touch slop the text follows the finger, and a flick glides on after the finger lifted.
                var followed = dragged > swipe * 0.8;
                var glided = flicked > swipe && !glide.IsEmpty;
                return new SelfTestResult(followed && glided, summary, clock.Elapsed);
            }
        }
        finally
        {
            scroll.PropertyChanged -= OnOffset;
        }
    }

    /// <summary>Waits until the display's frames come regularly for a second: nothing stalls the UI thread any more.</summary>
    private static async Task SettleAsync(ITouchSelfTestDevice device, Stopwatch clock, CancellationToken cancellationToken)
    {
        var frames = new List<TimeSpan>();
        var deadline = clock.Elapsed + SettleTimeout;
        using (device.RecordFrames(() => frames.Add(clock.Elapsed)))
        {
            while (clock.Elapsed < deadline)
            {
                await Task.Delay(250, cancellationToken);
                var recent = frames.Where(f => f > clock.Elapsed - TimeSpan.FromSeconds(1)).ToList();
                if (recent.Count >= 20 && recent.Zip(recent.Skip(1), (a, b) => b - a).All(gap => gap < TimeSpan.FromMilliseconds(100)))
                {
                    return;
                }
            }
        }
    }

    /// <summary>Waits for a glide to end, and for the frames after it to be drawn.</summary>
    private static async Task WaitForGlideAsync(CodeEditorView view, Stopwatch clock, CancellationToken cancellationToken)
    {
        var deadline = clock.Elapsed + GlideTimeout;
        await Task.Delay(100, cancellationToken);
        while ((view.TouchScrolling.IsFlinging || view.TouchScrolling.IsDragging) && clock.Elapsed < deadline)
        {
            await Task.Delay(PollInterval, cancellationToken);
        }

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background, cancellationToken);
        await Task.Delay(200, cancellationToken);
    }

    /// <summary>The frame statistics of a scroll, as one summary and one line of detail.</summary>
    private sealed record ScrollReport(string Summary, string Detail)
    {
        public static ScrollReport From(
            List<(TimeSpan Time, double Offset)> moves, List<TimeSpan> frames, TimeSpan released, TimeSpan stopped,
            IReadOnlyList<double> uiFrames, int collections, long allocated)
        {
            var culture = CultureInfo.InvariantCulture;

            // The display's refresh, from its frame intervals; and how many of its frames during the glide the text moved on.
            var intervals = frames.Zip(frames.Skip(1), (a, b) => (b - a).TotalMilliseconds).Where(i => i > 0).Order().ToList();
            var period = intervals.Count > 0 ? intervals[intervals.Count / 2] : 1000.0 / 60;
            var glideFrames = frames.Count(f => f > released && f <= stopped);
            var movedFrames = moves.Where(m => m.Time > released && m.Time <= stopped).Select(m => (int)(m.Time.TotalMilliseconds / period)).Distinct().Count();
            var missed = intervals.Count(i => i > period * 1.5);

            var ui = uiFrames.Order().ToList();
            var summary = string.Create(culture, $"{1000 / period:0} Hz display: the text moved on {movedFrames} of the {glideFrames} frames of the glide, {missed} frame gaps on the UI thread");
            var detail = ui.Count > 0
                ? string.Create(culture, $"UI thread per frame p50 {Percentile(ui, 0.5):0.0} ms, p95 {Percentile(ui, 0.95):0.0} ms, max {ui[^1]:0.0} ms over {ui.Count} frames")
                : "no frames timed";
            detail += string.Create(culture, $"; {collections} garbage collections, {allocated / 1024.0 / 1024:0.0} MB allocated in the process");
            return new ScrollReport(summary, detail);
        }

        private static double Percentile(List<double> sorted, double fraction) =>
            sorted.Count == 0 ? 0 : sorted[Math.Clamp((int)Math.Ceiling(fraction * sorted.Count) - 1, 0, sorted.Count - 1)];
    }
}
