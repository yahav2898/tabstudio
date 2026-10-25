using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
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
/// Started with the boolean extra <c>tabstudio.selftest</c>, it opens the sample, presses F5 and logs the
/// outcome under the <c>TabStudio</c> tag as a <c>TABSTUDIO_SELFTEST_OK</c> or <c>TABSTUDIO_SELFTEST_FAIL</c>
/// line. CI runs this on an emulator.
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
        | ConfigChanges.Navigation)]
public sealed class MainActivity : AvaloniaMainActivity<TabStudioApplication>
{
    /// <summary>The intent extra that runs the self test.</summary>
    public const string SelfTestExtra = "tabstudio.selftest";

    private const string LogTag = "TabStudio";
    private static readonly TimeSpan SelfTestTimeout = TimeSpan.FromMinutes(4);

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Before base.OnCreate: shows the splash screen, then switches to the app theme.
        AndroidXSplashScreen.InstallSplashScreen(this);
        base.OnCreate(savedInstanceState);

        if (Intent?.GetBooleanExtra(SelfTestExtra, false) == true)
        {
            Dispatcher.UIThread.Post(() => _ = RunSelfTestAsync());
        }
    }

    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        var context = ApplicationContext!;
        var dataDirectory = Path.Combine(context.FilesDir!.AbsolutePath, "TabStudio");
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .AfterSetup(b => ((TabStudioApplication)b.Instance!).UseDataDirectory(dataDirectory))
            .UseTabStudioPlatform(services => services.AddSingleton<IDeviceStatus>(_ => new AndroidDeviceStatus(context)));
    }

    private static async Task RunSelfTestAsync()
    {
        AndroidLog.Info(LogTag, "TABSTUDIO_SELFTEST_START");
        var app = Avalonia.Application.Current as TabStudioApplication;
        if (app?.Main is not { } main || app.Services is not { } services)
        {
            AndroidLog.Error(LogTag, "TABSTUDIO_SELFTEST_FAIL The app did not start.");
            return;
        }

        using var timeout = new CancellationTokenSource(SelfTestTimeout);
        var result = await SelfTest.RunAsync(main, services.GetRequiredService<SampleLibrary>(), timeout.Token);
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
