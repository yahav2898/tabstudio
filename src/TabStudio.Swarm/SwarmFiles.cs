using System.Security.Cryptography;

namespace TabStudio.Swarm;

/// <summary>How the swarm names files: by the SHA-256 of their bytes, so a device keeps each one once.</summary>
internal static class SwarmFiles
{
    /// <summary>The hash of some bytes, as lowercase hexadecimal.</summary>
    /// <param name="bytes">The bytes.</param>
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Whether a text is a hash this class made, so it can be used as a file name.</summary>
    /// <param name="text">The text.</param>
    public static bool IsHash(string? text) =>
        text is { Length: 64 } && text.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
