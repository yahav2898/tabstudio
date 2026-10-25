using Avalonia.Threading;
using TabStudio.Debugging;

namespace TabStudio.App.Swarm;

/// <summary>
/// Keeps TabStudio's UI drawing while an app it hosts is paused by the debugger on the UI thread: the paused thread runs
/// a nested dispatcher frame until the app continues, so the device's overlay says the app is paused and its Leave
/// button works.
/// </summary>
/// <remarks>
/// Only platforms whose dispatcher runs nested frames can pump (desktop, and the headless platform of tests). Android's
/// cannot, so there a paused app holds its UI thread still until it continues, as a native debugger holds an app.
/// </remarks>
public sealed class DispatcherWaitPump : IDebugWaitPump
{
    /// <summary>How often a waiting frame checks again, besides when it is woken.</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(100);

    private readonly Lock _gate = new();
    private readonly List<(DispatcherFrame Frame, Func<bool> IsWaiting)> _frames = [];

    /// <summary>The pump for TabStudio's UI thread.</summary>
    public static DispatcherWaitPump Instance { get; } = new();

    /// <inheritdoc />
    public bool CanPumpCurrentThread => Dispatcher.UIThread.CheckAccess() && Dispatcher.UIThread.SupportsRunLoops;

    /// <inheritdoc />
    public void PumpWhile(Func<bool> isWaiting)
    {
        ArgumentNullException.ThrowIfNull(isWaiting);
        var frame = new DispatcherFrame();
        lock (_gate)
        {
            _frames.Add((frame, isWaiting));
        }

        var timer = new DispatcherTimer(CheckInterval, DispatcherPriority.Background, (_, _) => Check());
        try
        {
            timer.Start();

            // In case the program continued before the frame was pushed.
            Dispatcher.UIThread.Post(Check, DispatcherPriority.Send);
            Dispatcher.UIThread.PushFrame(frame);
        }
        finally
        {
            timer.Stop();
            lock (_gate)
            {
                _frames.RemoveAll(f => ReferenceEquals(f.Frame, frame));
            }
        }
    }

    /// <inheritdoc />
    public void Wake() => Dispatcher.UIThread.Post(Check, DispatcherPriority.Send);

    private void Check()
    {
        List<(DispatcherFrame Frame, Func<bool> IsWaiting)> frames;
        lock (_gate)
        {
            frames = [.. _frames];
        }

        foreach (var (frame, isWaiting) in frames)
        {
            if (!isWaiting())
            {
                frame.Continue = false;
            }
        }
    }
}
