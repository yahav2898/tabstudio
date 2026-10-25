using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging;
using TabStudio.Debugging.Building;
using TabStudio.Debugging.Symbols;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The debugger, as in Visual Studio: F5 builds the startup project for debugging and runs it, breakpoints and steps
/// pause it, and while it is paused the editor marks the current statement and the Locals, Watch and Call Stack panels
/// show the program's state. Ctrl+F5 still runs the program without the debugger.
/// </summary>
public sealed partial class WorkspaceViewModel : IEditorDebugger
{
    private DebugSession? _session;
    private ProjectInfo? _debuggedProject;
    private DebugPausedEventArgs? _pause;
    private ExceptionHelperViewModel? _exceptionHelper;
    private Task _pauseShown = Task.CompletedTask;
    private bool _restartRequested;

    /// <summary>How a debug session begins.</summary>
    private enum DebugStart
    {
        /// <summary>Run until a breakpoint (F5).</summary>
        Run,

        /// <summary>Stop at the first statement (F10, F11).</summary>
        StepInto,

        /// <summary>Run to the line with the caret (Ctrl+F10).</summary>
        RunToCursor,
    }

    /// <summary>Every breakpoint in the workspace.</summary>
    public BreakpointsViewModel Breakpoints { get; } = new();

    /// <summary>The Locals panel.</summary>
    public LocalsViewModel Locals { get; } = new();

    /// <summary>The Watch panel.</summary>
    public WatchViewModel Watch { get; } = new();

    /// <summary>The Call Stack panel.</summary>
    public CallStackViewModel CallStack { get; } = new();

    /// <summary>Whether the debugger runs a program, paused or not.</summary>
    public bool IsDebugging => State is IdeState.Debugging or IdeState.Paused;

    /// <summary>Whether the debugged program is paused, so it can be inspected and stepped.</summary>
    public bool IsPaused => State == IdeState.Paused;

    /// <summary>Whether the debugged program is running, so Break All can pause it.</summary>
    public bool IsDebuggeeRunning => State == IdeState.Debugging;

    /// <summary>The Start button's label: the startup project, or Continue while debugging.</summary>
    public string StartDebuggingLabel => IsDebugging ? "Continue" : RunLabel;

    /// <summary>The Start button's tooltip.</summary>
    public string StartDebuggingToolTip => IsDebugging ? "Continue (F5)" : "Start Debugging (F5)";

    /// <summary>The Debug menu's first command: Start Debugging, or Continue while debugging.</summary>
    public string StartDebuggingMenuHeader => IsDebugging ? "_Continue" : "_Start Debugging";

    /// <summary>Whether the Locals panel is the visible bottom panel.</summary>
    public bool IsLocalsSelected => SelectedPanel == BottomPanel.Locals;

    /// <summary>Whether the Watch panel is the visible bottom panel.</summary>
    public bool IsWatchSelected => SelectedPanel == BottomPanel.Watch;

    /// <summary>Whether the Call Stack panel is the visible bottom panel.</summary>
    public bool IsCallStackSelected => SelectedPanel == BottomPanel.CallStack;

    /// <summary>Whether the Locals, Watch and Call Stack tabs are shown: while debugging, as in Visual Studio's debug layout.</summary>
    public bool ShowsDebugPanels => IsDebugging || SelectedPanel is BottomPanel.Locals or BottomPanel.Watch or BottomPanel.CallStack;

    /// <summary>Whether the Call Stack is shown beside Locals or Watch, so both are in view while debugging.</summary>
    public bool ShowsCallStackBeside => IsDebugging && SelectedPanel is BottomPanel.Locals or BottomPanel.Watch;

    /// <summary>
    /// Whether the floating editing buttons are shown: over code, not over a notebook, which has buttons of its own;
    /// while debugging, the debug buttons take their place.
    /// </summary>
    public bool ShowsEditorTouchBar => HasDocuments && !IsDebugging && ActiveDocument?.ShowsEditorActions != false;

    /// <summary>
    /// Whether the floating editing and debug buttons are folded into one small button at the edge of the editor, so
    /// they cover no code. One tap folds or unfolds them.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTouchBarCollapsed { get; set; }

    /// <summary>Whether to pause when an exception is about to go unhandled. On by default, as in Visual Studio.</summary>
    [ObservableProperty]
    public partial bool BreakOnUnhandledExceptions { get; set; } = true;

    /// <summary>Completes when the latest pause is fully shown: the editor, Locals and Watch. For tests.</summary>
    internal Task PauseShown => _pauseShown;

    /// <summary>Whether a step can run: the program is paused, or it is not running yet and F10 or F11 start it.</summary>
    private bool CanStep => IsPaused || CanRun();

    /// <inheritdoc />
    void IEditorDebugger.ToggleBreakpoint(string filePath, int line) => Breakpoints.Toggle(filePath, line);

    /// <summary>Folds the floating touch buttons away, or brings them back.</summary>
    [RelayCommand]
    private void ToggleTouchBar() => IsTouchBarCollapsed = !IsTouchBarCollapsed;

    /// <inheritdoc />
    async Task IEditorDebugger.EditBreakpointConditionAsync(string filePath, int line)
    {
        if (UsesAdapterBreakpoints(filePath))
        {
            await EditAdapterBreakpointAsync(filePath, line, BreakpointOption.Condition);
            return;
        }

        var breakpoint = Breakpoints.Find(filePath, line) ?? Breakpoints.Add(filePath, line);
        var condition = await Prompt.AskAsync(
            "Breakpoint Condition",
            $"Line {breakpoint.Line} of {breakpoint.FileName}. Stop only when this is true, such as count == 3 or order.Total > 100: " +
            "compare variables, members and items with values, joined by && and ||. Method calls are not supported.",
            "Apply",
            breakpoint.Condition ?? string.Empty);
        if (condition is not null)
        {
            Breakpoints.SetCondition(breakpoint, condition);
        }
    }

    /// <inheritdoc />
    void IEditorDebugger.ClearBreakpointCondition(string filePath, int line)
    {
        if (Breakpoints.Find(filePath, line) is { } breakpoint)
        {
            Breakpoints.SetCondition(breakpoint, null);
        }
    }

    /// <inheritdoc />
    async Task<VariableTreeViewModel?> IEditorDebugger.EvaluateDataTipAsync(string expression)
    {
        if (_adapter is not null)
        {
            return await EvaluateAdapterDataTipAsync(expression);
        }

        if (_pause is not { } pause || string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = await Task.Run(() => FindValue(pause, expression));
        if (value is null || !ReferenceEquals(pause, _pause))
        {
            return null;
        }

        var tree = new VariableTreeViewModel();
        await tree.ShowAsync([new VariableSource(expression, expression, value)]);
        return tree;
    }

    /// <summary>F5: builds the startup project for debugging and runs it under the debugger, or continues the paused program.</summary>
    [RelayCommand(CanExecute = nameof(CanStartDebugging))]
    private Task StartDebuggingAsync()
    {
        if (_adapter is not null)
        {
            return ContinueAdapterAsync();
        }

        if (IsPaused)
        {
            Resume(session => session.Continue());
            return Task.CompletedTask;
        }

        return LaunchAsync(DebugStart.Run);
    }

    private bool CanStartDebugging() => IsPaused || CanRun();

    /// <summary>F11: runs the current statement, stepping into the calls it makes; before debugging, starts at the first statement.</summary>
    [RelayCommand(CanExecute = nameof(CanStep))]
    private Task StepIntoAsync() => _adapter is not null ? StepAdapterAsync(AdapterStep.Into) : StepOrStartAsync(session => session.StepInto());

    /// <summary>F10: runs the current statement and the calls it makes; before debugging, starts at the first statement.</summary>
    [RelayCommand(CanExecute = nameof(CanStep))]
    private Task StepOverAsync() => _adapter is not null ? StepAdapterAsync(AdapterStep.Over) : StepOrStartAsync(session => session.StepOver());

    /// <summary>Shift+F11: runs to the end of the current function and pauses in its caller.</summary>
    [RelayCommand(CanExecute = nameof(IsPaused))]
    private void StepOut()
    {
        if (_adapter is not null)
        {
            _ = StepAdapterAsync(AdapterStep.Out);
            return;
        }

        Resume(session => session.StepOut());
    }

    /// <summary>Ctrl+Alt+Break: pauses the running program at the next statement of its own code.</summary>
    [RelayCommand(CanExecute = nameof(IsDebuggeeRunning))]
    private void BreakAll()
    {
        _session?.Pause();
        _ = PauseAdapterAsync();
        StatusText = "Pausing at the next statement of the program's own code";
    }

    /// <summary>Shift+F5: stops debugging, or stops a program started without debugging.</summary>
    [RelayCommand(CanExecute = nameof(CanStopDebugging))]
    private void StopDebugging()
    {
        if (IsRunning)
        {
            _run?.Cancel();
        }
        else
        {
            _session?.Stop();
            _ = StopAdapterAsync();
        }
    }

    private bool CanStopDebugging() => IsDebugging || IsRunning;

    /// <summary>Ctrl+Shift+F5: stops debugging and starts again with a fresh build.</summary>
    [RelayCommand(CanExecute = nameof(IsDebugging))]
    private void Restart()
    {
        _restartRequested = _session is not null;
        _session?.Stop();
        RestartAdapter();
    }

    /// <summary>Ctrl+F10: runs until the line with the caret, starting the program if it is not running.</summary>
    [RelayCommand(CanExecute = nameof(CanRunToCursor))]
    private Task RunToCursorAsync()
    {
        if (ActiveDocument is not { } document)
        {
            return Task.CompletedTask;
        }

        if (!IsPaused)
        {
            return LaunchAsync(DebugStart.RunToCursor);
        }

        if (_adapter is not null)
        {
            return RunAdapterToCursorAsync(document);
        }

        if (!Resume(session => session.RunToLine(document.FilePath, document.CaretLine)))
        {
            StatusText = $"There is no code to run to at or after line {document.CaretLine}";
        }

        return Task.CompletedTask;
    }

    private bool CanRunToCursor() => HasDocuments && (IsPaused || CanRun());

    /// <summary>F9: sets or removes a breakpoint on the caret's line.</summary>
    [RelayCommand(CanExecute = nameof(HasDocuments))]
    private void ToggleBreakpoint()
    {
        if (ActiveDocument is { } document)
        {
            Breakpoints.Toggle(document.FilePath, document.CaretLine);
        }
    }

    /// <summary>Ctrl+Shift+F9: removes every breakpoint.</summary>
    [RelayCommand]
    private void DeleteAllBreakpoints() => Breakpoints.Clear();

    /// <summary>Alt+Num *: shows the statement where the program is paused.</summary>
    [RelayCommand(CanExecute = nameof(IsPaused))]
    private void ShowNextStatement()
    {
        if (CallStack.Frames.Count > 0)
        {
            CallStack.Activate(CallStack.Frames[0]);
        }
    }

    /// <summary>Wires the debugger's panels to the workspace. Called once, by the constructor.</summary>
    private void InitializeDebugging()
    {
        PropertyChanged += OnDebuggingPropertyChanged;
        Output.PropertyChanged += OnOutputPropertyChangedWhileDebugging;
        CallStack.FrameActivated += async (_, frame) => await ShowFrameAsync(frame);
        Breakpoints.Changed += (_, _) => ScheduleSessionSave();
        Watch.ExpressionsChanged += (_, _) => ScheduleSessionSave();
        Locals.Tree.AddWatchRequested += async (_, row) =>
        {
            await Watch.AddAsync(row.Path);
            SelectPanel(BottomPanel.Watch);
        };
    }

    /// <summary>Restores breakpoints and watches from a saved session. Called by <see cref="LoadAsync"/>.</summary>
    private void RestoreDebuggingState(SessionState? session)
    {
        Breakpoints.Load(session?.Breakpoints);
        Watch.Load(session?.WatchExpressions);
    }

    /// <summary>Adds the breakpoints and watches to a session about to be saved.</summary>
    private SessionState WithDebuggingState(SessionState state) =>
        state with { Breakpoints = Breakpoints.Save(), WatchExpressions = [.. Watch.Expressions] };

    /// <summary>Shows breakpoints and execution points in a document that just opened.</summary>
    private void AttachDebugging(DocumentViewModel document)
    {
        document.Debugger = this;
        Breakpoints.Attach(document);
        ApplyExecutionPoints(document);
    }

    /// <summary>Stops tracking a document that is closing.</summary>
    private void DetachDebugging(DocumentViewModel document)
    {
        Breakpoints.Detach(document);
        document.ShowExecutionPoints(null, null, null);
        document.Debugger = null;
    }

    /// <summary>Stops the debugged program when the workspace closes.</summary>
    private void DisposeDebugging()
    {
        if (_session is { } session)
        {
            _session = null;
            session.Paused -= OnSessionPaused;
            session.Resumed -= OnSessionResumed;
            session.Dispose();
        }

        DisposeAdapterSession();
    }

    partial void OnBreakOnUnhandledExceptionsChanged(bool value)
    {
        if (_session is { } session)
        {
            session.BreakOnUnhandledExceptions = value;
        }

        ApplyBreakOnUnhandledToAdapter(value);
    }

    private Task StepOrStartAsync(Func<DebugSession, bool> step)
    {
        if (IsPaused)
        {
            Resume(step);
            return Task.CompletedTask;
        }

        return LaunchAsync(DebugStart.StepInto);
    }

    /// <summary>Resumes the paused program with a command such as Step Over.</summary>
    /// <returns><see langword="false"/> when nothing was paused or the command did not apply.</returns>
    private bool Resume(Func<DebugSession, bool> command) => _session is { } session && IsPaused && command(session);

    private async Task LaunchAsync(DebugStart start)
    {
        if (UsesLaunchTarget && CurrentLaunchTarget is { } target && _session is null)
        {
            await LaunchAdapterAsync(target, start, noDebug: false);
            return;
        }

        if (StartupProject is not { } project || _language is not { } language || _session is not null)
        {
            return;
        }

        if (UsesWindowsCompatibility(project))
        {
            await RunWindowsAppAsync(project, fromDebugger: true);
            return;
        }

        if (IsLiveApp(project))
        {
            await RunLiveProjectAsync(project, fromDebugger: true);
            return;
        }

        // Python and Node.js run with their toolchain; their debug adapters attach here once connected.
        if (IsToolchainProject(project))
        {
            await RunToolchainProjectAsync(project);
            return;
        }

        var cursorFile = ActiveDocument?.FilePath;
        var cursorLine = ActiveDocument?.CaretLine ?? 1;

        DebugBuild? debugBuild = null;
        var built = await BuildProjectsAsync([project], showOutput: true, async (target, cancellationToken) =>
        {
            debugBuild = await language.BuildForDebuggingAsync(target, cancellationToken);
            return debugBuild.Result;
        });
        if (built is not { Success: true } || debugBuild is not { Success: true } build)
        {
            return;
        }

        var session = new DebugSession(build) { BreakOnUnhandledExceptions = BreakOnUnhandledExceptions };
        session.Paused += OnSessionPaused;
        session.Resumed += OnSessionResumed;
        _session = session;
        _debuggedProject = project;
        Breakpoints.Bind(session);
        switch (start)
        {
            case DebugStart.StepInto:
                session.Pause();
                break;
            case DebugStart.RunToCursor when cursorFile is not null:
                session.RunToLine(cursorFile, cursorLine);
                break;
        }

        State = IdeState.Debugging;
        StatusText = $"Debugging {project.Name}";
        var input = Output.BeginInput();
        var packages = language.GetPackageRuntimeAssemblies(project.FilePath);
        _ = RunSessionAsync(session, project, packages, input);
    }

    private async Task RunSessionAsync(DebugSession session, ProjectInfo project, IReadOnlyList<string> packages, ConsoleInput input)
    {
        RunResult result;
        try
        {
            result = await session.RunAsync([], packages, Output, input);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // RunAsync reports how the program ended in its result; an exception here is a debugger bug.
            Output.WriteLine($"The debugger stopped: {ex.Message}", OutputKind.Failure);
            result = new RunResult(RunOutcome.Crashed, -1, TimeSpan.Zero, ex);
        }
        finally
        {
            Output.EndInput();
        }

        EndSession(session, project, result);
    }

    private void EndSession(DebugSession session, ProjectInfo project, RunResult result)
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
        Breakpoints.Unbind();
        CallStack.Clear();
        Locals.Clear();
        _ = Watch.EvaluateAsync(null);
        ShowExecutionPoints();

        var (message, kind) = DescribeExit(project.Name, result);
        Output.WriteLine();
        Output.WriteLine(message, kind);
        State = result.Outcome == RunOutcome.Crashed ? IdeState.Failed : IdeState.Ready;
        StatusText = result.Outcome switch
        {
            RunOutcome.Completed => "Ready",
            RunOutcome.Stopped => "Debugging stopped",
            _ => "The program stopped with an error",
        };

        if (SelectedPanel is BottomPanel.Locals or BottomPanel.Watch or BottomPanel.CallStack)
        {
            SelectedPanel = BottomPanel.Output;
        }

        if (_restartRequested)
        {
            _restartRequested = false;
            _ = LaunchAsync(DebugStart.Run);
        }
    }

    private void OnSessionPaused(object? sender, DebugPausedEventArgs e)
    {
        // Raised on the program's thread just before it blocks: show the pause on the UI thread.
        if (sender is DebugSession session)
        {
            _ui.Post(() => _pauseShown = ShowPauseAsync(session, e));
        }
    }

    private void OnSessionResumed(object? sender, EventArgs e)
    {
        if (sender is not DebugSession session)
        {
            return;
        }

        // Raised by the command that resumed, which is on the UI thread, before the program continues.
        if (_ui.CheckAccess())
        {
            ShowRunning(session);
        }
        else
        {
            _ui.Post(() => ShowRunning(session));
        }
    }

    private async Task ShowPauseAsync(DebugSession session, DebugPausedEventArgs pause)
    {
        if (!ReferenceEquals(session, _session) || !ReferenceEquals(session.CurrentPause, pause))
        {
            return;
        }

        _pause = pause;
        _exceptionHelper = pause is { Reason: PauseReason.Exception, Exception: { } exception }
            ? new ExceptionHelperViewModel(exception, pause.CallStack, StartDebuggingCommand, StopDebuggingCommand)
            : null;
        State = IdeState.Paused;
        StatusText = DescribePause(pause);
        CallStack.Show(pause.CallStack);

        // Like Visual Studio's debug layout: the variables come into view when the program pauses.
        if (!IsBottomPanelVisible || SelectedPanel is not (BottomPanel.Locals or BottomPanel.Watch or BottomPanel.CallStack))
        {
            SelectPanel(BottomPanel.Locals);
        }

        await ShowFrameAsync(CallStack.ActiveFrame);
        if (ReferenceEquals(_pause, pause))
        {
            await Task.WhenAll(Locals.ShowAsync(pause.Locals), Watch.EvaluateAsync(pause));
        }
    }

    private void ShowRunning(DebugSession session)
    {
        if (!ReferenceEquals(session, _session))
        {
            return;
        }

        _pause = null;
        _exceptionHelper = null;
        State = IdeState.Debugging;
        StatusText = $"Debugging {_debuggedProject?.Name}";
        CallStack.Clear();
        Locals.Tree.IsStale = true;
        Watch.MarkStale();
        ShowExecutionPoints();
    }

    /// <summary>Marks a frame's statement in the editor and opens its file, as choosing a frame in the Call Stack does.</summary>
    private async Task ShowFrameAsync(StackFrameViewModel? frame)
    {
        if (_adapter is not null)
        {
            await ShowAdapterFrameAsync(frame);
            return;
        }

        ShowExecutionPoints();
        if (_pause is not { CallStack.Count: > 0 } pause || frame is null)
        {
            return;
        }

        Locals.ShowFrameNote(pause.CallStack[0].MethodName, frame.IsTop ? null : frame.MethodName);
        if (frame.Location is { } location && await OpenDocumentAsync(location.FilePath) is { } document)
        {
            ApplyExecutionPoints(document);
            document.NavigateTo(location.Line, location.Column);
        }
    }

    private void ShowExecutionPoints()
    {
        foreach (var document in Documents)
        {
            ApplyExecutionPoints(document);
        }
    }

    private void ApplyExecutionPoints(DocumentViewModel document)
    {
        var current = _pause is { CallStack.Count: > 0 } pause ? pause.CallStack[0].Location : _adapterCurrent;
        var caller = CallStack.ActiveFrame is { IsTop: false } frame ? frame.Location : null;
        document.ShowExecutionPoints(PointIn(document, current), PointIn(document, caller), _exceptionHelper);
    }

    private void OnDebuggingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(State):
                OnPropertyChanged(nameof(IsDebugging));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(IsDebuggeeRunning));
                OnPropertyChanged(nameof(StartDebuggingLabel));
                OnPropertyChanged(nameof(StartDebuggingToolTip));
                OnPropertyChanged(nameof(StartDebuggingMenuHeader));
                OnPropertyChanged(nameof(ShowsDebugPanels));
                OnPropertyChanged(nameof(ShowsCallStackBeside));
                OnPropertyChanged(nameof(ShowsEditorTouchBar));
                NotifyDebugCommands();
                break;
            case nameof(RunLabel):
                OnPropertyChanged(nameof(StartDebuggingLabel));
                break;
            case nameof(StartupProject):
                NotifyDebugCommands();
                break;
            case nameof(HasDocuments):
                OnPropertyChanged(nameof(ShowsEditorTouchBar));
                NotifyDebugCommands();
                break;
            case nameof(ActiveDocument):
                OnPropertyChanged(nameof(ShowsEditorTouchBar));
                break;
            case nameof(SelectedPanel):
                OnPropertyChanged(nameof(IsLocalsSelected));
                OnPropertyChanged(nameof(IsWatchSelected));
                OnPropertyChanged(nameof(IsCallStackSelected));
                OnPropertyChanged(nameof(ShowsDebugPanels));
                OnPropertyChanged(nameof(ShowsCallStackBeside));
                break;
        }
    }

    private void OnOutputPropertyChangedWhileDebugging(object? sender, PropertyChangedEventArgs e)
    {
        // A debugged program waiting for input needs the Output panel, wherever the debug layout left the user.
        if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && Output.IsWaitingForInput && IsDebugging)
        {
            SelectPanel(BottomPanel.Output);
        }
    }

    private void NotifyDebugCommands()
    {
        StartDebuggingCommand.NotifyCanExecuteChanged();
        StepIntoCommand.NotifyCanExecuteChanged();
        StepOverCommand.NotifyCanExecuteChanged();
        StepOutCommand.NotifyCanExecuteChanged();
        BreakAllCommand.NotifyCanExecuteChanged();
        StopDebuggingCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
        RunToCursorCommand.NotifyCanExecuteChanged();
        ToggleBreakpointCommand.NotifyCanExecuteChanged();
        ShowNextStatementCommand.NotifyCanExecuteChanged();
    }

    private static ExecutionPoint? PointIn(DocumentViewModel document, DebugSourceSpan? span) =>
        span is not null && string.Equals(span.FilePath, document.FilePath, StringComparison.Ordinal)
            ? new ExecutionPoint(span.Start, span.Length)
            : null;

    private static DebugValue? FindValue(DebugPausedEventArgs pause, string expression)
    {
        try
        {
            return pause.FindValue(expression);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A DataTip is best effort: a path the evaluator cannot follow shows nothing.
            return null;
        }
    }

    private static string DescribePause(DebugPausedEventArgs pause)
    {
        if (pause.ConditionError is { } error)
        {
            return $"The breakpoint's condition could not be evaluated: {error}";
        }

        var where = pause.Location is { } location
            ? $"{Path.GetFileName(location.FilePath)}, line {location.Line}"
            : "external code";
        return pause.Reason switch
        {
            PauseReason.Breakpoint => $"Paused at a breakpoint · {where}",
            PauseReason.Exception => $"Exception Unhandled: {pause.Exception?.GetType().FullName}",
            PauseReason.Pause => $"Paused (Break All) · {where}",
            PauseReason.RunToCursor => $"Paused at the cursor · {where}",
            _ => $"Paused · {where}",
        };
    }

    private static (string Message, OutputKind Kind) DescribeExit(string name, RunResult result)
    {
        var code = result.ExitCode.ToString(CultureInfo.InvariantCulture);
        var exited = $"The program '{name}' has exited with code {code} (0x{result.ExitCode:x}).";
        return result.Outcome switch
        {
            RunOutcome.Completed or RunOutcome.Stopped => (exited, OutputKind.Info),
            RunOutcome.NoEntryPoint => ($"'{name}' has no Main method or top-level statements to run.", OutputKind.Failure),
            _ => (exited, OutputKind.Failure),
        };
    }
}
