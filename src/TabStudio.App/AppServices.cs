using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.App.Toolbox;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.DesktopMode;

namespace TabStudio.App;

/// <summary>
/// The composition root: every service and view model TabStudio creates, wired in one place.
/// </summary>
public static class AppServices
{
    /// <summary>Builds the service provider.</summary>
    /// <param name="paths">Where the app keeps its data.</param>
    /// <param name="configurePlatform">Registers platform services, such as Android's <see cref="IDeviceStatus"/>.</param>
    public static ServiceProvider Create(AppPaths paths, Action<IServiceCollection>? configurePlatform = null)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var services = new ServiceCollection();

        // Platform: defaults first; the platform head replaces what it implements.
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<IDeviceStatus, DefaultDeviceStatus>();
        services.AddSingleton<IStoragePicker, TopLevelStoragePicker>();
        services.AddSingleton<IWebViewFactory, NoWebViewFactory>();
        services.AddSingleton<INotifications, NoSystemNotifications>();
        services.AddSingleton<IAppActivity, AvaloniaAppActivity>();
        services.AddSingleton<IQrScanner, NoQrScanner>();
        services.AddSingleton<IDeviceIdentity, DefaultDeviceIdentity>();
        services.AddSingleton<IExternalDisplayService, NoExternalDisplayService>();
        configurePlatform?.Invoke(services);
        services.AddSingleton(provider => DeviceProfile.For(provider.GetRequiredService<IDeviceStatus>().Device));

        // App services.
        services.AddSingleton(paths);
        services.AddSingleton<CompilerServices>();
        services.AddSingleton<RecentWorkspaces>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton<SampleLibrary>();
        services.AddSingleton<ProjectTemplates>();
        services.AddSingleton<FolderImporter>();
        services.AddSingleton(_ =>
        {
            var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("TabStudio/0.1 (+https://github.com/yahav2898/tabstudio)");
            return http;
        });
        // Tests register a GitHubAccount of their own first, talking to a fake GitHub.
        services.TryAddSingleton<GitHubAccount>();
        services.AddSingleton<GitHubAccountViewModel>();
        services.AddSingleton<GitHubImporter>();
        services.AddSingleton<LinkService>();
        services.AddSingleton(_ => ToolboxCatalog.LoadEmbedded());
        services.AddSingleton<DesktopModeSettingsStore>();
        services.AddSingleton<DesktopModeViewModel>();

        // Every screen the app being built runs on: the preview, virtual devices and the swarm's phones.
        services.AddSingleton<Swarm.AppScreenRegistry>();
        Swarm.SwarmServiceRegistration.AddSwarm(services);

        // Language servers and debug adapters start through the platform's IServerProcessLauncher, if it registers one.
        services.TryAddSingleton(provider => new Services.LanguageServers.LanguageServerServices(provider.GetService<TabStudio.LanguageServers.Hosting.IServerProcessLauncher>() ?? TabStudio.LanguageServers.Hosting.ProcessServerLauncher.Instance));

        // View models.
        services.AddSingleton<LinkViewModel>();
        services.AddSingleton<StartViewModel>();
        // Workspaces are created and disposed by MainViewModel. They are built with ActivatorUtilities rather
        // than registered as transient, because the container would keep every disposable transient alive.
        services.AddSingleton<Func<WorkspaceViewModel>>(provider => () => ActivatorUtilities.CreateInstance<WorkspaceViewModel>(provider));
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
