using TabStudio.Link.Security;

namespace TabStudio.Link.Networking;

/// <summary>A host that answered a discovery request on the local network.</summary>
/// <param name="Name">Its name, as it announced it.</param>
/// <param name="Fingerprint">The fingerprint it announced. Unverified until TLS checks it.</param>
/// <param name="Endpoint">Where it answered from, with the port it listens on.</param>
public sealed record LinkDiscoveredHost(string Name, LinkFingerprint Fingerprint, LinkEndpoint Endpoint);
