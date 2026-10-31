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
/// <para>
/// Its type is "data sync": downloads and local file processing. Android 15 lets such a service run six hours a day,
/// so it stops itself before then.
/// </para>
/// <para>
/// It is started with <c>startService</c> while TabStudio is in front, and goes into the foreground itself. It was
/// started with <c>startForegroundService</c>, which obliges the service to call <c>startForeground</c> within ten
/// seconds and ends the app with an ANR when it does not, or when the service is stopped first ("Bringing down service
/// while still waiting for start foreground"). Operations begin and end in quick succession, as the self test's
/// toolchain installs do one after another, so a stop could land between a start and its <c>startForeground</c>, and
/// Android ended TabStudio in the middle of an install. A plain start has no such deadline.
/// </para>
/// </remarks>
// 1 is ForegroundService.TypeDataSync, written as a number: the constant is Android 10's, above the minimum version.
[Service(Name = "com.tabstudio.ide.OperationsService", Exported = false, ForegroundServiceType = (ForegroundService)1)]
public sealed class OperationsService : Service
{
    /// <summary>The id of the service's ongoing notification.</summary>
    internal const int NotificationId = 7302;

    /// <summary>The longest the service runs at a time, inside Android 15's six hours a day for data sync.</summary>
    private static readonly TimeSpan Longest = TimeSpan.FromHours(5.5);

    private PowerManager.WakeLock? _wakeLock;
    private Handler? _handler;
    private int _lastStartId;

    /// <summary>The running service, once it is in the foreground. Used on the main thread only.</summary>
    internal static OperationsService? Current { get; private set; }

    /// <inheritdoc />
    public override IBinder? OnBind(Intent? intent) => null;

    /// <inheritdoc />
    public override void OnCreate()
    {
        base.OnCreate();
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
        _lastStartId = startId;
        var running = AndroidNotifications.Running;
        if (running is null)
        {
            // The operations ended before the start arrived: there is nothing to keep running.
            StopSelf(startId);
            return StartCommandResult.NotSticky;
        }

        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                StartForeground(NotificationId, running, ForegroundService.TypeDataSync);
            }
            else
            {
                StartForeground(NotificationId, running);
            }
        }
        catch (Java.Lang.IllegalStateException)
        {
            // Android 12 and later refuse the foreground from the background (ForegroundServiceStartNotAllowedException)
            // when the user left TabStudio between the start and now: the operation runs as long as Android lets
            // TabStudio run, as it does when the start itself is refused.
            StopSelf(startId);
            return StartCommandResult.NotSticky;
        }

        Current = this;
        return StartCommandResult.NotSticky;
    }

    /// <summary>Takes the service and its notification away, when the last operation ended.</summary>
    /// <remarks>
    /// Stops with the id of the last start it handled, which Android ignores while a newer start is waiting. Without it
    /// <c>StopSelf()</c> also took down the start Android was making for the next operation, before that start could
    /// call <c>startForeground</c>, and Android ended the app ("did not then call Service.startForeground()"): an
    /// operation that ended just after the next one began, as when one toolchain install follows another.
    /// </remarks>
    internal void Stop()
    {
        if (ReferenceEquals(Current, this))
        {
            // The next operation starts the service again rather than updating one on its way out.
            Current = null;
        }

        StopForeground(StopForegroundFlags.Remove);
        StopSelf(_lastStartId);
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
