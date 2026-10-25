namespace TabStudio.App.Services.Link;

/// <summary>Where this device's link to a host stands.</summary>
public enum LinkClientState
{
    /// <summary>Not linked: this device compiles on its own.</summary>
    Off,

    /// <summary>Connecting to a host, or pairing with it.</summary>
    Connecting,

    /// <summary>Linked: the host compiles for this device.</summary>
    Connected,

    /// <summary>The link was lost; this device compiles on its own and keeps trying to reconnect.</summary>
    Reconnecting,
}
