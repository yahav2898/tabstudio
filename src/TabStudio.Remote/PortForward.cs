using Renci.SshNet;

namespace TabStudio.Remote;

/// <summary>A loopback-only SSH tunnel owned by the workspace, closed with its connection.</summary>
public sealed class PortForward : IDisposable
{
    private readonly SshClient _client;
    private readonly ForwardedPortLocal _port;
    private int _disposed;
    public PortForward(SshConnection connection, int remotePort)
    {
        if (remotePort is < 1 or > 65535) { throw new ArgumentOutOfRangeException(nameof(remotePort)); }
        _client = connection.Ssh;
        _port = new ForwardedPortLocal("127.0.0.1", 0, "127.0.0.1", (uint)remotePort);
        try { _client.AddForwardedPort(_port); _port.Start(); }
        catch { _port.Dispose(); throw; }
        Address = new Uri($"http://127.0.0.1:{_port.BoundPort}/");
    }
    public Uri Address { get; }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
        try { _port.Stop(); _client.RemoveForwardedPort(_port); }
        finally { _port.Dispose(); }
    }
}
