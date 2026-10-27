using TabStudio.Link;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.Live.Sessions;

/// <summary>Who a host is, what it shares and where it listens.</summary>
public sealed record LiveHostOptions
{
    /// <summary>The host's identity, presented in TLS: the same as its TabStudio Link identity.</summary>
    public required LinkIdentity Identity { get; init; }

    /// <summary>The host's name, as guests see it, such as <c>Yahav's tablet</c>.</summary>
    public required string Name { get; init; }

    /// <summary>What the host is, one of <see cref="LinkDeviceKinds"/>.</summary>
    public string Kind { get; init; } = LinkDeviceKinds.Tablet;

    /// <summary>The shared workspace's folder: guests see this folder and nothing outside it.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>The shared workspace's name.</summary>
    public required string WorkspaceName { get; init; }

    /// <summary>What guests open: the solution's path in the workspace, or <see langword="null"/> for the folder.</summary>
    public string? EntryPath { get; init; }

    /// <summary>The TCP port to listen on when it is free; 0 for any port.</summary>
    public int Port { get; init; } = LiveProtocol.DefaultPort;

    /// <summary>Listens on the loopback address only, for tests and the self test.</summary>
    public bool LoopbackOnly { get; init; }

    /// <summary>What a guest may do when it joins: edit (the default) or only watch.</summary>
    public LiveRole DefaultRole { get; init; } = LiveRole.Editor;

    /// <summary>Whether the session starts in classroom mode.</summary>
    public bool IsClassroom { get; init; }

    /// <summary>How many guests the session takes at once.</summary>
    public int MaxGuests { get; init; } = LiveProtocol.MaxGuests;

    /// <summary>How long the session's code stays the same before the host draws a new one; the QR code changes with it.</summary>
    public TimeSpan PairingCodeLifetime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>How long a guest whose connection dropped stays in the list, waiting to reconnect.</summary>
    public TimeSpan ReconnectGrace { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>The TabStudio version the host announces and requires of guests; tests change it.</summary>
    public string AppVersion { get; init; } = LinkProtocol.AppVersion;
}
