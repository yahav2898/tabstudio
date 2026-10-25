using System.Text.Json.Serialization;

namespace TabStudio.Link.Networking;

/// <summary>The methods of the handshake that follows the TLS handshake.</summary>
internal static class LinkHandshakeMethods
{
    /// <summary>Host to client, first: who the host is, and its nonce.</summary>
    public const string Welcome = "link/welcome";

    /// <summary>Client to host: who the client is, and its pairing proof when it pairs. Answered with <see cref="LinkAccepted"/>.</summary>
    public const string Hello = "link/hello";
}

/// <summary>The host's first message.</summary>
/// <param name="Protocol">The host's <see cref="LinkProtocol.Version"/>.</param>
/// <param name="Version">The host's TabStudio version.</param>
/// <param name="Name">The host's name.</param>
/// <param name="Kind">What the host is: <c>phone</c>, <c>tablet</c> or <c>computer</c>.</param>
/// <param name="Nonce">The host's random nonce, base64url.</param>
/// <param name="Role">What the host offers: builds, or a swarm to join.</param>
internal sealed record LinkWelcome(int Protocol, string Version, string Name, string Kind, string Nonce, LinkRole Role);

/// <summary>The client's request to be served.</summary>
/// <param name="Protocol">The client's <see cref="LinkProtocol.Version"/>.</param>
/// <param name="Version">The client's TabStudio version.</param>
/// <param name="Name">The client's name.</param>
/// <param name="Kind">What the client is: <c>phone</c>, <c>tablet</c> or <c>computer</c>.</param>
/// <param name="Nonce">The client's random nonce, base64url.</param>
/// <param name="PairingProof">When pairing: the client's proof that it knows the pairing code, base64url.</param>
/// <param name="Role">What the client expects the host to offer.</param>
internal sealed record LinkHello(int Protocol, string Version, string Name, string Kind, string Nonce, string? PairingProof, LinkRole Role);

/// <summary>The host's acceptance.</summary>
/// <param name="HostProof">When pairing: the host's proof that it knows the pairing code, base64url.</param>
internal sealed record LinkAccepted(string? HostProof);

/// <summary>Source-generated JSON for the handshake.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(LinkWelcome))]
[JsonSerializable(typeof(LinkHello))]
[JsonSerializable(typeof(LinkAccepted))]
internal sealed partial class LinkHandshakeJsonContext : JsonSerializerContext
{
}
