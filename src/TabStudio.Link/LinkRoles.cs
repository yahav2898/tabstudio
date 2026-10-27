namespace TabStudio.Link;

/// <summary>What TabStudio Link says when a device reaches a host of the other role.</summary>
internal static class LinkRoles
{
    /// <summary>Why a device was refused by a host of another role, in words for the user.</summary>
    /// <param name="host">The host's name.</param>
    /// <param name="hostRole">What the host offers.</param>
    public static string Mismatch(string host, LinkRole hostRole) => hostRole switch
    {
        LinkRole.Swarm => $"{host} coordinates a TabStudio Swarm and builds nothing for other devices. To join it, choose File > TabStudio Link > Join a swarm and scan the code its Swarm window shows.",
        LinkRole.Live => $"{host} shares a TabStudio Live session. To join it, choose File > Join Live Session and scan the code its Share Session window shows.",
        _ => $"{host} is a TabStudio Link host, not a swarm. To join a swarm, scan the code the tablet's Swarm window shows when you tap Add a device.",
    };
}
