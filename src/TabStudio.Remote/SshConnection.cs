using Renci.SshNet;
using Renci.SshNet.Common;

namespace TabStudio.Remote;

/// <summary>First-use keys require confirmation; changed keys are rejected before authentication.</summary>
public sealed class SshHostKeyException(string fingerprint, bool changed) : IOException(changed
    ? $"The SSH server key changed ({fingerprint}). Connection refused."
    : $"Verify the server fingerprint before connecting: {fingerprint}")
{
    public string Fingerprint { get; } = fingerprint;
    public bool Changed { get; } = changed;
}

/// <summary>A workspace's SSH and SFTP transports. Both validate the same pinned host key.</summary>
public sealed class SshConnection : IDisposable
{
    private readonly SshIdentity _identity;
    private int _disposed;

    private SshConnection(SshHost host, SshIdentity identity, SshClient ssh, SftpClient sftp)
    {
        Host = host;
        _identity = identity;
        Ssh = ssh;
        Sftp = sftp;
    }

    public SshHost Host { get; }
    public SshClient Ssh { get; }
    public SftpClient Sftp { get; }
    public bool IsConnected => Ssh.IsConnected && Sftp.IsConnected;

    /// <summary>Takes ownership of the identity, including on failure.</summary>
    public static async Task<SshConnection> ConnectAsync(SshHost host, SshIdentity identity, string? expectedFingerprint, CancellationToken cancellationToken = default)
    {
        host.Validate();
        ConnectionInfo Info() => new(host.HostName, host.Port, host.User, new PrivateKeyAuthenticationMethod(host.User, identity.Source))
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        var ssh = new SshClient(Info()) { KeepAliveInterval = TimeSpan.FromSeconds(20) };
        var sftp = new SftpClient(Info()) { OperationTimeout = TimeSpan.FromSeconds(30), KeepAliveInterval = TimeSpan.FromSeconds(20) };
        string? observed = null;
        void Validate(object? sender, HostKeyEventArgs args)
        {
            observed = "SHA256:" + args.FingerPrintSHA256.TrimEnd('=');
            args.CanTrust = expectedFingerprint is not null && string.Equals(observed, expectedFingerprint, StringComparison.Ordinal);
        }
        ssh.HostKeyReceived += Validate;
        sftp.HostKeyReceived += Validate;
        try
        {
            await ssh.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await sftp.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return new SshConnection(host, identity, ssh, sftp);
        }
        catch (Exception ex)
        {
            ssh.Dispose();
            sftp.Dispose();
            identity.Dispose();
            if (observed is not null && observed != expectedFingerprint) { throw new SshHostKeyException(observed, expectedFingerprint is not null); }
            if (ex is OperationCanceledException) { throw; }
            throw new IOException($"SSH connection to {host.DisplayName} failed: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
        Ssh.Dispose();
        Sftp.Dispose();
        _identity.Dispose();
    }
}
