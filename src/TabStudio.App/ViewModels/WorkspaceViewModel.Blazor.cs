using System.Diagnostics;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Razor;
using TabStudio.Compilation.Running;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Web.RazorApps;
using TabStudio.Web.WebAssembly;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Blazor, Razor Pages and MVC. Before a web app starts, it gets what the SDK gives it in development (see
/// <see cref="RazorAppPreparer"/>): its static web assets, Blazor's scripts, its scoped CSS bundle and, for a Blazor
/// Web App with WebAssembly components, its client built for the browser. A Blazor WebAssembly app is served by the
/// <see cref="WebAssemblyAppHost"/> and runs in the web preview on .NET for WebAssembly.
/// </summary>
/// <remarks>
/// While an app runs, saving a scoped CSS file rebuilds the bundle and reloads the page; saving a file of a project the
/// app references, such as its WebAssembly client, restarts it as its own files do; saving a WebAssembly app's web
/// content serves it again and reloads the page.
/// </remarks>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The Error List keeps a web app's problems under its project file and this suffix, beside its restore's.</summary>
    private const string WebAppDiagnosticsSuffix = "#web";

    private PreparedServerApp? _webApp;
    private WebAssemblyAppHost? _webAssemblyHost;
    private WebAssemblyApp? _webAssemblyApp;
    private int? _webAssemblyPort;

    /// <summary>The host of the Blazor WebAssembly app being served, for the self test.</summary>
    internal WebAssemblyAppHost? RunningWebAssemblyApp => _webAssemblyHost;

    /// <summary>Whether F5 serves a project as a Blazor WebAssembly app, which runs in the web preview.</summary>
    private static bool IsWebAssemblyApp(ProjectInfo project) =>
        project.Kind == ProjectKind.BlazorWebAssembly
        && project.Language == ProjectLanguage.CSharp
        && !ProjectClassifier.IsBlazorWebAppClient(project);

    /// <summary>
    /// The app F5 runs for a project: the project itself, or for the WebAssembly client of a Blazor Web App, the app
    /// that references it, since the client runs inside it.
    /// </summary>
    private ProjectInfo AppToRun(ProjectInfo project)
    {
        if (!ProjectClassifier.IsBlazorWebAppClient(project) || Workspace is not { } workspace)
        {
            return project;
        }

        var app = workspace.Projects.FirstOrDefault(p => IsWebApp(p)
            && RazorAppPreparer.ReferencedProjects(p, workspace.Projects).Any(r => string.Equals(r.FilePath, project.FilePath, StringComparison.Ordinal)));
        if (app is null)
        {
            return project;
        }

        Output.WriteLine($"{project.Name} is the WebAssembly part of {app.Name}, so {app.Name} starts, with it.", OutputKind.Info);
        return app;
    }

    /// <summary>Gets a built ASP.NET Core app ready to start, reporting what went wrong in the Error List and the Output panel.</summary>
    /// <returns>The app's assets and arguments, or <see langword="null"/> when its assets could not be written.</returns>
    private async Task<PreparedServerApp?> PrepareWebAppAsync(ProjectInfo project, BuildResult build, LaunchProfile profile, CancellationToken cancellationToken)
    {
        try
        {
            var prepared = await new RazorAppPreparer(_compiler.Packages).PrepareServerAppAsync(
                RazorBuild(project, build, profile), new Progress<string>(text => StatusText = text), cancellationToken);
            ReportWebAppDiagnostics(project, prepared.Diagnostics);
            if (prepared.HasWebAssemblyClient)
            {
                Output.WriteLine($"The WebAssembly components of {project.Name} run in the web preview on .NET for WebAssembly, without the debugger.", OutputKind.Info);
            }

            return prepared;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Output.WriteLine($"The static web assets of {project.Name} could not be written: {ex.Message}", OutputKind.Failure);
            return null;
        }
    }

    /// <summary>Serves one build of a Blazor WebAssembly app until it is stopped.</summary>
    private async Task ServeWebAssemblyAppAsync(ProjectInfo project, BuildResult build, CompiledAssembly entry, bool restarted)
    {
        var profile = LaunchSettings.Read(project.DirectoryPath);
        State = IdeState.Running;
        StatusText = restarted ? $"Restarting {project.Name}" : $"Starting {project.Name}";
        _run = new CancellationTokenSource();
        _liveProject = project;
        var clock = Stopwatch.StartNew();
        RunResult result;
        try
        {
            var prepared = await new RazorAppPreparer(_compiler.Packages).PrepareWebAssemblyAppAsync(
                RazorBuild(project, build, profile), new Progress<string>(text => StatusText = text), _run.Token);
            ReportWebAppDiagnostics(project, prepared.Diagnostics);
            if (prepared.App is not { } app)
            {
                var reason = prepared.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverityLevel.Error)?.Message ?? "it could not be put together.";
                Output.WriteLine($"{entry.Name} could not start: {reason}", OutputKind.Failure);
                result = new RunResult(RunOutcome.Crashed, -1, clock.Elapsed);
            }
            else
            {
                await using var host = await WebAssemblyAppHost.StartAsync(app, _webAssemblyPort, _run.Token);
                _webAssemblyHost = host;
                _webAssemblyApp = app;
                _webAssemblyPort = host.Address.Port;
                Output.WriteLine($"{project.Name} is served at {host.Address} and runs in the web preview on .NET for WebAssembly {app.Runtime.Version}.", OutputKind.Info);
                WebPreview.Show(project.Name, host.Address, profile.LaunchUrl);
                OnWebAppStarted(project);
                SelectSideBarPanel(SideBarPanel.WebPreview);
                StatusText = $"Running {project.Name} at {host.Address.GetLeftPart(UriPartial.Authority)}";
                await Task.Delay(Timeout.Infinite, _run.Token);
                result = new RunResult(RunOutcome.Stopped, -1, clock.Elapsed);
            }
        }
        catch (OperationCanceledException)
        {
            result = new RunResult(RunOutcome.Stopped, -1, clock.Elapsed);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Output.WriteLine($"{project.Name} could not be served: {ex.Message}", OutputKind.Failure);
            result = new RunResult(RunOutcome.Crashed, -1, clock.Elapsed, ex);
        }
        finally
        {
            _webAssemblyHost = null;
            _webAssemblyApp = null;
            _liveProject = null;
            WebPreview.ShowStopped();
            OnWebAppStopped();
            _run.Dispose();
            _run = null;
        }

        FinishLiveRun(project, result);
    }

    /// <summary>
    /// Handles a saved file of the running web app that the general rule of live apps does not: scoped CSS, which is
    /// bundled; the files of the projects the app references; and a WebAssembly app's web content.
    /// </summary>
    /// <returns>Whether the save was handled.</returns>
    private bool OnDocumentSavedForRazorApp(DocumentViewModel document)
    {
        if (_liveProject is not { } project || _run is not { } run || Workspace is not { } workspace
            || !(IsWebApp(project) || IsWebAssemblyApp(project))
            || workspace.FindProjectForFile(document.FilePath) is not { } owner)
        {
            return false;
        }

        var path = document.FilePath;
        var isApp = string.Equals(owner.FilePath, project.FilePath, StringComparison.Ordinal);
        if (!isApp && !RazorAppPreparer.ReferencedProjects(project, workspace.Projects).Any(r => string.Equals(r.FilePath, owner.FilePath, StringComparison.Ordinal)))
        {
            return false;
        }

        // A server app's bundle is rebuilt in place; a WebAssembly app's comes with its next build.
        if (IsScopedCss(path))
        {
            if (_webApp is { } webApp)
            {
                _ = RefreshScopedCssAsync(project, webApp);
            }
            else
            {
                Restart(run);
            }

            return true;
        }

        var relative = Path.GetRelativePath(owner.DirectoryPath, path);
        var isWebContent = relative.StartsWith("wwwroot" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (isApp)
        {
            if (IsWebAssemblyApp(project) && isWebContent)
            {
                _ = ServeWebContentAgainAsync();
                return true;
            }

            // Otherwise the general rule: a source file restarts the app, a file it serves as is reloads the page.
            return false;
        }

        // A referenced project, such as a Blazor Web App's client: its source restarts the app, its web content reloads.
        if (!isWebContent && RestartingExtensions.Contains(Path.GetExtension(path)))
        {
            Restart(run);
        }
        else
        {
            WebPreview.ReloadCommand.Execute(null);
        }

        return true;
    }

    private void Restart(CancellationTokenSource run)
    {
        _restartLiveApp = true;
        run.Cancel();
    }

    /// <summary>A component's or view's stylesheet, such as <c>Counter.razor.css</c>.</summary>
    private static bool IsScopedCss(string path) =>
        path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) && RazorProjects.IsRazorFile(path[..^".css".Length]);

    /// <summary>Rebuilds a running app's scoped CSS bundles, then reloads the page to show them.</summary>
    private async Task RefreshScopedCssAsync(ProjectInfo project, PreparedServerApp webApp)
    {
        try
        {
            var diagnostics = await Task.Run(() => webApp.Assets.Refresh());
            ReportWebAppDiagnostics(project, diagnostics);
            WebPreview.ReloadCommand.Execute(null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Output.WriteLine($"The scoped CSS of {project.Name} could not be bundled: {ex.Message}", OutputKind.Failure);
        }
    }

    /// <summary>Serves a WebAssembly app's web content again, with its start page's placeholders, then reloads the page.</summary>
    private async Task ServeWebContentAgainAsync()
    {
        if (_webAssemblyHost is not { } host || _webAssemblyApp is not { } app)
        {
            return;
        }

        try
        {
            await host.UpdateAsync(app);
            WebPreview.ReloadCommand.Execute(null);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Output.WriteLine($"The web content could not be served again: {ex.Message}", OutputKind.Failure);
        }
    }

    /// <summary>Shows the problems of getting a web app ready in the Error List, replacing its previous ones, and in the Output panel.</summary>
    private void ReportWebAppDiagnostics(ProjectInfo project, IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        ErrorList.SetRestoreDiagnostics(project.FilePath + WebAppDiagnosticsSuffix, diagnostics);
        foreach (var diagnostic in diagnostics.Where(d => d.Severity >= DiagnosticSeverityLevel.Warning))
        {
            Output.WriteLine(
                $"{project.Name}: {diagnostic}",
                diagnostic.Severity == DiagnosticSeverityLevel.Error ? OutputKind.Failure : OutputKind.Program);
        }
    }

    private RazorAppBuild RazorBuild(ProjectInfo project, BuildResult build, LaunchProfile profile)
    {
        var language = _language ?? throw new InvalidOperationException("The workspace is closed.");
        return new RazorAppBuild
        {
            Project = project,
            Projects = Workspace?.Projects ?? [project],
            Build = build,
            PackageAssemblies = language.GetPackageRuntimeAssemblies,
            Environment = profile.Environment ?? "Development",
        };
    }
}
