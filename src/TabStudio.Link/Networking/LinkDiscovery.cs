using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using TabStudio.Link.Security;

namespace TabStudio.Link.Networking;

/// <summary>
/// Finds TabStudio Link hosts on the local network with a UDP broadcast, so a tablet finds its host again when the
/// host's address changed. Answers are only hints: the TLS handshake still checks the host's fingerprint.
/// </summary>
/// <remarks>
/// A request is <c>TABSTUDIO-LINK/1 DISCOVER &lt;fingerprint or *&gt;</c>; a host answers with
/// <c>TABSTUDIO-LINK/1 HOST &lt;fingerprint&gt; &lt;port&gt; &lt;name&gt;</c>. Networks that filter broadcasts, as some
/// guest Wi-Fi does, find nothing; the addresses a host had before are tried anyway.
/// </remarks>
public static class LinkDiscovery
{
    /// <summary>The first word of every discovery datagram.</summary>
    internal const string Magic = "TABSTUDIO-LINK/1";

    /// <summary>Asks the local network for hosts.</summary>
    /// <param name="fingerprint">Only the host with this fingerprint answers; <see langword="null"/> for every host.</param>
    /// <param name="wait">How long to wait for answers.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <param name="port">The UDP port hosts answer on.</param>
    /// <returns>The hosts that answered, each once.</returns>
    public static async Task<IReadOnlyList<LinkDiscoveredHost>> FindAsync(LinkFingerprint? fingerprint, TimeSpan wait, CancellationToken cancellationToken, int port = LinkProtocol.DiscoveryPort)
    {
        var found = new Dictionary<string, LinkDiscoveredHost>(StringComparer.Ordinal);
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { EnableBroadcast = true };
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            var request = Encoding.UTF8.GetBytes($"{Magic} DISCOVER {fingerprint?.Value ?? "*"}");
            foreach (var target in BroadcastAddresses())
            {
                try
                {
                    await socket.SendToAsync(request, SocketFlags.None, new IPEndPoint(target, port), cancellationToken).ConfigureAwait(false);
                }
                catch (SocketException)
                {
                    // That network does not allow broadcasts; the others may.
                }
            }

            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            waiting.CancelAfter(wait);
            var buffer = new byte[1024];
            while (true)
            {
                SocketReceiveFromResult received;
                try
                {
                    received = await socket.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), waiting.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (received.RemoteEndPoint is IPEndPoint from
                    && ParseAnswer(Encoding.UTF8.GetString(buffer, 0, received.ReceivedBytes), from.Address) is { } host
                    && (fingerprint is null || host.Fingerprint == fingerprint))
                {
                    found.TryAdd(host.Fingerprint.Value, host);
                }
            }
        }
        catch (SocketException)
        {
            // No network to ask.
        }

        return [.. found.Values];
    }

    /// <summary>The answer a host sends to a request, or <see langword="null"/> when the request is not for it.</summary>
    /// <param name="request">The request's text.</param>
    /// <param name="fingerprint">The host's fingerprint.</param>
    /// <param name="port">The host's TCP port.</param>
    /// <param name="name">The host's name.</param>
    internal static string? Answer(string request, LinkFingerprint fingerprint, int port, string name)
    {
        var parts = request.Split(' ', 3);
        if (parts.Length != 3 || parts[0] != Magic || parts[1] != "DISCOVER" || (parts[2] != "*" && parts[2] != fingerprint.Value))
        {
            return null;
        }

        return $"{Magic} HOST {fingerprint.Value} {port.ToString(CultureInfo.InvariantCulture)} {Uri.EscapeDataString(name)}";
    }

    /// <summary>Reads a host's answer.</summary>
    internal static LinkDiscoveredHost? ParseAnswer(string answer, IPAddress from)
    {
        var parts = answer.Split(' ', 5);
        if (parts.Length != 5 || parts[0] != Magic || parts[1] != "HOST"
            || !LinkFingerprint.TryParse(parts[2], out var fingerprint)
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            return null;
        }

        var address = from.IsIPv4MappedToIPv6 ? from.MapToIPv4() : from;
        return new LinkDiscoveredHost(Uri.UnescapeDataString(parts[4]), fingerprint, new LinkEndpoint(address.ToString(), port));
    }

    /// <summary>The broadcast address of every IPv4 network this device is on, the limited broadcast and the loopback.</summary>
    private static List<IPAddress> BroadcastAddresses()
    {
        var addresses = new List<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork && unicast.IPv4Mask is { } mask && !mask.Equals(IPAddress.Any))
                    {
                        var address = unicast.Address.GetAddressBytes();
                        var bits = mask.GetAddressBytes();
                        for (var i = 0; i < 4; i++)
                        {
                            address[i] |= (byte)~bits[i];
                        }

                        var broadcast = new IPAddress(address);
                        if (!addresses.Contains(broadcast))
                        {
                            addresses.Add(broadcast);
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            // The limited broadcast reaches the network the device is on.
        }

        return addresses;
    }
}
