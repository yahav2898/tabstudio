using TabStudio.Link;
using TabStudio.Link.Security;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>How a <see cref="BridgeServer"/> listens, who it is, whom it serves and where it keeps its files.</summary>
public sealed record BridgeServerOptions
{
    /// <summary>The PC's TabStudio Link identity, presented in TLS; the same one its Link host uses.</summary>
    public required LinkIdentity Identity { get; init; }

    /// <summary>The tablets paired with the bridge. Pairing adds to it.</summary>
    public required LinkTrustStore Tablets { get; init; }

    /// <summary>The PC's name, as tablets show it, such as <c>DESKTOP-7Q2M</c>.</summary>
    public required string PcName { get; init; }

    /// <summary>The bridge's folder: the tablets' workspaces under <c>Tablets</c>, its tools, such as netcoredbg, under <c>Tools</c>.</summary>
    public required string DataFolder { get; init; }

    /// <summary>The TCP port to listen on when it is free; 0 for any port.</summary>
    public int Port { get; init; } = BridgeProtocol.DefaultPort;

    /// <summary>Listens on the loopback address only, for tests.</summary>
    public bool LoopbackOnly { get; init; }

    /// <summary>Keeps the PC from sleeping by itself while a tablet is connected.</summary>
    public bool KeepAwakeWhileConnected { get; init; } = true;

    /// <summary>Where to look for build tools; the PC's own folders when <see langword="null"/>.</summary>
    public BridgeToolchainLocations? Locations { get; init; }

    /// <summary>The TabStudio version the bridge announces and requires; tests change it.</summary>
    public string AppVersion { get; init; } = LinkProtocol.AppVersion;

    /// <summary>The bridge's tools folder.</summary>
    public string ToolsFolder => Path.Combine(DataFolder, "Tools");
}
