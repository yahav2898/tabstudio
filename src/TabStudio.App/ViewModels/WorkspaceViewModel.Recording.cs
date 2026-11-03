using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private DebugSession? _recordedSession;
    private bool _updatingTimeline;
    private string? _recordingExitStatus;

    /// <summary>Size of the next recording's ring. Recording remains off until Start with Recording is chosen.</summary>
    [ObservableProperty]
    public partial int RecordingBufferMegabytes { get; set; } = 64;

    /// <summary>Whether a retained recording is available, including after the program exits.</summary>
    public bool HasRecording => _recordedSession?.Recording is { Count: > 0 };

    /// <summary>Whether the editor and inspector panels show immutable historical state.</summary>
    public bool IsHistorical => _pause?.IsHistorical == true;

    /// <summary>Last retained zero-based timeline index.</summary>
    public double RecordingMaximum => Math.Max(0, (_recordedSession?.Recording?.Count ?? 0) - 1);

    /// <summary>Selected retained timeline index.</summary>
    [ObservableProperty]
    public partial double RecordingPosition { get; set; }

    /// <summary>Historical marker, selected step, thread, buffer usage and eviction information.</summary>
    public string RecordingLabel => _recordedSession?.Recording is { } recording
        ? $"{(IsHistorical ? "Historical" : IsDebuggeeRunning ? "Recording" : "Recorded")} · step {(IsHistorical ? _pause?.RecordedStep : recording.GetStep((int)RecordingPosition)?.Sequence)} · {recording.Count} retained · thread {_pause?.ThreadId ?? recording.GetStep((int)RecordingPosition)?.ThreadId} · {recording.UsedBytes / 1024d:F1} KiB · {recording.EvictedSteps} evicted · {recording.DroppedSteps} dropped"
        : string.Empty;

    /// <summary>History can be inspected only while execution is stopped.</summary>
    public bool CanNavigateRecording => HasRecording && !IsDebuggeeRunning && !IsBusy && !IsRunning;

    private bool CanStartRecording => CanRun() && !IsRunOnSwarm && !UsesLaunchTarget &&
        StartupProject is { Language: ProjectLanguage.CSharp } project && !IsLiveApp(project) && _session is null;

    /// <summary>Starts the in-process C# debugger with recording explicitly enabled.</summary>
    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private Task StartWithRecordingAsync() => LaunchAsync(DebugStart.Record);

    /// <summary>Shift+F10: skips called frames while stepping backwards.</summary>
    [RelayCommand(CanExecute = nameof(CanNavigateRecording))]
    private Task StepBackAsync() => SeekFoundAsync(session => session.FindStepBack());

    /// <summary>Steps backwards into a recorded call.</summary>
    [RelayCommand(CanExecute = nameof(CanNavigateRecording))]
    private Task StepBackIntoAsync() => ShowHistoricalAsync(_recordedSession?.StepBackInto());

    /// <summary>Goes to a preceding breakpoint hit or a derivable exception cause.</summary>
    [RelayCommand(CanExecute = nameof(CanNavigateRecording))]
    private Task ReverseContinueAsync() => SeekFoundAsync(session => session.FindReverseContinue());

    /// <summary>Finds the last write to a Locals, Watch or DataTip variable.</summary>
    [RelayCommand(CanExecute = nameof(CanNavigateRecording))]
    private Task JumpToLastWriteAsync(string? path) => path is null ? Task.CompletedTask : SeekFoundAsync(session => session.FindLastWrite(path));

    /// <summary>
    /// Searches the recording on a background thread, which only reads it, then selects what was found here on the
    /// UI thread, which owns the debugger's selected step and every panel that shows it.
    /// </summary>
    private async Task SeekFoundAsync(Func<DebugSession, int> find)
    {
        if (_recordedSession is not { } session) return;
        var index = await Task.Run(() => find(session));
        if (index < 0 || !ReferenceEquals(session, _recordedSession)) return;
        await ShowHistoricalAsync(session.SeekHistory(index));
    }

    /// <summary>The session whose recording the timeline shows, for tests.</summary>
    internal DebugSession? RecordedSession => _recordedSession;

    /// <summary>Returns to the live paused point without running code.</summary>
    [RelayCommand(CanExecute = nameof(IsHistorical))]
    private async Task ReturnToLiveAsync()
    {
        _recordedSession?.ReturnToLive();
        if (_session?.CurrentPause is { } pause)
        {
            await ShowPauseAsync(_session, pause, selectCause: false);
        }
        else
        {
            _pause = null;
            CallStack.Clear(); Locals.Clear();
            await Watch.EvaluateAsync(null);
            StatusText = _recordingExitStatus ?? "Ready";
            ShowExecutionPoints();
            RefreshRecording();
        }
    }

    /// <summary>Configures the bounded history retained by the next recording.</summary>
    [RelayCommand]
    private async Task ConfigureRecordingAsync()
    {
        var text = await Prompt.AskAsync("Recording buffer", "Ring size in MiB (1–512). Oldest complete steps are evicted when full. Recording is off until Start with Recording.",
            "Save", RecordingBufferMegabytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (int.TryParse(text, out var value) && value is >= 1 and <= 512) RecordingBufferMegabytes = value;
    }

    partial void OnRecordingBufferMegabytesChanged(int value)
    {
        if (value is < 1 or > 512) RecordingBufferMegabytes = Math.Clamp(value, 1, 512);
        ScheduleSessionSave();
    }

    partial void OnRecordingPositionChanged(double value)
    {
        if (!_updatingTimeline && CanNavigateRecording)
            _pauseShown = ShowHistoricalAsync(_recordedSession?.SeekHistory((int)Math.Round(value)));
    }

    private async Task ShowHistoricalAsync(DebugPausedEventArgs? pause)
    {
        if (pause is null) return;
        // A loop can revisit exactly the same source span with different values. Clear the old point first so
        // open DataTips close even when the new historical point has the same location.
        foreach (var document in Documents) document.ShowExecutionPoints(null, null, null);
        _pause = pause;
        _exceptionHelper = null;
        StatusText = $"Historical · step {pause.RecordedStep} · {Path.GetFileName(pause.Location?.FilePath)}, line {pause.Location?.Line}";
        CallStack.Show(pause.CallStack.Select(f => f with { MethodName = f.MethodName + " [historical]" }).ToArray());
        SelectPanel(BottomPanel.Locals);
        await ShowFrameAsync(CallStack.ActiveFrame);
        if (ReferenceEquals(_pause, pause) || _pause?.RecordedStep == pause.RecordedStep)
            await Task.WhenAll(Locals.ShowAsync(_pause!.Locals), Watch.EvaluateAsync(_pause));
        Locals.Tree.IsHistorical = Watch.Tree.IsHistorical = true;
        RefreshRecording();
    }

    private void RefreshRecording()
    {
        _updatingTimeline = true;
        RecordingPosition = Math.Max(0, _recordedSession?.HistoryIndex ?? 0);
        _updatingTimeline = false;
        Locals.Tree.IsHistorical = Watch.Tree.IsHistorical = IsHistorical;
        Locals.Tree.CanFindLastWrite = Watch.Tree.CanFindLastWrite = CanNavigateRecording;
        OnPropertyChanged(nameof(HasRecording));
        OnPropertyChanged(nameof(IsHistorical));
        OnPropertyChanged(nameof(RecordingMaximum));
        OnPropertyChanged(nameof(RecordingLabel));
        OnPropertyChanged(nameof(CanNavigateRecording));
        OnPropertyChanged(nameof(ShowsDebugPanels));
        OnPropertyChanged(nameof(ShowsCallStackBeside));
        OnPropertyChanged(nameof(ShowsEditorTouchBar));
        NotifyRecordingCommands();
    }

    private void NotifyRecordingCommands()
    {
        StartWithRecordingCommand.NotifyCanExecuteChanged();
        StepBackCommand.NotifyCanExecuteChanged(); StepBackIntoCommand.NotifyCanExecuteChanged();
        ReverseContinueCommand.NotifyCanExecuteChanged(); JumpToLastWriteCommand.NotifyCanExecuteChanged();
        ReturnToLiveCommand.NotifyCanExecuteChanged();
    }

    private void ClearRecording()
    {
        _recordedSession = null;
        _recordingExitStatus = null;
        if (IsHistorical)
        {
            _pause = null; _exceptionHelper = null;
            CallStack.Clear(); Locals.Clear();
            _ = Watch.EvaluateAsync(null);
            ShowExecutionPoints();
        }
        RefreshRecording();
    }
}
