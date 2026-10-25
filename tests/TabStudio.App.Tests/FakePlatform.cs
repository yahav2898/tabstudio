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
