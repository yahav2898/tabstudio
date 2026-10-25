using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using AndroidApplication = Android.App.Application;

namespace TabStudio.Android.Probe;

/// <summary>
/// TEMPORARY: a minimal Avalonia application that proves the Android head works before the real IDE exists.
/// </summary>
/// <remarks>
/// <para>
/// It shows one dark page and, on a background thread, compiles and runs a small C# solution with
/// TabStudio.Compilation, then reports the result on screen and as one logcat line that CI waits for
/// (see <see cref="ProbeLog"/>).
/// </para>
/// <para>
/// Everything in the Probe folder goes away when <c>TabStudio.App.TabStudioApplication</c> lands: switch the
/// type argument of <see cref="MainActivity"/>'s base class and delete this folder.
/// </para>
/// </remarks>
public sealed class ProbeApp : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var cacheDirectory = AndroidApplication.Context.CacheDir?.AbsolutePath ?? Path.GetTempPath();
            singleView.MainView = new ProbeView(new ProbeRunner(cacheDirectory));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
