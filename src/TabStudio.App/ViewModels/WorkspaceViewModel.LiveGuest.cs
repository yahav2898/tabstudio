using TabStudio.App.Services;
using TabStudio.App.Services.Live;
using TabStudio.Core.Diagnostics;
using TabStudio.Debugging;
using TabStudio.Debugging.Symbols;
using TabStudio.Link;
using TabStudio.Live;
using TabStudio.Live.Sessions;
using TabStudio.Swarm;

namespace TabStudio.App.ViewModels;

/// <summary>
/// A guest of a TabStudio Live session: the workspace is a copy of the host's, kept in step, where this device edits and
/// IntelliSense runs, while the host builds, runs and debugs. Its Output, Terminal, Error List, breakpoints and pauses
/// show here as the host's IDE shows them; F5, the steps and Break All go to the host's debugger when the host allows
/// this guest to step, and Locals, Watch and DataTips read the host's paused program.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>How long the debugger waits for the host to answer.</summary>
    private static readonly TimeSpan LiveAnswerTimeout = TimeSpan.FromSeconds(15);

    private LiveDebugState? _liveGuestDebug;
    private DebugSourceSpan? _liveCurrent;
    private bool _applyingLiveBreakpoints;

    /// <summary>Opens the session of the copy this workspace loaded, when it is a guest's copy.</summary>
    private void AttachGuestWorkspace()
    {
        if (_live?.Guest is { HasEnded: false } guest && Workspace is { } workspace && _live.IsGuestCopy(workspace.RootDirectory))
        {
            AttachLiveSession(guest);
        }
    }

    /// <summary>The workspace finished loading: a guest shows what the host does now, which loading covered.</summary>
    private void OnLoadedForLive()
    {
        if (LiveSession is LiveGuest guest)
        {
            ApplyGuestRun(guest);
        }
    }

    private void AttachLiveGuest(LiveGuest guest)
    {
        guest.RunChanged += OnGuestRunChanged;
        guest.DebugChanged += OnGuestDebugChanged;
        guest.ErrorsChanged += OnGuestErrorsChanged;
        guest.BreakpointsChanged += OnGuestBreakpointsChanged;
        guest.OutputWritten += OnGuestOutputWritten;
        guest.OutputCleared += OnGuestOutputCleared;
        guest.TerminalWritten += OnGuestTerminalWritten;
        guest.DocumentReloaded += OnGuestDocumentReloaded;
        Breakpoints.Changed += OnBreakpointsChangedForGuest;
        Terminal.Mirror(input => WriteHostTerminal(guest, input));

        Output.Clear();
        foreach (var output in guest.OutputSoFar)
        {
            Output.Write(output.Text, (OutputKind)(int)output.Kind);
        }

        Terminal.ShowMirrored(guest.TerminalSoFar);
        ApplyGuestErrors(guest);
        ApplyGuestBreakpoints(guest);
        ApplyGuestRun(guest);
        ApplyGuestDebug(guest);
    }

    private void DetachLiveGuest(LiveGuest guest)
    {
        guest.RunChanged -= OnGuestRunChanged;
        guest.DebugChanged -= OnGuestDebugChanged;
        guest.ErrorsChanged -= OnGuestErrorsChanged;
        guest.BreakpointsChanged -= OnGuestBreakpointsChanged;
        guest.OutputWritten -= OnGuestOutputWritten;
        guest.OutputCleared -= OnGuestOutputCleared;
        guest.TerminalWritten -= OnGuestTerminalWritten;
        guest.DocumentReloaded -= OnGuestDocumentReloaded;
        Breakpoints.Changed -= OnBreakpointsChangedForGuest;
        Terminal.Mirror(null);
        ErrorList.SetLiveHostDiagnostics([]);
        ShowGuestDebug(guest, null);
        if (State is IdeState.Running or IdeState.Debugging or IdeState.Paused or IdeState.Busy)
        {
            State = IdeState.Ready;
        }
    }

    /// <summary>Opens a document of the host's session on this guest and shares the editor's text with it.</summary>
    private async Task BindGuestDocumentAsync(LiveGuest guest, DocumentViewModel document, string path)
    {
        LiveDocument? shared;
        try
        {
            shared = await guest.OpenDocumentAsync(path);
        }
        catch (Exception ex) when (ex is LinkRemoteException or InvalidOperationException or IOException or OperationCanceledException)
        {
            StatusText = $"{document.FileName} is not shared: {ex.Message}";
            return;
        }

        if (shared is null)
        {
            return;
        }

        if (!ReferenceEquals(LiveSession, guest) || !Documents.Contains(document) || document.Live is not null)
        {
            if (!Documents.Any(d => ReferenceEquals(d.Live?.Live, shared)))
            {
                guest.CloseDocument(shared);
            }

            return;
        }

        _ = new LiveDocumentBinder(shared, document);
        OnLiveDocumentBound(document);
    }

    /// <summary>A guest saves: the host saves the document, and every copy of it changes.</summary>
    private async Task<bool> SaveGuestDocumentAsync(LiveGuest guest, DocumentViewModel document, LiveDocumentBinder binder)
    {
        if (binder.IsReadOnly)
        {
            StatusText = $"{guest.HostName} lets you watch this session, not save";
            return false;
        }

        try
        {
            await guest.SaveAsync(binder.Live);
            StatusText = $"Saved {document.FileName} on {guest.HostName}";
            return true;
        }
        catch (Exception ex) when (ex is LinkRemoteException or InvalidOperationException or IOException or OperationCanceledException)
        {
            StatusText = $"Could not save {document.FileName}: {ex.Message}";
            return false;
        }
    }

    private void OnLiveParticipantsChangedForGuest(LiveSession session)
    {
        if (session is LiveGuest guest)
        {
            Terminal.ShowMirrorTitle(guest.Self is { CanUseTerminal: true } ? $"{guest.HostName}'s Terminal" : $"{guest.HostName}'s Terminal (read-only)");
        }
    }

    private void OnGuestRunChanged(object? sender, EventArgs e)
    {
        if (sender is LiveGuest guest && ReferenceEquals(guest, LiveSession))
        {
            ApplyGuestRun(guest);
        }
    }

    private void OnGuestDebugChanged(object? sender, EventArgs e)
    {
        if (sender is LiveGuest guest && ReferenceEquals(guest, LiveSession))
        {
            ApplyGuestDebug(guest);
        }
    }

    private void OnGuestErrorsChanged(object? sender, EventArgs e)
    {
        if (sender is LiveGuest guest && ReferenceEquals(guest, LiveSession))
        {
            ApplyGuestErrors(guest);
        }
    }

    private void OnGuestBreakpointsChanged(object? sender, EventArgs e)
    {
        if (sender is LiveGuest guest && ReferenceEquals(guest, LiveSession))
        {
            ApplyGuestBreakpoints(guest);
        }
    }

    private void OnGuestOutputWritten(object? sender, LiveOutput output) => Output.Write(output.Text, (OutputKind)(int)output.Kind);

    private void OnGuestOutputCleared(object? sender, EventArgs e) => Output.Clear();

    private void OnGuestTerminalWritten(object? sender, ReadOnlyMemory<byte> data) => Terminal.ShowMirrored(data);

    private void OnGuestDocumentReloaded(object? sender, LiveDocument document) =>
        StatusText = $"{System.IO.Path.GetFileName(document.Path)} was loaded again as {LiveSession?.HostName} has it";

    private void WriteHostTerminal(LiveGuest guest, byte[] input)
    {
        if (guest.Self is { CanUseTerminal: true })
        {
            guest.WriteTerminal(input);
        }
        else
        {
            StatusText = $"{guest.HostName} has not allowed you to type in the Terminal";
        }
    }

    /// <summary>What the host's IDE does: its state, so the debug toolbar and the status bar show it here.</summary>
    private void ApplyGuestRun(LiveGuest guest)
    {
        var run = guest.RunState;
        State = run.Phase switch
        {
            LiveRunPhase.Building => IdeState.Busy,
            LiveRunPhase.Running => IdeState.Running,
            LiveRunPhase.Debugging => IdeState.Debugging,
            LiveRunPhase.Paused => IdeState.Paused,
            _ => IdeState.Ready,
        };
        if (run.Phase == LiveRunPhase.Idle)
        {
            StatusText = $"In {guest.HostName}'s session";
        }
        else if (run.StatusText.Length > 0)
        {
            StatusText = $"{guest.HostName}: {run.StatusText}";
        }
    }

    private void ApplyGuestErrors(LiveGuest guest)
    {
        var diagnostics = new List<BuildDiagnostic>(guest.Errors.Count);
        foreach (var error in guest.Errors)
        {
            DiagnosticLocation? location = null;
            if (error.Path is { } path)
            {
                if (guest.FullPathOf(path) is not { } full)
                {
                    continue;
                }

                location = new DiagnosticLocation(full, error.Start, error.Length, error.Line, error.Column);
            }

            var severity = (DiagnosticSeverityLevel)Math.Clamp(error.Severity, (int)DiagnosticSeverityLevel.Hidden, (int)DiagnosticSeverityLevel.Error);
            diagnostics.Add(new BuildDiagnostic(error.Id, severity, error.Message, location, error.Project));
        }

        ErrorList.SetLiveHostDiagnostics(diagnostics);
    }

    /// <summary>Shows the host's breakpoints, replacing this guest's.</summary>
    private void ApplyGuestBreakpoints(LiveGuest guest)
    {
        _applyingLiveBreakpoints = true;
        try
        {
            Breakpoints.Load([.. guest.Breakpoints
                .Select(b => guest.FullPathOf(b.FilePath) is { } full ? new SessionBreakpoint(full, b.Line, b.Condition) : null)
                .OfType<SessionBreakpoint>()]);
        }
        finally
        {
            _applyingLiveBreakpoints = false;
        }
    }

    /// <summary>A breakpoint set or removed here goes to the host, whose breakpoints every participant shares.</summary>
    private void OnBreakpointsChangedForGuest(object? sender, EventArgs e)
    {
        if (!_applyingLiveBreakpoints && LiveSession is LiveGuest guest)
        {
            _ = SyncBreakpointsToHostAsync(guest);
        }
    }

    private async Task SyncBreakpointsToHostAsync(LiveGuest guest)
    {
        var local = Breakpoints.All
            .Select(b => guest.RelativePathOf(b.FilePath) is { } path ? new SwarmBreakpoint(path, b.Line, b.Condition) : null)
            .OfType<SwarmBreakpoint>()
            .ToList();
        var remote = guest.Breakpoints;
        try
        {
            foreach (var added in local.Where(l => !remote.Contains(l)))
            {
                await guest.SetBreakpointAsync(added.FilePath, added.Line, added.Condition, remove: false);
            }

            foreach (var removed in remote.Where(r => !local.Any(l => l.FilePath == r.FilePath && l.Line == r.Line)))
            {
                await guest.SetBreakpointAsync(removed.FilePath, removed.Line, null, remove: true);
            }
        }
        catch (Exception ex) when (ex is LinkRemoteException or InvalidOperationException or OperationCanceledException)
        {
            StatusText = ex.Message;
            if (ReferenceEquals(LiveSession, guest))
            {
                ApplyGuestBreakpoints(guest);
            }
        }
    }

    private void ApplyGuestDebug(LiveGuest guest) => ShowGuestDebug(guest, guest.Debug);

    /// <summary>Shows where the host's program is paused, as the host's debugger shows it, or that it runs.</summary>
    private void ShowGuestDebug(LiveGuest guest, LiveDebugState? state)
    {
        var previous = _liveGuestDebug;
        _liveGuestDebug = state;
        if (state is null)
        {
            if (previous is null)
            {
                return;
            }

            _liveCurrent = null;
            _exceptionHelper = null;
            CallStack.Clear();
            Locals.Tree.IsStale = true;
            Watch.MarkStale();
            ShowExecutionPoints();
            return;
        }

        var frames = state.Frames.Select(f => new DebugStackFrame(f.MethodName, ToGuestSpan(guest, f.Location))).ToList();
        _liveCurrent = frames.Count > 0 ? frames[0].Location : null;
        _exceptionHelper = state.Exception is { } exception
            ? new ExceptionHelperViewModel(TypeOf(exception), MessageOf(exception), exception, StartDebuggingCommand, StopDebuggingCommand)
            : null;
        CallStack.Show(frames);

        // Like the host's debug layout: the variables come into view when the program pauses.
        if (previous?.PauseId != state.PauseId && (!IsBottomPanelVisible || SelectedPanel is not (BottomPanel.Locals or BottomPanel.Watch or BottomPanel.CallStack)))
        {
            SelectPanel(BottomPanel.Locals);
        }

        _pauseShown = ShowLiveFrameAsync(CallStack.ActiveFrame, followLocation: previous?.PauseId != state.PauseId);
    }

    private static string TypeOf(string exception) => exception.IndexOf(": ", StringComparison.Ordinal) is var at and > 0 ? exception[..at] : exception;

    private static string MessageOf(string exception) => exception.IndexOf(": ", StringComparison.Ordinal) is var at and > 0 ? exception[(at + 2)..] : string.Empty;

    private static DebugSourceSpan? ToGuestSpan(LiveGuest guest, DebugSourceSpan? span) =>
        span is not null && guest.FullPathOf(span.FilePath) is { } full ? span with { FilePath = full } : null;

    /// <summary>Shows a frame of the host's pause: its statement here, and the paused statement's locals and watches.</summary>
    private async Task ShowLiveFrameAsync(StackFrameViewModel? frame, bool followLocation = true)
    {
        ShowExecutionPoints();
        if (_liveGuestDebug is not { } state || LiveSession is not LiveGuest guest || frame is null)
        {
            return;
        }

        if (followLocation && frame.Location is { } location && await OpenDocumentAsync(location.FilePath) is { } document)
        {
            ApplyExecutionPoints(document);
            document.NavigateTo(location.Line, location.Column);
        }

        var sources = state.Locals
            .Select(v => new VariableSource(v.Name, v.Path, null) { Variable = new LiveDebugVariable(guest, state.PauseId, v) })
            .ToList();
        var top = state.Frames.Count > 0 ? state.Frames[0].MethodName : "the paused method";
        var note = frame.IsTop ? null : $"These are the locals of {top}, where {guest.HostName}'s program is paused.";
        await Task.WhenAll(
            Locals.ShowVariablesAsync(sources, note),
            Watch.EvaluateWithAsync(expression => EvaluateOnHostAsync(guest, state.PauseId, expression)));
    }

    private static async Task<VariableSource> EvaluateOnHostAsync(LiveGuest guest, long pauseId, string expression)
    {
        try
        {
            using var timeout = new CancellationTokenSource(LiveAnswerTimeout);
            var result = await guest.EvaluateAsync(pauseId, expression, timeout.Token);
            return result.Value is { } value
                ? new VariableSource(expression, expression, null) { Variable = new LiveDebugVariable(guest, pauseId, value) }
                : new VariableSource(expression, expression, null, result.Error ?? "There is no such value.");
        }
        catch (LinkRemoteException ex)
        {
            // The host answered with a refusal, such as code a viewer may not run.
            return new VariableSource(expression, expression, null, ex.Message);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new VariableSource(expression, expression, null, $"{guest.HostName} did not answer: {ex.Message}");
        }
    }

    /// <summary>A DataTip over the code, read from the host's paused program.</summary>
    private async Task<VariableTreeViewModel?> EvaluateLiveDataTipAsync(string expression)
    {
        if (_liveGuestDebug is not { } state || LiveSession is not LiveGuest guest || string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var source = await EvaluateOnHostAsync(guest, state.PauseId, expression);
        if (source.Variable is null || !ReferenceEquals(_liveGuestDebug, state))
        {
            return null;
        }

        var tree = new VariableTreeViewModel();
        await tree.ShowAsync([source]);
        return tree;
    }

    /// <summary>F5, the steps and Break All on a guest: the host's debugger does them, when the host allows this guest to.</summary>
    private async Task ControlHostDebuggerAsync(LiveDebugCommand command)
    {
        if (LiveSession is not LiveGuest guest)
        {
            return;
        }

        if (guest.Self is not { } self || !LivePermissions.CanControlDebugger(self))
        {
            StatusText = $"{guest.HostName} decides who steps the program, and has not allowed you to";
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(LiveAnswerTimeout);
            await guest.ControlDebuggerAsync(command, timeout.Token);
        }
        catch (Exception ex) when (ex is LinkRemoteException or InvalidOperationException or OperationCanceledException)
        {
            StatusText = ex.Message;
        }
    }

    /// <summary>A command only the host runs, asked on a guest.</summary>
    private void ExplainHostRuns() =>
        StatusText = $"{LiveSession?.HostName} builds and runs this session's program; you see its output and its debugger here";
}
