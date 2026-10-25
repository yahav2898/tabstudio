using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using TabStudio.Android.Probe;
using SplashScreen = AndroidX.Core.SplashScreen.SplashScreen;

namespace TabStudio.Android;

/// <summary>
/// The activity Android starts when TabStudio is launched. It hosts the Avalonia application full screen.
/// </summary>
/// <remarks>
/// <para>
/// The activity's Java name is fixed (<c>com.tabstudio.ide.MainActivity</c>) so that scripts can start it with
/// <c>adb shell am start -n com.tabstudio.ide/com.tabstudio.ide.MainActivity</c>, and so that renaming the C# class
/// never breaks launcher shortcuts.
/// </para>
/// <para>
/// It handles size, orientation, keyboard and theme changes itself instead of being recreated, so attaching a
/// keyboard or resizing TabStudio in split screen never interrupts the IDE.
/// </para>
/// </remarks>
[Activity(
    Name = "com.tabstudio.ide.MainActivity",
    Theme = "@style/Theme.TabStudio.Starting",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize
        | ConfigChanges.ScreenLayout | ConfigChanges.UiMode | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
        | ConfigChanges.Navigation)]
// TEMPORARY: ProbeApp stands in for the real application until TabStudio.App lands. To switch, change this one
// line to `AvaloniaMainActivity<TabStudio.App.TabStudioApplication>` and delete the Probe folder.
public sealed class MainActivity : AvaloniaMainActivity<ProbeApp>
{
    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Before base.OnCreate: shows the splash screen, then switches to the app theme.
        SplashScreen.InstallSplashScreen(this);
        base.OnCreate(savedInstanceState);
    }
}
