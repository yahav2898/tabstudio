using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TabStudio.Live.Files;
using TabStudio.Live.Text;
using TabStudio.Link.Messaging;
using TabStudio.Swarm;

namespace TabStudio.Live.Protocol;

/// <summary>Reads and writes a session's message bodies.</summary>
internal static class LiveJson
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

/// <summary>Source-generated JSON for a session's messages, so nothing depends on reflection.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(LiveJoinRequest))]
[JsonSerializable(typeof(LiveJoinResponse))]
[JsonSerializable(typeof(LiveFilesRequest))]
[JsonSerializable(typeof(LiveFilesResponse))]
[JsonSerializable(typeof(LivePathRequest))]
[JsonSerializable(typeof(LiveDocumentResponse))]
[JsonSerializable(typeof(LiveSaveRequest))]
[JsonSerializable(typeof(LiveDebugCommandRequest))]
[JsonSerializable(typeof(LiveVariablesRequest))]
[JsonSerializable(typeof(LiveVariablesResponse))]
[JsonSerializable(typeof(LiveEvaluateRequest))]
[JsonSerializable(typeof(LiveEvaluation))]
[JsonSerializable(typeof(LiveBreakpointRequest))]
[JsonSerializable(typeof(LiveHandNotice))]
[JsonSerializable(typeof(LiveEditNotice))]
[JsonSerializable(typeof(LivePresence))]
[JsonSerializable(typeof(LiveParticipantsNotice))]
[JsonSerializable(typeof(LiveFilesNotice))]
[JsonSerializable(typeof(LiveOutput))]
[JsonSerializable(typeof(LiveErrorsNotice))]
[JsonSerializable(typeof(LiveRunState))]
[JsonSerializable(typeof(LiveDebugNotice))]
[JsonSerializable(typeof(LiveBreakpointsNotice))]
[JsonSerializable(typeof(LiveEndNotice))]
[JsonSerializable(typeof(TextOperation))]
[JsonSerializable(typeof(LiveFileEntry))]
[JsonSerializable(typeof(SwarmVariable))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class LiveJsonContext : JsonSerializerContext
{
}
