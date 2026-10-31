using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using AndroidLog = Android.Util.Log;
using AndroidXSplashScreen = AndroidX.Core.SplashScreen.SplashScreen;

namespace TabStudio.Android;

/// <summary>
/// The activity Android starts when TabStudio is launched. It hosts the IDE full screen.
/// </summary>
/// <remarks>
/// <para>
/// The activity's Java name is fixed (<c>com.tabstudio.ide.MainActivity</c>) so that scripts can start it with
/// <c>adb shell am start -n com.tabstudio.ide/com.tabstudio.ide.MainActivity</c>, and so that renaming the C# class
/// never breaks launcher shortcuts.
/// </para>
/// <para>
/// It handles size, orientation, keyboard and theme changes itself instead of being recreated, so attaching a
/// keyboard or resizing TabStudio in split screen never interrupts the IDE. The window resizes for the
/// on-screen keyboard; on Android 15 and later, where apps are drawn edge to edge, the IDE pads itself instead.
/// </para>
/// <para>
/// Started with the boolean extra <c>tabstudio.selftest</c>, it opens the sample, presses F5, debugs it, debugs
/// it again with a TabStudio Link host compiling, serves a new web app, shows a new Avalonia app, and logs the
/// outcome under the <c>TabStudio</c> tag as a <c>TABSTUDIO_SELFTEST_OK</c> or <c>TABSTUDIO_SELFTEST_FAIL</c> line.
/// CI runs this on an emulator. With <c>tabstudio.selftest.desktop</c> as well, it then runs desktop mode on the
/// emulator's simulated second display, asking the CI script in its log to type and take screenshots.
/// </para>
/// </remarks>
[Activity(
    Name = "com.tabstudio.ide.MainActivity",
    Theme = "@style/Theme.TabStudio.Starting",
    MainLauncher = true,
    Exported = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize
        | ConfigChanges.ScreenLayout | ConfigChanges.UiMode | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
        | ConfigChanges.Navigation | ConfigChanges.FontScale | ConfigChanges.LayoutDirection)]
public sealed partial class MainActivity : AvaloniaMainActivity<TabStudioApplication>
{
    /// <summary>The intent extra that runs the self test.</summary>
    public const string SelfTestExtra = "tabstudio.selftest";

    /// <summary>The intent extra that adds desktop mode to the self test, on a device with a second display.</summary>
    public const string DesktopSelfTestExtra = "tabstudio.selftest.desktop";

    /// <summary>The optional independent self-test group: managed, ui, native, or all.</summary>
    public const string SelfTestShardExtra = "tabstudio.selftest.shard";

    /// <summary>The intent extra that runs only the experimental Flutter web self test instead of a shard.</summary>
    public const string FlutterSelfTestExtra = "tabstudio.selftest.flutter";

    private const string LogTag = "TabStudio";
    private static readonly TimeSpan SelfTestTimeout = TimeSpan.FromMinutes(15);

    // The app outlives an activity that Android recreates: its notifications follow whichever activity is current.
    private static MainActivity? _current;
    private static AndroidNotifications? _notifications;

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        _current = this;

        // Before base.OnCreate: shows the splash screen, then switches to the app theme.
        AndroidXSplashScreen.InstallSplashScreen(this);
        base.OnCreate(savedInstanceState);

        if (Intent?.GetBooleanExtra(SelfTestExtra, false) == true)
        {
            var desktop = Intent.GetBooleanExtra(DesktopSelfTestExtra, false);
            var shard = Intent.GetStringExtra(SelfTestShardExtra);
            var flutter = Intent.GetBooleanExtra(FlutterSelfTestExtra, false);
            var screenshots = GetExternalFilesDir(null) is { } files ? Path.Combine(files.AbsolutePath, "selftest") : null;
            Dispatcher.UIThread.Post(() => _ = RunSelfTestAsync(desktop, screenshots, shard, flutter));
        }

        // Started from a tapped notification: open the run it is about.
        _notifications?.HandleIntent(Intent);
    }

    /// <inheritdoc />
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        // A tapped notification brings the running activity back with the run it is about.
        _notifications?.HandleIntent(intent);
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        // TabStudio is closing: desktop mode ends, so the display shows again what it showed before.
        if (IsFinishing && (Avalonia.Application.Current as TabStudioApplication)?.Main?.DesktopMode is { IsActive: true } desktop)
        {
            desktop.ExitCommand.Execute(null);
        }

        if (ReferenceEquals(_current, this))
        {
            _current = null;
        }

        base.OnDestroy();
    }

    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Mono can resolve SqlClient's general runtime flavor. Select managed SNI before any provider
        // initializes; Android cannot load the Windows native SNI library. The switch keeps its historic name.
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
        var context = ApplicationContext!;
        var dataDirectory = Path.Combine(context.FilesDir!.AbsolutePath, "TabStudio");
        var notifications = _notifications ??= new AndroidNotifications(context, () => _current);
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .AfterSetup(b => ((TabStudioApplication)b.Instance!).UseDataDirectory(dataDirectory))
            .UseTabStudioPlatform(services =>
            {
                services.AddSingleton<TabStudio.Database.IDatabaseSecretStore>(_ => new AndroidDatabaseSecretStore(context));
                services.AddSingleton<IDeviceStatus>(_ => new AndroidDeviceStatus(context));
                services.AddSingleton<IAndroidAppInstaller>(_ => new AndroidAppInstaller(context));
                services.AddSingleton<IWebViewFactory>(_ => new AndroidWebViewFactory(() => this));
                services.AddSingleton<INotifications>(notifications);
                services.AddSingleton<IQrScanner>(_ => new AndroidQrScanner(() => this));
                services.AddSingleton<IImageClipboard>(_ => new AndroidImageClipboard(context));
                services.AddSingleton<IDeviceIdentity>(_ => new AndroidDeviceIdentity(context));

                // Presentations on external displays belong to the current activity.
                services.AddSingleton<IExternalDisplayService>(_ => _displays ??= new AndroidExternalDisplays(context, () => _current));
                services.AddSingleton(AndroidToolchainPlatform.Create(context));
                services.AddSingleton<TabStudio.Remote.ISshKeyStore, AndroidSshKeyStore>();
                services.AddSingleton<TabStudio.Toolchains.Microchip.IUsbHost>(_ => new AndroidUsbHost(context));
            });
    }

    private static async Task RunSelfTestAsync(bool desktop, string? screenshots, string? shardName, bool flutter)
    {
        if (!SelfTest.TryParseShard(shardName, out var shard))
        {
            AndroidLog.Error(LogTag, $"TABSTUDIO_SELFTEST_FAIL Unknown self-test shard '{shardName}'.");
            return;
        }

        AndroidLog.Info(LogTag, flutter ? "TABSTUDIO_SELFTEST_START flutter" : $"TABSTUDIO_SELFTEST_START shard={shard.ToString().ToLowerInvariant()}");
        var app = Avalonia.Application.Current as TabStudioApplication;
        if (app?.Main is not { } main || app.Services is not { } services)
        {
            AndroidLog.Error(LogTag, "TABSTUDIO_SELFTEST_FAIL The app did not start.");
            return;
        }

        using var timeout = new CancellationTokenSource(SelfTestTimeout);
        var toolchains = services.GetRequiredService<TabStudio.App.Services.Toolchains.ToolchainServices>().Manager;
        var preparationClock = System.Diagnostics.Stopwatch.StartNew();
        var phases = new Dictionary<string, string>(StringComparer.Ordinal);
        var phaseGate = new object();
        void ReportPreparation(object? sender, TabStudio.Toolchains.Installation.ToolchainStatus status)
        {
            var phase = status.Progress?.Stage.ToString() ?? status.State.ToString();
            lock (phaseGate)
            {
                if (phases.GetValueOrDefault(status.Toolchain.Id) == phase)
                {
                    return;
                }

                phases[status.Toolchain.Id] = phase;
            }

            AndroidLog.Info(LogTag, $"Toolchain preparation: {status.Toolchain.Id} {phase} at {preparationClock.Elapsed.TotalSeconds:0.0} s");
        }

        SelfTestResult result;
        toolchains.StatusChanged += ReportPreparation;
        try
        {
            result = flutter
                ? await SelfTest.RunFlutterWebAsync(main, services.GetRequiredService<ProjectTemplates>(), timeout.Token)
                : await SelfTest.RunShardAsync(main, services.GetRequiredService<SampleLibrary>(), services.GetRequiredService<ProjectTemplates>(), screenshots, shard, timeout.Token);
        }
        finally
        {
            toolchains.StatusChanged -= ReportPreparation;
        }
        if (result.Success && !flutter && (shard is SelfTestShard.All or SelfTestShard.Ui))
        {
            var scrolling = await SelfTest.ScrollEditorAsync(main, services.GetRequiredService<SampleLibrary>(), new AndroidTouchSelfTestDevice(() => _current), line => AndroidLog.Info(LogTag, line), timeout.Token);
            result = new SelfTestResult(scrolling.Success, $"{result.Message} | {scrolling.Message}", result.Duration + scrolling.Duration);
        }

        if (result.Success && desktop && !flutter && (shard is SelfTestShard.All or SelfTestShard.Ui))
        {
            var tablet = (app.ApplicationLifetime as ISingleViewApplicationLifetime)?.MainView as TabStudio.App.Views.MainView;
            var input = new AndroidDesktopSelfTestInput(line => AndroidLog.Info(LogTag, line), _displays, () => _current);
            var part = tablet is null
                ? new SelfTestResult(false, "Desktop: the tablet's view is missing.", TimeSpan.Zero)
                : await SelfTest.RunDesktopModeAsync(main, tablet, input, screenshots, timeout.Token);
            result = new SelfTestResult(part.Success, $"{result.Message} | {part.Message}", result.Duration + part.Duration);
        }

        var seconds = result.Duration.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        var line = $"{(result.Success ? "TABSTUDIO_SELFTEST_OK" : "TABSTUDIO_SELFTEST_FAIL")} {seconds} s: {result.Message}";
        if (result.Success)
        {
            AndroidLog.Info(LogTag, line);
        }
        else
        {
            AndroidLog.Error(LogTag, line);
        }
    }
}
