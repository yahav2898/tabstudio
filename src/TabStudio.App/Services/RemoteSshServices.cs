using Renci.SshNet;
using TabStudio.Remote;

namespace TabStudio.App.Services;

/// <summary>Saved SSH hosts and the device's identity provider. Connections belong to their workspace.</summary>
public sealed class RemoteSshServices
{
    private readonly ISshKeyStore _keys;
    public RemoteSshServices(AppPaths paths, ISshKeyStore? keys = null)
    {
        _keys = keys ?? new FileSshKeyStore(Path.Combine(paths.State, "SSH", "keys"));
        Hosts = new SshHostStore(Path.Combine(paths.State, "SSH"));
    }
    public SshHostStore Hosts { get; }
    public SshIdentity Identity(SshHost host)
    {
        if (host.IdentityFile is not { Length: > 0 } path) { return _keys.GetOrCreate(host.KeyId); }
        try
        {
            var file = new PrivateKeyFile(path);
            return new SshIdentity(file, string.Empty, file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Renci.SshNet.Common.SshException)
        {
            throw new IOException("The imported IdentityFile cannot be opened. Use an unencrypted key in private app storage, or generate a device key in Remote Explorer. " + ex.Message, ex);
        }
    }
    public Task<SshConnection> ConnectAsync(SshHost host, CancellationToken cancellationToken) => Task.Run(async () =>
        await SshConnection.ConnectAsync(host, Identity(host), Hosts.Fingerprint(host), cancellationToken).ConfigureAwait(false), cancellationToken);

    /// <summary>Exercises the real platform key store and RSA SHA-2 wire format without requiring a test server.</summary>
    public void CheckDeviceKey()
    {
        using var identity = _keys.GetOrCreate("selftest");
        var data = System.Security.Cryptography.RandomNumberGenerator.GetBytes(64);
        foreach (var algorithm in identity.Source.HostKeyAlgorithms)
        {
            if (!algorithm.VerifySignature(data, algorithm.Sign(data))) { throw new IOException("The device SSH key did not verify its authentication signature."); }
        }
    }
}
