using System.Globalization;
using TabStudio.App.Swarm;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging;
using TabStudio.Debugging.Building;
using TabStudio.Swarm;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Debugging on the swarm, with the tablet's own debugger: a breakpoint on any device pauses the app there (and, with
/// Pause all on a breakpoint, on every device), and the editor marks the device's current statement while Locals,
/// Watch, DataTips and the Call Stack show the device's state, the device named in every frame. The Call Stack's box
/// chooses among the paused devices. Continue, the steps, Break All, Run to Cursor and Stop act on the swarm.
/// </summary>
/// <remarks>
/// Follows the pattern of debug adapters (<c>WorkspaceViewModel.DebugAdapters.cs</c>): every debug command asks
/// <see cref="_swarmDebug"/> first. The tablet keeps a debug session of the same build that never runs, so the
/// Breakpoints panel binds breakpoints to statements as it does for a program run here.
/// </remarks>
public sealed partial class WorkspaceViewModel
{
    private SwarmDebugState? _swarmDebug;
    private DebugSourceSpan? _swarmCurrent;

    /// <summary>The pause the debugger shows, for tests.</summary>
    internal SwarmPause? ShownSwarmPause => _swarmDebug?.Shown;

    /// <summary>Starts listening for pauses. Called once, by <see cref="InitializeSwarm"/>.</summary>
    private void InitializeSwarmDebugging(SwarmCoordinatorService swarm)
    {
        swarm.Paused += OnSwarmPaused;
        swarm.Resumed += OnSwarmResumed;
        Breakpoints.Changed += OnBreakpointsChangedForSwarm;
        CallStack.ThreadChosen += OnSwarmDeviceChosen;
    }

    private void DisposeSwarmDebugging(SwarmCoordinatorService swarm)
    {
        swarm.Paused -= OnSwarmPaused;
        swarm.Resumed -= OnSwarmResumed;
        Breakpoints.Changed -= OnBreakpointsChangedForSwarm;
        CallStack.ThreadChosen -= OnSwarmDeviceChosen;
        EndSwarmDebugging();
    }

    /// <summary>A debug run on the swarm begins: breakpoints bind to the build and go to the devices.</summary>
    private void BeginSwarmDebugging(ProjectInfo project, DebugBuild build)
    {
        EndSwarmDebugging();
        var shadow = new DebugSession(build) { BreakOnUnhandledExceptions = BreakOnUnhandledExceptions };
        _swarmDebug = new SwarmDebugState(project, shadow);
        Breakpoints.Bind(shadow);
        _ = SyncSwarmBreakpointsAsync();
    }

    /// <summary>The debug run on the swarm ended: the debugger's panels are cleared.</summary>
    private void EndSwarmDebugging()
    {
        if (_swarmDebug is not { } debug)
        {
            return;
        }

        _swarmDebug = null;
        _swarmCurrent = null;
        _exceptionHelper = null;
        Breakpoints.Unbind();
        debug.Shadow.Dispose();
        CallStack.Clear();
        CallStack.ClearThreads();
        Locals.Clear();
        _ = Watch.EvaluateWithAsync(null);
        ShowExecutionPoints();
        if (SelectedPanel is BottomPanel.Locals or BottomPanel.Watch or BottomPanel.CallStack)
        {
            SelectedPanel = BottomPanel.Output;
        }
    }

    private void OnBreakpointsChangedForSwarm(object? sender, EventArgs e)
    {
        if (_swarmDebug is not null)
        {
            _ = SyncSwarmBreakpointsAsync();
        }
    }

    /// <summary>Sends every breakpoint to the swarm, whose devices bind them as the tablet does.</summary>
    private async Task SyncSwarmBreakpointsAsync()
    {
        if (_swarmServices?.Coordinator.Coordinator is { } coordinator)
        {
            await coordinator.SetBreakpointsAsync([.. Breakpoints.All.Select(b => new SwarmBreakpoint(b.FilePath, b.Line, b.Condition))]);
        }
    }

    /// <summary>
    /// A device paused: shown unless the debugger already shows another device's pause, which stays in front while the
    /// devices that Pause all on a breakpoint stopped join the Call Stack's box. The device that steps is always shown.
    /// </summary>
    private void OnSwarmPaused(object? sender, SwarmPause pause)
    {
        if (_swarmDebug is not { } debug)
        {
            return;
        }

        if (ReferenceEquals(debug.Stepping, pause.Device))
        {
            debug.Stepping = null;
        }
        else if (debug.Shown is { IsCurrent: true } shown && !ReferenceEquals(shown.Device, pause.Device))
        {
            ShowSwarmDevices(shown);
            return;
        }

        _pauseShown = ShowSwarmPauseAsync(pause);
    }

    /// <summary>A device continued, or was lost while paused: the debugger shows another paused device, or that the app runs.</summary>
    private void OnSwarmResumed(object? sender, SwarmDevice device)
    {
        if (_swarmDebug is not { Shown: { } shown } debug)
        {
            return;
        }

        if (shown.IsCurrent)
        {
            ShowSwarmDevices(shown);
        }
        else if (!ReferenceEquals(debug.Stepping, shown.Device) && PausedDevices().FirstOrDefault()?.Pause is { } next)
        {
            _pauseShown = ShowSwarmPauseAsync(next);
        }
        else
        {
            ShowSwarmRunning();
        }
    }

    private async Task ShowSwarmPauseAsync(SwarmPause pause)
    {
        if (_swarmDebug is not { } debug || !pause.IsCurrent)
        {
            return;
        }

        debug.Shown = pause;
        var device = pause.Device;

        // A breakpoint of Run to Cursor is only for that run.
        if (debug.CursorBreakpoint is not null)
        {
            debug.CursorBreakpoint = null;
            _ = SyncSwarmBreakpointsAsync();
        }

        _swarmCurrent = pause.Location;
        _exceptionHelper = pause.Exception is { } fault
            ? new ExceptionHelperViewModel(fault.Type, fault.Message, $"{fault.Type}: {fault.Message}{Environment.NewLine}{fault.StackTrace}".TrimEnd(), StartDebuggingCommand, StopDebuggingCommand)
            : null;
        State = IdeState.Paused;
        StatusText = DescribeSwarmPause(pause);
        ShowSwarmDevices(pause);
        CallStack.Show([.. pause.Frames.Select(f => new DebugStackFrame($"{device.Name}!{f.MethodName}", f.Location))]);

        // Like Visual Studio's debug layout: the variables come into view when the program pauses.
        if (!IsBottomPanelVisible || SelectedPanel is not (BottomPanel.Locals or BottomPanel.Watch or BottomPanel.CallStack))
        {
            SelectPanel(BottomPanel.Locals);
        }

        await ShowSwarmFrameAsync(CallStack.ActiveFrame);
    }

    /// <summary>The Call Stack's box: every paused device, the shown one chosen.</summary>
    private void ShowSwarmDevices(SwarmPause shown)
    {
        var paused = PausedDevices();
        var options = paused.Select((d, i) => new DebugThreadOption(i + 1, $"{d.Name} · {DescribeWhere(d.Pause!)}")).ToList();
        var index = paused.FindIndex(d => ReferenceEquals(d, shown.Device));
        CallStack.ShowThreads(options, index >= 0 ? options[index] : null);
    }

    private void OnSwarmDeviceChosen(object? sender, DebugThreadOption option)
    {
        if (_swarmDebug is not { } debug)
        {
            return;
        }

        var paused = PausedDevices();
        if (option.Id >= 1 && option.Id <= paused.Count && paused[option.Id - 1].Pause is { } pause && !ReferenceEquals(pause, debug.Shown))
        {
            _pauseShown = ShowSwarmPauseAsync(pause);
        }
    }

    /// <summary>Shows a frame: its statement in the editor, and the paused statement's variables in Locals and Watch.</summary>
    private async Task ShowSwarmFrameAsync(StackFrameViewModel? frame)
    {
        ShowExecutionPoints();
        if (_swarmDebug?.Shown is not { } pause || frame is null)
        {
            return;
        }

        if (frame.Location is { } location && await OpenDocumentAsync(location.FilePath) is { } document)
        {
            ApplyExecutionPoints(document);
            document.NavigateTo(location.Line, location.Column);
        }

        List<VariableSource> sources = [];
        try
        {
            foreach (var variable in await pause.GetLocalsAsync(CancellationToken.None))
            {
                sources.Add(new VariableSource(variable.Name, variable.Path, null) { Variable = new SwarmDebugVariable(pause, variable) });
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            sources.Add(new VariableSource("Locals", string.Empty, null, $"{pause.Device.Name} did not send its variables: {ex.Message}"));
        }

        if (!ReferenceEquals(_swarmDebug?.Shown, pause))
        {
            return;
        }

        var top = pause.Frames.Count > 0 ? pause.Frames[0].MethodName : "the paused method";
        var note = frame.IsTop ? null : $"These are the locals of {top} on {pause.Device.Name}, where the app is paused.";
        await Task.WhenAll(
            Locals.ShowVariablesAsync(sources, note),
            Watch.EvaluateWithAsync(expression => EvaluateOnSwarmAsync(pause, expression)));
    }

    private static async Task<VariableSource> EvaluateOnSwarmAsync(SwarmPause pause, string expression)
    {
        try
        {
            var result = await pause.EvaluateAsync(expression, CancellationToken.None);
            return result.Value is { } value
                ? new VariableSource(expression, expression, null) { Variable = new SwarmDebugVariable(pause, value) }
                : new VariableSource(expression, expression, null, result.Error ?? "There is no such value.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new VariableSource(expression, expression, null, $"{pause.Device.Name} did not answer: {ex.Message}");
        }
    }

    /// <summary>A DataTip over the code, read from the device the debugger shows.</summary>
    private async Task<VariableTreeViewModel?> EvaluateSwarmDataTipAsync(string expression)
    {
        if (_swarmDebug?.Shown is not { IsCurrent: true } pause || string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var source = await EvaluateOnSwarmAsync(pause, expression);
        if (source.Variable is null || !ReferenceEquals(_swarmDebug?.Shown, pause))
        {
            return null;
        }

        var tree = new VariableTreeViewModel();
        await tree.ShowAsync([source]);
        return tree;
    }

    /// <summary>F5 while paused: every paused device continues, together.</summary>
    private async Task ContinueSwarmAsync()
    {
        if (_swarmServices?.Coordinator.Coordinator is not { } coordinator)
        {
            return;
        }

        // Shown as running at once, as for a program debugged here; a device that pauses again shows itself.
        ShowSwarmRunning();
        await coordinator.ContinueAllAsync();
    }

    /// <summary>F10, F11 and Shift+F11: the shown device steps; the others continue with it.</summary>
    private async Task StepSwarmAsync(SwarmResumeMode mode)
    {
        if (_swarmDebug is not { Shown: { IsCurrent: true } pause } debug || _swarmServices?.Coordinator.Coordinator is not { } coordinator)
        {
            return;
        }

        debug.Stepping = pause.Device;
        ShowSwarmRunning();
        await pause.ResumeAsync(mode, CancellationToken.None);
        await coordinator.ContinueAllAsync(except: pause.Device);
    }

    /// <summary>Break All: every device pauses at the next statement of the app's own code.</summary>
    private async Task PauseSwarmAsync()
    {
        if (_swarmServices?.Coordinator.Coordinator is { } coordinator)
        {
            await coordinator.PauseAllAsync();
        }
    }

    /// <summary>Ctrl+F10 while paused: every device runs to the caret's line, as a breakpoint there for this run would.</summary>
    private async Task RunSwarmToCursorAsync(DocumentViewModel document)
    {
        if (_swarmServices?.Coordinator.Coordinator is not { } coordinator || _swarmDebug is not { } debug)
        {
            return;
        }

        debug.CursorBreakpoint = (document.FilePath, document.CaretLine);
        ShowSwarmRunning();
        await coordinator.SetBreakpointsAsync([.. Breakpoints.All.Select(b => new SwarmBreakpoint(b.FilePath, b.Line, b.Condition)), new SwarmBreakpoint(document.FilePath, document.CaretLine, null)]);
        await coordinator.ContinueAllAsync();
    }

    /// <summary>Shift+F5 while debugging on the swarm: the app stops on every device.</summary>
    private bool StopSwarmDebugging()
    {
        if (_swarmDebug is null)
        {
            return false;
        }

        _run?.Cancel();
        return true;
    }

    /// <summary>Ctrl+Shift+F5: a fresh build replaces the app on every device, as a save does.</summary>
    private bool RestartSwarmDebugging()
    {
        if (_swarmDebug is null || _run is not { } run)
        {
            return false;
        }

        _restartSwarm = true;
        run.Cancel();
        return true;
    }

    private void ShowSwarmRunning()
    {
        if (_swarmDebug is not { } debug)
        {
            return;
        }

        debug.Shown = null;
        _swarmCurrent = null;
        _exceptionHelper = null;
        State = IdeState.Debugging;
        StatusText = $"Debugging {debug.Project.Name} on the swarm";
        CallStack.Clear();
        CallStack.ClearThreads();
        Locals.Tree.IsStale = true;
        Watch.MarkStale();
        ShowExecutionPoints();

    }

    private List<SwarmDevice> PausedDevices() =>
        _swarmServices?.Coordinator.Devices.Where(d => d.Pause is { IsCurrent: true }).ToList() ?? [];

    private static string DescribeSwarmPause(SwarmPause pause)
    {
        var device = pause.Device.Name;
        if (pause.ConditionError is { } error)
        {
            return $"The breakpoint's condition could not be evaluated on {device}: {error}";
        }

        var where = DescribeWhere(pause);
        return pause.Reason switch
        {
            PauseReason.Breakpoint => $"Paused at a breakpoint on {device} · {where}",
            PauseReason.Exception => $"Exception Unhandled on {device}: {pause.Exception?.Type}",
            PauseReason.Pause => $"Paused (Break All) on {device} · {where}",
            PauseReason.RunToCursor => $"Paused at the cursor on {device} · {where}",
            _ => $"Paused on {device} · {where}",
        };
    }

    private static string DescribeWhere(SwarmPause pause) =>
        pause.Location is { } location
            ? string.Create(CultureInfo.CurrentCulture, $"{Path.GetFileName(location.FilePath)}, line {location.Line}")
            : "external code";

    /// <summary>A debug run on the swarm.</summary>
    /// <param name="project">The debugged project.</param>
    /// <param name="shadow">A session of the same build that never runs, which the Breakpoints panel binds to.</param>
    private sealed class SwarmDebugState(ProjectInfo project, DebugSession shadow)
    {
        /// <summary>The debugged project.</summary>
        public ProjectInfo Project { get; } = project;

        /// <summary>The session the Breakpoints panel binds to.</summary>
        public DebugSession Shadow { get; } = shadow;

        /// <summary>The pause the debugger shows.</summary>
        public SwarmPause? Shown { get; set; }

        /// <summary>The breakpoint of a Run to Cursor, until the next pause.</summary>
        public (string FilePath, int Line)? CursorBreakpoint { get; set; }

        /// <summary>The device that steps, which is shown when it pauses again.</summary>
        public SwarmDevice? Stepping { get; set; }
    }
}
