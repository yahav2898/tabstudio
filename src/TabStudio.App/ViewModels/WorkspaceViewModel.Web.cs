using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Web apps: F5 on an ASP.NET Core project serves it on the device and shows it in the web preview. Like every
/// live app, it restarts when one of its files is saved (see <c>WorkspaceViewModel.LiveApps.cs</c>).
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private int? _webPort;

    /// <summary>The web preview, which shows the running web app.</summary>
    public WebPreviewViewModel WebPreview { get; }

    /// <summary>Whether the side bar shows the web preview.</summary>
    public bool IsWebPreviewSelected => SelectedSideBarPanel == SideBarPanel.WebPreview;

    /// <summary>Whether F5 serves a project as a web app rather than running it as a program.</summary>
    private static bool IsWebApp(ProjectInfo project) => project.Kind is ProjectKind.AspNetCore or ProjectKind.BlazorWebApp;

    /// <summary>Serves one build of a web app until it stops.</summary>
    private async Task ServeWebAppAsync(ProjectInfo project, BuildResult build, CompiledAssembly entry, bool restarted)
    {
        var profile = LaunchSettings.Read(project.DirectoryPath);
        State = IdeState.Running;
        StatusText = restarted ? $"Restarting {project.Name}" : $"Starting {project.Name}";
        _run = new CancellationTokenSource();
        _liveProject = project;
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
            using var webApp = await PrepareWebAppAsync(project, build, profile, _run.Token);
            options = options with { Arguments = [.. webApp?.Arguments ?? [], .. options.Arguments] };
            _webApp = webApp;
            result = await ServeUntilStoppedAsync(project, build, entry, packages, profile, options, _run.Token);
        }
        catch (OperationCanceledException)
        {
            result = new RunResult(RunOutcome.Stopped, -1, TimeSpan.Zero);
        }
        finally
        {
            EndHotReload();
            _webApp = null;
            _liveProject = null;
            WebPreview.ShowStopped();
            OnWebAppStopped();
            _run.Dispose();
            _run = null;
        }

        FinishLiveRun(project, result);
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
        await using var web = await WebAppHost.StartAsync(entry, build.Dependencies, packages, project.DirectoryPath, Output, options,
            loaded: assembly => _ui.Post(() =>
            {
                if (_run is not null && !cancellationToken.IsCancellationRequested) BeginHotReload(project, build, assembly);
            }), cancellationToken);
        if (!web.IsRunning)
        {
            return await web.Completion;
        }

        _webPort = web.Address.Port;
        Output.WriteLine($"{project.Name} is listening on {web.Address}", OutputKind.Info);
        WebPreview.Show(project.Name, web.Address, profile.LaunchUrl);
        OnWebAppStarted(project);
        SelectSideBarPanel(SideBarPanel.WebPreview);
        StatusText = $"Running {project.Name} at {web.Address.GetLeftPart(UriPartial.Authority)}";

        Task<RunResult>? stopping = null;
        using (cancellationToken.Register(() => stopping = web.StopAsync()))
        {
            var result = await web.Completion;
            return stopping is null ? result : await stopping;
        }
    }
}
