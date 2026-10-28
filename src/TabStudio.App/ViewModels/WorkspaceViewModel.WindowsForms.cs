using System.Diagnostics;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Compilation.WindowsForms;
using TabStudio.Core.Workspaces;
using TabStudio.Forms.Hosting;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Windows Forms apps: F5 runs the app on TabStudio's Forms layer and shows its windows in the app preview, in
/// Windows 11 frames, beside the code. The app has its own UI thread with its own message loop, so ShowDialog and
/// MessageBox.Show wait as on Windows; closing the main form ends the run, and an exception the app does not handle
/// ends the app, never TabStudio. Like every live app, it restarts when one of its files is saved.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>How long a stopped app gets to close its windows and return from Main.</summary>
    private static readonly TimeSpan WindowsFormsStopTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Whether F5 shows a project in the app preview as a Windows Forms app.</summary>
    private static bool IsWindowsFormsApp(ProjectInfo project) =>
        project.IsRoslynLanguage && project.IsExecutable
        && (project.Kind == ProjectKind.WinForms || WindowsFormsProject.UsesWindowsForms(project));

    /// <summary>Shows one build of a Windows Forms app until it ends, is stopped or crashes.</summary>
    private async Task ShowWindowsFormsAppAsync(ProjectInfo project, BuildResult build, CompiledAssembly entry, bool restarted)
    {
        State = IdeState.Running;
        StatusText = restarted ? $"Restarting {project.Name}" : $"Starting {project.Name}";
        _run = new CancellationTokenSource();
        _liveProject = project;
        var clock = Stopwatch.StartNew();
        ConsoleLease? console = null;
        RunEnvironment? environment = null;
        LoadedProgram? program = null;
        FormsHost? host = null;
        RunResult result;
        try
        {
            // The app's Console.WriteLine goes to the Output panel while it runs.
            console = await ProcessConsole.AcquireAsync(_run.Token);
            console.Redirect(Output);
            environment = new RunEnvironment(_serviceEnvironment);

            // The app's forms are TabStudio's Forms layer's types, which draw on the preview's desktop.
            program = LoadedProgram.Load(entry, build.Dependencies, _language!.GetPackageRuntimeAssemblies(project.FilePath), IsSharedWithWindowsForms);
            if (program.EntryAssembly.EntryPoint is not { } entryPoint)
            {
                result = new RunResult(RunOutcome.NoEntryPoint, -1, clock.Elapsed);
            }
            else
            {
                host = FormsHost.Start(FormsHost.RunEntryPoint(entryPoint), program.EntryAssembly, project.DirectoryPath);
                AppPreview.Show(project.Name, host.View, note: null);
                SelectSideBarPanel(SideBarPanel.AppPreview);
                StatusText = $"Running {project.Name}";
                Output.WriteLine($"{project.Name} is running in the app preview.", OutputKind.Info);
                result = await WaitForWindowsFormsAppAsync(host, clock, _run.Token);
            }
        }
        catch (OperationCanceledException)
        {
            result = new RunResult(RunOutcome.Stopped, -1, clock.Elapsed);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An assembly the app needs that does not load. The message first, then where it happened, as for a crash.
            Output.WriteLine($"{project.Name} could not start: {ex.Message}", OutputKind.Error);
            Output.WriteLine(ex.ToString(), OutputKind.Error);
            result = new RunResult(RunOutcome.Crashed, -1, clock.Elapsed, ex);
        }
        finally
        {
            _liveProject = null;
            AppPreview.ShowStopped();
            host?.Dispose();
            program?.Dispose();
            environment?.Dispose();
            console?.Dispose();
            _run.Dispose();
            _run = null;
        }

        FinishLiveRun(project, result);
    }

    /// <summary>Waits until the app's Main returns or throws, or the run is stopped, which closes the app's windows.</summary>
    private async Task<RunResult> WaitForWindowsFormsAppAsync(FormsHost host, Stopwatch clock, CancellationToken stop)
    {
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (stop.Register(() => stopped.TrySetResult()))
        {
            if (await Task.WhenAny(stopped.Task, host.Completion) != host.Completion)
            {
                host.Stop();
                await Task.WhenAny(host.Completion, Task.Delay(WindowsFormsStopTimeout, CancellationToken.None));
                return new RunResult(RunOutcome.Stopped, -1, clock.Elapsed);
            }
        }

        var exit = await host.Completion;
        if (exit.Restart)
        {
            // Application.Restart: the app runs again, built and loaded afresh, as a new process would be.
            _restartLiveApp = true;
            _restartRequestedByApp = true;
        }

        if (exit.Error is { } error)
        {
            Output.WriteLine($"Unhandled exception. {error}", OutputKind.Error);
            return new RunResult(RunOutcome.Crashed, -1, clock.Elapsed, error);
        }

        return new RunResult(exit.Stopped ? RunOutcome.Stopped : RunOutcome.Completed, exit.ExitCode, clock.Elapsed);
    }

    /// <summary>The Forms layer and Avalonia come from TabStudio, so the app's forms are drawn by the preview.</summary>
    private static bool IsSharedWithWindowsForms(string assemblyName) =>
        assemblyName == "TabStudio.Forms" || Previews.AvaloniaAppHost.IsSharedWithHost(assemblyName);
}
