namespace TabStudio.Forms.Runtime;

/// <summary>Timers that tick on the app's UI thread, as Windows Forms timers do.</summary>
internal sealed partial class FormsRuntime
{
    /// <summary>Starts a timer whose ticks run on the UI thread. A tick is skipped while the previous one waits.</summary>
    /// <param name="interval">The interval in milliseconds.</param>
    /// <param name="tick">The work of each tick.</param>
    /// <returns>Stops the timer when disposed.</returns>
    public IDisposable StartTimer(int interval, Action tick) => new RuntimeTimer(this, Math.Max(1, interval), tick);

    /// <summary>Runs work once on the UI thread after a delay.</summary>
    /// <param name="delay">The delay in milliseconds.</param>
    /// <param name="action">The work.</param>
    /// <returns>Cancels the work when disposed before it runs.</returns>
    public IDisposable RunAfter(int delay, Action action)
    {
        IDisposable? timer = null;

        // The tick runs on the UI thread, which is running this method, so the timer is assigned before it ticks.
        timer = StartTimer(delay, () =>
        {
            timer?.Dispose();
            action();
        });
        return timer;
    }

    /// <summary>A thread-pool timer that posts its ticks to the UI thread.</summary>
    private sealed class RuntimeTimer : IDisposable
    {
        private readonly FormsRuntime _runtime;
        private readonly Action _tick;
        private readonly Timer _timer;
        private int _pending;
        private volatile bool _stopped;

        public RuntimeTimer(FormsRuntime runtime, int interval, Action tick)
        {
            _runtime = runtime;
            _tick = tick;
            _timer = new Timer(_ => OnElapsed(), null, interval, interval);
        }

        public void Dispose()
        {
            _stopped = true;
            _timer.Dispose();
        }

        private void OnElapsed()
        {
            if (_stopped || _runtime.IsStopping || Interlocked.Exchange(ref _pending, 1) == 1)
            {
                return;
            }

            _runtime.Post(() =>
            {
                Volatile.Write(ref _pending, 0);
                if (!_stopped)
                {
                    _tick();
                }
            });
        }
    }
}
