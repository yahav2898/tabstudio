using Android.Security.Keystore;
using Android.Runtime;
using Java.Security;
using Java.Security.Interfaces;
using TabStudio.Remote;

namespace TabStudio.Android;

/// <summary>Device-generated, non-exportable SSH identities. SSH.NET sends data to Android Keystore for signing.</summary>
public sealed class AndroidSshKeyStore : ISshKeyStore
{
    public SshIdentity GetOrCreate(string id)
    {
        if (id.Length == 0 || id.Any(c => !char.IsAsciiLetterOrDigit(c))) { throw new ArgumentException("Invalid SSH key id.", nameof(id)); }
        var alias = "tabstudio.ssh." + id;
        using var store = KeyStore.GetInstance("AndroidKeyStore")!;
        store.Load(null);
        if (!store.ContainsAlias(alias))
        {
            using var generator = KeyPairGenerator.GetInstance(KeyProperties.KeyAlgorithmRsa, "AndroidKeyStore")!;
            using var builder = new KeyGenParameterSpec.Builder(alias, KeyStorePurpose.Sign | KeyStorePurpose.Verify);
            using var spec = builder.SetKeySize(3072)!
                .SetDigests(KeyProperties.DigestSha256, KeyProperties.DigestSha512)!
                .SetSignaturePaddings(KeyProperties.SignaturePaddingRsaPkcs1)!
                .Build()!;
            generator.Initialize(spec);
            using var pair = generator.GenerateKeyPair();
        }
        using var certificate = store.GetCertificate(alias)!;
        using var publicKey = certificate.PublicKey!;
        var rsa = publicKey.JavaCast<IRSAPublicKey>();
        var exponent = rsa.PublicExponent!.ToByteArray()!;
        var modulus = rsa.Modulus!.ToByteArray()!;
        var source = new SigningKeySource(exponent, modulus, (data, hash) =>
        {
            using var keyStore = KeyStore.GetInstance("AndroidKeyStore")!;
            keyStore.Load(null);
            using var key = keyStore.GetKey(alias, null)!;
            using var signature = Signature.GetInstance(hash == System.Security.Cryptography.HashAlgorithmName.SHA512 ? "SHA512withRSA" : "SHA256withRSA")!;
            signature.InitSign((IPrivateKey)key);
            signature.Update(data);
            return signature.Sign()!;
        });
        return new SshIdentity(source, source.PublicKey);
    }
}
