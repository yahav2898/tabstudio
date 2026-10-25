using TabStudio.Link;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.Swarm.Coordinator;

/// <summary>Who a coordinator is, where it listens and whom it takes into the swarm.</summary>
public sealed record SwarmCoordinatorOptions
{
    /// <summary>The coordinator's identity, presented in TLS: the same as its TabStudio Link identity.</summary>
    public required LinkIdentity Identity { get; init; }

    /// <summary>The devices paired with the swarm. Pairing adds to it; removing a device forgets it.</summary>
    public required LinkTrustStore Devices { get; init; }

    /// <summary>The coordinator's name, as the devices' overlays show it, such as <c>Yahav's tablet</c>.</summary>
    public required string Name { get; init; }

    /// <summary>What the coordinator is, one of <see cref="LinkDeviceKinds"/>.</summary>
    public string Kind { get; init; } = LinkDeviceKinds.Tablet;

    /// <summary>The TCP port to listen on when it is free; 0 for any port.</summary>
    public int Port { get; init; } = SwarmProtocol.DefaultPort;

    /// <summary>Listens on the loopback address only, for tests and the self test.</summary>
    public bool LoopbackOnly { get; init; }

    /// <summary>How many devices the swarm takes at once.</summary>
    public int MaxDevices { get; init; } = SwarmProtocol.MaxDevices;

    /// <summary>How often the coordinator measures each device's latency.</summary>
    public TimeSpan PingInterval { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>How long one device may take to receive and start a build before it counts as failed.</summary>
    public TimeSpan DeployTimeout { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>The TabStudio version the coordinator announces and requires of devices; tests change it.</summary>
    public string AppVersion { get; init; } = LinkProtocol.AppVersion;
}
