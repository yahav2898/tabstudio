using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>The self test's Avalonia part: an Avalonia app restored, built and shown natively in the app preview.</summary>
public static partial class SelfTest
{
    /// <summary>The Avalonia project the self test creates.</summary>
    public const string AvaloniaAppName = "SelfTestAvalonia";

    /// <summary>
    /// Creates an Avalonia app from the template, presses Ctrl+F5, reads the greeting its view model binds, taps its
    /// button twice and stops it: Avalonia restored from NuGet, its XAML loaded at run time and drawn by TabStudio.
    /// </summary>
    /// <returns>Whether the app showed its greeting and counted the taps, with a one-line summary.</returns>
    private static async Task<SelfTestResult> ShowAvaloniaAppAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(ProjectTemplates.AvaloniaId, AvaloniaAppName));
        if (main.Workspace is not { } workspace || !workspace.RunCommand.CanExecute(null))
        {
            return new SelfTestResult(false, "Avalonia: the app did not open.", clock.Elapsed);
        }

        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            while (!workspace.AppPreview.IsRunning && !run.IsCompleted)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (workspace.AppPreview.View is not { } view)
            {
                return new SelfTestResult(false, $"Avalonia: the app did not start: {LastLines(workspace)}", clock.Elapsed);
            }

            var shown = clock.Elapsed;
            var button = view.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "CountButton");
            if (button?.Command is not ICommand count || !Texts(view).Contains("Hello from Avalonia!"))
            {
                return new SelfTestResult(false, $"Avalonia: the view is not the template's: {string.Join(", ", Texts(view))}", clock.Elapsed);
            }

            count.Execute(null);
            count.Execute(null);
            while (!Texts(view).Contains("Clicked 2 times") && clock.Elapsed < shown + TimeSpan.FromSeconds(10))
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            var counted = Texts(view).Contains("Clicked 2 times");
            var message = counted
                ? string.Create(CultureInfo.InvariantCulture, $"Avalonia: shown in {shown.TotalSeconds:0.0} s, the button counted 2 taps")
                : $"Avalonia: the button did not count: {string.Join(", ", Texts(view))}";
            return new SelfTestResult(counted, message, clock.Elapsed);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(cancellationToken);
        }
    }

    private static List<string> Texts(Control view) =>
        [.. view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty)];
}
