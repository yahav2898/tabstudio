using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's Avalonia part: an Avalonia app restored, built and shown natively in the app preview, and its view
/// previewed in the XAML designer.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The Avalonia project the self test creates.</summary>
    public const string AvaloniaAppName = "SelfTestAvalonia";

    /// <summary>How long the designer may take to build the project and render the view.</summary>
    private static readonly TimeSpan DesignerTimeout = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Creates an Avalonia app from the template, presses Ctrl+F5, reads the greeting its view model binds, taps its
    /// button twice and stops it: Avalonia restored from NuGet, its XAML loaded at run time and drawn by TabStudio.
    /// Then it opens the app's main view in the XAML designer.
    /// </summary>
    /// <returns>Whether the app showed its greeting and counted the taps, and the designer previewed it, with a summary.</returns>
    private static async Task<SelfTestResult> ShowAvaloniaAppAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(ProjectTemplates.AvaloniaId, AvaloniaAppName));
        if (main.Workspace is not { } workspace || !workspace.RunCommand.CanExecute(null))
        {
            return new SelfTestResult(false, "Avalonia: the app did not open.", clock.Elapsed);
        }

        var run = workspace.RunCommand.ExecuteAsync(null);
        TimeSpan shown;
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

            shown = clock.Elapsed;
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

            if (!Texts(view).Contains("Clicked 2 times"))
            {
                return new SelfTestResult(false, $"Avalonia: the button did not count: {string.Join(", ", Texts(view))}", clock.Elapsed);
            }
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(cancellationToken);
        }

        var ran = string.Create(CultureInfo.InvariantCulture, $"Avalonia: shown in {shown.TotalSeconds:0.0} s, the button counted 2 taps");
        var designed = await DesignAvaloniaViewAsync(workspace, cancellationToken);
        return new SelfTestResult(designed.Success, $"{ran} | {designed.Message}", clock.Elapsed);
    }

    /// <summary>
    /// Opens the Avalonia app's main view in the XAML designer and waits for its preview: the project built in the
    /// background, and its XAML compiled at run time in design mode, with the template's design-time data.
    /// </summary>
    /// <returns>Whether the preview showed the view model's greeting, with a one-line summary.</returns>
    private static async Task<SelfTestResult> DesignAvaloniaViewAsync(WorkspaceViewModel workspace, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var path = Path.Combine(Path.GetDirectoryName(workspace.StartupProject!.FilePath)!, "Views", "MainView.axaml");
        if (await workspace.OpenDocumentAsync(path) is not { Designer: { } designer })
        {
            return new SelfTestResult(false, "Designer: MainView.axaml did not open in the designer.", clock.Elapsed);
        }

        while (!PreviewShows(designer, "Hello from Avalonia!") && designer.Error is null && clock.Elapsed < DesignerTimeout)
        {
            await Task.Delay(PollInterval, cancellationToken);
        }

        return PreviewShows(designer, "Hello from Avalonia!")
            ? new SelfTestResult(true, string.Create(CultureInfo.InvariantCulture, $"Designer: previewed MainView.axaml in {clock.Elapsed.TotalSeconds:0.0} s"), clock.Elapsed)
            : new SelfTestResult(false, $"Designer: no preview: {designer.Error ?? designer.Status}", clock.Elapsed);
    }

    private static bool PreviewShows(XamlDesignerViewModel designer, string text) =>
        designer.Preview is { } preview && Texts(preview).Contains(text);

    private static List<string> Texts(Control view) =>
        [.. view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty)];
}
