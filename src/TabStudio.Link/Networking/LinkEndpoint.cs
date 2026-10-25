using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TabStudio.Link.Networking;

/// <summary>A host address as people type it: <c>192.168.1.23</c>, <c>192.168.1.23:47811</c>, <c>[fe80::1]:47811</c> or a name.</summary>
/// <param name="Host">The IP address or host name.</param>
/// <param name="Port">The TCP port.</param>
public sealed record LinkEndpoint(string Host, int Port)
{
    /// <summary>Reads an address, with <see cref="LinkProtocol.DefaultPort"/> when it has no port.</summary>
    /// <param name="text">What was typed.</param>
    /// <param name="endpoint">The address, when the text is one.</param>
    public static bool TryParse(string? text, [NotNullWhen(true)] out LinkEndpoint? endpoint) => TryParse(text, LinkProtocol.DefaultPort, out endpoint);

    /// <summary>Reads an address.</summary>
    /// <param name="text">What was typed.</param>
    /// <param name="defaultPort">The port when the text has none.</param>
    /// <param name="endpoint">The address, when the text is one.</param>
    public static bool TryParse(string? text, int defaultPort, [NotNullWhen(true)] out LinkEndpoint? endpoint)
    {
        endpoint = null;
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace))
        {
            return false;
        }

        string host;
        string? portText = null;
        if (trimmed.StartsWith('['))
        {
            var close = trimmed.IndexOf(']', StringComparison.Ordinal);
            if (close < 2)
            {
                return false;
            }

            host = trimmed[1..close];
            var rest = trimmed[(close + 1)..];
            if (rest.Length > 0)
            {
                if (rest[0] != ':')
                {
                    return false;
                }

                portText = rest[1..];
            }
        }
        else if (trimmed.Count(c => c == ':') == 1)
        {
            var colon = trimmed.IndexOf(':', StringComparison.Ordinal);
            host = trimmed[..colon];
            portText = trimmed[(colon + 1)..];
        }
        else
        {
            // No colon, or a bare IPv6 address with several.
            host = trimmed;
        }

        var port = defaultPort;
        if (host.Length == 0
            || (portText is not null && (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535)))
        {
            return false;
        }

        endpoint = new LinkEndpoint(host, port);
        return true;
    }

    /// <summary>The address as people read it: the port only when it is not the default one.</summary>
    public override string ToString()
    {
        var host = Host.Contains(':', StringComparison.Ordinal) ? $"[{Host}]" : Host;
        return Port == LinkProtocol.DefaultPort ? host : $"{host}:{Port.ToString(CultureInfo.InvariantCulture)}";
    }
}
