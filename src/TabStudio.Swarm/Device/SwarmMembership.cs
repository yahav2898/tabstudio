namespace TabStudio.Swarm.Device;

/// <summary>Where a device stands with a swarm.</summary>
public enum SwarmMembership
{
    /// <summary>Not in a swarm.</summary>
    Off,

    /// <summary>Pairing with, or connecting to, a coordinator.</summary>
    Joining,

    /// <summary>In a swarm: the coordinator can send it apps.</summary>
    Joined,

    /// <summary>The connection was lost; the device tries to reconnect until it leaves.</summary>
    Reconnecting,
}
