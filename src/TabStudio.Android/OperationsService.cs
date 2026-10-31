using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace TabStudio.Android;

/// <summary>
/// The foreground service that keeps TabStudio running while long operations run: toolchain installs, builds,
/// project setup and downloads. Android freezes an app soon after it goes to the background unless a foreground
/// service runs, and such an operation stopped where it was until the user came back. The service's ongoing
/// notification shows the newest operation and its progress (<see cref="AndroidNotifications.KeepRunning"/>), and
/// a partial wake lock keeps the processor on while the screen is off.
/// </summary>
/// <remarks>
/// Its type is "data sync": downloads and local file processing. Android 15 lets such a service run six hours a day,
/// so it stops itself before then.
/// </remarks>
// 1 is ForegroundService.TypeDataSync, written as a number: the constant is Android 10's, above the minimum version.
[Service(Name = "com.tabstudio.ide.OperationsService", Exported = false, ForegroundServiceType = (ForegroundService)1)]
public sealed class OperationsService : Service
{
    /// <summary>The id of the service's ongoing notification.</summary>
    internal const int NotificationId = 7302;

    /// <summary>The longest the service runs at a time, inside Android 15's six hours a day for data sync.</summary>
    private static readonly TimeSpan Longest = TimeSpan.FromHours(5.5);

    private static bool _startPending;

    private PowerManager.WakeLock? _wakeLock;
    private Handler? _handler;

    /// <summary>The running service, once it is in the foreground. Used on the main thread only.</summary>
    internal static OperationsService? Current { get; private set; }

    /// <summary>Requests one start on the main thread while progress arrives before Android creates the service.</summary>
    internal static void EnsureStarted(Context context)
    {
        if (Current is not null || _startPending) return;
        _startPending = true;
        try
        {
            context.StartForegroundService(new Intent(context, typeof(OperationsService)));
        }
        catch (Exception ex) when (ex is Java.Lang.IllegalStateException or Java.Lang.SecurityException)
        {
            _startPending = false;
            throw;
        }
    }

    /// <inheritdoc />
    public override IBinder? OnBind(Intent? intent) => null;

    /// <inheritdoc />
    public override void OnCreate()
    {
        base.OnCreate();
        // Meet Android's foreground deadline before wake-lock/binder work or queued progress updates.
        EnterForeground();
        if (GetSystemService(PowerService) is PowerManager power)
        {
            _wakeLock = power.NewWakeLock(WakeLockFlags.Partial, "TabStudio:operations");
            _wakeLock?.SetReferenceCounted(false);
            _wakeLock?.Acquire((long)Longest.TotalMilliseconds);
        }

        _handler = new Handler(Looper.MainLooper!);
        _handler.PostDelayed(Stop, (long)Longest.TotalMilliseconds);
    }

    /// <inheritdoc />
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Android requires startForeground soon after startForegroundService, even when the operations ended in
        // between; the service then stops at once.
        if (!EnterForeground())
        {
            Stop();
        }

        return StartCommandResult.NotSticky;
    }

    private bool EnterForeground()
    {
        var running = AndroidNotifications.Running;
        var notification = running ?? AndroidNotifications.BuildRunning(this, "TabStudio", "Finishing", null);
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        // Only now may the operations stop it: stopping a service started with startForegroundService before it called
        // startForeground is an error Android ends the app for, which the last operation ending right after the first
        // began did under load (the emulator's self test).
        Current = this;
        _startPending = false;
        return running is not null;
    }

    /// <summary>Takes the service and its notification away, when the last operation ended.</summary>
    internal void Stop()
    {
        // A following operation must request a start, not update an instance whose destruction is queued.
        if (ReferenceEquals(Current, this)) Current = null;
        StopForeground(StopForegroundFlags.Remove);
        StopSelf();
    }

    /// <inheritdoc />
    public override void OnDestroy()
    {
        _handler?.RemoveCallbacksAndMessages(null);
        if (_wakeLock is { IsHeld: true } wakeLock)
        {
            wakeLock.Release();
        }

        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }

        base.OnDestroy();
    }
}
