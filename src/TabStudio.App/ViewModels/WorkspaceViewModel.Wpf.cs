using System.Diagnostics;
using System.Text.Json;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Web.RazorApps;
using TabStudio.Web.Serving;
using TabStudio.Web.WebAssembly;
using TabStudio.Wpf;
using TabStudio.Wpf.Browser;

namespace TabStudio.App.ViewModels;

/// <summary>
/// WPF apps, which build on OpenSilver: F5 builds the app with OpenSilver's XAML compiler, gets its browser host
/// ready (see <see cref="WpfAppPreparer"/>) and serves it to the web preview, where it runs on .NET for WebAssembly
/// with its first window under its title, shown whole (Fit), at its size (100%) or filling the pane (Fill) as chosen for
/// the project in the preview's toolbar. What the app writes to its console, and its exceptions,
/// come to the Output panel. Saving one of its files builds it again and serves the new build at the same address;
/// the window's close button stops it.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The Error List keeps a WPF app's problems of getting it ready under its project file and this suffix.</summary>
    private const string WpfAppDiagnosticsSuffix = "#wpf";

    /// <summary>What .NET for WebAssembly and Blazor write to the console that is about them, not the app.</summary>
    private static readonly string[] WpfRuntimeNoise = ["Debugging hotkey:", "dotnet Loaded ", "MONO_WASM:", "[blazor]"];

    private WebAssemblyAppHost? _wpfHost;
    private bool _wpfWindowClosed;

    /// <summary>The host of the WPF app being served, for the self test and the tests.</summary>
    internal WebAssemblyAppHost? RunningWpfApp => _wpfHost;

    /// <summary>Whether F5 serves a project as a WPF app, which runs in the web preview on OpenSilver.</summary>
    private static bool IsWpfApp(ProjectInfo project) =>
        WpfProject.UsesWpf(project) && project.IsExecutable && project.Language == ProjectLanguage.CSharp;

    /// <summary>
    /// Serves one build of a WPF app until it is stopped or restarted. A restart keeps the server and serves the new
    /// build at the same address; the page reloads.
    /// </summary>
    private async Task ServeWpfAppAsync(ProjectInfo project, BuildResult build, CompiledAssembly entry, bool restarted)
    {
        State = IdeState.Running;
        StatusText = restarted ? $"Restarting {project.Name}" : $"Starting {project.Name}";
        _run = new CancellationTokenSource();
        _liveProject = project;
        _wpfWindowClosed = false;
        var clock = Stopwatch.StartNew();
        RunResult result;
        try
        {
            var references = await _compiler.GetReferencesAsync();
            var prepared = await new WpfAppPreparer(_compiler.Packages).PrepareAsync(
                project, build, references, new Progress<string>(text => StatusText = text), _run.Token);
            ErrorList.SetRestoreDiagnostics(project.FilePath + WpfAppDiagnosticsSuffix, prepared.Diagnostics);
            if (prepared.App is not { } app)
            {
                var reason = prepared.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverityLevel.Error)?.Message ?? "it could not be put together.";
                Output.WriteLine($"{entry.Name} could not start: {reason}", OutputKind.Failure);
                result = new RunResult(RunOutcome.Crashed, -1, clock.Elapsed);
            }
            else
            {
                await ServeAsync(project, app, restarted);
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
            _liveProject = null;
            _run.Dispose();
            _run = null;
        }

        // A restart serves the next build from the same server; anything else stops it.
        if (!_restartLiveApp || result.Outcome == RunOutcome.Crashed || _disposed)
        {
            await StopWpfHostAsync();
            WebPreview.ShowStopped();
        }

        if (_wpfWindowClosed)
        {
            Output.WriteLine($"The window of '{project.Name}' was closed.", OutputKind.Info);
        }

        FinishLiveRun(project, result);
    }

    /// <summary>Serves a build: a new server the first time, the same one with the new build on a restart.</summary>
    private async Task ServeAsync(ProjectInfo project, WebAssemblyApp app, bool restarted)
    {
        var token = _run?.Token ?? CancellationToken.None;
        if (restarted && _wpfHost is { } running)
        {
            await running.UpdateAsync(app, token);
            Output.WriteLine($"{project.Name} is served again at {running.Address}, with the new build.", OutputKind.Info);
            WebPreview.ShowWpfApp(project.Name, running.Address, project.FilePath);
            WebPreview.ReloadCommand.Execute(null);
        }
        else
        {
            await StopWpfHostAsync();
            var host = await WebAssemblyAppHost.StartAsync(app, _webAssemblyPort, token);
            host.MessageReceived += OnWpfMessage;
            _wpfHost = host;
            _webAssemblyPort = host.Address.Port;
            Output.WriteLine(
                $"{project.Name} is served at {host.Address} and runs in the web preview on OpenSilver {OpenSilverVersion(app)} and .NET for WebAssembly {app.Runtime.Version}.",
                OutputKind.Info);
            WebPreview.ShowWpfApp(project.Name, host.Address, project.FilePath);
        }

        SelectSideBarPanel(SideBarPanel.WebPreview);
        StatusText = $"Running {project.Name} at {_wpfHost!.Address.GetLeftPart(UriPartial.Authority)}";
    }

    private async Task StopWpfHostAsync()
    {
        if (_wpfHost is not { } host)
        {
            return;
        }

        _wpfHost = null;
        host.MessageReceived -= OnWpfMessage;
        await host.DisposeAsync();
    }

    /// <summary>
    /// What the app's page posts: its console output and uncaught errors for the Output panel, and its window's close
    /// button, which stops it. Called on a thread-pool thread.
    /// </summary>
    private void OnWpfMessage(object? sender, PostedMessage message)
    {
        switch (message.Route)
        {
            case WpfBrowserApps.ConsoleRoute:
                foreach (var (level, text) in ReadConsole(message.Body))
                {
                    if (level != "debug" && !WpfRuntimeNoise.Any(noise => text.StartsWith(noise, StringComparison.Ordinal)))
                    {
                        Output.WriteLine(text, level == "error" ? OutputKind.Error : OutputKind.Program);
                    }
                }

                break;
            case WpfBrowserApps.CloseRoute:
                _ui.Post(() =>
                {
                    if (_run is { } run && ReferenceEquals(sender, _wpfHost))
                    {
                        _wpfWindowClosed = true;
                        run.Cancel();
                    }
                });
                break;
        }
    }

    /// <summary>The console lines a page posted: <c>[{ "level": "log", "text": "…" }]</c>.</summary>
    private static List<(string Level, string Text)> ReadConsole(string body)
    {
        var lines = new List<(string, string)>();
        try
        {
            using var json = JsonDocument.Parse(body);
            foreach (var entry in json.RootElement.EnumerateArray())
            {
                if (entry.TryGetProperty("text", out var text) && text.GetString() is { } value)
                {
                    lines.Add((entry.TryGetProperty("level", out var level) ? level.GetString() ?? "log" : "log", value));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // Not what TabStudio's page posts; nothing to show.
        }

        return lines;
    }

    /// <summary>The version of OpenSilver an app runs on, from the folder of its OpenSilver.dll.</summary>
    private static string OpenSilverVersion(WebAssemblyApp app) =>
        app.Assemblies.FirstOrDefault(a => a.Name == "OpenSilver")?.FilePath is { } path && Directory.GetParent(path)?.Parent?.Parent is { } package
            ? package.Name
            : OpenSilverPackages.Version;

    /// <summary>
    /// Restarts a running WPF app when a file of a project it references is saved; its own files restart it by the
    /// general rule of live apps.
    /// </summary>
    /// <returns>Whether the save was handled.</returns>
    private bool OnDocumentSavedForWpfApp(DocumentViewModel document)
    {
        if (_liveProject is not { } project || _run is not { } run || !IsWpfApp(project) || Workspace is not { } workspace
            || workspace.FindProjectForFile(document.FilePath) is not { } owner
            || string.Equals(owner.FilePath, project.FilePath, StringComparison.Ordinal)
            || !RestartingExtensions.Contains(Path.GetExtension(document.FilePath))
            || !RazorAppPreparer.ReferencedProjects(project, workspace.Projects).Any(r => string.Equals(r.FilePath, owner.FilePath, StringComparison.Ordinal)))
        {
            return false;
        }

        Restart(run);
        return true;
    }
}
