using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Web apps: F5 on an ASP.NET Core project serves it on the device and shows it in the web preview, and saving
/// one of its files while it runs rebuilds and restarts it, so the preview stays live.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private static readonly HashSet<string> RestartingExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".json", ".cshtml", ".razor", ".props", ".targets",
    };

    private ProjectInfo? _webProject;
    private bool _restartWebApp;
    private int? _webPort;

    /// <summary>The web preview, which shows the running web app.</summary>
    public WebPreviewViewModel WebPreview { get; }

    /// <summary>Whether the side bar shows the web preview.</summary>
    public bool IsWebPreviewSelected => SelectedSideBarPanel == SideBarPanel.WebPreview;

    /// <summary>Whether F5 serves a project as a web app rather than running it as a program.</summary>
    private static bool IsWebApp(ProjectInfo project) => project.Kind == ProjectKind.AspNetCore;

    /// <summary>
    /// Builds and serves a web app until it is stopped, starting it again whenever one of its files is saved.
    /// </summary>
    /// <param name="project">The web project.</param>
    /// <param name="fromDebugger">Whether F5 asked for the debugger, which web apps start without.</param>
    private async Task RunWebProjectAsync(ProjectInfo project, bool fromDebugger)
    {
        var restarted = false;
        do
        {
            _restartWebApp = false;
            var build = await BuildProjectsAsync([project], showOutput: true);
            if (build is not { Success: true } || build.EntryAssembly is not { } entry)
            {
                WebPreview.ShowStopped();
                return;
            }

            if (fromDebugger && !restarted)
            {
                Output.WriteLine("Started without the debugger: it pauses a program on its main thread, and a web app answers requests on others.", OutputKind.Info);
            }

            await ServeAsync(project, build, entry, restarted);
            restarted = true;
        }
        while (_restartWebApp && !_disposed);
    }

    /// <summary>Serves one build of a web app until it stops.</summary>
    private async Task ServeAsync(ProjectInfo project, BuildResult build, CompiledAssembly entry, bool restarted)
    {
        var profile = LaunchSettings.Read(project.DirectoryPath);
        State = IdeState.Running;
        StatusText = restarted ? $"Restarting {project.Name}" : $"Starting {project.Name}";
        _run = new CancellationTokenSource();
        _webProject = project;
        RunResult result;
        try
        {
            var options = new WebAppOptions
            {
                Environment = profile.Environment ?? "Development",
                Arguments = profile.CommandLineArgs,
                Port = _webPort,
            };
            var packages = _language!.GetPackageRuntimeAssemblies(project.FilePath);
            result = await ServeUntilStoppedAsync(project, build, entry, packages, profile, options, _run.Token);
        }
        catch (OperationCanceledException)
        {
            result = new RunResult(RunOutcome.Stopped, -1, TimeSpan.Zero);
        }
        finally
        {
            _webProject = null;
            WebPreview.ShowStopped();
            _run.Dispose();
            _run = null;
        }

        if (_restartWebApp && result.Outcome != RunOutcome.Crashed)
        {
            Output.WriteLine();
            Output.WriteLine($"Restarting '{project.Name}' with the saved changes.", OutputKind.Info);
            return;
        }

        _restartWebApp = false;
        ReportRunResult(project, result);
    }

    private async Task<RunResult> ServeUntilStoppedAsync(
        ProjectInfo project,
        BuildResult build,
        CompiledAssembly entry,
        IReadOnlyList<string> packages,
        LaunchProfile profile,
        WebAppOptions options,
        CancellationToken cancellationToken)
    {
        await using var web = await WebAppHost.StartAsync(entry, build.Dependencies, packages, project.DirectoryPath, Output, options, cancellationToken);
        if (!web.IsRunning)
        {
            return await web.Completion;
        }

        _webPort = web.Address.Port;
        Output.WriteLine($"{project.Name} is listening on {web.Address}", OutputKind.Info);
        WebPreview.Show(project.Name, web.Address, profile.LaunchUrl);
        SelectSideBarPanel(SideBarPanel.WebPreview);
        StatusText = $"Running {project.Name} at {web.Address.GetLeftPart(UriPartial.Authority)}";

        Task<RunResult>? stopping = null;
        using (cancellationToken.Register(() => stopping = web.StopAsync()))
        {
            var result = await web.Completion;
            return stopping is null ? result : await stopping;
        }
    }

    /// <summary>
    /// Restarts the running web app when one of its source files is saved, or reloads its page when a file it
    /// serves as is, such as one in <c>wwwroot</c>, changes.
    /// </summary>
    private void OnDocumentSavedForWeb(DocumentViewModel document)
    {
        if (_webProject is not { } project || _run is not { } run
            || !document.FilePath.StartsWith(project.DirectoryPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return;
        }

        var relative = Path.GetRelativePath(project.DirectoryPath, document.FilePath);
        if (!RestartingExtensions.Contains(Path.GetExtension(document.FilePath))
            || relative.StartsWith("wwwroot" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            WebPreview.ReloadCommand.Execute(null);
            return;
        }

        _restartWebApp = true;
        run.Cancel();
    }
}
