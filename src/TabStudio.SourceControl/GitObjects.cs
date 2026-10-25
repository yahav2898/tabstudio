using System.Security.Cryptography;
using System.Text;

namespace TabStudio.SourceControl;

/// <summary>Git object hashing, so local files can be compared with GitHub's trees without downloading them.</summary>
public static class GitObjects
{
    /// <summary>The mode of a regular file in a Git tree.</summary>
    public const string RegularFileMode = "100644";

    /// <summary>The mode of an executable file in a Git tree.</summary>
    public const string ExecutableFileMode = "100755";

    /// <summary>The SHA-1 Git gives a file's content: the hash of <c>"blob {length}\0"</c> followed by the bytes.</summary>
    /// <param name="content">The file's bytes.</param>
    /// <returns>The lowercase hexadecimal hash.</returns>
    public static string BlobHash(ReadOnlySpan<byte> content)
    {
        var header = Encoding.ASCII.GetBytes($"blob {content.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)}\0");
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        sha.AppendData(header);
        sha.AppendData(content);
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }
}
