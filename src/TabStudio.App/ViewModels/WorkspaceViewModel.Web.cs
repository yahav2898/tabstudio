using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging;
using TabStudio.Debugging.Building;

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
    private Task ServeWebAppAsync(ProjectInfo project, BuildResult build, CompiledAssembly entry, bool restarted) =>
        ServeWebAppAsync(project, build, entry, restarted, debugger: null);

    /// <summary>
    /// Serves one build of a web app until it stops; under the debugger, the build is the debug build and the app
    /// runs in the debug session, which stops with it.
    /// </summary>
    private async Task ServeWebAppAsync(ProjectInfo project, BuildResult build, CompiledAssembly entry, bool restarted, DebugSession? debugger)
    {
        var profile = LaunchSettings.Read(project.DirectoryPath);
        State = debugger is null ? IdeState.Running : IdeState.Debugging;
        StatusText = restarted ? $"Restarting {project.Name}" : debugger is null ? $"Starting {project.Name}" : $"Debugging {project.Name}";
        _run = new CancellationTokenSource();
        // Stopping the app (Shift+F5, or a save that restarts it) releases a request paused at a breakpoint first.
        using var releases = debugger is null ? default : _run.Token.Register(debugger.Stop);
        _liveProject = project;
        RunResult result;
        try
        {
            var options = new WebAppOptions
            {
                Environment = profile.Environment ?? "Development",
                EnvironmentVariables = _serviceEnvironment,
                Arguments = profile.CommandLineArgs,
                Port = _webPort,
            };
            var packages = _language!.GetPackageRuntimeAssemblies(project.FilePath);
            using var webApp = await PrepareWebAppAsync(project, build, profile, _run.Token);
            options = options with { Arguments = [.. webApp?.Arguments ?? [], .. options.Arguments] };
            _webApp = webApp;
            result = await ServeUntilStoppedAsync(project, build, entry, packages, profile, options, hotReload: debugger is null, _run.Token);
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
        bool hotReload,
        CancellationToken cancellationToken)
    {
        await using var web = await WebAppHost.StartAsync(entry, build.Dependencies, packages, project.DirectoryPath, Output, options,
            loaded: assembly => _ui.Post(() =>
            {
                if (hotReload && _run is not null && !cancellationToken.IsCancellationRequested) BeginHotReload(project, build, assembly);
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
        StatusText = State == IdeState.Debugging
            ? $"Debugging {project.Name} at {web.Address.GetLeftPart(UriPartial.Authority)}"
            : $"Running {project.Name} at {web.Address.GetLeftPart(UriPartial.Authority)}";

        Task<RunResult>? stopping = null;
        using (cancellationToken.Register(() => stopping = web.StopAsync()))
        {
            var result = await web.Completion;
            return stopping is null ? result : await stopping;
        }
    }
}

/// <summary>Web apps under the debugger: F5 serves the debug build of an ASP.NET Core app in a debug session.</summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>
    /// F5 on an ASP.NET Core or Blazor Web App: builds it for debugging and serves it in a debug session, so
    /// breakpoints in its endpoints, pages, controllers and services stop the request that reached them, with stepping,
    /// Locals, Watch, the Call Stack and the exception helper. An exception that leaves the app's code pauses there, as
    /// one Visual Studio calls unhandled by user code. Saving restarts it with a new debug build.
    /// </summary>
    /// <returns><see langword="false"/> when the app could not be built for debugging only because instrumenting it failed, so it should run without the debugger.</returns>
    private async Task<bool> DebugWebAppAsync(ProjectInfo project, bool restarted)
    {
        if (_language is not { } language)
        {
            return false;
        }

        DebugBuild? debugBuild = null;
        var built = await BuildProjectsAsync([project], showOutput: !restarted, async (target, token) =>
        {
            debugBuild = await language.BuildForDebuggingAsync(target, token);
            return debugBuild.Result;
        });
        if (built is not { Success: true } || debugBuild is not { Success: true, Result.EntryAssembly: { } entry } build)
        {
            if (built?.Diagnostics.Any(d => d.Id == DebugBuilder.InstrumentationFailedId) == true)
            {
                Output.WriteLine($"{project.Name} could not be built for debugging ({DebugBuilder.InstrumentationFailedId}), so it starts without the debugger.", OutputKind.Warning);
                return false;
            }

            // As for a run: nothing to show, and the run ends.
            await StopWpfHostAsync();
            WebPreview.ShowStopped();
            AppPreview.ShowStopped();
            _restartLiveApp = false;
            return true;
        }

        var session = new DebugSession(build) { BreakOnUnhandledExceptions = BreakOnUnhandledExceptions };
        session.Paused += OnSessionPaused;
        session.Resumed += OnSessionResumed;
        _session = session;
        _debuggedProject = project;
        Breakpoints.Bind(session);
        try
        {
            await session.RunHostedAsync(async stop =>
            {
                // Stop (Shift+F5) stops the debug session, and with it the app.
                using var stops = stop.Register(() => _ui.Post(() => _run?.Cancel()));
                await InvokeOnUiAsync(async () =>
                {
                    await ServeWebAppAsync(project, build.Result, entry, restarted, session);
                    return true;
                });
                return true;
            });
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            Output.WriteLine($"The debugger stopped: {ex.Message}", OutputKind.Failure);
        }
        finally
        {
            EndLiveAppDebugging(session);
        }

        // Restart (Ctrl+Shift+F5) stopped the session: the app is debugged again from a new build.
        if (_restartRequested)
        {
            _restartRequested = false;
            _restartLiveApp = true;
        }

        return true;
    }

    /// <summary>After a debugged live app stopped: the debugger's panels empty, as when a program ends.</summary>
    private void EndLiveAppDebugging(DebugSession session)
    {
        session.Paused -= OnSessionPaused;
        session.Resumed -= OnSessionResumed;
        session.Dispose();
        if (!ReferenceEquals(_session, session))
        {
            return;
        }

        _session = null;
        _debuggedProject = null;
        _pause = null;
        _exceptionHelper = null;
        _shownThread = null;
        Breakpoints.Unbind();
        CallStack.Clear();
        CallStack.ClearThreads();
        Locals.Clear();
        _ = Watch.EvaluateAsync(null);
        ShowExecutionPoints();
        if (State is IdeState.Debugging or IdeState.Paused)
        {
            State = IdeState.Ready;
        }

        if (SelectedPanel is BottomPanel.Locals or BottomPanel.Watch or BottomPanel.CallStack)
        {
            SelectedPanel = BottomPanel.Output;
        }
    }
}
