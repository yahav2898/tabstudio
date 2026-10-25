using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace TabStudio.WindowsCompatibility.Display.X11;

/// <summary>Where an X server listens: a Unix domain socket file, or a TCP port.</summary>
public sealed record X11Endpoint
{
    private X11Endpoint(string? socketPath, string? host, int port)
    {
        SocketPath = socketPath;
        Host = host;
        Port = port;
    }

    /// <summary>The socket file, such as <c>/tmp/.X11-unix/X5</c>, for a Unix domain socket.</summary>
    public string? SocketPath { get; }

    /// <summary>The host, for TCP.</summary>
    public string? Host { get; }

    /// <summary>The port, for TCP: 6000 plus the display number.</summary>
    public int Port { get; }

    /// <summary>An X server's Unix domain socket.</summary>
    /// <param name="socketPath">The socket file, as this process sees it.</param>
    public static X11Endpoint Unix(string socketPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(socketPath);
        return new X11Endpoint(socketPath, null, 0);
    }

    /// <summary>An X server's TCP port.</summary>
    /// <param name="host">The host, such as <c>127.0.0.1</c>.</param>
    /// <param name="display">The display number; the port is 6000 plus it.</param>
    public static X11Endpoint Tcp(string host, int display)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        ArgumentOutOfRangeException.ThrowIfNegative(display);
        return new X11Endpoint(null, host, 6000 + display);
    }

    /// <summary>Opens a connected socket to the server.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    internal async Task<Socket> ConnectAsync(CancellationToken cancellationToken)
    {
        if (SocketPath is { } path)
        {
            var unix = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await unix.ConnectAsync(new UnixDomainSocketEndPoint(path), cancellationToken).ConfigureAwait(false);
                return unix;
            }
            catch
            {
                unix.Dispose();
                throw;
            }
        }

        var tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await tcp.ConnectAsync(new IPEndPoint(IPAddress.Parse(Host!), Port), cancellationToken).ConfigureAwait(false);
            return tcp;
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public override string ToString() => SocketPath ?? string.Create(CultureInfo.InvariantCulture, $"{Host}:{Port}");
}
