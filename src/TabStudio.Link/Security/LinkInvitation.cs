using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace TabStudio.Link.Security;

/// <summary>
/// Everything a tablet needs to pair with a host, as the host's QR code carries it: a
/// <c>tabstudio-link://pair</c> URI with the host's name, its addresses on the local network, its port, its
/// certificate fingerprint and the pairing code.
/// </summary>
/// <param name="DeviceName">The host's name, such as <c>Pixel 9</c>.</param>
/// <param name="Addresses">The host's addresses on the local network, the most likely first.</param>
/// <param name="Port">The TCP port the host listens on.</param>
/// <param name="Fingerprint">The host's certificate fingerprint, which the tablet pins.</param>
/// <param name="Code">The one-time pairing code.</param>
public sealed record LinkInvitation(string DeviceName, IReadOnlyList<string> Addresses, int Port, LinkFingerprint Fingerprint, LinkPairingCode Code)
{
    /// <summary>The URI scheme.</summary>
    public const string Scheme = "tabstudio-link";

    private const string Prefix = Scheme + "://pair?";

    /// <summary>The URI the QR code encodes.</summary>
    public string ToUri()
    {
        var uri = new StringBuilder(Prefix);
        uri.Append("v=").Append(LinkProtocol.Version.ToString(CultureInfo.InvariantCulture));
        uri.Append("&n=").Append(Uri.EscapeDataString(DeviceName));
        uri.Append("&a=").Append(string.Join(',', Addresses.Select(address => Uri.EscapeDataString(address))));
        uri.Append("&p=").Append(Port.ToString(CultureInfo.InvariantCulture));
        uri.Append("&f=").Append(Fingerprint.Value);
        uri.Append("&c=").Append(Code.Value);
        return uri.ToString();
    }

    /// <summary>Reads an invitation from a scanned URI.</summary>
    /// <param name="text">The QR code's text.</param>
    /// <param name="invitation">The invitation, when the text is a valid one.</param>
    public static bool TryParse(string? text, [NotNullWhen(true)] out LinkInvitation? invitation)
    {
        invitation = null;
        if (text is null || !text.Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in text.Trim()[Prefix.Length..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0)
            {
                values[pair[..equals]] = pair[(equals + 1)..];
            }
        }

        if (!values.TryGetValue("n", out var name)
            || !values.TryGetValue("a", out var addresses)
            || !values.TryGetValue("p", out var portText)
            || !int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535
            || !values.TryGetValue("f", out var fingerprintText) || !LinkFingerprint.TryParse(fingerprintText, out var fingerprint)
            || !values.TryGetValue("c", out var codeText) || !LinkPairingCode.TryParse(codeText, out var code))
        {
            return false;
        }

        var addressList = addresses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(address => Uri.UnescapeDataString(address))
            .ToList();
        if (addressList.Count == 0)
        {
            return false;
        }

        invitation = new LinkInvitation(Uri.UnescapeDataString(name), addressList, port, fingerprint, code);
        return true;
    }
}
