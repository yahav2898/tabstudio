using TabStudio.Services.Compose;

namespace TabStudio.Services;

/// <summary>Optional OCI backend. Native services remain available when an image cannot run.</summary>
public interface IServiceImages
{
    /// <summary>Whether the definition can be attempted as an image.</summary>
    bool CanRun(ComposeService service);

    /// <summary>Starts and waits for an image, reporting logs and subsequent state changes.</summary>
    Task<IServiceImageProcess> StartAsync(ComposeProject project, ComposeService service,
        IReadOnlyList<ServicePort> ports, Action<string> log, Action<ServiceState, string?> changed,
        CancellationToken cancellationToken);
}

/// <summary>A workspace-owned image process. Stopping it keeps its writable files and volumes.</summary>
public interface IServiceImageProcess
{
    /// <summary>Whether its main process is running.</summary>
    bool IsRunning { get; }
    /// <summary>The final device endpoints.</summary>
    IReadOnlyList<ServicePort> Ports { get; }
    /// <summary>Stops the process and detaches its observers.</summary>
    Task StopAsync(CancellationToken cancellationToken);
}
