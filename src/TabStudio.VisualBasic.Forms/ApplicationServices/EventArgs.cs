using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Microsoft.VisualBasic.ApplicationServices;

/// <summary>The data of <see cref="WindowsFormsApplicationBase.Startup"/>: the command line, and whether to cancel.</summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
[ComVisible(false)]
public class StartupEventArgs : CancelEventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="args">The command-line arguments; <see langword="null"/> for none.</param>
    public StartupEventArgs(ReadOnlyCollection<string>? args) => CommandLine = args ?? ReadOnlyCollection<string>.Empty;

    /// <summary>The command-line arguments.</summary>
    public ReadOnlyCollection<string> CommandLine { get; }
}

/// <summary>
/// The data of <see cref="WindowsFormsApplicationBase.StartupNextInstance"/>: the command line of the instance that
/// started while the single-instance app ran, and whether to bring the app to the front.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public class StartupNextInstanceEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="args">The command-line arguments of the new instance; <see langword="null"/> for none.</param>
    /// <param name="bringToForegroundFlag">Whether to activate the main form.</param>
    public StartupNextInstanceEventArgs(ReadOnlyCollection<string>? args, bool bringToForegroundFlag)
    {
        CommandLine = args ?? ReadOnlyCollection<string>.Empty;
        BringToForeground = bringToForegroundFlag;
    }

    /// <summary>Whether the main form is activated after the event.</summary>
    public bool BringToForeground { get; set; }

    /// <summary>The command-line arguments of the new instance.</summary>
    public ReadOnlyCollection<string> CommandLine { get; }
}

/// <summary>
/// The data of <see cref="WindowsFormsApplicationBase.UnhandledException"/>: the exception, and whether the app ends
/// after the event, which it does unless a handler sets <see cref="ExitApplication"/> to <see langword="false"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
[ComVisible(false)]
public class UnhandledExceptionEventArgs : ThreadExceptionEventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="exitApplication">Whether the app ends after the event.</param>
    /// <param name="exception">The exception.</param>
    public UnhandledExceptionEventArgs(bool exitApplication, Exception exception)
        : base(exception) => ExitApplication = exitApplication;

    /// <summary>Whether the app ends after the event.</summary>
    public bool ExitApplication { get; set; }
}

/// <summary>
/// The data of <see cref="WindowsFormsApplicationBase.ApplyApplicationDefaults"/>: the app-wide defaults a handler may
/// change before the first form is created.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
[ComVisible(false)]
public class ApplyApplicationDefaultsEventArgs : EventArgs
{
    internal ApplyApplicationDefaultsEventArgs(int minimumSplashScreenDisplayTime, HighDpiMode highDpiMode, SystemColorMode colorMode)
    {
        MinimumSplashScreenDisplayTime = minimumSplashScreenDisplayTime;
        HighDpiMode = highDpiMode;
        ColorMode = colorMode;
    }

    /// <summary>The app's color mode. TabStudio draws forms in Windows' classic colors whatever it is.</summary>
    public SystemColorMode ColorMode { get; set; }

    /// <summary>The default font of the app's controls, or <see langword="null"/> to keep the standard one.</summary>
    public Font? Font { get; set; }

    /// <summary>The high-DPI mode. TabStudio's app preview scales forms itself.</summary>
    public HighDpiMode HighDpiMode { get; set; }

    /// <summary>How long, in milliseconds, the splash screen stays at least.</summary>
    public int MinimumSplashScreenDisplayTime { get; set; } = WindowsFormsApplicationBase.MinimumSplashExposureDefault;
}
