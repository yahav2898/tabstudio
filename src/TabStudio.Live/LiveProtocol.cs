namespace TabStudio.Live;

/// <summary>The well-known numbers of TabStudio Live, which runs on TabStudio Link.</summary>
public static class LiveProtocol
{
    /// <summary>The TCP port a host listens on when it is free; otherwise it takes any port and its QR code says which.</summary>
    public const int DefaultPort = 47814;

    /// <summary>How many guests a session takes at once: a class of thirty and their teacher's second device.</summary>
    public const int MaxGuests = 32;

    /// <summary>The largest file a session shares; no source file is this large.</summary>
    public const long MaxFileLength = 8L * 1024 * 1024;

    /// <summary>The most files a session shares.</summary>
    public const int MaxFiles = 10_000;

    /// <summary>The most bytes a session shares.</summary>
    public const long MaxTotalLength = 256L * 1024 * 1024;

    /// <summary>How many characters of the host's Output a guest that joins late sees.</summary>
    public const int OutputTailLength = 64 * 1024;

    /// <summary>How many bytes of the host's Terminal a guest that joins late sees.</summary>
    public const int TerminalTailLength = 64 * 1024;

    /// <summary>The longest name a participant is shown with.</summary>
    public const int MaxNameLength = 40;

    /// <summary>How many participant colors there are; the host has the first.</summary>
    public const int Colors = 8;
}
