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
            _ = Main.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
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
