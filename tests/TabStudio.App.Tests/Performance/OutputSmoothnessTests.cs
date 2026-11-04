using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TabStudio.App.Services;
using TabStudio.App.Tests.Extensions;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Performance;

/// <summary>
/// A program that prints as fast as it can, in a tight loop, into the Output panel shown on screen: the IDE must keep
/// answering taps and drawing frames while the text pours in, and show all of it soon after the program stops. Writes
/// <c>smoothness-output.md</c>.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class OutputSmoothnessTests
{
    private const int Lines = 20_000;

    [AvaloniaFact]
    public async Task A_program_printing_in_a_tight_loop_leaves_the_IDE_responsive()
    {
        if (Smoothness.IsLightPass)
        {
            return;
        }

        var (beforeTime, beforeStalls, beforeFrames) = await FloodAsync(oldWay: true);
        var (afterTime, afterStalls, afterFrames) = await FloodAsync(oldWay: false);

        var report = new StringBuilder("# A program printing 20,000 lines as fast as it can\n\n");
        var culture = CultureInfo.InvariantCulture;
        report.AppendLine("- The Output panel shown in a 1280 × 400 window, keeping its last 1,000,000 characters; a frame asked for every 16 ms meanwhile.\n");
        report.AppendLine("| Appending | Every line shown after | Frames drawn meanwhile | The UI thread meanwhile |");
        report.AppendLine("| --- | --- | --- | --- |");
        report.AppendLine(culture, $"| As before 0.21.11: everything queued in one job at normal priority, one insertion per write | {beforeTime.TotalSeconds:0.00} s | {beforeFrames} | {beforeStalls} |");
        report.AppendLine(culture, $"| In batches of 64 KB behind input and drawing | {afterTime.TotalSeconds:0.00} s | {afterFrames} | {afterStalls} |");
        Smoothness.Write("output", report.ToString());
    }

    /// <summary>
    /// Prints the lines from a thread of their own into a shown Output panel, through the panel, or the way the panel
    /// appended them before 0.21.11: a copy of that code, writing into the same kind of view.
    /// </summary>
    private static async Task<(TimeSpan Shown, string Stalls, int Frames)> FloodAsync(bool oldWay)
    {
        var output = new OutputViewModel(new AvaloniaUiDispatcher(), 1_000_000);
        var view = new OutputView { DataContext = output };
        var window = new Window { Width = 1280, Height = 400, Content = view };
        window.Show();
        Smoothness.Settle(window);
        var shown = 0;
        output.Written += (_, e) => shown += e.Text.Count(c => c == '\n');
        var editor = view.FindControl<AvaloniaEdit.TextEditor>("Editor") ?? throw new InvalidOperationException("The Output panel has no editor.");
        var pending = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var scheduled = 0;
        void OldFlush()
        {
            Interlocked.Exchange(ref scheduled, 0);
            while (pending.TryDequeue(out var text))
            {
                output.Document.Insert(output.Document.TextLength, text);
                shown++;
            }

            if (output.Document.TextLength > 1_000_000)
            {
                var cut = output.Document.GetLineByOffset(output.Document.TextLength - 750_000).EndOffset;
                output.Document.Remove(0, Math.Min(output.Document.TextLength, cut + 1));
            }

            editor.TextArea.TextView.Redraw();
            editor.ScrollToEnd();
        }

        using var stalls = new UiStallMonitor(Dispatcher.UIThread, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(4));
        var clock = Stopwatch.StartNew();
        var printing = Task.Run(() =>
        {
            for (var i = 0; i < Lines; i++)
            {
                var line = string.Create(CultureInfo.InvariantCulture, $"Line {i}: the program says something\n");
                if (oldWay)
                {
                    pending.Enqueue(line);
                    if (Interlocked.Exchange(ref scheduled, 1) == 0)
                    {
                        Dispatcher.UIThread.Post(OldFlush);
                    }
                }
                else
                {
                    output.Write(line);
                }
            }
        });

        // The person keeps using the IDE: a frame is asked for every 16 ms while the program prints.
        var frames = 0;
        while (!printing.IsCompleted || shown < Lines)
        {
            await Task.Delay(16);
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            frames++;
            Assert.True(clock.Elapsed < TimeSpan.FromMinutes(3), $"the output was not shown after 3 minutes: {shown} of {Lines} lines");
        }

        await printing;
        var printed = clock.Elapsed;
        stalls.Dispose();
        Assert.Contains($"Line {Lines - 1}: the program says something", output.Document.Text, StringComparison.Ordinal);
        window.Close();
        return (printed, stalls.Summary(), frames);
    }
}
