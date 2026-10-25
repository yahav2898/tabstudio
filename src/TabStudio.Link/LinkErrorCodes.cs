namespace TabStudio.Link;

/// <summary>The error codes a request can be answered with.</summary>
public static class LinkErrorCodes
{
    /// <summary>The handler failed; the message says why.</summary>
    public const string Failed = "failed";

    /// <summary>The request was canceled.</summary>
    public const string Canceled = "canceled";

    /// <summary>The method is not one the other device knows.</summary>
    public const string UnknownMethod = "unknown-method";

    /// <summary>The request's body is missing or malformed.</summary>
    public const string InvalidRequest = "invalid-request";

    /// <summary>The connection is not authenticated yet.</summary>
    public const string NotAuthenticated = "not-authenticated";

    /// <summary>The host does not know this device.</summary>
    public const string NotPaired = "not-paired";

    /// <summary>The pairing code was wrong or has expired.</summary>
    public const string WrongCode = "wrong-code";

    /// <summary>The devices run different versions of TabStudio.</summary>
    public const string VersionMismatch = "version-mismatch";

    /// <summary>The host is linked to another device.</summary>
    public const string Busy = "busy";

    /// <summary>A device tried to link to itself.</summary>
    public const string Self = "self";

    /// <summary>The request needs a workspace and none is loaded.</summary>
    public const string NoWorkspace = "no-workspace";
}
