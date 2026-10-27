using TabStudio.Link;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.Live.Sessions;

/// <summary>Who a guest is, and where it keeps its copy of the workspace.</summary>
public sealed record LiveGuestOptions
{
    /// <summary>The guest's identity, presented in TLS: the same as its TabStudio Link identity.</summary>
    public required LinkIdentity Identity { get; init; }

    /// <summary>The guest's name, as the others see it.</summary>
    public required string Name { get; init; }

    /// <summary>What the guest is, one of <see cref="LinkDeviceKinds"/>.</summary>
    public string Kind { get; init; } = LinkDeviceKinds.Tablet;

    /// <summary>The folder for copies of shared workspaces; each session gets a folder in it, deleted when it ends.</summary>
    public required string MirrorDirectory { get; init; }

    /// <summary>How often the guest pings the host.</summary>
    public TimeSpan PingInterval { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>The pauses between reconnection attempts; the last one repeats.</summary>
    public IReadOnlyList<TimeSpan> ReconnectDelays { get; init; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5)];

    /// <summary>The TabStudio version the guest announces; tests change it.</summary>
    public string AppVersion { get; init; } = LinkProtocol.AppVersion;
}
