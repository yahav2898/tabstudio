using System.Collections.Concurrent;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using TabStudio.App.Platform;
using Uri = Android.Net.Uri;

namespace TabStudio.Android;

/// <summary>Streams APKs into PackageInstaller sessions and lets Android ask the user to install them.</summary>
public sealed class AndroidAppInstaller(Context context) : IAndroidAppInstaller
{
    /// <inheritdoc />
    public async Task<string> InstallAndLaunchAsync(string apk, CancellationToken cancellationToken)
    {
        var manager = context.PackageManager ?? throw new InvalidOperationException("Android's package manager is unavailable.");
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && !manager.CanRequestPackageInstalls())
        {
            context.StartActivity(new Intent(global::Android.Provider.Settings.ActionManageUnknownAppSources, Uri.Parse("package:" + context.PackageName))
                .AddFlags(ActivityFlags.NewTask));
            throw new InvalidOperationException("Allow TabStudio to install apps in Android Settings, then choose Run Android App again.");
        }

        var info = manager.GetPackageArchiveInfo(apk, PackageInfoFlags.Activities)
            ?? throw new InvalidOperationException("The build output is not a valid Android APK.");
        var package = info.PackageName ?? throw new InvalidOperationException("The APK has no package name.");
        var installer = manager.PackageInstaller ?? throw new InvalidOperationException("Android's installer is unavailable.");
        using var parameters = new PackageInstaller.SessionParams(PackageInstallMode.FullInstall);
        parameters.SetAppPackageName(package);
        parameters.SetSize(new FileInfo(apk).Length);
        var sessionId = installer.CreateSession(parameters);
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        InstallResultReceiver.Pending[sessionId] = result;
        try
        {
            using var session = installer.OpenSession(sessionId);
            using (var source = File.OpenRead(apk))
            using (var target = session.OpenWrite("base.apk", 0, source.Length))
            {
                await source.CopyToAsync(target, cancellationToken);
                session.Fsync(target);
            }

            var callback = new Intent(context, typeof(InstallResultReceiver)).PutExtra("tabstudio.session", sessionId);
            var flags = PendingIntentFlags.UpdateCurrent;
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                flags |= PendingIntentFlags.Mutable;
            }

            using var pending = PendingIntent.GetBroadcast(context, sessionId, callback, flags)
                ?? throw new InvalidOperationException("Android could not create the installation callback.");
            session.Commit(pending.IntentSender);
            await result.Task.WaitAsync(cancellationToken);
            var launch = manager.GetLaunchIntentForPackage(package)
                ?? throw new InvalidOperationException($"{package} was installed but has no launchable activity.");
            context.StartActivity(launch.AddFlags(ActivityFlags.NewTask));
            return package;
        }
        finally
        {
            InstallResultReceiver.Pending.TryRemove(sessionId, out _);
            if (!result.Task.IsCompletedSuccessfully)
            {
                installer.AbandonSession(sessionId);
            }
        }
    }
}

/// <summary>Receives PackageInstaller's final result, including its request for the system confirmation UI.</summary>
[BroadcastReceiver(Exported = false)]
public sealed class InstallResultReceiver : BroadcastReceiver
{
    internal static ConcurrentDictionary<int, TaskCompletionSource> Pending { get; } = new();

    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null || !Pending.TryGetValue(intent.GetIntExtra("tabstudio.session", -1), out var completion))
        {
            return;
        }

        var status = intent.GetIntExtra(PackageInstaller.ExtraStatus, (int)PackageInstallStatus.Failure);
        if (status == (int)PackageInstallStatus.PendingUserAction)
        {
#pragma warning disable CA1422 // The typed API is Android 13+; this app also runs on Android 7+.
            if (intent.GetParcelableExtra(Intent.ExtraIntent) is Intent confirmation)
#pragma warning restore CA1422
            {
                context.StartActivity(confirmation.AddFlags(ActivityFlags.NewTask));
            }
            else
            {
                completion.TrySetException(new InvalidOperationException("Android did not provide an installation confirmation."));
            }
        }
        else if (status == (int)PackageInstallStatus.Success)
        {
            completion.TrySetResult();
        }
        else
        {
            completion.TrySetException(new InvalidOperationException("Android installation failed: " + intent.GetStringExtra(PackageInstaller.ExtraStatusMessage)));
        }
    }
}
