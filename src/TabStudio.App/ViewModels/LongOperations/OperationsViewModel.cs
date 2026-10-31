using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.Core.Progress;

namespace TabStudio.App.ViewModels.LongOperations;

/// <summary>
/// The long operations running now, for the status bar and the small panel over it: downloading an SDK, installing
/// packages, restoring NuGet packages, configuring CMake, building, analyzing. The status bar shows the newest with its
/// stage, numbers and time left; the panel lists them all, each with Cancel where cancelling is safe and its raw log
/// behind "Show details". While any runs, the platform keeps TabStudio running in the background with the newest one's
/// progress in an ongoing notification (Android's foreground service); where it cannot, one that runs for 30 seconds in
/// the background gets a notification of its own. Each that ran in the background says how it ended.
/// </summary>
public sealed partial class OperationsViewModel : ObservableObject, IDisposable
{
    /// <summary>How often the time left and the background check are refreshed while operations run.</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The least time between two changes of the ongoing notification: Android drops a notification updated more than
    /// a few times a second, and a download reports every few kilobytes.
    /// </summary>
    private static readonly TimeSpan KeepRunningInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long the app is still kept running after the last operation ended. Operations often follow one another (the
    /// toolchains of a batch, a restore then a build): each one restarted Android's foreground service, which must reach
    /// <c>startForeground</c> within seconds of every start, and under load a start that missed it ended the app.
    /// </summary>
    internal static readonly TimeSpan ReleaseDelay = TimeSpan.FromSeconds(10);

    /// <summary>The tag of the ongoing notification that keeps TabStudio running.</summary>
    internal const string RunningTag = "operations";

    private readonly IUiDispatcher _ui;
    private readonly INotifications _notifications;

    /// <summary>The platform's notifications, which the self test drives directly to check the keep-running service.</summary>
    internal INotifications Notifications => _notifications;
    private readonly IAppActivity? _activity;
    private readonly ITimer _timer;
    private readonly ITimer _release;
    private readonly TimeProvider _time;
    private bool _refreshing;
    private bool _disposed;
    private SystemNotification? _kept;
    private bool _releasing;
    private long _keptAt;

    /// <summary>Creates the operations.</summary>
    /// <param name="ui">The UI thread, where operations are begun and reported.</param>
    /// <param name="notifications">The system's notifications; none when <see langword="null"/>.</param>
    /// <param name="activity">Whether the app is in the background; always in the foreground when <see langword="null"/>.</param>
    /// <param name="time">The clock; the system's when <see langword="null"/>.</param>
    public OperationsViewModel(IUiDispatcher ui, INotifications? notifications = null, IAppActivity? activity = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
        _notifications = notifications ?? new NoSystemNotifications();
        _activity = activity;
        time ??= TimeProvider.System;
        _time = time;
        Tracker = new OperationTracker(time) { IsInBackground = activity?.IsInBackground == true };
        Tracker.Changed += OnChanged;
        Tracker.Logged += OnLogged;
        Tracker.NotificationDue += OnNotificationDue;
        Tracker.NotificationEnded += OnNotificationEnded;
        if (_activity is not null)
        {
            _activity.Changed += OnActivityChanged;
        }

        _notifications.Opened += OnNotificationOpened;
        // The refresh runs only while an operation does: an idle IDE woke up every two seconds for nothing.
        _timer = time.CreateTimer(_ => _ui.Post(Refresh), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _release = time.CreateTimer(_ => _ui.Post(Release), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>The operations and their state, which tests and the integrations use.</summary>
    public OperationTracker Tracker { get; }

    /// <summary>
    /// Which operations keep TabStudio running in the background: by default all. Work that only the open IDE needs,
    /// such as analyzing the solution again, can wait for the user to come back.
    /// </summary>
    public Func<TrackedOperation, bool> KeepsAppRunning { get; init; } = _ => true;

    /// <summary>The operations running now, oldest first.</summary>
    public ObservableCollection<OperationItemViewModel> Items { get; } = [];

    /// <summary>The newest operation, which the status bar shows.</summary>
    public OperationItemViewModel? Current => Items.Count > 0 ? Items[^1] : null;

    /// <summary>Whether any operation runs.</summary>
    public bool HasOperations => Items.Count > 0;

    /// <summary>
    /// The status bar's text: "Downloading Pico SDK 120/340 MB · about 2 min left", and "+1 more" when others run too.
    /// </summary>
    public string? StatusText => Current is not { } current ? null
        : Items.Count > 1 ? $"{current.Text} (+{Items.Count - 1} more)" : current.Text;

    /// <summary>Whether the panel with every operation is open over the status bar.</summary>
    [ObservableProperty]
    public partial bool IsPanelOpen { get; set; }

    /// <summary>Starts an operation, or returns the running one with the same key. Call on the UI thread.</summary>
    /// <param name="key">Identifies it, such as <c>restore</c>.</param>
    /// <param name="title">What it is.</param>
    /// <param name="progress">Its first stage.</param>
    /// <param name="cancel">What Cancel does, where cancelling is safe.</param>
    public TrackedOperation Begin(string key, string title, OperationProgress? progress = null, Action? cancel = null) =>
        Tracker.Begin(key, title, progress, cancel);

    /// <summary>The running operation with a key, if any.</summary>
    /// <param name="key">Its key.</param>
    public TrackedOperation? Find(string key) => Tracker.Find(key);

    /// <summary>Opens or closes the panel.</summary>
    [RelayCommand]
    private void TogglePanel() => IsPanelOpen = HasOperations && !IsPanelOpen;

    /// <summary>Closes the panel.</summary>
    [RelayCommand]
    private void ClosePanel() => IsPanelOpen = false;

    private void OnChanged(object? sender, TrackedOperation operation)
    {
        var item = Items.FirstOrDefault(i => ReferenceEquals(i.Operation, operation));
        if (operation.IsCompleted)
        {
            if (item is not null)
            {
                Items.Remove(item);
            }
        }
        else if (item is null)
        {
            Items.Add(new OperationItemViewModel(operation));
        }
        else
        {
            item.Refresh();
        }

        if (!HasOperations)
        {
            IsPanelOpen = false;
        }

        if (!_disposed && HasOperations != _refreshing)
        {
            _refreshing = HasOperations;
            _timer.Change(_refreshing ? RefreshInterval : Timeout.InfiniteTimeSpan, _refreshing ? RefreshInterval : Timeout.InfiniteTimeSpan);
        }

        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(HasOperations));
        OnPropertyChanged(nameof(StatusText));
        UpdateKeepRunning(force: operation.IsCompleted || item is null);
    }

    /// <summary>
    /// Keeps the app running while operations that need it run, with the newest one's progress, and lets it go when
    /// the last one ends.
    /// </summary>
    /// <param name="force">Whether to pass a change on at once: one began or ended.</param>
    private void UpdateKeepRunning(bool force)
    {
        if (_disposed)
        {
            return;
        }

        var running = Items.Where(i => KeepsAppRunning(i.Operation)).ToList();
        if (running.Count == 0)
        {
            if (_kept is not null && !_releasing)
            {
                // Let go a little later, so an operation that follows keeps the running service.
                _releasing = true;
                _release.Change(ReleaseDelay, Timeout.InfiniteTimeSpan);
            }

            return;
        }

        if (_releasing)
        {
            _releasing = false;
            _release.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        var newest = running[^1].Operation;
        var title = running.Count > 1 ? $"{newest.Title} (+{running.Count - 1} more)" : newest.Title;
        var percent = newest.Progress.Fraction is { } fraction ? (int)Math.Round(fraction * 100) : (int?)null;
        var progress = new SystemNotification(RunningTag, title, newest.Text, IsFailure: false) { IsProgress = true, ProgressPercent = percent };
        if (progress == _kept || (!force && _kept is not null && _time.GetElapsedTime(_keptAt) < KeepRunningInterval))
        {
            // The two-second refresh passes on what was held back.
            return;
        }

        _kept = progress;
        _keptAt = _time.GetTimestamp();
        _notifications.KeepRunning(progress);
    }

    /// <summary>The grace period after the last operation passed with none begun: the app is let go.</summary>
    private void Release()
    {
        _releasing = false;
        if (_disposed || _kept is null || Items.Any(i => KeepsAppRunning(i.Operation)))
        {
            return;
        }

        _kept = null;
        _notifications.KeepRunning(null);
    }

    private void OnLogged(object? sender, TrackedOperation operation) =>
        Items.FirstOrDefault(i => ReferenceEquals(i.Operation, operation))?.RefreshDetails();

    private void Refresh()
    {
        if (_disposed || Items.Count == 0)
        {
            return;
        }

        foreach (var item in Items)
        {
            item.Refresh();
        }

        OnPropertyChanged(nameof(StatusText));
        UpdateKeepRunning(force: false);
        Tracker.Poll();
    }

    private void OnActivityChanged(object? sender, EventArgs e) => Tracker.IsInBackground = _activity?.IsInBackground == true;

    private void OnNotificationDue(object? sender, TrackedOperation operation)
    {
        // The ongoing notification that keeps TabStudio running already shows the progress.
        if (_notifications.KeepsRunning)
        {
            return;
        }

        var percent = operation.Progress.Fraction is { } fraction ? (int)Math.Round(fraction * 100) : (int?)null;
        _notifications.Show(new SystemNotification(Tag(operation), operation.Title, operation.Text, IsFailure: false)
        {
            IsProgress = true,
            ProgressPercent = percent,
        });
    }

    private void OnNotificationEnded(object? sender, TrackedOperation operation)
    {
        // Work that waits for the user to come back, such as analysis, does not report how it ended.
        if (_notifications.KeepsRunning && !KeepsAppRunning(operation))
        {
            return;
        }

        var (title, text) = operation.Outcome switch
        {
            OperationOutcome.Failed => ($"{operation.Title} failed", "Open TabStudio for the details."),
            OperationOutcome.Cancelled => ($"{operation.Title} cancelled", "Stopped before it finished."),
            _ => ($"{operation.Title} finished", "Done in " + Duration(operation.Elapsed) + "."),
        };
        _notifications.Show(new SystemNotification(Tag(operation), title, text, operation.Outcome == OperationOutcome.Failed));
    }

    private static string Tag(TrackedOperation operation) => TagPrefix + operation.Key;

    /// <summary>The tag prefix of operations' notifications; tapping one opens the panel.</summary>
    private const string TagPrefix = "operation:";

    private void OnNotificationOpened(object? sender, string tag)
    {
        if (tag.StartsWith(TagPrefix, StringComparison.Ordinal) && HasOperations)
        {
            IsPanelOpen = true;
        }
    }

    private static string Duration(TimeSpan elapsed) => elapsed < TimeSpan.FromMinutes(1)
        ? $"{(int)elapsed.TotalSeconds} s"
        : elapsed < TimeSpan.FromHours(1) ? $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds} s" : $"{(int)elapsed.TotalHours} h {elapsed.Minutes} min";

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_kept is not null)
        {
            _kept = null;
            _notifications.KeepRunning(null);
        }

        _disposed = true;
        _timer.Dispose();
        _release.Dispose();
        Tracker.Changed -= OnChanged;
        Tracker.Logged -= OnLogged;
        Tracker.NotificationDue -= OnNotificationDue;
        Tracker.NotificationEnded -= OnNotificationEnded;
        _notifications.Opened -= OnNotificationOpened;
        if (_activity is not null)
        {
            _activity.Changed -= OnActivityChanged;
        }
    }
}

/// <summary>One operation in the status bar and the operations panel.</summary>
public sealed partial class OperationItemViewModel : ObservableObject
{
    /// <summary>Shows an operation.</summary>
    /// <param name="operation">The operation.</param>
    public OperationItemViewModel(TrackedOperation operation)
    {
        Operation = operation;
    }

    /// <summary>The operation.</summary>
    public TrackedOperation Operation { get; }

    /// <summary>What it is, such as "Installing Pico SDK".</summary>
    public string Title => Operation.Title;

    /// <summary>"Downloading Pico SDK 120/340 MB · about 2 min left".</summary>
    public string Text => Operation.Text;

    /// <summary>The stage and its numbers, without the estimate.</summary>
    public string Stage => Operation.Progress.Text;

    /// <summary>"about 2 min left", once it can be estimated.</summary>
    public string? RemainingText => Operation.RemainingText;

    /// <summary>How much is done, from 0 to 100, for the progress bar.</summary>
    public double Percent => (Operation.Progress.Fraction ?? 0) * 100;

    /// <summary>Whether the progress bar moves without numbers.</summary>
    public bool IsIndeterminate => Operation.Progress.Fraction is null;

    /// <summary>Whether Cancel is offered.</summary>
    public bool CanCancel => Operation.CanCancel && !Operation.IsCancelling;

    /// <summary>Whether the raw log is shown.</summary>
    [ObservableProperty]
    public partial bool ShowDetails { get; set; }

    /// <summary>"Show details" or "Hide details", for the button that toggles the raw log.</summary>
    public string DetailsLabel => ShowDetails ? "Hide details" : "Show details";

    /// <summary>Whether there is a raw log to show.</summary>
    public bool HasDetails => Operation.Details.Count > 0;

    /// <summary>The last lines of the raw log, while it is shown.</summary>
    public string Details => ShowDetails ? string.Join('\n', Operation.Details.TakeLast(400)) : string.Empty;

    /// <summary>Asks the operation to stop.</summary>
    [RelayCommand]
    private void Cancel()
    {
        Operation.Cancel();
        Refresh();
    }

    /// <summary>Shows or hides the raw log.</summary>
    [RelayCommand]
    private void ToggleDetails() => ShowDetails = !ShowDetails;

    partial void OnShowDetailsChanged(bool value)
    {
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(DetailsLabel));
    }

    /// <summary>Raises the changes of everything the operation says.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Stage));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(Percent));
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(CanCancel));
        RefreshDetails();
    }

    /// <summary>Raises the change of the raw log, while it is shown.</summary>
    public void RefreshDetails()
    {
        OnPropertyChanged(nameof(HasDetails));
        if (ShowDetails)
        {
            OnPropertyChanged(nameof(Details));
        }
    }
}
