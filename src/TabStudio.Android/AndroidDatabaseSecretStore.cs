using System.Security.Cryptography;
using System.Text;
using Android.Content;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using TabStudio.Database;

namespace TabStudio.Android;

// The AES key never leaves AndroidKeyStore. Ciphertexts are excluded from device backup.
internal sealed class AndroidDatabaseSecretStore(Context context) : IDatabaseSecretStore
{
    private const string Alias = "TabStudio.Database.Connections.v1";
    private readonly SemaphoreSlim _gate = new(1);
    private string FileFor(string key) => Path.Combine(context.NoBackupFilesDir!.AbsolutePath, "database-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))));

    public async Task SetAsync(string key, string value, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            cipher.Init(CipherMode.EncryptMode, GetKey());
            cipher.UpdateAAD(Encoding.UTF8.GetBytes(key));
            var encrypted = cipher.DoFinal(Encoding.UTF8.GetBytes(value))!;
            var iv = cipher.GetIV()!;
            var bytes = new byte[1 + iv.Length + encrypted.Length]; bytes[0] = (byte)iv.Length;
            iv.CopyTo(bytes, 1); encrypted.CopyTo(bytes, 1 + iv.Length);
            var file = FileFor(key);
            await File.WriteAllBytesAsync(file + ".tmp", bytes, token).ConfigureAwait(false);
            File.Move(file + ".tmp", file, overwrite: true);
        }
        finally { _gate.Release(); }
    }

    public async Task<string?> GetAsync(string key, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var file = FileFor(key); if (!File.Exists(file)) return null;
            var bytes = await File.ReadAllBytesAsync(file, token).ConfigureAwait(false);
            if (bytes.Length < 30 || bytes[0] != 12) throw new CryptographicException("Invalid encrypted connection.");
            using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            using var parameters = new GCMParameterSpec(128, bytes[1..13]);
            cipher.Init(CipherMode.DecryptMode, GetKey(), parameters);
            cipher.UpdateAAD(Encoding.UTF8.GetBytes(key));
            return Encoding.UTF8.GetString(cipher.DoFinal(bytes[13..])!);
        }
        finally { _gate.Release(); }
    }

    public Task RemoveAsync(string key, CancellationToken token = default) { token.ThrowIfCancellationRequested(); File.Delete(FileFor(key)); return Task.CompletedTask; }

    private static IKey GetKey()
    {
        using var store = KeyStore.GetInstance("AndroidKeyStore")!; store.Load(null);
        if (store.GetKey(Alias, null) is { } existing) return existing;
        using var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;
        using var builder = new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt);
        using var spec = builder.SetBlockModes(KeyProperties.BlockModeGcm).SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone).SetRandomizedEncryptionRequired(true).Build();
        generator.Init(spec); return generator.GenerateKey()!;
    }
}
