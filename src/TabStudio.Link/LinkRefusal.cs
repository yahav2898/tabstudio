namespace TabStudio.Link;

/// <summary>Why a host refused a connection.</summary>
public enum LinkRefusal
{
    /// <summary>The host does not know this device, and no pairing code was given.</summary>
    NotPaired,

    /// <summary>The pairing code was wrong or has expired.</summary>
    WrongCode,

    /// <summary>The devices run different versions of TabStudio.</summary>
    VersionMismatch,

    /// <summary>The host is already linked to another device.</summary>
    Busy,

    /// <summary>The host's certificate is not the one paired or scanned: it may be another device.</summary>
    WrongHost,

    /// <summary>The host could not prove that it knows the pairing code: it may be another device.</summary>
    HostNotVerified,

    /// <summary>A device tried to link to itself.</summary>
    Self,
}
