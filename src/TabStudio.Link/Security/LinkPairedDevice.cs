namespace TabStudio.Link.Security;

/// <summary>A device this one is paired with: a tablet a host serves, or a host a tablet links to.</summary>
/// <param name="Name">The device's name, such as <c>Pixel 9</c> or <c>Yahav's tablet</c>.</param>
/// <param name="Kind">What the device is, such as <c>phone</c>, <c>tablet</c> or <c>computer</c>.</param>
/// <param name="Fingerprint">The device's certificate fingerprint, which identifies it.</param>
/// <param name="PairedAt">When the devices paired.</param>
/// <param name="LastSeen">When the devices were last connected.</param>
/// <param name="Addresses">For a host: its addresses on the local network when it was last seen, the most likely first.</param>
/// <param name="Port">For a host: the TCP port it listened on.</param>
public sealed record LinkPairedDevice(
    string Name,
    string Kind,
    LinkFingerprint Fingerprint,
    DateTimeOffset PairedAt,
    DateTimeOffset LastSeen,
    IReadOnlyList<string> Addresses,
    int Port);
