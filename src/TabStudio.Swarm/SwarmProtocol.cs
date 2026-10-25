namespace TabStudio.Swarm;

/// <summary>The well-known numbers of TabStudio Swarm, which runs on TabStudio Link.</summary>
public static class SwarmProtocol
{
    /// <summary>The TCP port a coordinator listens on when it is free; otherwise it takes any port and its QR code says which.</summary>
    public const int DefaultPort = 47813;

    /// <summary>How many devices a coordinator takes at once.</summary>
    public const int MaxDevices = 16;

    /// <summary>The query parameter that carries a web app's per-session secret on a device's first request.</summary>
    public const string WebSecretParameter = "tabstudio-swarm";

    /// <summary>The cookie that carries a web app's per-session secret on a device's later requests.</summary>
    public const string WebSecretCookie = "tabstudio-swarm";
}
