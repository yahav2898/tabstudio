using System.Diagnostics;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    /// <summary>Installs Flutter, fetches packages, serves a web preview, saves an edit and waits for Hot Restart.</summary>
    public static async Task<SelfTestResult> RunFlutterWebAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(ProjectTemplates.FlutterWebId, "FlutterSelfTest"));
        if (main.Workspace is not { } workspace) return new(false, "Flutter: workspace did not open", clock.Elapsed);
        try
        {
            if (workspace.Toolchains.Manager.CanInstall)
            {
                await workspace.ToolchainStore.InstallAsync("flutter").WaitAsync(cancellationToken);
            }
            await workspace.Toolchains.Manager.RefreshAsync(cancellationToken: cancellationToken);
            if (!workspace.Toolchains.Manager.IsReady("flutter"))
                return new(false, "Flutter SDK is unavailable: " + workspace.Toolchains.Manager.Status("flutter").Error, clock.Elapsed);
            await workspace.InstallProjectPackagesCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            await workspace.RunCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            while (!workspace.WebPreview.IsRunning && workspace.IsDebuggingWithAdapter)
            {
                await Task.Delay(100, cancellationToken);
            }
            if (workspace.WebPreview.AppAddress is not { } address || !workspace.WebPreview.IsRunning)
                return new(false, "Flutter web preview did not start: " + LastLines(workspace), clock.Elapsed);
            using var http = new HttpClient();
            var page = await http.GetStringAsync(address, cancellationToken);
            if (!page.Contains("flutter_bootstrap.js", StringComparison.Ordinal))
                return new(false, "Flutter web preview did not serve the generated entry page", clock.Elapsed);
            var path = Path.Combine(workspace.Workspace!.RootDirectory, "lib/main.dart");
            var document = await workspace.OpenDocumentAsync(path);
            if (document is null) return new(false, "Flutter: main.dart did not open", clock.Elapsed);
            document.Document.Text = document.Document.Text.Replace("Count: $count", "Updated count: $count", StringComparison.Ordinal);
            await workspace.SaveAllCoreAsync();
            while (!workspace.Output.Text.Contains("Flutter Hot Restart completed.", StringComparison.Ordinal)
                && workspace.IsDebuggingWithAdapter)
            {
                await Task.Delay(100, cancellationToken);
            }
            var success = workspace.Output.Text.Contains("Flutter Hot Restart completed.", StringComparison.Ordinal);
            return new(success, success ? "Flutter web: pub get, preview HTTP page and Hot Restart after saving succeeded" : LastLines(workspace), clock.Elapsed);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(false, "Flutter: " + ex.Message, clock.Elapsed);
        }
        finally { workspace.StopDebuggingCommand.Execute(null); }
    }
}
