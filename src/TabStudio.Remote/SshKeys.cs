using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Security;

namespace TabStudio.Remote;

/// <summary>The platform creates keys and supplies signatures without exposing private key material to SSH.NET.</summary>
public interface ISshKeyStore
{
    SshIdentity GetOrCreate(string id);
}

public sealed class SshIdentity(IPrivateKeySource source, string publicKey, IDisposable? owner = null) : IDisposable
{
    public IPrivateKeySource Source { get; } = source;
    public string PublicKey { get; } = publicKey;
    public void Dispose() => owner?.Dispose();
}

/// <summary>RSA SHA-2 SSH signatures backed by a platform signing callback (Android Keystore on the tablet).</summary>
public sealed class SigningKeySource : IPrivateKeySource
{
    public SigningKeySource(byte[] exponent, byte[] modulus, Func<byte[], HashAlgorithmName, byte[]> sign)
    {
        var data = SshWire.Join(SshWire.String("ssh-rsa"), SshWire.Mpint(exponent), SshWire.Mpint(modulus));
        PublicKey = "ssh-rsa " + Convert.ToBase64String(data) + " TabStudio";
        HostKeyAlgorithms = [new SigningAlgorithm("rsa-sha2-512", data, exponent, modulus, HashAlgorithmName.SHA512, sign), new SigningAlgorithm("rsa-sha2-256", data, exponent, modulus, HashAlgorithmName.SHA256, sign)];
    }
    public string PublicKey { get; }
    public IReadOnlyCollection<HostAlgorithm> HostKeyAlgorithms { get; }

    private sealed class SigningAlgorithm(string name, byte[] publicKey, byte[] exponent, byte[] modulus, HashAlgorithmName hash, Func<byte[], HashAlgorithmName, byte[]> signer) : HostAlgorithm(name)
    {
        public override byte[] Data => publicKey;
        public override byte[] Sign(byte[] data) => SshWire.Join(SshWire.String(Name), SshWire.String(signer(data, hash)));
        public override bool VerifySignature(byte[] data, byte[] signature)
        {
            var offset = 0;
            if (Encoding.UTF8.GetString(SshWire.ReadString(signature, ref offset)) != Name) { return false; }
            var raw = SshWire.ReadString(signature, ref offset);
            if (offset != signature.Length) { return false; }
            using var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters { Exponent = exponent.AsSpan().TrimStart((byte)0).ToArray(), Modulus = modulus.AsSpan().TrimStart((byte)0).ToArray() });
            return rsa.VerifyData(data, raw, hash, RSASignaturePadding.Pkcs1);
        }
    }
}

/// <summary>The desktop counterpart: device-generated RSA keys in a private directory with mode 0600 files.</summary>
public sealed class FileSshKeyStore(string directory) : ISshKeyStore
{
    public SshIdentity GetOrCreate(string id)
    {
        if (id.Length == 0 || id.Any(c => !char.IsAsciiLetterOrDigit(c))) { throw new ArgumentException("Invalid key id.", nameof(id)); }
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) { File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        var path = Path.Combine(directory, id + ".pem");
        var rsa = RSA.Create(3072);
        try
        {
            if (File.Exists(path)) { rsa.ImportFromPem(File.ReadAllText(path)); }
            else
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                if (!OperatingSystem.IsWindows()) { options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; }
                using var stream = new FileStream(path, options);
                using var writer = new StreamWriter(stream);
                writer.Write(rsa.ExportPkcs8PrivateKeyPem());
            }
            var parameters = rsa.ExportParameters(false);
            var source = new SigningKeySource(parameters.Exponent!, parameters.Modulus!, (data, hash) => rsa.SignData(data, hash, RSASignaturePadding.Pkcs1));
            return new SshIdentity(source, source.PublicKey, rsa);
        }
        catch { rsa.Dispose(); throw; }
    }
}

internal static class SshWire
{
    public static byte[] String(string text) => String(Encoding.UTF8.GetBytes(text));
    public static byte[] String(byte[] data)
    {
        var result = new byte[data.Length + 4];
        BinaryPrimitives.WriteInt32BigEndian(result, data.Length);
        data.CopyTo(result, 4);
        return result;
    }
    public static byte[] Mpint(byte[] unsigned)
    {
        var value = unsigned.AsSpan().TrimStart((byte)0).ToArray();
        return String(value.Length > 0 && value[0] >= 128 ? Join([0], value) : value);
    }
    public static byte[] Join(params byte[][] arrays) => arrays.SelectMany(a => a).ToArray();
    public static byte[] ReadString(byte[] data, ref int offset)
    {
        if (offset < 0 || offset > data.Length - 4) { throw new InvalidDataException("Invalid SSH signature."); }
        var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
        offset += 4;
        if (length < 0 || length > data.Length - offset) { throw new InvalidDataException("Invalid SSH signature field."); }
        var field = data.AsSpan(offset, length).ToArray(); offset += length; return field;
    }
}
