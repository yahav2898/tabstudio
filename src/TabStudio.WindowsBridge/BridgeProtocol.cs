using TabStudio.Link;

namespace TabStudio.WindowsBridge;

/// <summary>The well-known numbers of the Windows Bridge, a role beside TabStudio Link's host on the same protocol.</summary>
/// <remarks>
/// The bridge listens on its own port and answers discovery on its own port, so one PC can be a TabStudio Link host
/// and a Windows Bridge at once: the tablet pairs with each separately, and the same certificate identifies the PC.
/// </remarks>
public static class BridgeProtocol
{
    /// <summary>The TCP port a bridge listens on when it is free; otherwise it takes any port and says which.</summary>
    public const int DefaultPort = LinkProtocol.DefaultPort + 2;

    /// <summary>The UDP port bridges answer discovery requests on.</summary>
    public const int DiscoveryPort = LinkProtocol.DefaultPort + 3;

    /// <summary>The netcoredbg release the bridge installs for debugging .NET apps (MIT licensed, by Samsung).</summary>
    public const string NetcoredbgVersion = "3.2.0-1092";
}
