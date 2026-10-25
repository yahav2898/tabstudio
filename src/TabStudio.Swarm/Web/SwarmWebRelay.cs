using System.Net.Sockets;
using TabStudio.Link.Messaging;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Web;

/// <summary>
/// The tablet's end of the web tunnel: for every connection a device's web view opens, connects to the web app the
/// tablet serves on its loopback address, and carries the bytes through the device's swarm connection.
/// </summary>
/// <remarks>
/// The relay only ever connects to the address of the web app that runs on the swarm now, so a device cannot reach
/// anything else through it. Thread-safe.
/// </remarks>
internal sealed class SwarmWebRelay
{
    private readonly Func<Uri?> _target;
    private readonly Lock _gate = new();
    private readonly Dictionary<(LinkConnection Link, int Stream), SwarmTunnel> _tunnels = [];

    /// <summary>Creates a relay.</summary>
    /// <param name="target">Where the web app runs now, if one does.</param>
    public SwarmWebRelay(Func<Uri?> target) => _target = target;

    /// <summary>Connects a device's new connection to the web app.</summary>
    /// <param name="link">The device's swarm connection.</param>
    /// <param name="stream">The connection's number on the device.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task<SwarmConnectResponse> ConnectAsync(LinkConnection link, int stream, CancellationToken cancellationToken)
    {
        if (_target() is not { } target)
        {
            return new SwarmConnectResponse(false, "No web app runs on the swarm now.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(target.Host, target.Port, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            socket.Dispose();
            return new SwarmConnectResponse(false, $"The web app does not answer on the tablet: {ex.Message}");
        }

        var tunnel = new SwarmTunnel(stream, socket, link, Remove);
        SwarmTunnel? replaced;
        lock (_gate)
        {
            _tunnels.Remove((link, stream), out replaced);
            _tunnels[(link, stream)] = tunnel;
        }

        replaced?.Abort();
        tunnel.Start(ReadOnlyMemory<byte>.Empty);
        return new SwarmConnectResponse(true, null);
    }

    /// <summary>Bytes a device sent for one of its connections.</summary>
    public void Deliver(LinkConnection link, int stream, byte[] data) => Find(link, stream)?.Deliver(data);

    /// <summary>A device closed its direction of one of its connections.</summary>
    public void CloseFromDevice(LinkConnection link, int stream) => Find(link, stream)?.CloseFromRemote();

    /// <summary>Closes every connection of a device, as when it was lost.</summary>
    /// <param name="link">The device's swarm connection.</param>
    public void CloseAll(LinkConnection link)
    {
        foreach (var tunnel in Take(t => ReferenceEquals(t.Link, link)))
        {
            tunnel.Abort();
        }
    }

    /// <summary>Closes every connection, as when the web app stops or restarts.</summary>
    public void CloseAll()
    {
        foreach (var tunnel in Take(_ => true))
        {
            tunnel.Abort();
        }
    }

    private SwarmTunnel? Find(LinkConnection link, int stream)
    {
        lock (_gate)
        {
            return _tunnels.GetValueOrDefault((link, stream));
        }
    }

    private List<SwarmTunnel> Take(Func<SwarmTunnel, bool> which)
    {
        lock (_gate)
        {
            var taken = _tunnels.Values.Where(which).ToList();
            foreach (var tunnel in taken)
            {
                _tunnels.Remove((tunnel.Link, tunnel.Stream));
            }

            return taken;
        }
    }

    private void Remove(SwarmTunnel tunnel)
    {
        lock (_gate)
        {
            if (_tunnels.TryGetValue((tunnel.Link, tunnel.Stream), out var current) && ReferenceEquals(current, tunnel))
            {
                _tunnels.Remove((tunnel.Link, tunnel.Stream));
            }
        }
    }
}
