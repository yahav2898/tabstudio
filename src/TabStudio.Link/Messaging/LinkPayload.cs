using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TabStudio.Link.Messaging;

/// <summary>What a received request, notification or response carries: a JSON body and binary attachments.</summary>
public sealed class LinkPayload
{
    /// <summary>Creates a payload.</summary>
    /// <param name="body">The JSON body, if any.</param>
    /// <param name="attachments">The attachments, in the order they were sent.</param>
    public LinkPayload(JsonElement? body, IReadOnlyList<byte[]> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        Body = body;
        Attachments = attachments;
    }

    /// <summary>A payload with neither body nor attachments.</summary>
    public static LinkPayload Empty { get; } = new(null, []);

    /// <summary>The JSON body, if any.</summary>
    public JsonElement? Body { get; }

    /// <summary>The binary attachments, such as compiled assemblies, in the order they were sent.</summary>
    public IReadOnlyList<byte[]> Attachments { get; }

    /// <summary>Reads the body as a value.</summary>
    /// <param name="typeInfo">The source-generated JSON contract of the value.</param>
    /// <exception cref="LinkProtocolException">There is no body, or it is not a value of this type.</exception>
    public T Read<T>(JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        if (Body is not { } body)
        {
            throw new LinkProtocolException($"The message has no {typeof(T).Name}.");
        }

        T? value;
        try
        {
            value = body.Deserialize(typeInfo);
        }
        catch (JsonException ex)
        {
            throw new LinkProtocolException($"The message does not hold a valid {typeof(T).Name}: {ex.Message}", ex);
        }

        return value is null ? throw new LinkProtocolException($"The message's {typeof(T).Name} is empty.") : value;
    }

    /// <summary>Returns an attachment by its position.</summary>
    /// <param name="index">The attachment's position, as the message's body refers to it.</param>
    /// <exception cref="LinkProtocolException">There is no such attachment.</exception>
    public byte[] GetAttachment(int index) =>
        index >= 0 && index < Attachments.Count
            ? Attachments[index]
            : throw new LinkProtocolException($"The message refers to attachment {index}, but it has {Attachments.Count}.");
}
