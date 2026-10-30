namespace TabStudio.App.Platform;

/// <summary>A notification for the system's notification shade.</summary>
/// <param name="Tag">Identifies what the notification is about; a newer notification with the same tag replaces it.</param>
/// <param name="Title">The first line, such as "CI #57 failed".</param>
/// <param name="Text">The second line, such as "Fix the build · main · 3m 12s".</param>
/// <param name="IsFailure">Whether it reports a failure, which the system may show more prominently.</param>
public sealed record SystemNotification(string Tag, string Title, string Text, bool IsFailure)
{
    /// <summary>
    /// Whether it shows an operation still running, such as an SDK download TabStudio continues in the background: an
    /// ongoing notification with a progress bar, which a later one with the same tag replaces when it ends.
    /// </summary>
    public bool IsProgress { get; init; }

    /// <summary>How much of the operation is done, from 0 to 100, or <see langword="null"/> for a bar without numbers.</summary>
    public int? ProgressPercent { get; init; }
}

/// <summary>
/// The system's notifications. Android implements this with a notification channel; elsewhere TabStudio has
/// only its in-app notices and <see cref="IsSupported"/> is false.
/// </summary>
public interface INotifications
{
    /// <summary>Whether the platform has system notifications.</summary>
    bool IsSupported { get; }

    /// <summary>Whether the user allows TabStudio's notifications (Android 13 and later ask).</summary>
    bool IsAllowed { get; }

    /// <summary>Raised on the UI thread when the user taps a notification, with its tag.</summary>
    event EventHandler<string>? Opened;

    /// <summary>Asks the user to allow notifications, if the platform asks.</summary>
    /// <returns>Whether notifications are allowed now.</returns>
    Task<bool> RequestPermissionAsync();

    /// <summary>Shows a notification, replacing one with the same tag.</summary>
    /// <param name="notification">What to show.</param>
    void Show(SystemNotification notification);

    /// <summary>
    /// Whether <see cref="KeepRunning"/> keeps TabStudio running in the background and shows the progress itself, as
    /// Android's foreground service does; operations then need no progress notification of their own.
    /// </summary>
    bool KeepsRunning { get; }

    /// <summary>
    /// Keeps TabStudio running while long operations run, with the newest one's progress in an ongoing notification.
    /// Without it Android freezes an app in the background, and an install or a build stopped where it was until the
    /// user came back. Called on the UI thread, again as the progress changes, and with <see langword="null"/> when the
    /// last operation ends.
    /// </summary>
    /// <param name="progress">What runs and how far it is, or <see langword="null"/> when nothing runs any more.</param>
    void KeepRunning(SystemNotification? progress);
}

/// <summary>The notifications of a platform without system notifications, such as the desktop host.</summary>
public sealed class NoSystemNotifications : INotifications
{
    /// <inheritdoc />
    public bool IsSupported => false;

    /// <inheritdoc />
    public bool IsAllowed => false;

    /// <inheritdoc />
    public event EventHandler<string>? Opened
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public Task<bool> RequestPermissionAsync() => Task.FromResult(false);

    /// <inheritdoc />
    public void Show(SystemNotification notification)
    {
        // The in-app notice is all there is on this platform.
    }

    /// <inheritdoc />
    public bool KeepsRunning => false;

    /// <inheritdoc />
    public void KeepRunning(SystemNotification? progress)
    {
        // A desktop process keeps running behind other windows.
    }
}
