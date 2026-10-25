using Android.App;
using Android.OS;
using Android.Views;
using Avalonia.Android;
using TabStudio.App.Services;
using AvaloniaPixelPoint = Avalonia.PixelPoint;

namespace TabStudio.Android;

/// <summary>
/// The self test's finger and frames on a device or an emulator: swipes injected into TabStudio's own window through
/// Android's input system, as touch screen events (which an app may do to its own window), so they reach the IDE the
/// way a finger's do; and the display's frames from the main thread's <see cref="Choreographer"/>.
/// </summary>
/// <param name="activity">The activity whose window the touches go to.</param>
internal sealed class AndroidTouchSelfTestDevice(Func<Activity?> activity) : ITouchSelfTestDevice
{
    // A touch screen reports a moving finger about every 8 ms, batched by Android once per frame.
    private static readonly TimeSpan MoveInterval = TimeSpan.FromMilliseconds(8);

    /// <inheritdoc />
    public async Task SwipeAsync(AvaloniaPixelPoint origin, AvaloniaPixelPoint destination, TimeSpan duration, CancellationToken cancellationToken)
    {
        if (activity()?.Window?.DecorView is not { } decor || FindView(decor) is not { } view)
        {
            throw new InvalidOperationException("TabStudio's view is not on the screen.");
        }

        // The view's pixels are the screen's, offset by where the view is on the screen.
        var location = new int[2];
        view.GetLocationOnScreen(location);
        var (x0, y0) = (origin.X + location[0], origin.Y + location[1]);
        var (x1, y1) = (destination.X + location[0], destination.Y + location[1]);

        // Injecting waits for the window to take each event, so it runs off the UI thread, which handles them. Each event
        // carries the time the finger reaches its point on a steady 8 ms schedule, as a touch screen stamps its reports:
        // a busy UI thread delays when the events arrive, as it would a finger's, but not the finger's own timeline,
        // from which the editor reads the speed of a flick.
        await Task.Run(
            () =>
            {
                using var instrumentation = new Instrumentation();
                var down = SystemClock.UptimeMillis();
                Send(instrumentation, down, down, MotionEventActions.Down, x0, y0);
                var steps = Math.Max(1, (int)Math.Round(duration / MoveInterval));
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (var step = 1; step <= steps; step++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var due = MoveInterval * step;
                    var wait = due - clock.Elapsed;
                    if (wait > TimeSpan.Zero)
                    {
                        Thread.Sleep(wait);
                    }

                    var t = (double)step / steps;
                    Send(instrumentation, down, down + (long)due.TotalMilliseconds, MotionEventActions.Move, x0 + ((x1 - x0) * t), y0 + ((y1 - y0) * t));
                }

                // The finger lifts where its last report put it, a few milliseconds later.
                Send(instrumentation, down, down + (long)(MoveInterval * steps).TotalMilliseconds + 4, MotionEventActions.Up, x1, y1);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public IDisposable RecordFrames(Action frame) => new FrameRecorder(frame);

    private static AvaloniaView? FindView(View view)
    {
        if (view is AvaloniaView avalonia)
        {
            return avalonia;
        }

        if (view is ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                if (group.GetChildAt(i) is { } child && FindView(child) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static void Send(Instrumentation instrumentation, long downTime, long eventTime, MotionEventActions action, double x, double y)
    {
        var pointer = new MotionEvent.PointerProperties { Id = 0, ToolType = MotionEventToolType.Finger };
        var position = new MotionEvent.PointerCoords { X = (float)x, Y = (float)y, Pressure = 1, Size = 1 };
        using var e = MotionEvent.Obtain(downTime, eventTime, action, 1, [pointer], [position], 0, 0, 1, 1, 0, 0, InputSourceType.Touchscreen, 0);
        instrumentation.SendPointerSync(e);
    }

    /// <summary>Calls back at the start of each frame from the main thread's Choreographer, until disposed.</summary>
    private sealed class FrameRecorder : IDisposable
    {
        private readonly ChoreographerFrameCallback _callback;

        public FrameRecorder(Action frame)
        {
            _callback = new ChoreographerFrameCallback(frame);
            Choreographer.Instance?.PostFrameCallback(_callback);
        }

        public void Dispose()
        {
            _callback.Stopped = true;
            Choreographer.Instance?.RemoveFrameCallback(_callback);
            _callback.Dispose();
        }
    }
}

/// <summary>A Choreographer frame callback that calls an action and asks for the next frame, until stopped.</summary>
internal sealed class ChoreographerFrameCallback : Java.Lang.Object, Choreographer.IFrameCallback
{
    private readonly Action _frame;

    /// <summary>Creates the callback.</summary>
    /// <param name="frame">What to do at the start of each frame.</param>
    public ChoreographerFrameCallback(Action frame)
    {
        _frame = frame;
    }

    /// <summary>Whether to stop asking for frames.</summary>
    public bool Stopped { get; set; }

    /// <inheritdoc />
    public void DoFrame(long frameTimeNanos)
    {
        if (Stopped)
        {
            return;
        }

        _frame();
        Choreographer.Instance?.PostFrameCallback(this);
    }
}
