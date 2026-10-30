using TabStudio.App.Platform;

namespace TabStudio.App.Tests;

/// <summary>System notifications that remember what they showed, and a user who allows them when asked.</summary>
internal sealed class FakeNotifications : INotifications
{
    /// <summary>The notifications shown, in order.</summary>
    public List<SystemNotification> Shown { get; } = [];

    /// <summary>How often the user was asked to allow notifications.</summary>
    public int PermissionRequests { get; private set; }

    /// <summary>Whether the user allows them when asked.</summary>
    public bool AllowsWhenAsked { get; set; } = true;

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public bool IsAllowed { get; private set; }

    /// <inheritdoc />
    public event EventHandler<string>? Opened;

    /// <inheritdoc />
    public Task<bool> RequestPermissionAsync()
    {
        PermissionRequests++;
        IsAllowed = AllowsWhenAsked;
        return Task.FromResult(IsAllowed);
    }

    /// <inheritdoc />
    public void Show(SystemNotification notification) => Shown.Add(notification);

    /// <inheritdoc />
    /// <remarks>False unless a test says otherwise, as on a platform without a foreground service.</remarks>
    public bool KeepsRunning { get; set; }

    /// <summary>What <see cref="KeepRunning"/> was called with, in order; <see langword="null"/> where it let the app go.</summary>
    public List<SystemNotification?> Kept { get; } = [];

    /// <summary>Whether the app is kept running now.</summary>
    public bool IsKeptRunning => Kept.Count > 0 && Kept[^1] is not null;

    /// <inheritdoc />
    public void KeepRunning(SystemNotification? progress) => Kept.Add(progress);

    /// <summary>Taps a notification, as the user would in the notification shade.</summary>
    public void Tap(SystemNotification notification) => Opened?.Invoke(this, notification.Tag);
}

/// <summary>An app the test moves to the background and back.</summary>
internal sealed class FakeAppActivity : IAppActivity
{
    /// <inheritdoc />
    public bool IsInBackground { get; private set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Moves the app to the background or the foreground.</summary>
    public void Set(bool inBackground)
    {
        IsInBackground = inBackground;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
