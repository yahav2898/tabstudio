using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace TabStudio.App.Tests.Performance;

/// <summary>What one UI action cost on the UI thread, with the layout and the frame it caused.</summary>
/// <param name="Time">From the action to the end of the frame that shows it.</param>
/// <param name="Allocated">Bytes allocated on the UI thread meanwhile.</param>
/// <param name="Collections">Garbage collections meanwhile.</param>
internal readonly record struct UiCost(TimeSpan Time, long Allocated, int Collections)
{
    /// <summary>"12.3 ms, 45 KB".</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Time.TotalMilliseconds:0.0} ms, {Allocated / 1024.0:0} KB{(Collections > 0 ? $", {Collections} GCs" : string.Empty)}");
}

/// <summary>
/// Measures what a person feels in the headless IDE: how long the UI thread is busy with one action, through the layout
/// and the frame that show its result, and writes each benchmark's report beside the screenshots
/// (<c>TABSTUDIO_SCREENSHOTS</c>), so CI publishes it.
/// </summary>
internal static class Smoothness
{
    /// <summary>Runs an action on the UI thread and everything it queued, then the layout and a frame of the window.</summary>
    /// <param name="window">The window that shows the result.</param>
    /// <param name="action">What the person did.</param>
    public static UiCost Measure(Window window, Action action)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(action);
        Settle(window);
        var collections = GC.CollectionCount(0);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        action();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        clock.Stop();
        return new UiCost(clock.Elapsed, GC.GetAllocatedBytesForCurrentThread() - allocated, GC.CollectionCount(0) - collections);
    }

    /// <summary>Lets the window finish what it had queued, so the next measurement starts from rest.</summary>
    /// <param name="window">The window.</param>
    public static void Settle(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The median and the worst of some costs, as "median 1.2 ms, worst 3.4 ms".</summary>
    /// <param name="costs">The costs.</param>
    public static string Spread(IReadOnlyList<UiCost> costs)
    {
        ArgumentNullException.ThrowIfNull(costs);
        if (costs.Count == 0)
        {
            return "none";
        }

        var sorted = costs.Select(c => c.Time.TotalMilliseconds).Order().ToList();
        var bytes = costs.Average(c => c.Allocated);
        return string.Create(CultureInfo.InvariantCulture, $"median {sorted[sorted.Count / 2]:0.0} ms, worst {sorted[^1]:0.0} ms, {bytes / 1024:0} KB each");
    }

    /// <summary>The median time of some costs.</summary>
    /// <param name="costs">The costs.</param>
    public static TimeSpan Median(IReadOnlyList<UiCost> costs)
    {
        ArgumentNullException.ThrowIfNull(costs);
        var sorted = costs.Select(c => c.Time).Order().ToList();
        return sorted.Count == 0 ? TimeSpan.Zero : sorted[sorted.Count / 2];
    }

    /// <summary>Writes a benchmark's report as <c>smoothness-{name}.md</c> beside the screenshots.</summary>
    /// <param name="name">The benchmark.</param>
    /// <param name="markdown">The report.</param>
    public static void Write(string name, string markdown)
    {
        var folder = Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS") ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, $"smoothness-{name}.md"), markdown);
    }
}
