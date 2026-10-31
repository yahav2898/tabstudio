using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Services.Appearance;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App;

/// <summary>
/// The TabStudio application, shared by every platform. Platform heads configure it before it starts:
/// <c>AppBuilder.Configure&lt;TabStudioApplication&gt;().UseTabStudioPlatform(services => ...)</c>.
/// </summary>
public sealed partial class TabStudioApplication : Application
{
    private Action<IServiceCollection>? _configurePlatform;
    private string? _dataDirectory;
    private CrashLog? _crashLog;

    /// <summary>The app's services, once the framework has initialized.</summary>
    public ServiceProvider? Services { get; private set; }

    /// <summary>The root view model, once the framework has initialized.</summary>
    public MainViewModel? Main { get; private set; }

    /// <summary>Adds platform services, such as Android's device status. Call before the app starts.</summary>
    /// <param name="configure">Registers services.</param>
    public void ConfigurePlatform(Action<IServiceCollection> configure) => _configurePlatform += configure;

    /// <summary>Keeps all app data under a specific folder instead of the platform default. Used by tests.</summary>
    /// <param name="directory">The folder.</param>
    public void UseDataDirectory(string directory) => _dataDirectory = directory;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        var paths = _dataDirectory is null ? AppPaths.CreateDefault() : new AppPaths(_dataDirectory);
        Services = AppServices.Create(paths, _configurePlatform);

        // Windows Forms apps copy images through the device's own clipboard where Avalonia's holds only text.
        if (Services.GetService<Platform.IImageClipboard>() is { } images)
        {
            TabStudio.Forms.Hosting.FormsDesktop.ClipboardImages = new Platform.FormsClipboardImages(images);
        }

        // The saved theme first, so the first frame is drawn in it; tests (no lifetime) start dark and choose their own.
        ThemeSync.Start(this);
        // Hidden spinners and progress bars stop animating, so an idle IDE draws no frames.
        HiddenAnimations.Start();
        // A long press on a button names it, as a hover does with a mouse.
        Controls.TouchToolTips.Start();
        // Text size, touch targets, accessible names, no animations and a mirrored layout when the device asks.
        Accessibility.AccessibilityFeatures.Start(Accessibility.SystemAccessibility.Current);
        if (ApplicationLifetime is not null)
        {
            Services.GetRequiredService<AppearanceService>().Apply();
        }

        Main = Services.GetRequiredService<MainViewModel>();

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow { DataContext = Main };
                desktop.ShutdownRequested += (_, _) => Main.SaveSession();
                break;

            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new MainView { DataContext = Main };
                break;
        }

        // On Android the app can be stopped at any time once it is in the background: save the session first.
        if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
        {
            activatable.Deactivated += (_, _) => Main.SaveSession();
        }

        if (ApplicationLifetime is not null)
        {
            // An error nothing handled ended the app, with every tool window, build and debug session in it, whatever
            // the error was: a dropped connection in a log download, a folder the picker could not read. It is
            // written to the crash log and shown in the IDE, which goes on; only the runtime's own failures still end
            // it, after the session with the unsaved text is saved.
            var crashLog = _crashLog = Services.GetRequiredService<CrashLog>();
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception exception)
                {
                    crashLog.Write(exception, "Background thread (the app ended)");
                }

                SaveSessionBeforeCrash();
            };
            _ = Main.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnUnhandledException(object? sender, Avalonia.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var exception = e.Exception;
        if (!CrashLog.IsRecoverable(exception))
        {
            _crashLog?.Write(exception, "UI thread (the app ended)");
            SaveSessionBeforeCrash();
            return;
        }

        e.Handled = true;

        // After every handler had its turn: the preview's own handler takes the errors of the user's app.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (CrashLog.Claimed.TryGetValue(exception, out _))
            {
                return;
            }

            var report = _crashLog?.Write(exception, "UI thread");
            Main?.ReportUnexpectedError(exception, report);
        });
    }

    private void SaveSessionBeforeCrash()
    {
        try
        {
            Main?.SaveSession();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // The crash's own error is what gets reported.
        }
    }
}

/// <summary>Extensions for configuring TabStudio from a platform head.</summary>
public static class TabStudioAppBuilderExtensions
{
    /// <summary>Registers platform services before the app starts.</summary>
    /// <param name="builder">The app builder.</param>
    /// <param name="configure">Registers services, replacing defaults such as <see cref="Platform.IDeviceStatus"/>.</param>
    public static AppBuilder UseTabStudioPlatform(this AppBuilder builder, Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.AfterSetup(b => ((TabStudioApplication)b.Instance!).ConfigurePlatform(configure));
    }
}
