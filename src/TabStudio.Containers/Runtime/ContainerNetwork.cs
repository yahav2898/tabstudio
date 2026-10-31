using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TabStudio.Containers.Runtime;

/// <summary>
/// The network containers share. Android gives an app no network namespace, so instead every container gets its own
/// loopback address (<c>127.42.x.y</c>), which PRoot's <c>--net-map</c> extension makes its programs listen on;
/// names resolve to those addresses through each container's <c>/etc/hosts</c>; and published ports listen on the
/// device's localhost, on the port asked for when it is free and unprivileged and on a free one otherwise.
/// </summary>
public sealed class ContainerNetwork
{
    /// <summary>The address <c>host.docker.internal</c> resolves to: the device itself.</summary>
    public const string HostAddress = "127.42.255.254";

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Lease> _leases = new(StringComparer.Ordinal);
    private readonly Func<int, bool> _isFree;
    private readonly Func<int> _freePort;

    /// <summary>Creates the network.</summary>
    /// <param name="folder">Where the shared rule file goes.</param>
    /// <param name="isFree">Whether a device port is free; the default tries to listen on it.</param>
    /// <param name="freePort">A free device port; the default asks the system for one.</param>
    public ContainerNetwork(string folder, Func<int, bool>? isFree = null, Func<int>? freePort = null)
    {
        Directory.CreateDirectory(folder);
        PeersFile = Path.Combine(folder, "peers.rules");
        _isFree = isFree ?? IsFree;
        _freePort = freePort ?? FreePort;
        WritePeers();
    }

    /// <summary>The rule file every container reads: how to reach the others' published ports.</summary>
    public string PeersFile { get; }

    /// <summary>A container's place on the network.</summary>
    /// <param name="Id">The container.</param>
    /// <param name="Address">Its loopback address.</param>
    /// <param name="Names">The names that resolve to it.</param>
    /// <param name="Ports">Its published ports.</param>
    /// <param name="Exposed">Ports other containers reach through the device rather than its address.</param>
    public sealed record Lease(string Id, string Address, IReadOnlyList<string> Names, IReadOnlyList<PublishedPort> Ports, IReadOnlyDictionary<int, int> Exposed);

    /// <summary>
    /// Gives a container its address and ports. A port asked for that is in use fails; a privileged one (below
    /// 1024, which an app may not open) moves to a free port and says so.
    /// </summary>
    /// <param name="id">The container.</param>
    /// <param name="preferredAddress">The address it had before, kept when still free.</param>
    /// <param name="names">Its name, host name and aliases.</param>
    /// <param name="ports">The ports to publish.</param>
    /// <exception cref="ContainerException">A port asked for is in use.</exception>
    public Lease Allocate(string id, string? preferredAddress, IReadOnlyList<string> names, IReadOnlyList<PortBinding> ports)
    {
        foreach (var port in ports)
        {
            if (port.Protocol != "tcp") throw new ContainerException("This version publishes TCP ports only; UDP requires a remote container host.");
            if (port.ContainerPort is < 1 or > 65535 || port.HostPort is < 0 or > 65535)
                throw new ContainerException("Container ports must be 1–65535; a published port can also be 0 for automatic allocation.");
            if (port.HostIp is not (null or "127.0.0.1" or "::1" or "0.0.0.0" or "::"))
                throw new ContainerException("Publish on localhost or 0.0.0.0; binding a particular device interface is unavailable in this version.");
        }
        lock (_gate)
        {
            _leases.Remove(id);
            var address = preferredAddress is not null && _leases.Values.All(l => l.Address != preferredAddress) ? preferredAddress : NextAddress();
            var used = _leases.Values.SelectMany(l => l.Ports).Select(p => p.HostPort).ToHashSet();
            var published = new List<PublishedPort>();
            foreach (var port in ports.DistinctBy(p => (p.ContainerPort, p.Protocol)))
            {
                string? note = null;
                var host = port.HostPort ?? 0;
                if (host is > 0 and < 1024 && !OperatingSystem.IsWindows())
                {
                    note = $"Port {host} needs root, which apps do not have";
                    host = 0;
                }

                if (host > 0 && (used.Contains(host) || !_isFree(host)))
                {
                    throw new ContainerException($"Port {host} is already in use on this device; publish {port.ContainerPort} on another port (-p other:{port.ContainerPort}).");
                }

                if (host == 0 && port.HostPort is null && port.ContainerPort >= 1024 && !used.Contains(port.ContainerPort) && _isFree(port.ContainerPort))
                {
                    host = port.ContainerPort;
                }

                while (host == 0 || used.Contains(host))
                {
                    host = _freePort();
                }

                used.Add(host);
                var wide = port.HostIp is "0.0.0.0" or "::";
                published.Add(new(port.ContainerPort, port.Protocol, host, wide ? "0.0.0.0" : "127.0.0.1", note is null ? null : $"{note}; it is on {host}."));
            }

            var lease = new Lease(id, address, [.. names.Where(n => n.Length > 0).Distinct(StringComparer.Ordinal)], published,
                published.Where(p => p.Protocol == "tcp").ToDictionary(p => p.ContainerPort, p => p.HostPort));
            _leases[id] = lease;
            WritePeers();
            return lease;
        }
    }

    /// <summary>Frees a container's address and ports.</summary>
    public void Release(string id)
    {
        lock (_gate)
        {
            if (_leases.Remove(id))
            {
                WritePeers();
            }
        }
    }

    /// <summary>The containers on the network now.</summary>
    public IReadOnlyList<Lease> Leases
    {
        get
        {
            lock (_gate)
            {
                return [.. _leases.Values];
            }
        }
    }

    /// <summary>The <c>--net-map</c> option for a container.</summary>
    public string ProotOption(Lease lease)
    {
        var rules = new List<string> { "A" + lease.Address };
        rules.AddRange(lease.Ports.Where(p => p.Protocol == "tcp").Select(p => $"{(p.HostIp == "0.0.0.0" ? "W" : "P")}{p.ContainerPort.ToString(CultureInfo.InvariantCulture)}={p.HostPort.ToString(CultureInfo.InvariantCulture)}"));
        rules.Add("@" + PeersFile);
        return "--net-map=" + string.Join(',', rules);
    }

    /// <summary>
    /// The container's <c>/etc/hosts</c>: localhost, itself, every other container by its names, the device as
    /// <c>host.docker.internal</c>, and any extra entries.
    /// </summary>
    public string Hosts(Lease lease, IReadOnlyList<string> extraHosts)
    {
        var text = new StringBuilder();
        text.Append("127.0.0.1\tlocalhost\n::1\tlocalhost ip6-localhost ip6-loopback\n");
        text.Append(lease.Address).Append('\t').AppendJoin(' ', lease.Names).Append('\n');
        lock (_gate)
        {
            foreach (var other in _leases.Values.Where(l => l.Id != lease.Id && l.Names.Count > 0))
            {
                text.Append(other.Address).Append('\t').AppendJoin(' ', other.Names).Append('\n');
            }
        }

        text.Append(HostAddress).Append("\thost.docker.internal gateway.docker.internal\n");
        foreach (var entry in extraHosts)
        {
            var colon = entry.LastIndexOf(':');
            if (colon <= 0) continue;
            var address = entry[(colon + 1)..];
            text.Append(address == "host-gateway" ? HostAddress : address).Append('\t').Append(entry[..colon]).Append('\n');
        }

        return text.ToString();
    }

    private string NextAddress()
    {
        var taken = _leases.Values.Select(l => l.Address).ToHashSet(StringComparer.Ordinal);
        for (var n = 2; n < 65000; n++)
        {
            var candidate = $"127.42.{(n >> 8).ToString(CultureInfo.InvariantCulture)}.{(n & 255).ToString(CultureInfo.InvariantCulture)}";
            if ((n & 255) is not (0 or 255) && !taken.Contains(candidate) && candidate != HostAddress)
            {
                return candidate;
            }
        }

        throw new ContainerException("Too many containers are running.");
    }

    /// <summary>Rewrites the shared rule file atomically: published ports by address, and the device's address.</summary>
    private void WritePeers()
    {
        var text = new StringBuilder();
        foreach (var lease in _leases.Values)
        {
            foreach (var (port, host) in lease.Exposed)
            {
                text.Append('C').Append(lease.Address).Append(':').Append(port.ToString(CultureInfo.InvariantCulture))
                    .Append("=127.0.0.1:").Append(host.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        text.Append('C').Append(HostAddress).Append(":*=127.0.0.1:*\n");
        var partial = PeersFile + ".partial";
        File.WriteAllText(partial, text.ToString());
        File.Move(partial, PeersFile, overwrite: true);
    }

    private static bool IsFree(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

