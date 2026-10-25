using TabStudio.Debugging;
using TabStudio.Link;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Coordinator;

/// <summary>
/// Debugging on every device: breakpoints go to all of them, any of them can pause, and with
/// <see cref="PauseAllOnBreakpoint"/> one device's pause pauses the others until they continue together.
/// </summary>
public sealed partial class SwarmCoordinator
{
    private IReadOnlyList<SwarmBreakpoint> _breakpoints = [];
    private volatile bool _pauseAll;

    /// <summary>Raised when the debugged app paused on a device, with its call stack.</summary>
    public event EventHandler<SwarmPause>? Paused;

    /// <summary>Raised when a device's paused app continued, or the device was lost while paused.</summary>
    public event EventHandler<SwarmDevice>? Resumed;

    /// <summary>Whether a breakpoint, a step or an exception on one device pauses every other device too.</summary>
    public bool PauseAllOnBreakpoint
    {
        get => _pauseAll;
        set => _pauseAll = value;
    }

    /// <summary>The breakpoints every device that debugs the app has.</summary>
    public IReadOnlyList<SwarmBreakpoint> Breakpoints
    {
        get
        {
            lock (_gate)
            {
                return _breakpoints;
            }
        }
    }

    /// <summary>Replaces the breakpoints, on every device that debugs the app now and every one that gets a debug build later.</summary>
    /// <param name="breakpoints">All the breakpoints.</param>
    public async Task SetBreakpointsAsync(IReadOnlyList<SwarmBreakpoint> breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);
        IReadOnlyList<SwarmBreakpoint> copy = [.. breakpoints];
        lock (_gate)
        {
            if (_breakpoints.SequenceEqual(copy))
            {
                return;
            }

            _breakpoints = copy;
        }

        var body = SwarmJson.Body(new SwarmBreakpointsRequest(copy), SwarmJsonContext.Default.SwarmBreakpointsRequest);
        await Task.WhenAll(Devices.Where(d => d.IsDebugging).Select(async device =>
        {
            if (device.Connection is not { } connection)
            {
                return;
            }

            try
            {
                await connection.RequestAsync(SwarmMethods.Breakpoints, body).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is LinkConnectionLostException or LinkRemoteException or LinkProtocolException)
            {
                // A device that left gets the breakpoints with its next build.
            }
        })).ConfigureAwait(false);
    }

    /// <summary>Continues every paused device together, and cancels the pauses devices have not reached yet.</summary>
    /// <param name="except">A device to leave as it is, such as the one the debugger just stepped.</param>
    public Task ContinueAllAsync(SwarmDevice? except = null) =>
        Task.WhenAll(Devices
            .Where(d => !ReferenceEquals(d, except) && d.State is SwarmDeviceState.Paused or SwarmDeviceState.Pausing)
            .Select(d => QuietlyAsync(() => d.ResumeAsync(SwarmResumeMode.Continue, CancellationToken.None))));

    /// <summary>Pauses the app on every device that debugs it (Break All).</summary>
    /// <param name="except">A device to leave as it is, such as the one that just paused.</param>
    public Task PauseAllAsync(SwarmDevice? except = null) =>
        Task.WhenAll(Devices
            .Where(d => !ReferenceEquals(d, except) && d.IsDebugging && d.State == SwarmDeviceState.Running)
            .Select(d => QuietlyAsync(async () =>
            {
                await d.PauseAsync(CancellationToken.None).ConfigureAwait(false);
                return true;
            })));

    private void OnPaused(SwarmDevice device, SwarmPauseNotice notice)
    {
        var pause = new SwarmPause(device, notice);
        device.SetPause(pause);
        RaiseDeviceChanged(device);
        Paused?.Invoke(this, pause);

        // Break All pauses are the result of pausing all, or of the user's own Break All; the others pause the rest.
        if (_pauseAll && notice.Reason != PauseReason.Pause)
        {
            _ = PauseAllAsync(device);
        }
    }

    private void OnResumed(SwarmDevice device, SwarmResumedNotice notice)
    {
        if (device.EndPause(notice.Pause) is not null)
        {
            RaiseDeviceChanged(device);
            Resumed?.Invoke(this, device);
        }
    }

    private static async Task QuietlyAsync(Func<Task<bool>> request)
    {
        try
        {
            await request().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is LinkConnectionLostException or LinkRemoteException or LinkProtocolException or OperationCanceledException)
        {
            // A device that left or stopped meanwhile has nothing to pause or continue.
        }
    }
}
