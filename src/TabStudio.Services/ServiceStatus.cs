using TabStudio.Services.Compose;
using TabStudio.Services.Native;

namespace TabStudio.Services;

public enum ServiceState { Stopped, Installing, Starting, Healthy, Running, Completed, Unhealthy, Stopping, Failed, Unsupported }
public sealed record ServiceStatus(ComposeService Definition, ServiceState State, ServiceMapping? Mapping, IReadOnlyList<ServicePort> Ports, string? Detail = null)
{
    public string Name => Definition.Name;
    public bool IsRunning => State is ServiceState.Starting or ServiceState.Healthy or ServiceState.Running or ServiceState.Unhealthy;
}
public sealed record ServiceLogLine(string Service, DateTimeOffset Time, string Text);

/// <summary>Installs verified Alpine packages through the device's existing package manager.</summary>
public interface IServicePackages
{
    Task<MappingResult> EnsureAsync(ComposeService service, Action<string> log, CancellationToken cancellationToken);
}
