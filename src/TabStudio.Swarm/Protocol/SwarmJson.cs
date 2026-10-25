using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TabStudio.Link.Messaging;

namespace TabStudio.Swarm.Protocol;

/// <summary>Reads and writes the swarm's message bodies.</summary>
internal static class SwarmJson
{
    /// <summary>A message body.</summary>
    /// <param name="value">The value.</param>
    /// <param name="typeInfo">Its source-generated contract.</param>
    public static JsonElement Body<T>(T value, JsonTypeInfo<T> typeInfo) => JsonSerializer.SerializeToElement(value, typeInfo);

    /// <summary>An answer.</summary>
    /// <param name="value">The value.</param>
    /// <param name="typeInfo">Its source-generated contract.</param>
    /// <param name="attachments">Its attachments, if any.</param>
    public static LinkReply Reply<T>(T value, JsonTypeInfo<T> typeInfo, IReadOnlyList<ReadOnlyMemory<byte>>? attachments = null) =>
        LinkReply.Of(value, typeInfo, attachments);
}

/// <summary>Source-generated JSON for the swarm's messages, so nothing depends on reflection.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SwarmHello))]
[JsonSerializable(typeof(SwarmWelcome))]
[JsonSerializable(typeof(SwarmScreenInfo))]
[JsonSerializable(typeof(SwarmPrepareRequest))]
[JsonSerializable(typeof(SwarmPrepareResponse))]
[JsonSerializable(typeof(SwarmRunRequest))]
[JsonSerializable(typeof(SwarmStopRequest))]
[JsonSerializable(typeof(SwarmStateNotice))]
[JsonSerializable(typeof(SwarmOutputNotice))]
[JsonSerializable(typeof(SwarmExitNotice))]
[JsonSerializable(typeof(SwarmConsoleInput))]
[JsonSerializable(typeof(SwarmPauseNotice))]
[JsonSerializable(typeof(SwarmResumedNotice))]
[JsonSerializable(typeof(SwarmResumeRequest))]
[JsonSerializable(typeof(SwarmResumeResponse))]
[JsonSerializable(typeof(SwarmBreakpointsRequest))]
[JsonSerializable(typeof(SwarmVariablesRequest))]
[JsonSerializable(typeof(SwarmVariablesResponse))]
[JsonSerializable(typeof(SwarmEvaluateRequest))]
[JsonSerializable(typeof(SwarmEvaluateResponse))]
[JsonSerializable(typeof(SwarmInput))]
[JsonSerializable(typeof(SwarmInputResponse))]
[JsonSerializable(typeof(SwarmCaptureResponse))]
[JsonSerializable(typeof(SwarmLayoutResponse))]
[JsonSerializable(typeof(SwarmTextRequest))]
[JsonSerializable(typeof(SwarmTextResponse))]
[JsonSerializable(typeof(SwarmNotice))]
[JsonSerializable(typeof(SwarmStreamNotice))]
[JsonSerializable(typeof(SwarmConnectResponse))]
internal sealed partial class SwarmJsonContext : JsonSerializerContext
{
}
