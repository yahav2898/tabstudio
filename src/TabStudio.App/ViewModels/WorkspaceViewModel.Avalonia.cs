using System.Diagnostics;
using TabStudio.App.Previews;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Avalonia apps: F5 shows the app's main view in the app preview, rendered natively beside the code by
/// TabStudio's own Avalonia. Like every live app, it restarts when one of its files is saved.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The app preview, which shows the running Avalonia app.</summary>
    public AppPreviewViewModel AppPreview { get; } = new();

    /// <summary>Whether the side bar shows the app preview.</summary>
    public bool IsAppPreviewSelected => SelectedSideBarPanel == SideBarPanel.AppPreview;

    /// <summary>Whether F5 shows a project in the app preview rather than running it as a program.</summary>
    private static bool IsAvaloniaApp(ProjectInfo project) => project.Kind == ProjectKind.Avalonia && project.Language == ProjectLanguage.CSharp;

    /// <summary>Shows one build of an Avalonia app until it is stopped or crashes.</summary>
    private async Task ShowAvaloniaAppAsync(ProjectInfo project, BuildResult build, CompiledAssembly entry, bool restarted)
    {
        State = IdeState.Running;
        StatusText = restarted ? $"Restarting {project.Name}" : $"Starting {project.Name}";
        _run = new CancellationTokenSource();
        _liveProject = project;
        ErrorList.SetRunError(Path.GetDirectoryName(project.FilePath)!, null);
        var clock = Stopwatch.StartNew();
        ConsoleLease? console = null;
        RunEnvironment? environment = null;
        AvaloniaAppHost? app = null;
        IReadOnlyList<string> packages = [];
        RunResult result;
        try
        {
            // The app's Console.WriteLine goes to the Output panel while it runs.
            console = await ProcessConsole.AcquireAsync(_run.Token);
            console.Redirect(Output);
            environment = new RunEnvironment(_serviceEnvironment);
            packages = _language!.GetPackageRuntimeAssemblies(project.FilePath);
            app = AvaloniaAppHost.Start(entry, build.Dependencies, packages);
            BeginHotReload(project, build, app.EntryAssembly, avalonia: app);
            var crashed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            app.Crashed += (_, exception) => crashed.TrySetResult(exception);

            AppPreview.Show(project.Name, app.View, app.Note);
            OnAvaloniaAppStarted(project, app);
            SelectSideBarPanel(SideBarPanel.AppPreview);
            StatusText = $"Running {project.Name}";
            Output.WriteLine($"{project.Name} is running in the app preview.", OutputKind.Info);

            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (_run.Token.Register(() => stopped.TrySetResult()))
            {
                if (await Task.WhenAny(stopped.Task, crashed.Task) == crashed.Task)
                {
                    var exception = await crashed.Task;
                    Output.WriteLine($"Unhandled exception. {exception}", OutputKind.Error);
                    result = new RunResult(RunOutcome.Crashed, -1, clock.Elapsed, exception);
                }
                else
                {
                    result = new RunResult(RunOutcome.Completed, 0, clock.Elapsed);
                }
            }
        }
        catch (OperationCanceledException)
        {
            result = new RunResult(RunOutcome.Stopped, -1, clock.Elapsed);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // XAML that does not load, or an exception in the app's startup code.
            // The message first, then where it happened, as for a crash.
            Output.WriteLine($"{project.Name} could not start: {ex.Message}", OutputKind.Error);
            if (AvaloniaAppHost.NewerAvaloniaNote(packages) is { } newer)
            {
                // The reason, in one line, before the exception it explains.
                Output.WriteLine(newer, OutputKind.Failure);
                ErrorList.SetRunError(Path.GetDirectoryName(project.FilePath)!, new TabStudio.Core.Diagnostics.BuildDiagnostic(
                    AvaloniaAppHost.NewerAvaloniaId, TabStudio.Core.Diagnostics.DiagnosticSeverityLevel.Error, newer, null, project.Name));
            }

            Output.WriteLine(ex.ToString(), OutputKind.Error);
            result = new RunResult(RunOutcome.Crashed, -1, clock.Elapsed, ex);
        }
        finally
        {
            EndHotReload();
            _liveProject = null;
            OnAvaloniaAppStopping();
            AppPreview.ShowStopped();
            app?.Dispose();
            environment?.Dispose();
            console?.Dispose();
            _run.Dispose();
            _run = null;
        }

        FinishLiveRun(project, result);
    }
}
