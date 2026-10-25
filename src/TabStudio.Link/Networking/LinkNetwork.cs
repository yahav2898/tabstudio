using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TabStudio.Link.Networking;

/// <summary>This device's addresses on the local network, which a host puts in its QR code.</summary>
public static class LinkNetwork
{
    /// <summary>
    /// The device's IPv4 addresses on its local networks, the one of the default route (usually Wi-Fi) first.
    /// Loopback, link-local and tunnel addresses are left out.
    /// </summary>
    public static IReadOnlyList<string> GetLocalAddresses()
    {
        var addresses = new List<string>();
        if (DefaultRouteAddress() is { } primary)
        {
            addresses.Add(primary.ToString());
        }

        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up
                    || adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (IsLocalNetworkAddress(unicast.Address) && !addresses.Contains(unicast.Address.ToString()))
                    {
                        addresses.Add(unicast.Address.ToString());
                    }
                }
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            // Some Android versions do not list interfaces to apps; the default route's address is enough.
        }

        return addresses;
    }

    /// <summary>Whether an address is a usable IPv4 address on a local network.</summary>
    /// <param name="address">The address.</param>
    public static bool IsLocalNetworkAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return !(bytes[0] == 169 && bytes[1] == 254) && bytes[0] != 0;
    }

    /// <summary>
    /// The address the device would send from to reach the internet: connecting a UDP socket sends nothing, but makes
    /// the system pick the route.
    /// </summary>
    private static IPAddress? DefaultRouteAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 9));
            return socket.LocalEndPoint is IPEndPoint local && IsLocalNetworkAddress(local.Address) ? local.Address : null;
        }
        catch (SocketException)
        {
            return null;
        }
    }
}
