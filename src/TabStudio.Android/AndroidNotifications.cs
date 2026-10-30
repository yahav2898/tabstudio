using Android.App;
using Android.Content;
using Android.Content.PM;
using Avalonia.Android;
using Avalonia.Threading;
using TabStudio.App.Platform;

namespace TabStudio.Android;

/// <summary>
/// System notifications on Android, in a "GitHub Actions" channel: a run that finished while TabStudio was in the
/// background. Tapping one brings TabStudio back and opens the run. Long operations, such as an SDK download that runs
/// on in the background, have a quiet "Long operations" channel with a progress bar: the ongoing notification of the
/// <see cref="OperationsService"/> that keeps TabStudio running while they do, and one that says how each ended. On
/// Android 13 and later the user is asked for the POST_NOTIFICATIONS permission the first time they turn notifications on.
/// </summary>
internal sealed class AndroidNotifications : INotifications
{
    /// <summary>The intent extra that carries a tapped notification's tag back to the activity.</summary>
    public const string TagExtra = "tabstudio.notification";

    private const string ChannelId = "github-actions";

    /// <summary>The channel of long operations that run on in the background, such as an SDK download: quiet, no sound.</summary>
    private const string OperationsChannelId = "operations";
    private const int PermissionRequestCode = 7301;

    /// <summary>Android 13's permission to post notifications (<c>Manifest.Permission.PostNotifications</c>).</summary>
    private const string PostNotificationsPermission = "android.permission.POST_NOTIFICATIONS";

    private static readonly string[] Permissions = [PostNotificationsPermission];

    private readonly Context _context;
    private readonly Func<Activity?> _activity;

    /// <summary>The ongoing notification of the running operations, which <see cref="OperationsService"/> shows; none when nothing runs.</summary>
    internal static Notification? Running { get; private set; }

    /// <summary>Creates the notifications and their channel.</summary>
    /// <param name="context">The application context.</param>
    /// <param name="activity">Returns the activity, which asks for permissions and opens from a notification.</param>
    public AndroidNotifications(Context context, Func<Activity?> activity)
    {
        _context = context;
        _activity = activity;
        if (context.GetSystemService(Context.NotificationService) is NotificationManager manager)
        {
            var channel = new NotificationChannel(ChannelId, "GitHub Actions runs", NotificationImportance.Default)
            {
                Description = "When a workflow run you are waiting for finishes.",
            };
            manager.CreateNotificationChannel(channel);
            manager.CreateNotificationChannel(new NotificationChannel(OperationsChannelId, "Long operations", NotificationImportance.Low)
            {
                Description = "Downloads, installs and builds that run on while TabStudio is in the background, and how they ended.",
            });
        }
    }

    /// <inheritdoc />
    public event EventHandler<string>? Opened;

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public bool IsAllowed
    {
        get
        {
            if (_context.GetSystemService(Context.NotificationService) is not NotificationManager manager || !manager.AreNotificationsEnabled())
            {
                return false;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                return _context.CheckSelfPermission(PostNotificationsPermission) == Permission.Granted;
            }

            return true;
        }
    }

    /// <inheritdoc />
    public Task<bool> RequestPermissionAsync()
    {
        if (IsAllowed)
        {
            return Task.FromResult(true);
        }

        // Before Android 13 there is nothing to ask: the user turned notifications off in the system settings.
        return OperatingSystem.IsAndroidVersionAtLeast(33) && _activity() is AvaloniaActivity activity
            ? AskAsync(activity)
            : Task.FromResult(false);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android33.0")]
    private static Task<bool> AskAsync(AvaloniaActivity activity)
    {
        var answer = new TaskCompletionSource<bool>();
        void OnResult(int requestCode, string[] permissions, Permission[] results)
        {
            if (requestCode != PermissionRequestCode)
            {
                return;
            }

            activity.RequestPermissionsResult -= OnResult;
            answer.TrySetResult(results.Length > 0 && results.All(r => r == Permission.Granted));
        }

        activity.RequestPermissionsResult += OnResult;
        activity.RequestPermissions(Permissions, PermissionRequestCode);
        return answer.Task;
    }

    /// <inheritdoc />
    public void Show(SystemNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (!IsAllowed || _context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
        {
            return;
        }

        var open = new Intent(_context, typeof(MainActivity))
            .SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop)
            .PutExtra(TagExtra, notification.Tag);
        var pending = PendingIntent.GetActivity(_context, notification.Tag.GetHashCode(StringComparison.Ordinal), open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var operation = notification.Tag.StartsWith("operation:", StringComparison.Ordinal);
        var builder = new Notification.Builder(_context, operation ? OperationsChannelId : ChannelId)
            .SetSmallIcon(Resource.Drawable.ic_stat_tabstudio)
            .SetContentTitle(notification.Title)
            .SetContentText(notification.Text)
            .SetStyle(new Notification.BigTextStyle().BigText(notification.Text))
            .SetCategory(notification.IsFailure ? Notification.CategoryError : notification.IsProgress ? Notification.CategoryProgress : Notification.CategoryStatus)
            .SetContentIntent(pending)
            .SetAutoCancel(!notification.IsProgress);
        if (notification.IsProgress)
        {
            // An ongoing operation: the bar moves, the shade does not buzz on every update, and it cannot be swiped away
            // while it runs; the notification that says how it ended replaces it.
            builder.SetOngoing(true)
                .SetOnlyAlertOnce(true)
                .SetProgress(100, notification.ProgressPercent ?? 0, notification.ProgressPercent is null);
        }

        var built = builder.Build();
        manager.Notify(notification.Tag, 1, built);
    }

    /// <inheritdoc />
    public bool KeepsRunning => true;

    /// <inheritdoc />
    public void KeepRunning(SystemNotification? progress)
    {
        if (progress is null)
        {
            Running = null;
            OperationsService.Current?.Stop();
            return;
        }

        Running = BuildRunning(_context, progress.Title, progress.Text, progress.ProgressPercent);
        if (OperationsService.Current is not null)
        {
            // The service's notification follows the progress; it shows without the permission's prompt as well.
            (_context.GetSystemService(Context.NotificationService) as NotificationManager)?.Notify(OperationsService.NotificationId, Running);
            return;
        }

        try
        {
            // Operations begin while the user is in TabStudio, which is when Android lets a foreground service start.
            _context.StartForegroundService(new Intent(_context, typeof(OperationsService)));
        }
        catch (Java.Lang.IllegalStateException)
        {
            // Android 12 and later refuse to start one from the background (ForegroundServiceStartNotAllowedException):
            // the operation runs as long as Android lets TabStudio run, as before.
        }
        catch (Java.Lang.SecurityException)
        {
            // The foreground service permissions are in the manifest; a device that refuses them runs as before.
        }
    }

    /// <summary>The ongoing notification of running operations: quiet, with a progress bar, opening TabStudio's operations when tapped.</summary>
    /// <param name="context">A context.</param>
    /// <param name="title">The newest operation, such as "Installing Python (+1 more)".</param>
    /// <param name="text">Its stage and numbers.</param>
    /// <param name="percent">How much of it is done, or <see langword="null"/> for a bar without numbers.</param>
    internal static Notification BuildRunning(Context context, string title, string text, int? percent)
    {
        var open = new Intent(context, typeof(MainActivity))
            .SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop)
            .PutExtra(TagExtra, "operation:running");
        var pending = PendingIntent.GetActivity(context, OperationsService.NotificationId, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        return new Notification.Builder(context, OperationsChannelId)
            .SetSmallIcon(Resource.Drawable.ic_stat_tabstudio)
            .SetContentTitle(title)
            .SetContentText(text)
            .SetCategory(Notification.CategoryProgress)
            .SetContentIntent(pending)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .SetProgress(100, percent ?? 0, percent is null)
            .Build()!;
    }

    /// <summary>Opens what a tapped notification is about, when the activity was started or brought back by one.</summary>
    /// <param name="intent">The intent the activity received.</param>
    public void HandleIntent(Intent? intent)
    {
        if (intent?.GetStringExtra(TagExtra) is { Length: > 0 } tag)
        {
            intent.RemoveExtra(TagExtra);
            Dispatcher.UIThread.Post(() => Opened?.Invoke(this, tag));
        }
    }
}
