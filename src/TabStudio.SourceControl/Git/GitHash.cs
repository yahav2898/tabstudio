using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TabStudio.SourceControl.Git;

/// <summary>Git's SHA-1 object names, in hexadecimal and as their 20 bytes.</summary>
internal static class GitHash
{
    /// <summary>The length of a hash in bytes.</summary>
    public const int Size = 20;

    /// <summary>The all-zero hash Git uses for "no object", as when a push creates or deletes a branch.</summary>
    public static readonly string Zero = new('0', 40);

    /// <summary>Whether text is a full hash in hexadecimal.</summary>
    public static bool IsHash(string? text) => text is { Length: 40 } && text.All(char.IsAsciiHexDigit);

    /// <summary>The lowercase hexadecimal form of a hash.</summary>
    public static string ToHex(ReadOnlySpan<byte> hash) => Convert.ToHexStringLower(hash[..Size]);

    /// <summary>The bytes of a hash given in hexadecimal.</summary>
    public static byte[] FromHex(string hex)
    {
        if (!IsHash(hex))
        {
            throw new FormatException($"'{hex}' is not a Git object name.");
        }

        return Convert.FromHexString(hex);
    }

    /// <summary>The name Git gives an object: the SHA-1 of <c>"{kind} {length}\0"</c> and its content.</summary>
    public static string Of(GitObjectKind kind, ReadOnlySpan<byte> content)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        sha.AppendData(Header(kind, content.Length));
        sha.AppendData(content);
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    /// <summary>The header Git hashes and stores in front of an object's content.</summary>
    public static byte[] Header(GitObjectKind kind, long length) =>
        Encoding.ASCII.GetBytes($"{Name(kind)} {length.ToString(CultureInfo.InvariantCulture)}\0");

    /// <summary>The word Git uses for a kind of object.</summary>
    public static string Name(GitObjectKind kind) => kind switch
    {
        GitObjectKind.Commit => "commit",
        GitObjectKind.Tree => "tree",
        GitObjectKind.Blob => "blob",
        GitObjectKind.Tag => "tag",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The kind of object a word names; <see langword="null"/> for an unknown word.</summary>
    public static GitObjectKind? Parse(string name) => name switch
    {
        "commit" => GitObjectKind.Commit,
        "tree" => GitObjectKind.Tree,
        "blob" => GitObjectKind.Blob,
        "tag" => GitObjectKind.Tag,
        _ => null,
    };
}
