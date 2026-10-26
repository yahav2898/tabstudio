using System.Diagnostics;
using TabStudio.App.Previews;
using TabStudio.App.ViewModels;
using TabStudio.Uno.Templates;

namespace TabStudio.App.Services;

/// <summary>The emulator check of WinUI compiled and rendered in-process through Uno Platform.</summary>
public static partial class SelfTest
{
    /// <summary>Creates the WinUI template, presses F5, and reads its actual window title.</summary>
    private static async Task<SelfTestResult> ShowUnoAppAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        const string name = "SelfTestWinUI";
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(UnoTemplates.WinUIAppId, name));
        if (main.Workspace is not { } workspace || !workspace.StartDebuggingCommand.CanExecute(null))
        {
            return new SelfTestResult(false, "WinUI: the app did not open.", clock.Elapsed);
        }

        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        try
        {
            while (!workspace.AppPreview.IsRunning && !run.IsCompleted)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (workspace.AppPreview.View is not UnoPreviewControl { App: { } app })
            {
                return new SelfTestResult(false, $"WinUI: the preview did not start: {LastLines(workspace)}", clock.Elapsed);
            }

            // Read Uno's window rather than TabStudio's preview header, which also has the project name.
            var title = await app.GetTitleAsync().WaitAsync(cancellationToken);
            return new SelfTestResult(title == name, $"WinUI: Uno window '{title}' shown", clock.Elapsed);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(cancellationToken);
        }
    }
}
