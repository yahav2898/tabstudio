namespace TabStudio.Link.Networking;

/// <summary>What a device is, as the handshake tells it, for wording such as "Yahav's tablet".</summary>
public static class LinkDeviceKinds
{
    /// <summary>A phone.</summary>
    public const string Phone = "phone";

    /// <summary>A tablet.</summary>
    public const string Tablet = "tablet";

    /// <summary>A desktop or laptop computer.</summary>
    public const string Computer = "computer";
}
