using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using TabStudio.Link.Messaging;

namespace TabStudio.Link.Language;

/// <summary>What the host is doing, which it tells the tablet whenever it changes.</summary>
/// <param name="Activity">What the host is working on, such as <c>Building HelloTablet</c>, or <see langword="null"/> when idle.</param>
/// <param name="ThermalNote">Why the host holds back, such as <c>Warm · building on one core</c>, or <see langword="null"/>.</param>
/// <param name="Processors">The host's processor count.</param>
public sealed record LinkHostStatus(string? Activity, string? ThermalNote, int Processors)
{
    /// <summary>The notification that carries it, from host to tablet.</summary>
    public const string Method = "host/status";

    /// <summary>The notification's body.</summary>
    public JsonElement ToJson() => JsonSerializer.SerializeToElement(this, LinkLanguageJson.Info<LinkHostStatus>());

    /// <summary>Reads the status from a notification, if it is one.</summary>
    /// <param name="method">The notification's method.</param>
    /// <param name="payload">Its payload.</param>
    /// <param name="status">The status, when the notification is one.</param>
    public static bool TryRead(string method, LinkPayload payload, [NotNullWhen(true)] out LinkHostStatus? status)
    {
        ArgumentNullException.ThrowIfNull(payload);
        status = null;
        if (method != Method)
        {
            return false;
        }

        try
        {
            status = payload.Read(LinkLanguageJson.Info<LinkHostStatus>());
            return true;
        }
        catch (LinkProtocolException)
        {
            return false;
        }
    }
}
