using System.Numerics;
using System.Security.Cryptography;

namespace TabStudio.Extensions.Security;

/// <summary>
/// Verifies Ed25519 signatures (RFC 8032, "pure" Ed25519), the scheme Open VSX signs every package with. .NET's
/// base library has no Ed25519, and verifying is all TabStudio needs, so this follows the RFC's reference
/// algorithm with <see cref="BigInteger"/> arithmetic: slower than a dedicated library, but a package is
/// verified once, in a few milliseconds.
/// </summary>
/// <remarks>
/// The message is hashed as it is read, so a package of any size is verified without holding it in memory.
/// </remarks>
public static class Ed25519
{
    /// <summary>The length of a public key in bytes.</summary>
    public const int PublicKeyLength = 32;

    /// <summary>The length of a signature in bytes.</summary>
    public const int SignatureLength = 64;

    // The field prime p = 2^255 - 19, the group order q, and the curve constant d = -121665/121666.
    private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
    private static readonly BigInteger Q = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly BigInteger D = Mod(-121665 * Inverse(121666));
    private static readonly BigInteger SqrtMinusOne = BigInteger.ModPow(2, (P - 1) / 4, P);
    private static readonly Point BasePoint = CreateBasePoint();

    /// <summary>Verifies a signature of a message held in memory.</summary>
    /// <param name="publicKey">The 32-byte public key.</param>
    /// <param name="message">The signed message.</param>
    /// <param name="signature">The 64-byte signature.</param>
    /// <returns>Whether the signature is valid for the message and key.</returns>
    public static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        using var stream = new MemoryStream(message.ToArray(), writable: false);
        return Verify(publicKey, stream, signature);
    }

    /// <summary>Verifies a signature of a message read from a stream, such as a downloaded package.</summary>
    /// <param name="publicKey">The 32-byte public key.</param>
    /// <param name="message">The signed message, read to its end.</param>
    /// <param name="signature">The 64-byte signature.</param>
    /// <returns>Whether the signature is valid for the message and key.</returns>
    public static bool Verify(ReadOnlySpan<byte> publicKey, Stream message, ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (publicKey.Length != PublicKeyLength || signature.Length != SignatureLength)
        {
            return false;
        }

        if (Decompress(publicKey) is not { } a || Decompress(signature[..32]) is not { } r)
        {
            return false;
        }

        var s = new BigInteger(signature[32..], isUnsigned: true, isBigEndian: false);
        if (s >= Q)
        {
            return false;
        }

        // h = SHA-512(R || A || M) mod q.
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        sha.AppendData(signature[..32]);
        sha.AppendData(publicKey);
        var buffer = new byte[81920];
        int read;
        while ((read = message.Read(buffer, 0, buffer.Length)) > 0)
        {
            sha.AppendData(buffer, 0, read);
        }

        var h = new BigInteger(sha.GetHashAndReset(), isUnsigned: true, isBigEndian: false) % Q;

        // Valid when [s]B = R + [h]A.
        return Equal(Multiply(s, BasePoint), Add(r, Multiply(h, a)));
    }

    /// <summary>A point in extended coordinates: x = X/Z, y = Y/Z, x*y = T/Z.</summary>
    private readonly record struct Point(BigInteger X, BigInteger Y, BigInteger Z, BigInteger T);

    private static Point CreateBasePoint()
    {
        var y = Mod(4 * Inverse(5));
        var x = RecoverX(y, sign: false) ?? throw new InvalidOperationException("The Ed25519 base point is invalid.");
        return new Point(x, y, 1, Mod(x * y));
    }

    private static BigInteger Mod(BigInteger value)
    {
        var result = value % P;
        return result.Sign < 0 ? result + P : result;
    }

    private static BigInteger Inverse(BigInteger value) => BigInteger.ModPow(Mod(value), P - 2, P);

    private static Point Add(Point a, Point b)
    {
        var aa = Mod((a.Y - a.X) * (b.Y - b.X));
        var bb = Mod((a.Y + a.X) * (b.Y + b.X));
        var c = Mod(2 * a.T * b.T * D);
        var d = Mod(2 * a.Z * b.Z);
        var e = bb - aa;
        var f = d - c;
        var g = d + c;
        var h = bb + aa;
        return new Point(Mod(e * f), Mod(g * h), Mod(f * g), Mod(e * h));
    }

    private static Point Multiply(BigInteger scalar, Point point)
    {
        var result = new Point(0, 1, 1, 0);
        while (scalar.Sign > 0)
        {
            if (!scalar.IsEven)
            {
                result = Add(result, point);
            }

            point = Add(point, point);
            scalar >>= 1;
        }

        return result;
    }

    private static bool Equal(Point a, Point b) =>
        Mod((a.X * b.Z) - (b.X * a.Z)).IsZero && Mod((a.Y * b.Z) - (b.Y * a.Z)).IsZero;

    private static BigInteger? RecoverX(BigInteger y, bool sign)
    {
        if (y >= P)
        {
            return null;
        }

        var x2 = Mod(((y * y) - 1) * Inverse((D * y * y) + 1));
        if (x2.IsZero)
        {
            return sign ? null : BigInteger.Zero;
        }

        var x = BigInteger.ModPow(x2, (P + 3) / 8, P);
        if (!Mod((x * x) - x2).IsZero)
        {
            x = Mod(x * SqrtMinusOne);
        }

        if (!Mod((x * x) - x2).IsZero)
        {
            return null;
        }

        if (!x.IsEven != sign)
        {
            x = P - x;
        }

        return x;
    }

    private static Point? Decompress(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length != 32)
        {
            return null;
        }

        var bytes = encoded.ToArray();
        var sign = (bytes[31] & 0x80) != 0;
        bytes[31] &= 0x7F;
        var y = new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
        return RecoverX(y, sign) is { } x ? new Point(x, y, 1, Mod(x * y)) : null;
    }
}
