namespace TabStudio.Core.Progress;

/// <summary>How a long operation ended.</summary>
public enum OperationOutcome
{
    /// <summary>It did what it set out to do.</summary>
    Succeeded,

    /// <summary>It failed; its details say why.</summary>
    Failed,

    /// <summary>The user cancelled it.</summary>
    Cancelled,
}

/// <summary>
/// One long operation: downloading an SDK, installing packages, restoring NuGet packages, configuring CMake, building,
/// analyzing a solution. It has a title, the stage it is in with its numbers, an estimate of the time left, the raw log
/// behind "Show details", and Cancel where cancelling is safe.
/// </summary>
public sealed class TrackedOperation
{
    /// <summary>The most lines "Show details" keeps; older ones go first.</summary>
    public const int MaxDetailLines = 2000;

    private readonly OperationTracker _tracker;
    private readonly ProgressEstimator _estimator = new();
    private readonly LinkedList<string> _details = new();
    private Action? _cancel;

    internal TrackedOperation(OperationTracker tracker, string key, string title, OperationProgress progress, Action? cancel)
    {
        _tracker = tracker;
        Key = key;
        Title = title;
        Progress = progress;
        _cancel = cancel;
        Started = tracker.Now;
        _estimator.Add(progress, TimeSpan.Zero);
    }

    /// <summary>Identifies the operation, such as <c>toolchain:pico-sdk</c>: beginning one with the same key while it runs returns it.</summary>
    public string Key { get; }

    /// <summary>What the operation is, such as "Installing Pico SDK".</summary>
    public string Title { get; private set; }

    /// <summary>Where it stands now.</summary>
    public OperationProgress Progress { get; private set; }

    /// <summary>When it started, on the tracker's clock.</summary>
    public TimeSpan Started { get; }

    /// <summary>How long it has run.</summary>
    public TimeSpan Elapsed => _tracker.Now - Started;

    /// <summary>The time left in the current stage, once there is enough to estimate it.</summary>
    public TimeSpan? Remaining => _estimator.Remaining(Elapsed);

    /// <summary>"about 3 min left", or <see langword="null"/>.</summary>
    public string? RemainingText => Remaining is { } remaining ? ProgressEstimator.Format(remaining) : null;

    /// <summary>Whether Cancel is offered: only where stopping halfway leaves nothing broken.</summary>
    public bool CanCancel => _cancel is not null && !IsCompleted;

    /// <summary>Whether the user asked to cancel it.</summary>
    public bool IsCancelling { get; private set; }

    /// <summary>Whether it ended.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>How it ended, once it did.</summary>
    public OperationOutcome? Outcome { get; private set; }

    /// <summary>The raw log, newest last, at most <see cref="MaxDetailLines"/> lines.</summary>
    public IReadOnlyList<string> Details => [.. _details];

    /// <summary>Whether a system notification about it is showing, because it ran long while the app was in the background.</summary>
    public bool IsNotified { get; internal set; }

    /// <summary>When the notification was last updated, on the tracker's clock.</summary>
    internal TimeSpan NotifiedAt { get; set; }

    /// <summary>The text the notification last showed.</summary>
    internal string? NotifiedText { get; set; }

    /// <summary>"Downloading Pico SDK 120/340 MB · about 2 min left": the stage, its numbers and the estimate.</summary>
    public string Text => RemainingText is { } left ? $"{Progress.Text} · {left}" : Progress.Text;

    /// <summary>Moves the operation to a stage, with its numbers if it has them.</summary>
    /// <param name="progress">Where it stands.</param>
    public void Report(OperationProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (IsCompleted || progress == Progress)
        {
            return;
        }

        Progress = progress;
        _estimator.Add(progress, Elapsed);
        _tracker.OnChanged(this);
    }

    /// <summary>Moves the operation to a stage without numbers.</summary>
    /// <param name="stage">What is happening.</param>
    public void Report(string stage) => Report(new OperationProgress(stage));

    /// <summary>Renames the operation, as when one install leads to another.</summary>
    /// <param name="title">The new title.</param>
    public void Retitle(string title)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        if (title != Title)
        {
            Title = title;
            _tracker.OnChanged(this);
        }
    }

    /// <summary>Offers Cancel, or takes it away when stopping is no longer safe.</summary>
    /// <param name="cancel">What Cancel does, or <see langword="null"/> for none.</param>
    public void SetCancel(Action? cancel)
    {
        _cancel = cancel;
        _tracker.OnChanged(this);
    }

    /// <summary>
    /// Adds a line of the raw log; with <paramref name="parse"/>, its numbers too, as a build tool's <c>[37/120]</c>
    /// (see <see cref="OutputProgressParser"/>).
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="parse">Whether to read progress from it.</param>
    public void Log(string line, bool parse = false)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (IsCompleted)
        {
            return;
        }

        _details.AddLast(line);
        while (_details.Count > MaxDetailLines)
        {
            _details.RemoveFirst();
        }

        if (parse && OutputProgressParser.Parse(line.TrimEnd('\r'), Progress.Stage) is { } progress)
        {
            Report(progress);
            return;
        }

        _tracker.OnLogged(this);
    }

    /// <summary>Asks the operation to stop.</summary>
    public void Cancel()
    {
        if (_cancel is not { } cancel || IsCompleted || IsCancelling)
        {
            return;
        }

        IsCancelling = true;
        Report(new OperationProgress("Cancelling"));
        cancel();
    }

    /// <summary>Ends the operation, which leaves the status bar.</summary>
    /// <param name="outcome">How it ended; a cancelled operation counts as cancelled whatever it says.</param>
    public void Complete(OperationOutcome outcome = OperationOutcome.Succeeded)
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        Outcome = IsCancelling ? OperationOutcome.Cancelled : outcome;
        _tracker.OnCompleted(this);
    }
}

/// <summary>
/// The long operations running now, for the status bar and its panel: the newest is the one the status bar shows, and
/// each one that has run for <see cref="BackgroundThreshold"/> while the app is in the background gets a system
/// notification with its progress, which says how it ended when it does.
/// </summary>
public sealed class OperationTracker
{
    /// <summary>How long an operation runs in the background before it gets a notification.</summary>
    public static readonly TimeSpan BackgroundThreshold = TimeSpan.FromSeconds(30);

    /// <summary>How often a notification's text changes at most, so the shade does not flicker.</summary>
    public static readonly TimeSpan NotificationInterval = TimeSpan.FromSeconds(2);

    private readonly TimeProvider _time;
    private readonly long _origin;
    private readonly List<TrackedOperation> _active = [];
    private bool _inBackground;

    /// <summary>Creates a tracker on a clock.</summary>
    /// <param name="time">The clock; the system's when <see langword="null"/>.</param>
    public OperationTracker(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _origin = _time.GetTimestamp();
    }

    /// <summary>Raised when an operation starts, changes stage or numbers, or ends.</summary>
    public event EventHandler<TrackedOperation>? Changed;

    /// <summary>Raised when an operation logs a line that does not change its stage.</summary>
    public event EventHandler<TrackedOperation>? Logged;

    /// <summary>
    /// Raised when an operation's system notification should show or change: it has run for
    /// <see cref="BackgroundThreshold"/> and the app is in the background.
    /// </summary>
    public event EventHandler<TrackedOperation>? NotificationDue;

    /// <summary>Raised when an operation that had a notification ends, so it can say how.</summary>
    public event EventHandler<TrackedOperation>? NotificationEnded;

    /// <summary>The operations running now, oldest first.</summary>
    public IReadOnlyList<TrackedOperation> Active => _active;

    /// <summary>The newest operation running, which the status bar shows.</summary>
    public TrackedOperation? Current => _active.Count > 0 ? _active[^1] : null;

    /// <summary>The time since the tracker was created.</summary>
    public TimeSpan Now => _time.GetElapsedTime(_origin);

    /// <summary>Whether the app is in the background, where long operations get notifications.</summary>
    public bool IsInBackground
    {
        get => _inBackground;
        set
        {
            if (_inBackground == value)
            {
                return;
            }

            _inBackground = value;
            Poll();
        }
    }

    /// <summary>Starts an operation, or returns the running one with the same key.</summary>
    /// <param name="key">Identifies it, such as <c>restore</c> or <c>toolchain:pico-sdk</c>.</param>
    /// <param name="title">What it is, such as "Restoring NuGet packages".</param>
    /// <param name="progress">Its first stage; the title when <see langword="null"/>.</param>
    /// <param name="cancel">What Cancel does, where cancelling is safe.</param>
    public TrackedOperation Begin(string key, string title, OperationProgress? progress = null, Action? cancel = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(title);
        if (_active.FirstOrDefault(o => o.Key == key) is { } running)
        {
            running.Retitle(title);
            if (progress is not null)
            {
                running.Report(progress);
            }

            if (cancel is not null)
            {
                running.SetCancel(cancel);
            }

            return running;
        }

        var operation = new TrackedOperation(this, key, title, progress ?? new OperationProgress(title), cancel);
        _active.Add(operation);
        Changed?.Invoke(this, operation);
        return operation;
    }

    /// <summary>The running operation with a key, if any.</summary>
    /// <param name="key">Its key.</param>
    public TrackedOperation? Find(string key) => _active.FirstOrDefault(o => o.Key == key);

    /// <summary>
    /// Checks every running operation against the background threshold. The app calls this every few seconds while
    /// operations run, and the tracker on every change.
    /// </summary>
    public void Poll()
    {
        if (!_inBackground)
        {
            return;
        }

        foreach (var operation in _active.ToList())
        {
            if (operation.Elapsed < BackgroundThreshold)
            {
                continue;
            }

            var text = operation.Text;
            if (operation.IsNotified && (text == operation.NotifiedText || Now - operation.NotifiedAt < NotificationInterval))
            {
                continue;
            }

            operation.IsNotified = true;
            operation.NotifiedAt = Now;
            operation.NotifiedText = text;
            NotificationDue?.Invoke(this, operation);
        }
    }

    internal void OnChanged(TrackedOperation operation)
    {
        Changed?.Invoke(this, operation);
        Poll();
    }

    internal void OnLogged(TrackedOperation operation)
    {
        Logged?.Invoke(this, operation);
        Poll();
    }

    internal void OnCompleted(TrackedOperation operation)
    {
        _active.Remove(operation);
        Changed?.Invoke(this, operation);
        if (operation.IsNotified)
        {
            NotificationEnded?.Invoke(this, operation);
        }
    }
}
