using TabStudio.Compilation.Running;
using TabStudio.Debugging.Building;
using TabStudio.Debugging.Engine;
using TabStudio.Debugging.Inspection;
using TabStudio.Debugging.Instrumentation;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging;

/// <summary>
/// Runs one instrumented program under the debugger: breakpoints, Continue, Pause, Step Into, Over and Out, Stop,
/// break on unhandled exceptions, and the locals and call stack of the paused program.
/// </summary>
/// <remarks>
/// <para>
/// Android lets no app attach a debugger to itself, so TabStudio debugs by instrumentation: <see cref="DebugBuilder"/>
/// compiles the program with a call to the debugger before every statement, and the program pauses by blocking its
/// thread inside that call. While it is paused, the IDE inspects its values on its own thread.
/// </para>
/// <para>
/// A session runs its program once: create a session from a <see cref="DebugBuild"/>, set breakpoints, then call
/// <see cref="RunAsync"/>. Breakpoints can be added and removed at any time. Only one session runs at a time in the
/// process; a second <see cref="RunAsync"/> waits for the first to finish.
/// </para>
/// <para>
/// Thread safety: every member can be called from any thread. <see cref="Paused"/> is raised on the program's thread
/// that paused, just before it blocks; <see cref="Resumed"/> on the thread that called the resuming command, before
/// the program continues; <see cref="Exited"/> on a pool thread when the program ends. Handlers must not block, and
/// should hand the event to the UI thread. <see cref="DebugValue"/>s of a pause should be read while it lasts.
/// </para>
/// </remarks>
public sealed class DebugSession : IDisposable
{
    /// <summary>How long a property getter or <c>ToString</c> of the program may run while it is inspected.</summary>
    public static TimeSpan DefaultEvaluationTimeout { get; } = TimeSpan.FromSeconds(1);

    private const int NotStarted = 0;
    private const int Running = 1;
    private const int Finished = 2;

    private static readonly SemaphoreSlim ActiveSession = new(1, 1);

    private readonly DebugBuild _build;
    private readonly DebugEngine _engine;
    private readonly Lock _breakpointsGate = new();
    private readonly List<Breakpoint> _breakpoints = [];
    private readonly CancellationTokenSource _stop = new();
    private int _phase;
    private volatile bool _disposed;

    /// <summary>Creates a session for a successful debug build.</summary>
    /// <param name="build">The instrumented program.</param>
    /// <exception cref="ArgumentException">The build failed.</exception>
    public DebugSession(DebugBuild build)
        : this(build, DefaultEvaluationTimeout)
    {
    }

    /// <summary>Creates a session for a successful debug build.</summary>
    /// <param name="build">The instrumented program.</param>
    /// <param name="evaluationTimeout">How long one property getter or <c>ToString</c> may run during inspection.</param>
    /// <exception cref="ArgumentException">The build failed.</exception>
    public DebugSession(DebugBuild build, TimeSpan evaluationTimeout)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (!build.Success)
        {
            throw new ArgumentException("The build failed, so there is no program to debug.", nameof(build));
        }

        _build = build;
        _engine = new DebugEngine(build.DebugInfo, new ValueInspector(new Evaluator(evaluationTimeout)), OnPaused, OnResumed);
    }

    /// <summary>Raised when the program pauses. The event data has the location, call stack and locals.</summary>
    public event EventHandler<DebugPausedEventArgs>? Paused;

    /// <summary>Raised when the paused program is about to continue.</summary>
    public event EventHandler? Resumed;

    /// <summary>Raised once when the program has ended, however it ended.</summary>
    public event EventHandler<DebugExitedEventArgs>? Exited;

    /// <summary>Where every statement and method of the build is, for the breakpoint margin.</summary>
    public DebugInfoMap DebugInfo => _build.DebugInfo;

    /// <summary>Where the session is in its life.</summary>
    public DebugSessionState State => Volatile.Read(ref _phase) switch
    {
        NotStarted => DebugSessionState.NotStarted,
        Running => _engine.IsPaused ? DebugSessionState.Paused : DebugSessionState.Running,
        _ => DebugSessionState.Exited,
    };

    /// <summary>The current pause, or <see langword="null"/> while the program runs.</summary>
    public DebugPausedEventArgs? CurrentPause => _engine.CurrentPause;

    /// <summary>Whether to pause when an exception is about to leave the program unhandled. On by default.</summary>
    public bool BreakOnUnhandledExceptions
    {
        get => _engine.BreakOnUnhandledExceptions;
        set => _engine.BreakOnUnhandledExceptions = value;
    }

    /// <summary>The breakpoints, in the order they were set.</summary>
    public IReadOnlyList<Breakpoint> Breakpoints
    {
        get
        {
            lock (_breakpointsGate)
            {
                return [.. _breakpoints];
            }
        }
    }

    /// <summary>Sets a breakpoint on a line, or returns the one already there.</summary>
    /// <param name="filePath">Full path of the source file.</param>
    /// <param name="line">One-based line number.</param>
    /// <returns>The breakpoint; check <see cref="Breakpoint.IsBound"/> and <see cref="Breakpoint.Location"/> for where it hits.</returns>
    public Breakpoint SetBreakpoint(string filePath, int line)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);

        lock (_breakpointsGate)
        {
            var existing = _breakpoints.Find(b => b.Line == line && string.Equals(b.FilePath, filePath, StringComparison.Ordinal));
            if (existing is not null)
            {
                return existing;
            }

            var breakpoint = new Breakpoint(filePath, line, DebugInfo.FindStatement(filePath, line));
            _breakpoints.Add(breakpoint);
            _engine.SetBreakpoints(_breakpoints);
            return breakpoint;
        }
    }

    /// <summary>Removes the breakpoint on a line.</summary>
    /// <param name="filePath">Full path of the source file.</param>
    /// <param name="line">One-based line number the breakpoint was set on.</param>
    /// <returns><see langword="true"/> when there was one.</returns>
    public bool RemoveBreakpoint(string filePath, int line)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        lock (_breakpointsGate)
        {
            var removed = _breakpoints.RemoveAll(b => b.Line == line && string.Equals(b.FilePath, filePath, StringComparison.Ordinal)) > 0;
            _engine.SetBreakpoints(_breakpoints);
            return removed;
        }
    }

    /// <summary>Removes every breakpoint.</summary>
    public void ClearBreakpoints()
    {
        lock (_breakpointsGate)
        {
            _breakpoints.Clear();
            _engine.SetBreakpoints(_breakpoints);
        }
    }

    /// <summary>Runs the program under the debugger and waits until it ends.</summary>
    /// <param name="arguments">Command-line arguments passed to <c>Main</c>.</param>
    /// <param name="console">Receives the program's output.</param>
    /// <param name="input">Standard input, or <see langword="null"/> for an empty input.</param>
    /// <param name="cancellationToken">Stops the program, like <see cref="Stop"/>.</param>
    /// <returns>How the program ended.</returns>
    /// <exception cref="InvalidOperationException">The session already ran its program.</exception>
    public async Task<RunResult> RunAsync(
        IReadOnlyList<string> arguments,
        IConsoleSink console,
        ConsoleInput? input = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(console);
        if (Interlocked.CompareExchange(ref _phase, Running, NotStarted) != NotStarted)
        {
            throw new InvalidOperationException("This session has already run its program. Start a new session to debug again.");
        }

        RunResult result;
        try
        {
            await ActiveSession.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Volatile.Write(ref _phase, Finished);
            throw;
        }

        var sink = _engine.CreateSink();
        try
        {
            AppContext.SetData(DebugRuntimeSource.DataKey, sink);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
            result = await new ProgramRunner()
                .RunAsync(_build.Result.EntryAssembly!, _build.Result.Dependencies, arguments, console, input, stop.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            // Unblocks a paused thread and turns the hooks off; a program still running unwinds on its main thread.
            _engine.Stop();
            if (ReferenceEquals(AppContext.GetData(DebugRuntimeSource.DataKey), sink))
            {
                AppContext.SetData(DebugRuntimeSource.DataKey, null);
            }

            Volatile.Write(ref _phase, Finished);
            ActiveSession.Release();
        }

        Exited?.Invoke(this, new DebugExitedEventArgs(result));
        return result;
    }

    /// <summary>Continues the paused program until a breakpoint, an unhandled exception or the end.</summary>
    /// <returns><see langword="false"/> when the program was not paused.</returns>
    public bool Continue() => _engine.Resume(StepMode.Run);

    /// <summary>Runs the current statement, entering any call it makes into the program's own code.</summary>
    /// <returns><see langword="false"/> when the program was not paused.</returns>
    public bool StepInto() => _engine.Resume(StepMode.Into);

    /// <summary>Runs the current statement, including the calls it makes, and pauses at the next one.</summary>
    /// <remarks>
    /// Stepping over an <c>await</c> pauses at the next statement of the same method once it continues, even on another
    /// thread. When the method returns, it pauses at the caller's next statement.
    /// </remarks>
    /// <returns><see langword="false"/> when the program was not paused.</returns>
    public bool StepOver() => _engine.Resume(StepMode.Over);

    /// <summary>Runs to the end of the current function and pauses at the caller's next statement.</summary>
    /// <returns><see langword="false"/> when the program was not paused.</returns>
    public bool StepOut() => _engine.Resume(StepMode.Out);

    /// <summary>
    /// Run to Cursor: runs until the statement a breakpoint on this line would bind to. Resumes a paused program, or
    /// arms the target for a running one or one that has not started yet.
    /// </summary>
    /// <param name="filePath">Full path of the source file.</param>
    /// <param name="line">One-based line number.</param>
    /// <returns><see langword="false"/> when no statement starts on or after the line, or the session has stopped.</returns>
    public bool RunToLine(string filePath, int line)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return DebugInfo.FindStatement(filePath, line) is { } statement && _engine.RunTo(statement.Id);
    }

    /// <summary>Pauses the running program at the next statement any of its threads runs (Break All).</summary>
    /// <remarks>A program that is waiting outside its own code, for example for console input, pauses when it returns to it.</remarks>
    public void Pause() => _engine.Pause();

    /// <summary>
    /// Stops debugging: releases the program if it is paused, ends <see cref="RunAsync"/> with
    /// <see cref="RunOutcome.Stopped"/> and makes the program's main thread unwind at its next statement.
    /// </summary>
    public void Stop()
    {
        _engine.Stop();
        if (!_disposed)
        {
            _stop.Cancel();
        }
    }

    /// <summary>Stops the program if it is still running.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        _stop.Dispose();
    }

    private void OnPaused(DebugPausedEventArgs pause) => Paused?.Invoke(this, pause);

    private void OnResumed() => Resumed?.Invoke(this, EventArgs.Empty);
}
