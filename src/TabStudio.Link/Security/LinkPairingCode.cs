using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace TabStudio.Link.Security;

/// <summary>
/// The one-time code a host shows under its QR code, such as <c>7K3Q-M9XA</c>: eight characters of Crockford's
/// base32, 40 random bits. The tablet proves it knows the code to pair, and the host proves it back.
/// </summary>
/// <remarks>
/// The code never travels over the network: each side sends an HMAC of the connection's transcript keyed by
/// PBKDF2 of the code (see <see cref="LinkProofs"/>). A wrong guess makes the host draw a new code, so guessing
/// online gets one try per code, and the key stretching makes guessing offline slower than the code's lifetime.
/// </remarks>
public sealed record LinkPairingCode
{
    /// <summary>The number of characters.</summary>
    public const int Length = 8;

    /// <summary>Crockford's base32: digits and letters without I, L, O and U, which people misread.</summary>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private LinkPairingCode(string value)
    {
        Value = value;
    }

    /// <summary>The eight characters, upper case, without the dash.</summary>
    public string Value { get; }

    /// <summary>The code as it is shown: two groups of four, such as <c>7K3Q-M9XA</c>.</summary>
    public string DisplayText => $"{Value[..4]}-{Value[4..]}";

    /// <summary>Draws a new random code.</summary>
    public static LinkPairingCode Create() => new(RandomNumberGenerator.GetString(Alphabet, Length));

    /// <summary>
    /// Reads a code as people type it: in any case, with or without the dash and spaces, with O for 0 and I or L
    /// for 1.
    /// </summary>
    /// <param name="text">What was typed or scanned.</param>
    /// <param name="code">The code, when the text is one.</param>
    public static bool TryParse(string? text, [NotNullWhen(true)] out LinkPairingCode? code)
    {
        code = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = new StringBuilder(Length);
        foreach (var typed in text)
        {
            if (typed is '-' or ' ' or ' ')
            {
                continue;
            }

            var c = char.ToUpperInvariant(typed) switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                var other => other,
            };
            if (!Alphabet.Contains(c, StringComparison.Ordinal) || normalized.Length == Length)
            {
                return false;
            }

            normalized.Append(c);
        }

        if (normalized.Length != Length)
        {
            return false;
        }

        code = new LinkPairingCode(normalized.ToString());
        return true;
    }

    /// <summary>Whether two codes are the same, compared in constant time.</summary>
    /// <param name="other">The other code.</param>
    public bool Equals(LinkPairingCode? other) =>
        other is not null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Value), Encoding.ASCII.GetBytes(other.Value));

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => DisplayText;
}
