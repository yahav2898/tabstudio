using TabStudio.Link;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.Swarm.Device;

/// <summary>Who a device is when it joins a swarm, and where it keeps what it receives.</summary>
public sealed record SwarmMemberOptions
{
    /// <summary>The device's identity, presented in TLS: the same as its TabStudio Link identity.</summary>
    public required LinkIdentity Identity { get; init; }

    /// <summary>The coordinators this device paired with. Joining adds to it.</summary>
    public required LinkTrustStore Coordinators { get; init; }

    /// <summary>The device's name, as the tablet shows it, such as <c>Pixel 9</c>.</summary>
    public required string Name { get; init; }

    /// <summary>What the device is, one of <see cref="LinkDeviceKinds"/>.</summary>
    public string Kind { get; init; } = LinkDeviceKinds.Phone;

    /// <summary>Where the device keeps the files of the builds it received, by hash.</summary>
    public required string CacheDirectory { get; init; }

    /// <summary>How many bytes of received files the device keeps; the least recently used go first.</summary>
    public long CacheLimit { get; init; } = 512L * 1024 * 1024;

    /// <summary>How often the device pings the coordinator.</summary>
    public TimeSpan PingInterval { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>The pauses between reconnection attempts; the last one repeats.</summary>
    public IReadOnlyList<TimeSpan> ReconnectDelays { get; init; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(8)];

    /// <summary>The TabStudio version the device announces; tests change it.</summary>
    public string AppVersion { get; init; } = LinkProtocol.AppVersion;
}
