using System.Diagnostics.CodeAnalysis;
using TabStudio.Link.Security;

namespace TabStudio.WindowsBridge.Protocol;

/// <summary>
/// The Windows Bridge's QR code: a TabStudio Link invitation under its own name, <c>tabstudio-link://bridge</c>, so a
/// tablet that scans it in the wrong place says so instead of pairing with the bridge as a Link host.
/// </summary>
public static class BridgeInvitation
{
    private const string LinkPrefix = LinkInvitation.Scheme + "://pair?";
    private const string BridgePrefix = LinkInvitation.Scheme + "://bridge?";

    /// <summary>The URI the bridge's QR code encodes.</summary>
    /// <param name="invitation">The bridge's Link invitation.</param>
    public static string ToUri(LinkInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        return BridgePrefix + invitation.ToUri()[LinkPrefix.Length..];
    }

    /// <summary>Reads a bridge's invitation from a scanned QR code.</summary>
    /// <param name="text">The QR code's text.</param>
    /// <param name="invitation">The invitation, when the text is a bridge's.</param>
    public static bool TryParse(string? text, [NotNullWhen(true)] out LinkInvitation? invitation)
    {
        invitation = null;
        var trimmed = text?.Trim();
        return trimmed is not null
            && trimmed.StartsWith(BridgePrefix, StringComparison.OrdinalIgnoreCase)
            && LinkInvitation.TryParse(LinkPrefix + trimmed[BridgePrefix.Length..], out invitation);
    }

    /// <summary>Whether a scanned QR code is a TabStudio Link host's rather than a bridge's.</summary>
    /// <param name="text">The QR code's text.</param>
    public static bool IsLinkHostCode(string? text) => LinkInvitation.TryParse(text, out _);
}
