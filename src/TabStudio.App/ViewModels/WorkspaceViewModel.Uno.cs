using System.Diagnostics;
using Avalonia.Styling;
using TabStudio.App.Previews;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Uno.Hosting;

namespace TabStudio.App.ViewModels;

/// <summary>WinUI and Uno apps, compiled on the tablet and drawn in-process by Uno's Skia renderer.</summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>Whether a C# executable uses Uno's implementation of WinUI.</summary>
    private static bool IsUnoApp(ProjectInfo project) =>
        project.Language == ProjectLanguage.CSharp && project.IsExecutable && project.Kind is ProjectKind.Uno or ProjectKind.WinUI;

    /// <summary>Runs one build until Stop, a save, or an unhandled exception.</summary>
    private async Task ShowUnoAppAsync(ProjectInfo project, BuildResult build, CompiledAssembly entry, bool restarted)
    {
        State = IdeState.Running;
        StatusText = restarted ? $"Restarting {project.Name}" : $"Starting {project.Name}";
        _run = new CancellationTokenSource();
        _liveProject = project;
        var clock = Stopwatch.StartNew();
        ConsoleLease? console = null;
        UnoRuntime? runtime = null;
        UnoApp? app = null;
        UnoPreviewControl? preview = null;
        RunResult result;
        void Log(object? sender, UnoLogEventArgs message) => _ui.Post(() =>
            Output.WriteLine(message.Exception is { } exception ? $"{message.Message} {exception}" : message.Message,
                message.Level == UnoLogLevel.Error ? OutputKind.Error : OutputKind.Info));
        try
        {
            console = await ProcessConsole.AcquireAsync(_run.Token);
            console.Redirect(Output);
            var assets = UnoRuntimeAssets.From(_language!.GetPackageRuntimeAssemblies(project.FilePath));
            runtime = await UnoRuntime.StartAsync(assets, new UnoRuntimeOptions
            {
                CacheDirectory = Path.Combine(_paths.Cache, "Uno"),
                DarkTheme = global::Avalonia.Application.Current?.ActualThemeVariant != ThemeVariant.Light,
            }, _run.Token);
            runtime.Logged += Log;
            app = await runtime.LaunchAsync(project, entry, build.Dependencies, assets, _run.Token);
            var crashed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            app.UnhandledException += (_, e) => crashed.TrySetResult((Exception)e.ExceptionObject);
            preview = new UnoPreviewControl(runtime) { App = app };
            AppPreview.Show(project.Name, preview, "Runs on Uno Platform's Skia renderer. Windows-only APIs require the Windows Bridge.");
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
            Output.WriteLine($"{project.Name} could not start: {ex.Message}", OutputKind.Error);
            Output.WriteLine(ex.ToString(), OutputKind.Error);
            result = new RunResult(RunOutcome.Crashed, -1, clock.Elapsed, ex);
        }
        finally
        {
            _liveProject = null;
            AppPreview.ShowStopped();
            preview?.Dispose();
            if (runtime is not null)
            {
                runtime.Logged -= Log;
            }

            if (app is not null)
            {
                await app.DisposeAsync();
            }

            console?.Dispose();
            _run.Dispose();
            _run = null;
        }

        FinishLiveRun(project, result);
    }
}
