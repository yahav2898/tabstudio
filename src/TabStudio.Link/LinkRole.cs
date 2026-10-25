namespace TabStudio.Link;

/// <summary>What a device listening for TabStudio Link connections offers the devices that connect to it.</summary>
public enum LinkRole
{
    /// <summary>A build and language server: a phone or PC that compiles for the tablet that links to it.</summary>
    Host,

    /// <summary>A tablet coordinating a swarm: the phones and tablets that join it run the app it is building.</summary>
    Swarm,
}
