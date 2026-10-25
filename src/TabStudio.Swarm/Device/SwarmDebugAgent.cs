using System.Text.Json;
using TabStudio.Debugging;
using TabStudio.Link;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Device;

/// <summary>
/// The debugger on a device, as the tablet drives it: tells the tablet when the app pauses and continues, and answers
/// its requests for locals, members and Watch expressions while the app is paused, on a thread of its own, so a pause
/// on the app's UI thread does not stop it.
/// </summary>
/// <remarks>Thread-safe.</remarks>
internal sealed class SwarmDebugAgent : IDisposable
{
    private readonly DebugSession _session;
    private readonly long _run;
    private readonly Func<string, JsonElement, Task> _notify;
    private readonly Action<SwarmDeviceState> _report;
    private readonly Lock _gate = new();
    private readonly Dictionary<int, (DebugValue Value, string Path)> _values = [];
    private DebugPausedEventArgs? _pause;
    private long _pauseId;
    private int _nextReference;
    private bool _pausing;

    /// <summary>Starts reporting a session's pauses.</summary>
    /// <param name="session">The session, not run yet.</param>
    /// <param name="run">The run it debugs.</param>
    /// <param name="notify">Sends a notification to the tablet.</param>
    /// <param name="report">Reports the app's state on this device.</param>
    public SwarmDebugAgent(DebugSession session, long run, Func<string, JsonElement, Task> notify, Action<SwarmDeviceState> report)
    {
        _session = session;
        _run = run;
        _notify = notify;
        _report = report;
        _session.Paused += OnPaused;
        _session.Resumed += OnResumed;
    }

    /// <summary>The session.</summary>
    public DebugSession Session => _session;

    /// <summary>Whether the app is paused.</summary>
    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _pause is not null;
            }
        }
    }

    /// <summary>Replaces the breakpoints.</summary>
    /// <param name="breakpoints">All of them.</param>
    public void SetBreakpoints(IReadOnlyList<SwarmBreakpoint> breakpoints)
    {
        var wanted = breakpoints.ToHashSet();
        foreach (var existing in _session.Breakpoints)
        {
            if (!wanted.Contains(new SwarmBreakpoint(existing.FilePath, existing.Line, existing.Condition)))
            {
                _session.RemoveBreakpoint(existing.FilePath, existing.Line);
            }
        }

        foreach (var breakpoint in breakpoints)
        {
            _session.SetBreakpoint(breakpoint.FilePath, breakpoint.Line, breakpoint.Condition);
        }
    }

    /// <summary>Pauses the app at its next statement (Break All).</summary>
    public void Pause()
    {
        lock (_gate)
        {
            if (_pause is not null)
            {
                return;
            }

            _pausing = true;
        }

        _session.Pause();
        _report(SwarmDeviceState.Pausing);
    }

    /// <summary>Continues or steps the paused app, or cancels a pause it has not reached yet.</summary>
    /// <param name="pause">The pause it is about, or 0 for whatever is paused.</param>
    /// <param name="mode">How it continues.</param>
    /// <returns>Whether it continued.</returns>
    public bool Resume(long pause, SwarmResumeMode mode)
    {
        lock (_gate)
        {
            if (_pause is null)
            {
                if (!_pausing)
                {
                    return false;
                }

                _pausing = false;
                _session.CancelPause();
            }
            else if (pause != 0 && pause != _pauseId)
            {
                return false;
            }
        }

        if (!IsPaused)
        {
            _report(SwarmDeviceState.Running);
            return true;
        }

        return mode switch
        {
            SwarmResumeMode.StepInto => _session.StepInto(),
            SwarmResumeMode.StepOver => _session.StepOver(),
            SwarmResumeMode.StepOut => _session.StepOut(),
            _ => _session.Continue(),
        };
    }

    /// <summary>The locals of the pause (reference 0), or the members and items of one of its values.</summary>
    /// <exception cref="LinkRemoteException">The app is no longer paused there.</exception>
    public SwarmVariablesResponse Variables(long pause, int reference)
    {
        DebugPausedEventArgs current;
        (DebugValue Value, string Path)? parent = null;
        lock (_gate)
        {
            current = Current(pause);
            if (reference != 0)
            {
                parent = _values.TryGetValue(reference, out var known)
                    ? known
                    : throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "That value is not part of the pause.");
            }
        }

        // Expanding and formatting values may run the app's getters and ToString, with a time limit: not under the gate.
        IEnumerable<(DebugValue Value, string Path)> values = parent is { } owner
            ? owner.Value.Expand().Select(child => (child, ChildPath(owner.Path, child.Name)))
            : current.Locals.Select(local => (local, local.Name));
        return new SwarmVariablesResponse([.. values.Select(v => Describe(pause, v.Value, v.Path))]);
    }

    /// <summary>A Watch or DataTip expression's value in the pause.</summary>
    /// <exception cref="LinkRemoteException">The app is no longer paused there.</exception>
    public SwarmEvaluateResponse Evaluate(long pause, string expression)
    {
        DebugPausedEventArgs current;
        lock (_gate)
        {
            current = Current(pause);
        }

        DebugValue? value;
        try
        {
            value = current.FindValue(expression.Trim());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new SwarmEvaluateResponse(null, ex.Message);
        }

        return value is null
            ? new SwarmEvaluateResponse(null, $"'{expression.Trim()}' is not a variable, member or item in scope here.")
            : new SwarmEvaluateResponse(Describe(pause, value, expression.Trim()) with { Name = expression.Trim() }, null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _session.Paused -= OnPaused;
        _session.Resumed -= OnResumed;
    }

    private static string ChildPath(string parent, string child) => child.StartsWith('[') ? parent + child : $"{parent}.{child}";

    private DebugPausedEventArgs Current(long pause) =>
        _pause is { } current && (pause == 0 || pause == _pauseId)
            ? current
            : throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "The app is no longer paused there.");

    private SwarmVariable Describe(long pause, DebugValue value, string path)
    {
        var reference = 0;
        if (value.HasChildren)
        {
            lock (_gate)
            {
                if (pause == 0 || pause == _pauseId)
                {
                    reference = ++_nextReference;
                    _values[reference] = (value, path);
                }
            }
        }

        return new SwarmVariable(value.Name, value.Value, value.Type, reference, path);
    }

    /// <summary>Raised on the app's thread just before it waits: tells the tablet, without blocking.</summary>
    private void OnPaused(object? sender, DebugPausedEventArgs pause)
    {
        long id;
        lock (_gate)
        {
            _pause = pause;
            id = ++_pauseId;
            _pausing = false;
            _values.Clear();
        }

        var notice = new SwarmPauseNotice(
            _run,
            id,
            pause.Reason,
            [.. pause.CallStack.Select(f => new SwarmFrame(f.MethodName, f.Location))],
            pause.Exception is { } exception ? SwarmFault.From(exception) : null,
            pause.ConditionError,
            pause.Breakpoint?.FilePath,
            pause.Breakpoint?.Line ?? 0);
        _report(SwarmDeviceState.Paused);
        _ = _notify(SwarmMethods.Paused, SwarmJson.Body(notice, SwarmJsonContext.Default.SwarmPauseNotice));
    }

    private void OnResumed(object? sender, EventArgs e)
    {
        long id;
        lock (_gate)
        {
            id = _pauseId;
            _pause = null;
            _values.Clear();
        }

        _report(SwarmDeviceState.Running);
        _ = _notify(SwarmMethods.Resumed, SwarmJson.Body(new SwarmResumedNotice(_run, id), SwarmJsonContext.Default.SwarmResumedNotice));
    }
}
