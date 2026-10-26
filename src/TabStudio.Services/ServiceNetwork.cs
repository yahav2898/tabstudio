using System.Net;
using System.Net.Sockets;

namespace TabStudio.Services;

/// <summary>The localhost ports shared by every native service on the device.</summary>
public interface IServiceNetwork
{
    bool IsFree(int port);
    int FreePort();
    Task<bool> IsListeningAsync(int port, CancellationToken cancellationToken);
}

public sealed class LocalServiceNetwork : IServiceNetwork
{
    public bool IsFree(int port)
    {
        try { using var listener = new TcpListener(IPAddress.Loopback, port); listener.Start(); return true; }
        catch (SocketException) { return false; }
    }
    public int FreePort() { using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); return ((IPEndPoint)listener.LocalEndpoint).Port; }
    public async Task<bool> IsListeningAsync(int port, CancellationToken cancellationToken)
    {
        try { using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(false); return true; }
        catch (SocketException) { return false; }
    }
}
