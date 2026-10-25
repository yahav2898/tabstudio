using System.Numerics;
using System.Security.Cryptography;

namespace TabStudio.Testing.Extensions;

/// <summary>
/// Signs messages with Ed25519 (RFC 8032), so tests can sign packages the way Open VSX does. Slow and for tests only;
/// TabStudio itself only verifies signatures.
/// </summary>
public sealed class Ed25519TestSigner
{
    private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
    private static readonly BigInteger Q = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly BigInteger D = Mod(-121665 * Inverse(121666));
    private static readonly BigInteger SqrtMinusOne = BigInteger.ModPow(2, (P - 1) / 4, P);
    private static readonly (BigInteger X, BigInteger Y, BigInteger Z, BigInteger T) BasePoint = CreateBasePoint();

    private readonly BigInteger _scalar;
    private readonly byte[] _prefix;

    /// <summary>Creates a signer from a 32-byte secret key.</summary>
    public Ed25519TestSigner(byte[] secretKey)
    {
        ArgumentNullException.ThrowIfNull(secretKey);
        var hash = SHA512.HashData(secretKey);
        var scalar = hash[..32];
        scalar[0] &= 248;
        scalar[31] &= 127;
        scalar[31] |= 64;
        _scalar = new BigInteger(scalar, isUnsigned: true);
        _prefix = hash[32..];
        PublicKey = Compress(Multiply(_scalar, BasePoint));
    }

    /// <summary>The 32-byte public key.</summary>
    public byte[] PublicKey { get; }

    /// <summary>The public key in the PEM format Open VSX publishes.</summary>
    public string PublicKeyPem
    {
        get
        {
            byte[] prefix = [0x30, 0x2A, 0x30, 0x05, 0x06, 0x03, 0x2B, 0x65, 0x70, 0x03, 0x21, 0x00];
            return "-----BEGIN PUBLIC KEY-----\n" + Convert.ToBase64String([.. prefix, .. PublicKey]) + "\n-----END PUBLIC KEY-----\n";
        }
    }

    /// <summary>A signer with a key made from a seed, so tests are repeatable.</summary>
    public static Ed25519TestSigner FromSeed(string seed) => new(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed)));

    /// <summary>Signs a message.</summary>
    public byte[] Sign(byte[] message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var r = new BigInteger(SHA512.HashData([.. _prefix, .. message]), isUnsigned: true) % Q;
        var encodedR = Compress(Multiply(r, BasePoint));
        var k = new BigInteger(SHA512.HashData([.. encodedR, .. PublicKey, .. message]), isUnsigned: true) % Q;
        var s = (r + (k * _scalar)) % Q;
        var encodedS = new byte[32];
        s.TryWriteBytes(encodedS, out _, isUnsigned: true);
        return [.. encodedR, .. encodedS];
    }

    /// <summary>An Open VSX signature archive (<c>.sigzip</c>) for a package.</summary>
    public byte[] SignatureArchive(byte[] package)
    {
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var signature = zip.CreateEntry(".signature.sig").Open())
            {
                signature.Write(Sign(package));
            }

            using (var manifest = new StreamWriter(zip.CreateEntry(".signature.manifest").Open()))
            {
                manifest.Write("{\"package\":{\"size\":" + package.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}}");
            }

            zip.CreateEntry(".signature.p7s").Open().Dispose();
        }

        return stream.ToArray();
    }

    private static BigInteger Mod(BigInteger value)
    {
        var result = value % P;
        return result.Sign < 0 ? result + P : result;
    }

    private static BigInteger Inverse(BigInteger value) => BigInteger.ModPow(Mod(value), P - 2, P);

    private static (BigInteger X, BigInteger Y, BigInteger Z, BigInteger T) CreateBasePoint()
    {
        var y = Mod(4 * Inverse(5));
        var x2 = Mod(((y * y) - 1) * Inverse((D * y * y) + 1));
        var x = BigInteger.ModPow(x2, (P + 3) / 8, P);
        if (!Mod((x * x) - x2).IsZero)
        {
            x = Mod(x * SqrtMinusOne);
        }

        if (!x.IsEven)
        {
            x = P - x;
        }

        return (x, y, 1, Mod(x * y));
    }

    private static (BigInteger X, BigInteger Y, BigInteger Z, BigInteger T) Add(
        (BigInteger X, BigInteger Y, BigInteger Z, BigInteger T) a,
        (BigInteger X, BigInteger Y, BigInteger Z, BigInteger T) b)
    {
        var aa = Mod((a.Y - a.X) * (b.Y - b.X));
        var bb = Mod((a.Y + a.X) * (b.Y + b.X));
        var c = Mod(2 * a.T * b.T * D);
        var d = Mod(2 * a.Z * b.Z);
        var (e, f, g, h) = (bb - aa, d - c, d + c, bb + aa);
        return (Mod(e * f), Mod(g * h), Mod(f * g), Mod(e * h));
    }

    private static (BigInteger X, BigInteger Y, BigInteger Z, BigInteger T) Multiply(BigInteger scalar, (BigInteger X, BigInteger Y, BigInteger Z, BigInteger T) point)
    {
        (BigInteger X, BigInteger Y, BigInteger Z, BigInteger T) result = (0, 1, 1, 0);
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

    private static byte[] Compress((BigInteger X, BigInteger Y, BigInteger Z, BigInteger T) point)
    {
        var inverse = Inverse(point.Z);
        var x = Mod(point.X * inverse);
        var y = Mod(point.Y * inverse);
        var bytes = new byte[32];
        y.TryWriteBytes(bytes, out _, isUnsigned: true);
        if (!x.IsEven)
        {
            bytes[31] |= 0x80;
        }

        return bytes;
    }
}
