namespace TabStudio.Link.Security;

/// <summary>Base64url without padding (RFC 4648 §5), the form fingerprints and nonces take in URIs and JSON.</summary>
internal static class Base64Url
{
    /// <summary>Encodes bytes.</summary>
    public static string Encode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Decodes text, or returns <see langword="null"/> when it is not base64url.</summary>
    public static byte[]? TryDecode(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length % 4 == 1)
        {
            return null;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')
            {
                return null;
            }
        }

        var base64 = text.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - (base64.Length % 4)) % 4);
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
