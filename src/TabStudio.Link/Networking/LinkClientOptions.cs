using TabStudio.Link.Security;

namespace TabStudio.Link.Networking;

/// <summary>Who a tablet is when it connects to a host, and how patient it is.</summary>
public sealed record LinkClientOptions
{
    /// <summary>The tablet's identity, presented in TLS.</summary>
    public required LinkIdentity Identity { get; init; }

    /// <summary>The tablet's name, as the host shows it, such as <c>Yahav's tablet</c>.</summary>
    public required string DeviceName { get; init; }

    /// <summary>What the tablet is, one of <see cref="LinkDeviceKinds"/>.</summary>
    public string DeviceKind { get; init; } = LinkDeviceKinds.Tablet;

    /// <summary>How long one address may take to accept the connection.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(4);

    /// <summary>How long TLS and the handshake may take.</summary>
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>How often the tablet pings the host, which also measures the latency.</summary>
    public TimeSpan PingInterval { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>How long a ping may go unanswered before the host is treated as gone.</summary>
    public TimeSpan PingTimeout { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>The TabStudio version the tablet announces; tests change it.</summary>
    public string AppVersion { get; init; } = LinkProtocol.AppVersion;
}
