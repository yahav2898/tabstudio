using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Live apps, shown beside the code while they run: web apps in the web preview, and Avalonia and Windows Forms
/// apps in the app preview. Saving one of the app's files while it runs rebuilds it and starts it again, so the preview follows
/// the code; for a web app, a file it serves as is, under <c>wwwroot</c>, only reloads the page.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private static readonly HashSet<string> RestartingExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".json", ".cshtml", ".razor", ".axaml", ".props", ".targets", ".xaml",
    };

    private ProjectInfo? _liveProject;
    private bool _restartLiveApp;

    /// <summary>Whether F5 shows a project beside the code rather than running it as a program.</summary>
    private static bool IsLiveApp(ProjectInfo project) =>
        IsWebApp(project) || IsAvaloniaApp(project) || IsWindowsFormsApp(project) || IsWebAssemblyApp(project) || ProjectClassifier.IsBlazorWebAppClient(project) || IsWpfApp(project);

    /// <summary>
    /// Builds and runs a live app until it is stopped, starting it again whenever one of its files is saved.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="fromDebugger">Whether F5 asked for the debugger, which live apps start without.</param>
    private async Task RunLiveProjectAsync(ProjectInfo project, bool fromDebugger)
    {
        // Starts a build of the app and shows it, returning when it stops.
        project = AppToRun(project);
        Func<ProjectInfo, BuildResult, CompiledAssembly, bool, Task> start =
            IsWebApp(project) ? ServeWebAppAsync
            : IsWebAssemblyApp(project) ? ServeWebAssemblyAppAsync
            : IsWpfApp(project) ? ServeWpfAppAsync
            : IsWindowsFormsApp(project) ? ShowWindowsFormsAppAsync
            : ShowAvaloniaAppAsync;
        var restarted = false;
        do
        {
            _restartLiveApp = false;

            // A restart keeps the app's log so far in the Output panel, under the line that says why it restarted.
            var build = await BuildProjectsAsync([project], showOutput: !restarted);
            if (build is not { Success: true } || build.EntryAssembly is not { } entry)
            {
                await StopWpfHostAsync();
                WebPreview.ShowStopped();
                AppPreview.ShowStopped();
                OnLiveRunEnded();
                return;
            }

            if (fromDebugger && !restarted)
            {
                Output.WriteLine(
                    IsWebApp(project) ? "Started without the debugger: it pauses a program on its main thread, and a web app answers requests on others."
                    : IsWebAssemblyApp(project) ? "Started without the debugger: a WebAssembly app runs in the web preview's browser, out of the debugger's reach."
                    : IsWpfApp(project) ? "Started without the debugger: a WPF app runs on OpenSilver in the web preview's browser, out of the debugger's reach."
                    : IsWindowsFormsApp(project) ? "Started without the debugger: a Windows Forms app runs its own message loop, which the debugger does not pause yet."
                    : "Started without the debugger: an Avalonia app runs on TabStudio's own UI thread, which the debugger cannot pause.",
                    OutputKind.Info);
            }

            await start(project, build, entry, restarted);
            restarted = true;
        }
        while (_restartLiveApp && !_disposed);
        OnLiveRunEnded();
    }

    /// <summary>Ends one run of a live app: either the line that says it restarts, or its exit line.</summary>
    private void FinishLiveRun(ProjectInfo project, RunResult result)
    {
        if (_restartLiveApp && result.Outcome != RunOutcome.Crashed)
        {
            Output.WriteLine();
            Output.WriteLine($"Restarting '{project.Name}' with the saved changes.", OutputKind.Info);
            return;
        }

        _restartLiveApp = false;
        ReportRunResult(project, result);
    }

    /// <summary>
    /// Restarts the running live app when one of its source files is saved, or reloads a web app's page when a
    /// file it serves as is, such as one in <c>wwwroot</c>, changes.
    /// </summary>
    private void OnDocumentSavedForLiveApp(DocumentViewModel document)
    {
        if (OnDocumentSavedForRazorApp(document) || OnDocumentSavedForWpfApp(document))
        {
            return;
        }

        if (_liveProject is not { } project || _run is not { } run
            || !document.FilePath.StartsWith(project.DirectoryPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return;
        }

        // An Avalonia or Windows Forms app's files are all compiled into it or read by it, its images included.
        var relative = Path.GetRelativePath(project.DirectoryPath, document.FilePath);
        var restarts = IsAvaloniaApp(project) || IsWindowsFormsApp(project)
            || (RestartingExtensions.Contains(Path.GetExtension(document.FilePath))
                && !relative.StartsWith("wwwroot" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        if (!restarts)
        {
            if (IsWebApp(project) || IsWebAssemblyApp(project))
            {
                WebPreview.ReloadCommand.Execute(null);
            }

            return;
        }

        _restartLiveApp = true;
        run.Cancel();
    }
}
