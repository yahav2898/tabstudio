using System.Security.Cryptography;
using System.Text;

namespace TabStudio.Link.Security;

/// <summary>
/// The pairing proofs: each side shows that it knows the pairing code without sending it.
/// </summary>
/// <remarks>
/// <para>
/// The transcript is the SHA-256 of both certificates' fingerprints, as each side saw them in the TLS handshake, and
/// both sides' random nonces. The key is PBKDF2-SHA256 of the code, salted with the host's nonce. The client sends
/// HMAC-SHA256(key, "client" ‖ transcript); the host answers with HMAC-SHA256(key, "host" ‖ transcript).
/// </para>
/// <para>
/// Binding the fingerprints means a device in the middle, which must present its own certificate, cannot pass a proof
/// on to the real host: the transcripts differ. The fresh nonces make every proof single-use.
/// </para>
/// </remarks>
internal static class LinkProofs
{
    /// <summary>The length of a nonce in bytes.</summary>
    public const int NonceLength = 32;

    /// <summary>PBKDF2 iterations: a tenth of a second on a phone, and years to try every code offline.</summary>
    public const int KeyIterations = 100_000;

    private static readonly byte[] KeySalt = "TabStudio Link pairing key v1"u8.ToArray();
    private static readonly byte[] TranscriptLabel = "TabStudio Link transcript v1"u8.ToArray();
    private static readonly byte[] ClientLabel = "client"u8.ToArray();
    private static readonly byte[] HostLabel = "host"u8.ToArray();

    /// <summary>A fresh random nonce.</summary>
    public static byte[] CreateNonce() => RandomNumberGenerator.GetBytes(NonceLength);

    /// <summary>The hash both proofs cover.</summary>
    public static byte[] Transcript(LinkFingerprint host, LinkFingerprint client, ReadOnlySpan<byte> hostNonce, ReadOnlySpan<byte> clientNonce)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(TranscriptLabel);
        hash.AppendData(host.ToBytes());
        hash.AppendData(client.ToBytes());
        hash.AppendData(hostNonce);
        hash.AppendData(clientNonce);
        return hash.GetHashAndReset();
    }

    /// <summary>The proof key for a pairing code and a host nonce.</summary>
    public static byte[] DeriveKey(LinkPairingCode code, ReadOnlySpan<byte> hostNonce)
    {
        var salt = new byte[KeySalt.Length + hostNonce.Length];
        KeySalt.CopyTo(salt, 0);
        hostNonce.CopyTo(salt.AsSpan(KeySalt.Length));
        return Rfc2898DeriveBytes.Pbkdf2(Encoding.ASCII.GetBytes(code.Value), salt, KeyIterations, HashAlgorithmName.SHA256, 32);
    }

    /// <summary>What the client sends to prove it knows the code.</summary>
    public static byte[] ClientProof(byte[] key, byte[] transcript) => Mac(key, ClientLabel, transcript);

    /// <summary>What the host answers to prove it knows the code too.</summary>
    public static byte[] HostProof(byte[] key, byte[] transcript) => Mac(key, HostLabel, transcript);

    /// <summary>Compares a received proof with the expected one in constant time.</summary>
    public static bool Matches(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> received) =>
        expected.Length == received.Length && CryptographicOperations.FixedTimeEquals(expected, received);

    private static byte[] Mac(byte[] key, byte[] label, byte[] transcript)
    {
        using var mac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);
        mac.AppendData(label);
        mac.AppendData(transcript);
        return mac.GetHashAndReset();
    }
}
