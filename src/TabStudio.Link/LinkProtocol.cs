using System.Reflection;

namespace TabStudio.Link;

/// <summary>The versions and well-known numbers of the TabStudio Link protocol.</summary>
public static class LinkProtocol
{
    /// <summary>
    /// The protocol version. Both devices must speak the same one; it changes whenever a message changes shape.
    /// </summary>
    public const int Version = 1;

    /// <summary>The TCP port a host listens on when it is free; otherwise the host takes any port and says which.</summary>
    public const int DefaultPort = 47811;

    /// <summary>The UDP port hosts answer discovery requests on.</summary>
    public const int DiscoveryPort = 47812;

    /// <summary>
    /// The TabStudio version of this build, such as <c>0.3.0</c>. A tablet runs what its host compiled, including the
    /// debugger's instrumentation, so both devices must run the same version.
    /// </summary>
    public static string AppVersion { get; } = typeof(LinkProtocol).Assembly.GetName().Version is { } version
        ? $"{version.Major}.{version.Minor}.{version.Build}"
        : "0.0.0";

    /// <summary>The informational version of this build, for display.</summary>
    public static string AppDisplayVersion { get; } =
        typeof(LinkProtocol).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? AppVersion;
}
