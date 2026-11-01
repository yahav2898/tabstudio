using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Previews;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.HotReload;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;

namespace TabStudio.App.ViewModels;

/// <summary>C# Hot Reload and Edit and Continue for locally loaded Debug assemblies.</summary>
public sealed partial class WorkspaceViewModel
{
    private HotReloadSession? _hotReload;
    private ProjectInfo? _hotReloadProject;
    private DebugBuild? _hotReloadDebugBuild;
    private AvaloniaAppHost? _hotReloadAvalonia;
    private CancellationTokenSource? _hotReloadLifetime;
    private readonly SemaphoreSlim _hotReloadGate = new(1, 1);
    private bool _restartConsoleForHotReload;

    /// <summary>Automatically applies C# edits when a document is saved. Off until selected in the Debug menu.</summary>
    [ObservableProperty]
    public partial bool HotReloadOnSave { get; set; }

    /// <summary>Whether unsupported edits are waiting for a restart, exposing the toolbar's Restart action.</summary>
    [ObservableProperty]
    public partial bool HotReloadNeedsRestart { get; private set; }

    /// <summary>The number of successful metadata updates in the current run.</summary>
    [ObservableProperty]
    public partial int HotReloadGeneration { get; private set; }

    /// <summary>Alt+F10: applies the editor's current text without restarting the app or saving its documents.</summary>
    [RelayCommand(CanExecute = nameof(CanHotReload))]
    private async Task ApplyHotReloadAsync() => await ApplyCurrentEditsAsync();

    private bool CanHotReload() => HotReloadSession.IsSupported && (IsRunning || (_session is not null && IsDebugging));

    private void BeginHotReload(ProjectInfo project, BuildResult build, Assembly assembly, DebugBuild? debugBuild = null, AvaloniaAppHost? avalonia = null)
    {
        // Keep the source-image snapshot for recording guards even when this runtime cannot apply deltas.
        if (_disposed || build.EntryAssembly?.HotReloadCompilation is null) return;
        EndHotReload();
        _hotReloadProject = project;
        _hotReloadDebugBuild = debugBuild;
        _hotReloadAvalonia = avalonia;
        _hotReloadLifetime = new CancellationTokenSource();
        _hotReload = new HotReloadSession(build.EntryAssembly, build.Dependencies, assembly);
        ApplyHotReloadCommand.NotifyCanExecuteChanged();
    }

    private void EndHotReload()
    {
        _hotReloadLifetime?.Cancel();
        _hotReloadLifetime?.Dispose();
        _hotReloadLifetime = null;
        _hotReload?.Dispose();
        _hotReload = null;
        _hotReloadProject = null;
        _hotReloadDebugBuild = null;
        _hotReloadAvalonia = null;
        HotReloadNeedsRestart = false;
        HotReloadGeneration = 0;
    }

    private async Task<bool> ApplyCurrentEditsAsync()
    {
        if (!HotReloadSession.IsSupported && _session?.Recording is null)
        {
            ShowHotReloadResult(new(HotReloadStatus.Unsupported, HotReloadSession.UnsupportedReason, []));
            return false;
        }

        if (_hotReload is not { } session || _hotReloadProject is not { } project || _language is not { } language
            || _hotReloadLifetime is not { } lifetime)
        {
            ShowHotReloadResult(new(HotReloadStatus.Unsupported, "Hot Reload needs a local C# Debug app running in TabStudio. Browser WebAssembly and remote runs use a separate runtime; restart those apps to apply edits.", []));
            return false;
        }

        var token = lifetime.Token;
        var entered = false;
        try
        {
            await _hotReloadGate.WaitAsync(token);
            entered = true;
            // Text-change analysis is debounced. Apply the visible editor buffers now so Alt+F10 and Continue
            // always compile the current edits, including the last keystroke and unsaved referenced projects.
            foreach (var document in Documents.Where(d => language.ContainsDocument(d.FilePath)))
            {
                language.UpdateText(document.FilePath, document.CurrentText);
            }

            DebugBuild? debugBuild = null;
            BuildResult build;
            if (_hotReloadDebugBuild is not null)
            {
                debugBuild = await language.BuildForDebuggingAsync(project, token);
                build = debugBuild.Result;
            }
            else
            {
                build = await language.BuildAsync(project, token);
            }

            if (!ReferenceEquals(session, _hotReload) || token.IsCancellationRequested) return false;
            ErrorList.SetBuildResult(build);
            if (!build.Success)
            {
                ShowHotReloadResult(new(HotReloadStatus.CompilationErrors, "Fix the compilation errors before applying edits. The app is still using its previous code.", build.Diagnostics));
                return false;
            }

            var images = build.Dependencies.Append(build.EntryAssembly!).ToArray();
            if (_session?.Recording is not null)
            {
                // A recording resolves all retained events through its original immutable source map. Even a
                // line-only edit needs a new recording; ordinary Continue with unchanged code must still work.
                var changed = session.HasSourceOrResourceChanges(images);
                ShowHotReloadResult(changed
                    ? new(HotReloadStatus.RestartRequired, "C# edits require a restart while recording. Start a new recording to use the edits; the current history keeps its original source locations.", [])
                    : new(HotReloadStatus.NoChanges, "No C# edits to apply to the current recording.", []));
                return !changed;
            }

            if (debugBuild is not null && !SameDebugLayout(_hotReloadDebugBuild!, debugBuild))
            {
                ShowHotReloadResult(new(HotReloadStatus.RestartRequired, "Changing the number of instrumented statements, methods or local slots requires a restart to keep breakpoints and active frames correct.", []));
                return false;
            }

            var result = await Task.Run(() => session.Apply(images, token), token);
            if (!ReferenceEquals(session, _hotReload) || token.IsCancellationRequested) return false;
            ShowHotReloadResult(result);
            if (!result.Success) return false;

            if (debugBuild is not null && _session is { } debugger)
            {
                _hotReloadDebugBuild = debugBuild;
                debugger.UpdateDebugInfo(debugBuild.DebugInfo);
                Breakpoints.Bind(debugger);
            }

            if (result.Status == HotReloadStatus.Applied)
            {
                HotReloadGeneration++;
                if (_hotReloadAvalonia is { } app)
                {
                    app.RefreshContent();
                    AppPreview.Show(project.Name, app.View, app.Note);
                }
                else if (IsWebApp(project))
                {
                    WebPreview.ReloadCommand.Execute(null);
                }
            }

            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (ReferenceEquals(session, _hotReload))
                ShowHotReloadResult(new(HotReloadStatus.RestartRequired, $"Hot Reload could not complete: {ex.Message} Restart to apply the edits.", []));
            return false;
        }
        finally
        {
            if (entered) _hotReloadGate.Release();
        }
    }

    private static bool SameDebugLayout(DebugBuild before, DebugBuild after) =>
        before.DebugInfo.Methods.Select(m => (m.Id, m.Name, Slots: string.Join(';', m.Parameters)))
            .SequenceEqual(after.DebugInfo.Methods.Select(m => (m.Id, m.Name, Slots: string.Join(';', m.Parameters))))
        && before.DebugInfo.Statements.Select(s => (s.Id, s.MethodId, Slots: string.Join(';', s.Locals)))
            .SequenceEqual(after.DebugInfo.Statements.Select(s => (s.Id, s.MethodId, Slots: string.Join(';', s.Locals))));

    private void ShowHotReloadResult(HotReloadResult result)
    {
        HotReloadNeedsRestart = result.Status == HotReloadStatus.RestartRequired;
        StatusText = result.Message;
        Output.WriteLine("Hot Reload: " + result.Message, result.Success ? OutputKind.Info : OutputKind.Failure);
        foreach (var diagnostic in result.Diagnostics) Output.WriteLine(diagnostic.ToString(), OutputKind.Failure);
    }

    private async Task<bool> TryHotReloadOnSaveAsync(DocumentViewModel document)
    {
        if (!HotReloadSession.IsSupported || !HotReloadOnSave || _hotReload is null || !document.FileType.IsRoslyn) return false;
        await ApplyCurrentEditsAsync();
        return true; // Compilation errors/rude edits keep the current app alive and offer an explicit restart.
    }

    /// <summary>Restarts after a rude edit, only when the user selects the offered toolbar action.</summary>
    [RelayCommand]
    private void RestartForHotReload()
    {
        if (_session is not null)
        {
            _restartRequested = true;
            _session.Stop();
        }
        else if (_liveProject is not null)
        {
            _restartLiveApp = true;
            _run?.Cancel();
        }
        else if (_run is not null)
        {
            _restartConsoleForHotReload = true;
            _run.Cancel();
        }
    }
}
