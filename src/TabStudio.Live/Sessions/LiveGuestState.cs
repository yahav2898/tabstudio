namespace TabStudio.Live.Sessions;

/// <summary>Where a guest stands with a session.</summary>
public enum LiveGuestState
{
    /// <summary>Not in a session.</summary>
    Off,

    /// <summary>Pairing with the host, and fetching the shared files.</summary>
    Joining,

    /// <summary>In the session.</summary>
    Joined,

    /// <summary>The connection was lost; the guest reconnects until it leaves, and its edits wait.</summary>
    Reconnecting,

    /// <summary>The session ended: the host ended it, removed this guest, or the guest left.</summary>
    Ended,
}
