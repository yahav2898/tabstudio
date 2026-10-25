using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TabStudio.Link.Messaging;

/// <summary>What a request handler answers: a JSON body and binary attachments.</summary>
/// <param name="Body">The result, if any.</param>
/// <param name="Attachments">Binary attachments, such as compiled assemblies, which the body refers to by position.</param>
public sealed record LinkReply(JsonElement? Body, IReadOnlyList<ReadOnlyMemory<byte>> Attachments)
{
    /// <summary>An answer without a result, for requests that only need to know they were done.</summary>
    public static LinkReply Empty { get; } = new(null, []);

    /// <summary>An answer with a result.</summary>
    /// <param name="value">The result.</param>
    /// <param name="typeInfo">The result's source-generated JSON contract.</param>
    /// <param name="attachments">Binary attachments the result refers to by position, if any.</param>
    public static LinkReply Of<T>(T value, JsonTypeInfo<T> typeInfo, IReadOnlyList<ReadOnlyMemory<byte>>? attachments = null)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return new LinkReply(JsonSerializer.SerializeToElement(value, typeInfo), attachments ?? []);
    }
}
