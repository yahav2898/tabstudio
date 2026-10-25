using TabStudio.Link.Security;

namespace TabStudio.Link.Networking;

/// <summary>How a <see cref="LinkHost"/> listens, who it is and whom it serves.</summary>
public sealed record LinkHostOptions
{
    /// <summary>The host's identity, presented in TLS.</summary>
    public required LinkIdentity Identity { get; init; }

    /// <summary>The tablets paired with this host. Pairing adds to it.</summary>
    public required LinkTrustStore Clients { get; init; }

    /// <summary>The host's name, as tablets show it, such as <c>Pixel 9</c>.</summary>
    public required string DeviceName { get; init; }

    /// <summary>What the host is, one of <see cref="LinkDeviceKinds"/>.</summary>
    public string DeviceKind { get; init; } = LinkDeviceKinds.Phone;

    /// <summary>The TCP port to listen on when it is free; 0 for any port.</summary>
    public int Port { get; init; } = LinkProtocol.DefaultPort;

    /// <summary>Listens on the loopback address only, for tests and the self test.</summary>
    public bool LoopbackOnly { get; init; }

    /// <summary>How long a device may take for TLS and the handshake.</summary>
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>How long a pairing code stays valid before the host draws a new one.</summary>
    public TimeSpan PairingCodeLifetime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>How long a connected tablet may stay silent before the host treats it as gone. Tablets ping every few seconds.</summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>The TabStudio version the host announces and requires; tests change it.</summary>
    public string AppVersion { get; init; } = LinkProtocol.AppVersion;
}
