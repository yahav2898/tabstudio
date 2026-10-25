using TabStudio.Link.Security;

namespace TabStudio.Link.Networking;

/// <summary>Where a tablet connects to, and how it recognizes the host there.</summary>
/// <param name="Name">The host's name as the tablet knows it, for messages; <see langword="null"/> when not known yet.</param>
/// <param name="Addresses">Addresses to try in order, each optionally with a port.</param>
/// <param name="Port">The port for addresses without one.</param>
/// <param name="Fingerprint">The host's pinned fingerprint, from pairing or the QR code; <see langword="null"/> when pairing by typed code.</param>
/// <param name="Code">The pairing code, when pairing; <see langword="null"/> to connect to a paired host.</param>
public sealed record LinkTarget(string? Name, IReadOnlyList<string> Addresses, int Port, LinkFingerprint? Fingerprint, LinkPairingCode? Code)
{
    /// <summary>A host this tablet is paired with.</summary>
    /// <param name="host">The paired host.</param>
    /// <param name="addresses">Addresses to try first, such as where discovery just found the host.</param>
    public static LinkTarget Paired(LinkPairedDevice host, IEnumerable<string>? addresses = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        var all = (addresses ?? []).Concat(host.Addresses).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new LinkTarget(host.Name, all, host.Port > 0 ? host.Port : LinkProtocol.DefaultPort, host.Fingerprint, null);
    }

    /// <summary>A host whose QR code was scanned: its fingerprint is pinned, and the code pairs.</summary>
    /// <param name="invitation">The scanned invitation.</param>
    public static LinkTarget FromInvitation(LinkInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        return new LinkTarget(invitation.DeviceName, invitation.Addresses, invitation.Port, invitation.Fingerprint, invitation.Code);
    }

    /// <summary>A host whose address and code were typed: it must prove it knows the code.</summary>
    /// <param name="endpoint">The typed address.</param>
    /// <param name="code">The typed code.</param>
    public static LinkTarget FromCode(LinkEndpoint endpoint, LinkPairingCode code)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(code);
        return new LinkTarget(null, [endpoint.ToString()], endpoint.Port, null, code);
    }
}
