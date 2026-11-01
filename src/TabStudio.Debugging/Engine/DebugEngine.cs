using System.Runtime.CompilerServices;
using TabStudio.Debugging.Inspection;
using TabStudio.Debugging.Instrumentation;
using TabStudio.Debugging.Symbols;
using TabStudio.Debugging.Recording;

namespace TabStudio.Debugging.Engine;

/// <summary>
/// Receives the hooks of an instrumented program and decides when it pauses. The program's threads call the
/// <c>On*</c> hooks; the IDE calls the commands.
/// </summary>
/// <remarks>
/// <para>
/// A thread that pauses blocks inside its hook until the IDE resumes it. While one thread is paused, every other
/// thread of the program blocks at its next hook too (all-stop), so the values the IDE inspects hold still. Code
/// that only runs outside instrumented methods, such as a base library call in progress, keeps running.
/// </para>
/// <para>
/// All state changes happen under one monitor; the hot path of <see cref="OnStep"/> only reads volatile fields.
/// After <see cref="Stop"/>, hooks do nothing, except on the program's main thread, where they throw
/// <see cref="DebugSessionStoppedException"/> so the program unwinds. Other threads are left alone, because an
/// exception there could take down the whole app. A hosted program (<see cref="IsHosted"/>) has no main thread of its
/// own: it runs on its host's threads, which are never unwound.
/// </para>
/// <para>
/// A thread the <see cref="Pump"/> can keep alive, such as the host's UI thread, does not block while it waits: it runs
/// the host's message loop until the program continues.
/// </para>
/// </remarks>
internal sealed class DebugEngine
{
    private const int MaxFrames = 10_000;

    private readonly object _gate = new();
    private readonly AsyncLocal<DebugFrame?> _current = new();
    private readonly ConditionalWeakTable<Exception, FaultOrigin> _faults = new();
    private volatile DebugInfoMap _map;
    private readonly ValueInspector _inspector;
    private readonly ValueInspector _visualBasicInspector;
    private readonly Action<DebugPausedEventArgs> _onPaused;
    private readonly Action _onResumed;

    private volatile BreakpointTable _breakpoints = BreakpointTable.Empty;
    private volatile StepMode _mode;
    private volatile DebugFrame? _stepFrame;
    private volatile int _stepDepth;
    private volatile bool _paused;
    private volatile bool _stopped;
    private volatile bool _breakOnUnhandledExceptions = true;
    private volatile bool _hosted;
    private volatile IDebugWaitPump? _pump;
    private volatile int _runToStatement;
    private int _mainThreadId;

    internal DebugRecording? Recording { get; set; }

    // Guarded by _gate.
    private long _pauseGeneration;
    private long _resumedGeneration;
    private DebugFrame? _pausedFrame;
    private DebugPausedEventArgs? _currentPause;

    public DebugEngine(DebugInfoMap map, ValueInspector inspector, Action<DebugPausedEventArgs> onPaused, Action onResumed)
    {
        _map = map;
        _inspector = inspector.ForLanguage(visualBasic: false);
        _visualBasicInspector = inspector.ForLanguage(visualBasic: true);
        _onPaused = onPaused;
        _onResumed = onResumed;
    }

    public void UpdateDebugInfo(DebugInfoMap map)
    {
        lock (_gate)
        {
            _map = map;
        }
    }

    /// <summary>Whether to pause when an exception is about to leave the program unhandled.</summary>
    public bool BreakOnUnhandledExceptions
    {
        get => _breakOnUnhandledExceptions;
        set => _breakOnUnhandledExceptions = value;
    }

    /// <summary>Whether a thread of the program is paused.</summary>
    public bool IsPaused => _paused;

    /// <summary>
    /// Whether the program runs on its host's threads, as an Avalonia app does on the UI thread, rather than on a main
    /// thread of its own: then stopping never throws into its threads.
    /// </summary>
    public bool IsHosted
    {
        get => _hosted;
        set => _hosted = value;
    }

    /// <summary>Keeps the host's UI thread alive while it waits, or <see langword="null"/> to block every waiting thread.</summary>
    public IDebugWaitPump? Pump
    {
        get => _pump;
        set => _pump = value;
    }

    /// <summary>The current pause, or <see langword="null"/> while running.</summary>
    public DebugPausedEventArgs? CurrentPause
    {
        get
        {
            lock (_gate)
            {
                return _currentPause;
            }
        }
    }

    /// <summary>
    /// The sink the program's runtime reads from <see cref="AppContext"/>: the contract version and one delegate per
    /// hook, in the order <see cref="DebugRuntimeSource"/> documents.
    /// </summary>
    public object[] CreateSink() =>
    [
        DebugRuntimeSource.ContractVersion,
        new Func<int, object?>(OnEnter),
        new Action<object?>(OnExit),
        new Action<object?>(OnSuspend),
        new Action<object?>(OnResume),
        new Func<object?, int, bool>(OnStep),
        new Action<object?, int, object?[]?>(OnBreak),
        new Func<object?, Exception, object?[]?, bool>(OnFault),
        Recording is null ? null! : new Action<object?, int, string, object?>(OnWrite),
    ];

    // ---- Commands, called by the IDE ----

    /// <summary>Replaces the set of breakpoints.</summary>
    public void SetBreakpoints(IReadOnlyCollection<Breakpoint> breakpoints) =>
        _breakpoints = BreakpointTable.Create(breakpoints, _map.MaxStatementId);

    /// <summary>Breaks at the next statement any thread runs.</summary>
    public void Pause()
    {
        lock (_gate)
        {
            if (!_paused && !_stopped)
            {
                _mode = StepMode.Pause;
            }
        }
    }

    /// <summary>Cancels a <see cref="Pause"/> that has not paused the program yet.</summary>
    /// <returns><see langword="true"/> when a pause was pending.</returns>
    public bool CancelPause()
    {
        lock (_gate)
        {
            if (_paused || _mode != StepMode.Pause)
            {
                return false;
            }

            _mode = StepMode.Run;
            return true;
        }
    }

    /// <summary>Resumes the paused program.</summary>
    /// <param name="mode">How far it runs before pausing again.</param>
    /// <returns><see langword="false"/> when the program was not paused.</returns>
    public bool Resume(StepMode mode)
    {
        long generation;
        lock (_gate)
        {
            if (!_paused || _stopped || _resumedGeneration == _pauseGeneration)
            {
                return false;
            }

            generation = _pauseGeneration;
            _resumedGeneration = generation;
        }

        // Raised before the program continues, so it always comes before the next Paused.
        _onResumed();

        lock (_gate)
        {
            if (_stopped || _pauseGeneration != generation)
            {
                return false;
            }

            var frame = _pausedFrame;
            _stepFrame = frame;
            _stepDepth = frame?.Depth ?? 0;
            _mode = mode;
            _pausedFrame = null;
            _currentPause = null;
            _paused = false;
            Monitor.PulseAll(_gate);
        }

        _pump?.Wake();
        return true;
    }

    /// <summary>
    /// Runs until a statement (Run to Cursor): resumes the program when it is paused, or arms the target when it is
    /// running or has not started. A breakpoint or exception on the way pauses first and cancels the target.
    /// </summary>
    /// <param name="statementId">The statement to stop at.</param>
    /// <returns><see langword="false"/> when the session has stopped.</returns>
    public bool RunTo(int statementId)
    {
        if (_stopped)
        {
            return false;
        }

        _runToStatement = statementId;
        if (_paused)
        {
            Resume(StepMode.Run);
        }

        return true;
    }

    /// <summary>Releases every waiting thread and turns the hooks off.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _stopped = true;
            _paused = false;
            _pausedFrame = null;
            _currentPause = null;
            Monitor.PulseAll(_gate);
        }

        _pump?.Wake();
    }

    // ---- Hooks, called by the program ----

    private DebugFrame? OnEnter(int methodId)
    {
        if (Evaluator.IsEvaluating || _stopped)
        {
            ThrowIfStoppedOnMainThread();
            return null;
        }

        var parent = _current.Value;
        if (parent is null && !_hosted && !Thread.CurrentThread.IsThreadPoolThread)
        {
            // The first function entered without a caller on a dedicated thread is the program's entry point.
            Interlocked.CompareExchange(ref _mainThreadId, Environment.CurrentManagedThreadId, 0);
        }

        var frame = new DebugFrame(methodId, parent);
        _current.Value = frame;
        return frame;
    }

    private void OnExit(object? token)
    {
        if (token is not DebugFrame frame)
        {
            return;
        }

        frame.Exited = true;
        if (ReferenceEquals(_current.Value, frame))
        {
            _current.Value = frame.Parent;
        }
    }

    private void OnSuspend(object? token)
    {
        if (token is DebugFrame frame && ReferenceEquals(_current.Value, frame))
        {
            _current.Value = frame.Parent;
        }
    }

    private void OnResume(object? token)
    {
        if (token is not DebugFrame frame || Evaluator.IsEvaluating)
        {
            return;
        }

        var current = _current.Value;
        if (!ReferenceEquals(current, frame))
        {
            frame.Reparent(current);
            _current.Value = frame;
        }
    }

    private bool OnStep(object? token, int statementId)
    {
        if (Evaluator.IsEvaluating)
        {
            return false;
        }

        var frame = token as DebugFrame;
        if (frame is not null)
        {
            frame.StatementId = statementId;
        }

        if (_paused)
        {
            WaitWhilePaused();
        }

        if (_stopped)
        {
            ThrowIfStoppedOnMainThread();
            return false;
        }

        var mode = _mode;
        return Recording is not null || (mode != StepMode.Run && IsStepComplete(mode, frame)) || _breakpoints.Contains(statementId) || statementId == _runToStatement;
    }

    private void OnBreak(object? token, int statementId, object?[]? locals)
    {
        if (Evaluator.IsEvaluating)
        {
            return;
        }

        var frame = token as DebugFrame;
        var statement = _map.GetStatement(statementId);
        var slots = statement?.Locals;
        var visualBasic = SourceLanguage.IsVisualBasic(statement?.Location.FilePath);

        // A condition can run property getters of the program, so it is evaluated before taking the gate.
        DebugValue[]? values = null;
        string? conditionError = null;
        var conditionMet = true;
        if (_breakpoints.Find(statementId)?.Condition is { } condition)
        {
            values = CreateLocals(slots, locals, visualBasic);
            conditionMet = BreakpointCondition.IsMet(condition, values, visualBasic, out conditionError);
        }

        DebugPausedEventArgs? pause = null;
        lock (_gate)
        {
            WaitHoldingGate(() => _paused && !_stopped);
            var reason = ReasonToBreak(frame, statementId);
            if (!_stopped && frame is not null)
                Recording?.Record(frame, slots, locals, reason == PauseReason.Breakpoint && conditionMet);
            if (!_stopped && reason is { } pauseReason && (pauseReason != PauseReason.Breakpoint || conditionMet))
            {
                var breakpoint = pauseReason == PauseReason.Breakpoint ? _breakpoints.Find(statementId) : null;
                breakpoint?.RecordHit();
                pause = BeginPause(pauseReason, frame, values ?? CreateLocals(slots, locals, visualBasic), breakpoint, exception: null, conditionError);
            }
        }

        if (pause is null)
        {
            ThrowIfStoppedOnMainThread();
            return;
        }

        WaitForResume(pause);
    }

    /// <summary>
    /// The fault filter: runs while an exception searches for a handler, before anything unwinds. Reaching the
    /// filter of the entry point means no instrumented frame handles the exception.
    /// </summary>
    private bool OnFault(object? token, Exception exception, object?[]? values)
    {
        if (Evaluator.IsEvaluating || _stopped || exception is DebugSessionStoppedException)
        {
            return false;
        }

        var frame = token as DebugFrame;
        if (!_faults.TryGetValue(exception, out var origin))
        {
            // The first filter to see an exception belongs to the frame that threw it.
            origin = new FaultOrigin(frame, values);
            _faults.AddOrUpdate(exception, origin);
        }

        if (frame is null || frame.Parent is not null)
        {
            return false;
        }

        if (origin.Frame is not null) Recording?.FindExceptionCause(origin.Frame, exception);
        if (!_breakOnUnhandledExceptions) return false;

        // Environment.Exit, redirected so that the program ends and TabStudio does not (InProcessExit): it is how the
        // program ends, not an exception it failed to handle, so the debugger lets it end as its process would.
        if (TabStudio.Compilation.InProcessExit.TryGetExitCode(exception, out _)) return false;

        // Nothing has unwound yet, so the innermost frame of this flow is still the one that threw.
        var top = _current.Value ?? frame;
        var method = _map.GetMethod(top.MethodId);
        var locals = ReferenceEquals(origin.Frame, top)
            ? CreateLocals(method?.Parameters, origin.Values, SourceLanguage.IsVisualBasic(method?.Location.FilePath))
            : Array.Empty<DebugValue>();

        DebugPausedEventArgs pause;
        lock (_gate)
        {
            WaitHoldingGate(() => _paused && !_stopped);

            if (_stopped)
            {
                return false;
            }

            pause = BeginPause(PauseReason.Exception, top, locals, breakpoint: null, exception);
        }

        try
        {
            WaitForResume(pause);
        }
        catch (DebugSessionStoppedException)
        {
            // Stopped while paused: the exception goes on and ends the program.
        }

        return false;
    }

    // ---- Internals ----

    private void OnWrite(object? token, int site, string path, object? value)
    {
        if (!Evaluator.IsEvaluating && !_stopped && token is DebugFrame frame)
            Recording?.Write(frame, site, path, value);
    }

    private PauseReason? ReasonToBreak(DebugFrame? frame, int statementId)
    {
        var mode = _mode;
        if (mode == StepMode.Pause)
        {
            return PauseReason.Pause;
        }

        if (mode != StepMode.Run && IsStepComplete(mode, frame))
        {
            return PauseReason.Step;
        }

        if (_breakpoints.Contains(statementId))
        {
            return PauseReason.Breakpoint;
        }

        return statementId == _runToStatement ? PauseReason.RunToCursor : null;
    }

    private bool IsStepComplete(StepMode mode, DebugFrame? frame)
    {
        var target = _stepFrame;
        var depth = frame?.Depth ?? 0;
        return mode switch
        {
            StepMode.Pause or StepMode.Into => true,
            StepMode.Over => target is null || ReferenceEquals(frame, target) || (target.Exited && depth <= _stepDepth),
            StepMode.Out => target is null || (target.Exited && depth < _stepDepth),
            _ => false,
        };
    }

    /// <summary>Records a new pause. The caller holds the gate.</summary>
    private DebugPausedEventArgs BeginPause(
        PauseReason reason,
        DebugFrame? frame,
        IReadOnlyList<DebugValue> locals,
        Breakpoint? breakpoint,
        Exception? exception,
        string? conditionError = null)
    {
        _pauseGeneration++;
        _runToStatement = 0;
        _mode = StepMode.Run;
        _stepFrame = null;
        _pausedFrame = frame;
        _paused = true;
        _currentPause = new DebugPausedEventArgs(
            reason,
            CreateCallStack(frame),
            locals,
            breakpoint,
            exception,
            Environment.CurrentManagedThreadId,
            _pauseGeneration,
            conditionError);
        return _currentPause;
    }

    /// <summary>Tells the IDE the program paused, then blocks the thread until it resumes.</summary>
    private void WaitForResume(DebugPausedEventArgs pause)
    {
        _onPaused(pause);
        lock (_gate)
        {
            WaitHoldingGate(() => _paused && !_stopped && _pauseGeneration == pause.Generation);
        }

        ThrowIfStoppedOnMainThread();
    }

    private void WaitWhilePaused()
    {
        lock (_gate)
        {
            WaitHoldingGate(() => _paused && !_stopped);
        }
    }

    /// <summary>
    /// Waits while a condition holds. The caller holds the gate, and holds it again when this returns. A thread the
    /// pump keeps alive runs the host's message loop meanwhile, without the gate; any other thread blocks.
    /// </summary>
    private void WaitHoldingGate(Func<bool> waiting)
    {
        while (waiting())
        {
            if (_pump is not { CanPumpCurrentThread: true } pump)
            {
                Monitor.Wait(_gate);
                continue;
            }

            Monitor.Exit(_gate);
            try
            {
                pump.PumpWhile(() =>
                {
                    lock (_gate)
                    {
                        return waiting();
                    }
                });
            }
            finally
            {
                Monitor.Enter(_gate);
            }
        }
    }

    private void ThrowIfStoppedOnMainThread()
    {
        if (_stopped && !_hosted && !Evaluator.IsEvaluating && Environment.CurrentManagedThreadId == Volatile.Read(ref _mainThreadId))
        {
            throw new DebugSessionStoppedException();
        }
    }

    private List<DebugStackFrame> CreateCallStack(DebugFrame? frame)
    {
        var frames = new List<DebugStackFrame>();
        for (var current = frame; current is not null && frames.Count < MaxFrames; current = current.Parent)
        {
            var method = _map.GetMethod(current.MethodId);
            var statement = _map.GetStatement(current.StatementId);
            frames.Add(new DebugStackFrame(method?.Name ?? "?", statement?.Location ?? method?.Location));
        }

        return frames;
    }

    /// <summary>The values a hook passed, shown in the language of the code that paused.</summary>
    private DebugValue[] CreateLocals(IReadOnlyList<DebugLocalSlot>? slots, object?[]? values, bool visualBasic)
    {
        if (slots is null || values is null)
        {
            return [];
        }

        var inspector = visualBasic ? _visualBasicInspector : _inspector;
        var count = Math.Min(slots.Count, values.Length);
        var result = new List<DebugValue>(count);
        for (var i = 0; i < count; i++)
        {
            if (!slots[i].RecordingOnly || Recording is not null)
                result.Add(new DebugValue(slots[i].Name, slots[i].Type, values[i], inspector));
        }

        return result.ToArray();
    }

    /// <summary>The frame an exception was first seen in, and the parameter values its filter passed.</summary>
    private sealed record FaultOrigin(DebugFrame? Frame, object?[]? Values);

    /// <summary>An immutable lookup of the statements that have a breakpoint.</summary>
    private sealed class BreakpointTable
    {
        private readonly bool[] _flags;
        private readonly Dictionary<int, Breakpoint> _byStatement;

        private BreakpointTable(bool[] flags, Dictionary<int, Breakpoint> byStatement)
        {
            _flags = flags;
            _byStatement = byStatement;
        }

        public static BreakpointTable Empty { get; } = new([], []);

        public static BreakpointTable Create(IReadOnlyCollection<Breakpoint> breakpoints, int maxStatementId)
        {
            var flags = new bool[maxStatementId + 1];
            var byStatement = new Dictionary<int, Breakpoint>();
            foreach (var breakpoint in breakpoints)
            {
                var id = breakpoint.StatementId;
                if (id > 0 && id < flags.Length)
                {
                    flags[id] = true;
                    byStatement.TryAdd(id, breakpoint);
                }
            }

            return new BreakpointTable(flags, byStatement);
        }

        public bool Contains(int statementId) => (uint)statementId < (uint)_flags.Length && _flags[statementId];

        public Breakpoint? Find(int statementId) => _byStatement.GetValueOrDefault(statementId);
    }
}
