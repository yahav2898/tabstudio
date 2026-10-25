using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.Link.Messaging;

/// <summary>What a message is.</summary>
internal enum LinkMessageKind
{
    /// <summary>Asks for an answer: a response or an error with the same id.</summary>
    Request,

    /// <summary>Answers a request.</summary>
    Response,

    /// <summary>Answers a request with an error.</summary>
    Error,

    /// <summary>Tells something and expects no answer.</summary>
    Notification,
}

/// <summary>An error a request was answered with.</summary>
/// <param name="Code">One of <see cref="LinkErrorCodes"/>.</param>
/// <param name="Message">What went wrong, in words for the user.</param>
internal sealed record LinkError(string Code, string Message);

/// <summary>The JSON of every message.</summary>
/// <param name="Kind">What the message is.</param>
/// <param name="Id">The request id a request carries and its answer repeats; 0 for notifications.</param>
/// <param name="Method">The method of a request or notification.</param>
/// <param name="Body">The parameters of a request or notification, or the result of a response.</param>
/// <param name="Error">The error of an error answer.</param>
/// <param name="Attachments">
/// The ids of the binary attachments sent just before this message, in the order the receiver sees them.
/// </param>
internal sealed record LinkEnvelope(
    LinkMessageKind Kind,
    long Id,
    string? Method,
    JsonElement? Body,
    LinkError? Error,
    IReadOnlyList<int>? Attachments);

/// <summary>The body of the built-in <c>$/cancel</c> notification.</summary>
/// <param name="Id">The request to cancel.</param>
internal sealed record LinkCancelParams(long Id);

/// <summary>Source-generated JSON for the envelope, so nothing depends on reflection.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(LinkEnvelope))]
[JsonSerializable(typeof(LinkCancelParams))]
internal sealed partial class LinkMessagingJsonContext : JsonSerializerContext
{
}
