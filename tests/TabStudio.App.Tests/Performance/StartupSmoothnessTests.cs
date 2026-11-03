using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Tests.Extensions;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Performance;

/// <summary>
/// TabStudio's start before its first frame: the timeline of this test process's own start (the first in the process,
/// so its code was compiled as it ran, as on a cold start), then the same start again with every service created one
/// at a time, to name the ones that take longest, and the start screen's first frame. Writes
/// <c>smoothness-startup.md</c>.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class StartupSmoothnessTests
{
    [AvaloniaFact]
    public async Task The_services_and_the_start_screen_are_ready_quickly()
    {
        var culture = CultureInfo.InvariantCulture;
        var report = new StringBuilder("# TabStudio's start\n\n");
        report.AppendLine("## This test process's own start (cold)\n");
        report.AppendLine("| Part | Took | At |");
        report.AppendLine("| --- | --- | --- |");
        var marks = StartupTimeline.All;
        for (var i = 1; i < marks.Count; i++)
        {
            report.AppendLine(culture, $"| {marks[i].Name} | {(marks[i].At - marks[i - 1].At).TotalMilliseconds:0} ms | {marks[i].At.TotalMilliseconds:0} ms |");
        }

        // Again, warm, with the services created one at a time in the order they are registered.
        using var temp = new TempDirectory();
        IServiceCollection? registered = null;
        var clock = Stopwatch.StartNew();
        await using var services = AppServices.Create(new AppPaths(Path.Combine(temp.Path, "app")), collection => registered = collection);
        var built = clock.Elapsed;
        var created = new List<(string Service, TimeSpan Took)>();
        foreach (var descriptor in registered!.Where(d => !d.ServiceType.IsGenericTypeDefinition && d.Lifetime == ServiceLifetime.Singleton).ToList())
        {
            clock.Restart();
            var name = descriptor.ServiceType.Name;
            try
            {
                services.GetService(descriptor.ServiceType);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                name += $" (failed: {ex.GetType().Name})";
            }

            created.Add((name, clock.Elapsed));
        }

        clock.Restart();
        var main = services.GetRequiredService<MainViewModel>();
        var window = new Window { Width = 1280, Height = 800, Content = new MainView { DataContext = main } };
        window.Show();
        Smoothness.Settle(window);
        var firstFrame = clock.Elapsed;

        report.AppendLine(culture, $"\n## Again, warm\n\n- Building the service provider: {built.TotalMilliseconds:0} ms.");
        report.AppendLine(culture, $"- Every service, created one at a time: {created.Sum(c => c.Took.TotalMilliseconds):0} ms.");
        report.AppendLine(culture, $"- The main view's first frame on the start screen: {firstFrame.TotalMilliseconds:0} ms.\n");
        report.AppendLine("| Service (with what it needs that was not created before it) | Took |");
        report.AppendLine("| --- | --- |");
        foreach (var (service, took) in created.OrderByDescending(c => c.Took).Take(20))
        {
            report.AppendLine(culture, $"| {service} | {took.TotalMilliseconds:0.0} ms |");
        }

        Smoothness.Write("startup", report.ToString());
        window.Close();
    }
}
