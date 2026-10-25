using System.Net;
using System.Net.Sockets;

namespace TabStudio.Link.Tests;

/// <summary>Two ends of a real TCP connection on the loopback address.</summary>
internal static class Loopback
{
    public static async Task<(NetworkStream Client, NetworkStream Server)> ConnectAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var accepting = listener.AcceptSocketAsync();
            var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            var server = await accepting;
            server.NoDelay = true;
            return (new NetworkStream(client, ownsSocket: true), new NetworkStream(server, ownsSocket: true));
        }
        finally
        {
            listener.Stop();
        }
    }
}
