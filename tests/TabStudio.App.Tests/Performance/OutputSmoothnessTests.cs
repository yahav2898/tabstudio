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
    private const int Lines = 200_000;

    [AvaloniaFact]
    public async Task A_program_printing_in_a_tight_loop_leaves_the_IDE_responsive()
    {
        var output = new OutputViewModel(new AvaloniaUiDispatcher(), 1_000_000);
        var window = new Window { Width = 1280, Height = 400, Content = new OutputView { DataContext = output } };
        window.Show();
        Smoothness.Settle(window);
        var shown = 0;
        output.Written += (_, e) => shown += e.Text.Count(c => c == '\n');

        using var stalls = new UiStallMonitor(Dispatcher.UIThread, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(4));
        var clock = Stopwatch.StartNew();
        var printing = Task.Run(() =>
        {
            for (var i = 0; i < Lines; i++)
            {
                output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Line {i}: the program says something"));
            }
        });

        // The person keeps using the IDE: frames are drawn while the program prints.
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
        Assert.Contains($"Line {Lines - 1}: the program says something", output.Text, StringComparison.Ordinal);

        var report = new StringBuilder("# A program printing 200,000 lines as fast as it can\n\n");
        var culture = CultureInfo.InvariantCulture;
        report.AppendLine(culture, $"- The Output panel shown in a 1280 × 400 window; it keeps {output.Document.TextLength} characters at the end.");
        report.AppendLine(culture, $"- Every line shown {printed.TotalSeconds:0.00} s after the program started; {frames} frames asked for meanwhile.");
        report.AppendLine(culture, $"- The UI thread: {stalls.Summary()}.\n");
        report.Append(stalls.Table(10));
        Smoothness.Write("output", report.ToString());
        window.Close();
    }
}
