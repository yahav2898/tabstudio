using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels.Swarm;

namespace TabStudio.App.Swarm;

/// <summary>
/// The swarm's services, as one parameter for the workspace: the tablet's swarm, the screens of the app being built,
/// mirrored input, and where pictures of failed recorded tests go.
/// </summary>
/// <param name="coordinator">The tablet's swarm.</param>
/// <param name="registry">Every screen the app being built runs on.</param>
/// <param name="mirror">Mirrored input.</param>
/// <param name="paths">Where the app keeps its data.</param>
public sealed class SwarmServices(SwarmCoordinatorService coordinator, AppScreenRegistry registry, SwarmInputMirror mirror, AppPaths paths)
{
    /// <summary>The tablet's swarm.</summary>
    public SwarmCoordinatorService Coordinator { get; } = coordinator;

    /// <summary>Every screen the app being built runs on.</summary>
    public AppScreenRegistry Registry { get; } = registry;

    /// <summary>Mirrored input.</summary>
    public SwarmInputMirror Mirror { get; } = mirror;

    /// <summary>Where pictures of recorded tests that failed on a screen are kept.</summary>
    public string ScreenshotDirectory { get; } = Path.Combine(paths.Cache, "UiTests");
}

/// <summary>Registers the swarm: the tablet's side, the device's side and their view models.</summary>
public static class SwarmServiceRegistration
{
    /// <summary>Adds every swarm service. The <see cref="AppScreenRegistry"/> is registered by the app itself.</summary>
    /// <param name="services">The app's services.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddSwarm(this IServiceCollection services)
    {
        services.AddSingleton<SwarmSettingsStore>();
        services.AddSingleton<SwarmInputMirror>();
        services.AddSingleton<SwarmCoordinatorService>();
        services.AddSingleton<SwarmServices>();
        services.AddSingleton<SwarmDeviceHost>();
        services.AddSingleton<SwarmMemberService>();
        services.AddSingleton<SwarmJoinViewModel>();
        services.AddSingleton<SwarmDevicePageViewModel>();
        return services;
    }
}
