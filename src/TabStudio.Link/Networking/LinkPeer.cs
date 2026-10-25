using TabStudio.Link.Security;

namespace TabStudio.Link.Networking;

/// <summary>The device at the other end of an authenticated connection.</summary>
/// <param name="Name">Its name, such as <c>Pixel 9</c>.</param>
/// <param name="Kind">What it is: <c>phone</c>, <c>tablet</c> or <c>computer</c>.</param>
/// <param name="Fingerprint">Its certificate fingerprint, verified by TLS.</param>
/// <param name="Address">Its network address, as this device sees it.</param>
/// <param name="Security">The negotiated TLS version, such as <c>TLS 1.3</c>.</param>
public sealed record LinkPeer(string Name, string Kind, LinkFingerprint Fingerprint, string Address, string Security);
