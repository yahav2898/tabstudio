using System.Reflection;
using System.Runtime.CompilerServices;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
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
/// thread inside that call. While it is paused, the IDE inspects its values on its own thread. C# and Visual Basic
/// programs are debugged the same way.
/// </para>
/// <para>
/// A session runs its program once: create a session from a <see cref="DebugBuild"/>, set breakpoints, then call
/// <see cref="RunAsync(IReadOnlyList{string}, IConsoleSink, ConsoleInput, CancellationToken)"/>. Breakpoints can be added and removed at any time. Only one session runs at a time in the
/// process; a second <see cref="RunAsync(IReadOnlyList{string}, IConsoleSink, ConsoleInput, CancellationToken)"/> waits for the first to finish.
/// </para>
/// <para>
/// A host that loads and runs the program itself, such as the app preview of a device in a swarm showing an Avalonia
/// app, calls <see cref="Attach"/> once the program is loaded instead, and <see cref="EndHostedRun"/> when it ends.
/// Such hosted sessions can run side by side, each with its own program.
/// </para>
/// <para>
/// Thread safety: every member can be called from any thread. <see cref="Paused"/> is raised on the program's thread
/// that paused, just before it blocks; <see cref="Resumed"/> on the thread that called the resuming command, before
/// the program continues; <see cref="Exited"/> on a pool thread when the program ends. Handlers must not block, and
/// should hand the event to the UI thread. <see cref="DebugValue"/>s of a pause should be read while it lasts.
/// </para>
/// </remarks>
public sealed partial class DebugSession : IDisposable
{
    /// <summary>How long a property getter or <c>ToString</c> of the program may run while it is inspected.</summary>
    public static TimeSpan DefaultEvaluationTimeout { get; } = TimeSpan.FromSeconds(1);

    private const int NotStarted = 0;
    private const int Running = 1;
    private const int Finished = 2;

    private static readonly SemaphoreSlim ActiveSession = new(1, 1);

    /// <summary>Hosted sessions connect their programs one at a time, as that briefly uses the process-wide sink.</summary>
    private static readonly Lock AttachGate = new();

    private DebugBuild _build;
    private DebugInfoMap _debugInfo;
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
    /// <param name="recording">Opt-in ring settings, or null to leave recording off.</param>
    /// <exception cref="ArgumentException">The build failed.</exception>
    public DebugSession(DebugBuild build, TimeSpan evaluationTimeout, Recording.RecordingOptions? recording = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (!build.Success)
        {
            throw new ArgumentException("The build failed, so there is no program to debug.", nameof(build));
        }

        _build = build;
        _debugInfo = build.DebugInfo;
        Language = build.Language;
        _engine = new DebugEngine(build.DebugInfo, new ValueInspector(new Evaluator(evaluationTimeout)), OnPaused, OnResumed);
        if (recording is not null) _engine.Recording = new Recording.DebugRecording(build.DebugInfo, recording);
    }

    /// <summary>Raised when the program pauses. The event data has the location, call stack and locals.</summary>
    public event EventHandler<DebugPausedEventArgs>? Paused;

    /// <summary>Raised when the paused program is about to continue.</summary>
    public event EventHandler? Resumed;

    /// <summary>Raised once when the program has ended, however it ended.</summary>
    public event EventHandler<DebugExitedEventArgs>? Exited;

    /// <summary>Raised once the program's entry assembly is loaded, before Main runs, for Hot Reload.</summary>
    public event Action<Assembly>? Loaded;

    /// <summary>Where every statement and method of the build is, for the breakpoint margin.</summary>
    public DebugInfoMap DebugInfo => Volatile.Read(ref _debugInfo);

    /// <summary>
    /// The language of the debugged project: C# or Visual Basic. Values, types and breakpoint conditions read as the
    /// language of the code each pause is in, which may differ in a solution that mixes both.
    /// </summary>
    public ProjectLanguage Language { get; }

    /// <summary>Updates source locations after a Hot Reload that preserves statement/method ids and local slots.</summary>
    /// <param name="info">The validated instrumenter's map for the new generation.</param>
    public void UpdateDebugInfo(DebugInfoMap info)
    {
        ArgumentNullException.ThrowIfNull(info);
        _engine.UpdateDebugInfo(info);
        Volatile.Write(ref _debugInfo, info);
    }

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

    /// <summary>
    /// Keeps the host's UI alive when the program pauses on the host's UI thread, as an Avalonia app's code does;
    /// <see langword="null"/> to block every paused thread.
    /// </summary>
    public IDebugWaitPump? WaitPump
    {
        get => _engine.Pump;
        set => _engine.Pump = value;
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

    /// <summary>Sets a breakpoint on a line, or returns the one already there with the same condition.</summary>
    /// <param name="filePath">Full path of the source file.</param>
    /// <param name="line">One-based line number.</param>
    /// <param name="condition">When to stop, such as <c>count == 3</c> (see <see cref="Breakpoint"/>), or
    /// <see langword="null"/> to stop every time. A different condition replaces the breakpoint on the line.</param>
    /// <returns>The breakpoint; check <see cref="Breakpoint.IsBound"/> and <see cref="Breakpoint.Location"/> for where it hits.</returns>
    public Breakpoint SetBreakpoint(string filePath, int line, string? condition = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);

        var breakpoint = new Breakpoint(filePath, line, DebugInfo.FindStatement(filePath, line), condition);
        lock (_breakpointsGate)
        {
            var index = _breakpoints.FindIndex(b => b.Line == line && string.Equals(b.FilePath, filePath, StringComparison.Ordinal));
            if (index >= 0 && _breakpoints[index].Condition == breakpoint.Condition)
            {
                return _breakpoints[index];
            }

            if (index >= 0)
            {
                _breakpoints[index] = breakpoint;
            }
            else
            {
                _breakpoints.Add(breakpoint);
            }

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
    public Task<RunResult> RunAsync(
        IReadOnlyList<string> arguments,
        IConsoleSink console,
        ConsoleInput? input = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(arguments, packageAssemblies: [], console, input, cancellationToken);

    /// <summary>Runs a program that uses NuGet packages under the debugger and waits until it ends.</summary>
    /// <remarks>
    /// Package code is not instrumented: breakpoints and steps stay in the program's own code, and Step Into steps
    /// over calls into a package, as Just My Code does in Visual Studio.
    /// </remarks>
    /// <param name="arguments">Command-line arguments passed to <c>Main</c>.</param>
    /// <param name="packageAssemblies">
    /// Full paths of the package assemblies and native libraries the program may load, such as <c>RestoreResult.RuntimeFiles</c>. They
    /// are found by simple name and loaded from disk on demand, as <see cref="ProgramRunner"/> does.
    /// </param>
    /// <param name="console">Receives the program's output.</param>
    /// <param name="input">Standard input, or <see langword="null"/> for an empty input.</param>
    /// <param name="cancellationToken">Stops the program, like <see cref="Stop"/>.</param>
    /// <returns>How the program ended.</returns>
    /// <exception cref="InvalidOperationException">The session already ran its program.</exception>
    /// <param name="environment">Environment variables scoped to this debug run.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "The optional environment was added after the token; existing positional callers pass the token first.")]
    public async Task<RunResult> RunAsync(
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> packageAssemblies,
        IConsoleSink console,
        ConsoleInput? input = null,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(packageAssemblies);
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
            var runner = new ProgramRunner();
            runner.Loaded += assembly => Loaded?.Invoke(assembly);
            result = await runner
                .RunAsync(_build.Result.EntryAssembly!, _build.Result.Dependencies, packageAssemblies, arguments, console, input, stop.Token, environment)
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

    /// <summary>
    /// Debugs a program that its host loads and runs itself, such as an Avalonia app whose views the app preview
    /// creates: connects the instrumented code of its assemblies to this session. Call it once the assemblies are
    /// loaded and before any of their code runs, then <see cref="EndHostedRun"/> when the program ends.
    /// </summary>
    /// <remarks>
    /// The program runs on its host's threads, so stopping it never unwinds them: a stopped session's hooks do nothing.
    /// </remarks>
    /// <param name="assemblies">The program's instrumented assemblies; others are skipped.</param>
    /// <exception cref="InvalidOperationException">The session already ran or attached a program.</exception>
    public void Attach(IEnumerable<Assembly> assemblies)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(assemblies);
        if (Interlocked.CompareExchange(ref _phase, Running, NotStarted) != NotStarted)
        {
            throw new InvalidOperationException("This session has already run its program. Start a new session to debug again.");
        }

        _engine.IsHosted = true;
        var sink = _engine.CreateSink();
        lock (AttachGate)
        {
            // Each assembly reads the sink once, in its runtime's static constructor, so it stays with this session.
            var previous = AppContext.GetData(DebugRuntimeSource.DataKey);
            AppContext.SetData(DebugRuntimeSource.DataKey, sink);
            try
            {
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetType(DebugRuntimeSource.ClassName, throwOnError: false) is { } runtime)
                    {
                        RuntimeHelpers.RunClassConstructor(runtime.TypeHandle);
                    }
                }
            }
            finally
            {
                AppContext.SetData(DebugRuntimeSource.DataKey, previous);
            }
        }
    }

    /// <summary>
    /// Ends the run of a program connected with <see cref="Attach"/>: releases it if it is paused, turns its hooks off
    /// and raises <see cref="Exited"/>.
    /// </summary>
    /// <param name="result">How the program ended.</param>
    /// <returns><see langword="false"/> when the session was not running an attached program.</returns>
    public bool EndHostedRun(RunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!_engine.IsHosted || Interlocked.CompareExchange(ref _phase, Finished, Running) != Running)
        {
            return false;
        }

        _engine.Stop();
        Exited?.Invoke(this, new DebugExitedEventArgs(result));
        return true;
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
    /// <remarks>
    /// A program that is waiting outside its own code, for example for console input, pauses when it returns to it.
    /// Called before <see cref="RunAsync(IReadOnlyList{string}, IConsoleSink, ConsoleInput, CancellationToken)"/>, the program pauses at its first statement, which is how F11 starts
    /// debugging with Step Into.
    /// </remarks>
    public void Pause() => _engine.Pause();

    /// <summary>Cancels a <see cref="Pause"/> that has not paused the program yet, so it keeps running.</summary>
    /// <returns><see langword="true"/> when a pause was pending.</returns>
    public bool CancelPause() => _engine.CancelPause();

    /// <summary>
    /// Stops debugging: releases the program if it is paused, ends <see cref="RunAsync(IReadOnlyList{string}, IConsoleSink, ConsoleInput, CancellationToken)"/> with
    /// <see cref="RunOutcome.Stopped"/> and makes the program's main thread unwind at its next statement.
    /// </summary>
    public void Stop()
    {
        // The run ends as stopped first; released, the program's thread unwinds with an exception, which would
        // otherwise end the run as a crash when it gets there first.
        if (!_disposed)
        {
            _stop.Cancel();
        }

        _engine.Stop();
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
        // The stopped program's static hook delegates can still reference this session on Mono. Retain its
        // images/source map for the existing inspection contract, but release the much larger compiler graphs.
        var result = _build.Result;
        _build = _build with
        {
            Result = result with
            {
                EntryAssembly = result.EntryAssembly is { } entry ? entry with { HotReloadCompilation = null } : null,
                Dependencies = result.Dependencies.Select(a => a with { HotReloadCompilation = null }).ToArray(),
            },
        };
        Loaded = null;
    }

    private void OnPaused(DebugPausedEventArgs pause) => Paused?.Invoke(this, pause);

    private void OnResumed() => Resumed?.Invoke(this, EventArgs.Empty);
}
