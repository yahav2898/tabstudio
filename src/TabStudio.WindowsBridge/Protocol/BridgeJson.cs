using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TabStudio.Core.Diagnostics;
using TabStudio.Link.Messaging;

namespace TabStudio.WindowsBridge.Protocol;

/// <summary>
/// The JSON contract of the Windows Bridge's messages: source-generated, so nothing depends on reflection, and
/// without computed properties (such as <see cref="BuildDiagnostic.FileName"/>), which are worked out again on arrival.
/// </summary>
public static class BridgeJson
{
    /// <summary>The options every bridge message is written and read with.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>The contract of a message type.</summary>
    /// <typeparam name="T">The message type.</typeparam>
    public static JsonTypeInfo<T> Info<T>() => (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));

    /// <summary>A message as a body.</summary>
    /// <param name="value">The message.</param>
    /// <typeparam name="T">The message type.</typeparam>
    public static JsonElement ToJson<T>(T value) => JsonSerializer.SerializeToElement(value, Info<T>());

    /// <summary>Reads a message from a received payload.</summary>
    /// <param name="payload">The payload.</param>
    /// <typeparam name="T">The message type.</typeparam>
    /// <exception cref="TabStudio.Link.LinkProtocolException">The payload does not hold one.</exception>
    public static T Read<T>(LinkPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return payload.Read(Info<T>());
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(BridgeJsonContext.Default.Options)
        {
            TypeInfoResolver = BridgeJsonContext.Default.WithAddedModifier(SkipComputedProperties),
        };
        options.MakeReadOnly();
        return options;
    }

    /// <summary>Leaves out properties that are neither set nor passed to a constructor: they are computed.</summary>
    private static void SkipComputedProperties(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var i = info.Properties.Count - 1; i >= 0; i--)
        {
            var property = info.Properties[i];
            if (property.Set is null && property.AssociatedParameter is null)
            {
                info.Properties.RemoveAt(i);
            }
        }
    }
}

/// <summary>Source-generated JSON for every bridge message.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(BridgeInfo))]
[JsonSerializable(typeof(BridgeStatus))]
[JsonSerializable(typeof(BuildRequest))]
[JsonSerializable(typeof(BuildOutputLine))]
[JsonSerializable(typeof(BuildOutcome))]
[JsonSerializable(typeof(RunRequest))]
[JsonSerializable(typeof(AppStarted))]
[JsonSerializable(typeof(AppReference))]
[JsonSerializable(typeof(AppOutput))]
[JsonSerializable(typeof(AppWindows))]
[JsonSerializable(typeof(AppExited))]
[JsonSerializable(typeof(StreamRequest))]
[JsonSerializable(typeof(StreamStarted))]
[JsonSerializable(typeof(StreamReference))]
[JsonSerializable(typeof(StreamEnded))]
[JsonSerializable(typeof(FrameHeader))]
[JsonSerializable(typeof(FrameShown))]
[JsonSerializable(typeof(InputBatch))]
[JsonSerializable(typeof(ResizeRequest))]
[JsonSerializable(typeof(ClipboardText))]
[JsonSerializable(typeof(DebuggerOpen))]
[JsonSerializable(typeof(DebuggerData))]
[JsonSerializable(typeof(DebuggerClosed))]
internal sealed partial class BridgeJsonContext : JsonSerializerContext
{
}
