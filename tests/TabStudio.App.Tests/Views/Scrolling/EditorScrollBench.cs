using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using TabStudio.App.Input.Scrolling;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>One frame of a scroll: where the editor was scrolled to and what drawing it cost.</summary>
/// <param name="Offset">The vertical scroll offset of the frame.</param>
/// <param name="UiMs">The UI thread's work for the frame: input, layout, recording and the jobs it queued.</param>
/// <param name="InputMs">Moving the scroll offset and what reacts to it at once.</param>
/// <param name="LayoutMs">The layout pass.</param>
/// <param name="RenderMs">The render pass that records what changed, and the work queued on the UI thread.</param>
/// <param name="CompositorMs">The compositor drawing the frame: the render thread's work on a device.</param>
/// <param name="AllocatedBytes">Bytes allocated on the UI thread during the frame.</param>
/// <param name="LinesBuilt">Visual lines the editor built (formatted and colored) for the frame.</param>
/// <param name="Collections">Garbage collections during the frame.</param>
internal readonly record struct ScrollFrame(double Offset, double UiMs, double InputMs, double LayoutMs, double RenderMs, double CompositorMs, long AllocatedBytes, int LinesBuilt, int Collections);

/// <summary>The frames of one scroll and their statistics.</summary>
/// <param name="Name">What was scrolled, and how.</param>
/// <param name="Frames">The frames.</param>
internal sealed record ScrollRun(string Name, IReadOnlyList<ScrollFrame> Frames)
{
    /// <summary>A percentile of the UI thread's frame times.</summary>
    /// <param name="fraction">The fraction, such as 0.95.</param>
    public double UiPercentile(double fraction) => Percentile(Frames.Select(f => f.UiMs), fraction);

    /// <summary>The fraction of frames whose UI thread work took less than a time.</summary>
    /// <param name="milliseconds">The time.</param>
    public double FractionUnder(double milliseconds) => Frames.Count == 0 ? 1 : (double)Frames.Count(f => f.UiMs < milliseconds) / Frames.Count;

    /// <summary>The mean bytes allocated per frame in the first and the second half of the run.</summary>
    public (double First, double Second) AllocationsByHalf()
    {
        var half = Frames.Count / 2;
        return half == 0 ? (0, 0) : (Frames.Take(half).Average(f => f.AllocatedBytes), Frames.Skip(half).Average(f => f.AllocatedBytes));
    }

    /// <summary>The run as a row of the report's table.</summary>
    public string Row()
    {
        if (Frames.Count == 0)
        {
            return $"| {Name} | 0 | | | | | | | | | | |";
        }

        var (first, second) = AllocationsByHalf();
        var row = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;
        row.Append(culture, $"| {Name} | {Frames.Count} | {UiPercentile(0.5):0.00} | {UiPercentile(0.95):0.00} | {Frames.Max(f => f.UiMs):0.00} | {FractionUnder(8) * 100:0}% ");
        row.Append(culture, $"| {Percentile(Frames.Select(f => f.LayoutMs), 0.95):0.00} | {Percentile(Frames.Select(f => f.RenderMs), 0.95):0.00} ");
        row.Append(culture, $"| {Percentile(Frames.Select(f => f.CompositorMs), 0.95):0.00} | {first / 1024:0.0} → {second / 1024:0.0} ");
        row.Append(culture, $"| {Frames.Sum(f => f.Collections)} | {Frames.Average(f => f.LinesBuilt):0.0} / {Frames.Max(f => f.LinesBuilt)} |");
        return row.ToString();
    }

    /// <summary>The header of the report's table.</summary>
    public const string Header =
        "| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |\n" +
        "|---|---|---|---|---|---|---|---|---|---|---|---|";

    private static double Percentile(IEnumerable<double> values, double fraction)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? 0 : sorted[Math.Clamp((int)Math.Ceiling(fraction * sorted.Length) - 1, 0, sorted.Length - 1)];
    }
}

/// <summary>
/// Scrolls the real editor in a headless window frame by frame, as a drag or a fling moves it, and measures each frame:
/// the UI thread's input, layout and render passes (from Avalonia's own metrics where it publishes them), the
/// compositor's drawing, the allocations and the visual lines the editor had to build.
/// </summary>
internal sealed class EditorScrollBench : IDisposable
{
    /// <summary>A 60 Hz frame.</summary>
    public static readonly TimeSpan Frame60 = TimeSpan.FromSeconds(1.0 / 60);

    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly LineCounter _counter = new();

    public EditorScrollBench(Window window, CodeEditorView view)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(view);
        _window = window;
        _editor = view.Editor;
        ScrollViewer = _editor.GetVisualDescendants().OfType<ScrollViewer>().First();
        _editor.TextArea.TextView.LineTransformers.Add(_counter);
        Settle();
    }

    /// <summary>The editor's scroll viewer.</summary>
    public ScrollViewer ScrollViewer { get; }

    /// <summary>Avalonia's frame metrics.</summary>
    public FrameProbe Probe { get; } = new();

    /// <summary>The furthest the editor scrolls down.</summary>
    public double MaxOffset => Math.Max(0, ScrollViewer.Extent.Height - ScrollViewer.Viewport.Height);

    /// <summary>The offsets of a drag at a steady speed, one per frame.</summary>
    /// <param name="from">The first offset.</param>
    /// <param name="to">The last offset.</param>
    /// <param name="pixelsPerFrame">How far each frame moves.</param>
    public static List<double> Drag(double from, double to, double pixelsPerFrame)
    {
        var offsets = new List<double>();
        var step = Math.CopySign(pixelsPerFrame, to - from);
        for (var offset = from + step; step > 0 ? offset <= to : offset >= to; offset += step)
        {
            offsets.Add(offset);
        }

        return offsets;
    }

    /// <summary>The offsets of flings back and forth, each released at a velocity and gliding as on Android, one per frame.</summary>
    /// <param name="from">Where the first fling starts.</param>
    /// <param name="max">The furthest offset.</param>
    /// <param name="velocity">The release velocity in dips per second; its sign is the first fling's direction.</param>
    /// <param name="count">How many flings, alternating direction.</param>
    /// <param name="frame">The frame duration.</param>
    public static List<double> Flings(double from, double max, double velocity, int count, TimeSpan frame)
    {
        var offsets = new List<double>();
        var start = from;
        for (var i = 0; i < count; i++)
        {
            var fling = FlingSpline.FromVelocity(i % 2 == 0 ? velocity : -velocity).ClampedTo(start, max - start);
            for (var t = frame; t < fling.Duration + frame; t += frame)
            {
                offsets.Add(start + fling.PositionAt(t));
            }

            start += fling.Distance;
        }

        return offsets;
    }

    /// <summary>Scrolls through offsets, one frame each, and measures every frame.</summary>
    /// <param name="name">The run's name in the report.</param>
    /// <param name="offsets">The vertical offsets.</param>
    public ScrollRun Run(string name, IEnumerable<double> offsets)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        var frames = new List<ScrollFrame>();
        foreach (var offset in offsets)
        {
            frames.Add(Frame(() => ScrollViewer.Offset = new Vector(ScrollViewer.Offset.X, offset)));
        }

        return new ScrollRun(name, frames);
    }

    /// <summary>Draws one frame after some input, as the app's render loop does, and measures it.</summary>
    /// <param name="input">What happens before the frame, such as moving the scroll offset or a finger.</param>
    public ScrollFrame Frame(Action input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Probe.Take();
        var linesBefore = _counter.Count;
        var collections = GC.CollectionCount(0);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();

        input();
        var afterInput = Stopwatch.GetTimestamp();

        // The layout pass, then the render pass with everything else the frame queued on the UI thread.
        _window.UpdateLayout();
        var afterLayout = Stopwatch.GetTimestamp();
        Dispatcher.UIThread.RunJobs();
        var afterRender = Stopwatch.GetTimestamp();
        var ui = Probe.Take();

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var afterCompositor = Stopwatch.GetTimestamp();
        var compositor = Probe.Take();

        // The headless platform's own render timer may have drawn during the UI work: that time is the compositor's.
        var uiCompositor = ui.CompositorMs;
        var compositorMs = Probe.HasAvaloniaMetrics ? compositor.CompositorMs + uiCompositor : Milliseconds(afterRender, afterCompositor);
        return new ScrollFrame(
            ScrollViewer.Offset.Y,
            Milliseconds(start, afterRender) - uiCompositor,
            Milliseconds(start, afterInput),
            Milliseconds(afterInput, afterLayout),
            Milliseconds(afterLayout, afterRender) - uiCompositor,
            compositorMs,
            GC.GetAllocatedBytesForCurrentThread() - allocated,
            _counter.Count - linesBefore,
            GC.CollectionCount(0) - collections);
    }

    /// <summary>Renders until the editor is idle.</summary>
    public void Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        Dispatcher.UIThread.RunJobs();
        Probe.Take();
    }

    /// <summary>The time of each layout and render activity by control type while scrolling, slowest first, as report lines.</summary>
    /// <param name="offsets">The offsets to scroll through while profiling.</param>
    /// <param name="top">How many entries to keep.</param>
    public string ProfileLines(IEnumerable<double> offsets, int top = 18)
    {
        var frames = offsets.ToList();
        Probe.StartProfiling();
        try
        {
            Run("profile", frames);
        }
        finally
        {
            Probe.StopProfiling();
        }

        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Style attachments per frame: {(double)Probe.StyleAttachments / Math.Max(1, frames.Count):0.0}");
        text.AppendLine();
        text.AppendLine("| Activity and control | ms per frame |");
        text.AppendLine("|---|---|");
        foreach (var (key, total) in Probe.Profile.OrderByDescending(p => p.Value).Take(top))
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"| {key} | {total / Math.Max(1, frames.Count):0.000} |");
        }

        return text.ToString();
    }

    public void Dispose()
    {
        _editor.TextArea.TextView.LineTransformers.Remove(_counter);
        Probe.Dispose();
    }

    private static double Milliseconds(long from, long to) => Stopwatch.GetElapsedTime(from, to).TotalMilliseconds;

    /// <summary>Counts the visual lines the editor builds: it transforms every line it builds once.</summary>
    private sealed class LineCounter : IVisualLineTransformer
    {
        public int Count { get; private set; }

        public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements) => Count++;
    }
}
