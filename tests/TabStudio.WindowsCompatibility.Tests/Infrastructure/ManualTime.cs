namespace TabStudio.WindowsCompatibility.Tests.Infrastructure;

/// <summary>A clock a test moves by hand, whose timers fire when it passes their due time.</summary>
public sealed class ManualTime : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state, _now + dueTime);
        _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        _now += by;
        foreach (var timer in _timers.ToList())
        {
            if (!timer.Disposed && timer.Due <= _now)
            {
                timer.Disposed = true;
                timer.Callback(timer.State);
            }
        }
    }

    private sealed class ManualTimer(ManualTime owner, TimerCallback callback, object? state, DateTimeOffset due) : ITimer
    {
        public TimerCallback Callback { get; } = callback;

        public object? State { get; } = state;

        public DateTimeOffset Due { get; set; } = due;

        public bool Disposed { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = owner.GetUtcNow() + dueTime;
            return true;
        }

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
